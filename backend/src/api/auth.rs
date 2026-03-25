use std::fs;
use std::time::{Duration, SystemTime, UNIX_EPOCH};

use axum::extract::{Request, State};
use axum::http::header::{AUTHORIZATION, COOKIE, SET_COOKIE};
use axum::http::HeaderValue;
use axum::middleware::Next;
use axum::response::Response;
use hex::encode as hex_encode;
use serde::Deserialize;
use sha2::{Digest, Sha256};
use sqlx::Row;
use sqlx::SqlitePool;
use uuid::Uuid;

use crate::api::error::ApiError;
use crate::api::state::{AdminSession, AppState};
use crate::api::types::{
    AdminSessionResource, CreatedIntegrationTokenResource, IntegrationTokenResource,
};
use crate::domain::AdminMode;
use crate::time::timestamp_to_rfc3339;

const ADMIN_SESSION_COOKIE: &str = "gumo_admin_session";
const ADMIN_SESSION_TTL: Duration = Duration::from_secs(60 * 60 * 12);

#[derive(Debug, Deserialize)]
pub struct LoginRequest {
    pub password: String,
}

#[derive(Debug, Deserialize)]
pub struct CreateIntegrationTokenRequest {
    pub label: String,
}

pub async fn require_integration_token(
    State(state): State<AppState>,
    request: Request,
    next: Next,
) -> Result<Response, ApiError> {
    let provided = request
        .headers()
        .get(AUTHORIZATION)
        .and_then(|value| value.to_str().ok())
        .and_then(parse_bearer_token)
        .ok_or_else(|| unauthorized("missing bearer token"))?;

    let provided_hash = hash_secret(provided);
    let valid = sqlx::query(
        "SELECT 1 FROM integration_tokens WHERE token_hash = ?1 AND is_enabled = 1 LIMIT 1",
    )
    .bind(provided_hash)
    .fetch_optional(state.db())
    .await
    .map_err(|_| unauthorized("failed to validate integration token"))?
    .is_some();

    if !valid {
        return Err(unauthorized("invalid integration token"));
    }

    Ok(next.run(request).await)
}

pub async fn require_admin_session(
    State(state): State<AppState>,
    request: Request,
    next: Next,
) -> Result<Response, ApiError> {
    current_admin_identity(&state, request.headers())?;
    Ok(next.run(request).await)
}

pub fn current_session_resource(
    state: &AppState,
    headers: &axum::http::HeaderMap,
) -> Result<AdminSessionResource, ApiError> {
    match state.config().auth.admin_mode {
        AdminMode::Local => match current_local_session(state, headers)? {
            Some(session) => Ok(AdminSessionResource {
                authenticated: true,
                mode: "local".to_string(),
                username: Some(session.username),
            }),
            None => Ok(AdminSessionResource {
                authenticated: false,
                mode: "local".to_string(),
                username: None,
            }),
        },
        AdminMode::Proxy => {
            let username = headers
                .get(
                    state
                        .config()
                        .auth
                        .proxy_user_header
                        .as_deref()
                        .ok_or_else(|| unauthorized("proxy auth is not configured"))?,
                )
                .and_then(|value| value.to_str().ok())
                .map(str::trim)
                .filter(|value| !value.is_empty())
                .map(str::to_string);
            Ok(AdminSessionResource {
                authenticated: username.is_some(),
                mode: "proxy".to_string(),
                username,
            })
        }
    }
}

pub fn login_local(state: &AppState, password: &str) -> Result<Option<HeaderValue>, ApiError> {
    if state.config().auth.admin_mode != AdminMode::Local {
        return Err(ApiError::bad_request(
            "admin login is unavailable when proxy auth is enabled",
        ));
    }

    verify_local_password(state, password)?;
    let token = Uuid::new_v4().simple().to_string();
    let expires_at_unix = unix_now() + ADMIN_SESSION_TTL.as_secs();
    let session = AdminSession {
        username: "owner".to_string(),
        expires_at_unix,
    };

    let mut sessions = state
        .admin_sessions()
        .lock()
        .map_err(|_| unauthorized("admin session state is unavailable"))?;
    sessions.insert(token.clone(), session);
    drop(sessions);

    build_session_cookie(&token, expires_at_unix).map(Some)
}

pub fn logout_local(
    state: &AppState,
    headers: &axum::http::HeaderMap,
) -> Result<Option<HeaderValue>, ApiError> {
    if state.config().auth.admin_mode != AdminMode::Local {
        return Ok(None);
    }

    if let Some(token) = extract_cookie(headers, ADMIN_SESSION_COOKIE) {
        let mut sessions = state
            .admin_sessions()
            .lock()
            .map_err(|_| unauthorized("admin session state is unavailable"))?;
        sessions.remove(&token);
    }

    build_expired_cookie().map(Some)
}

pub async fn list_integration_tokens(
    pool: &SqlitePool,
) -> Result<Vec<IntegrationTokenResource>, ApiError> {
    let rows = sqlx::query(
        r#"
        SELECT public_id, label, is_enabled, created_at, updated_at
        FROM integration_tokens
        ORDER BY created_at DESC
        "#,
    )
    .fetch_all(pool)
    .await
    .map_err(internal_error)?;

    Ok(rows
        .into_iter()
        .map(|row| IntegrationTokenResource {
            id: row.get("public_id"),
            label: row.get("label"),
            enabled: row.get::<i64, _>("is_enabled") == 1,
            created_at: timestamp_to_rfc3339(&row.get::<String, _>("created_at")),
            updated_at: timestamp_to_rfc3339(&row.get::<String, _>("updated_at")),
        })
        .collect())
}

pub async fn create_integration_token(
    pool: &SqlitePool,
    label: String,
) -> Result<CreatedIntegrationTokenResource, ApiError> {
    let label = label.trim();
    if label.is_empty() {
        return Err(ApiError::bad_request("token label is required"));
    }

    let public_id = format!("token_{}", Uuid::new_v4().simple());
    let plaintext_token = format!(
        "gumo_{}{}",
        Uuid::new_v4().simple(),
        Uuid::new_v4().simple()
    );
    let token_hash = hash_secret(&plaintext_token);

    sqlx::query(
        r#"
        INSERT INTO integration_tokens (public_id, label, token_hash, is_enabled, created_at, updated_at)
        VALUES (?1, ?2, ?3, 1, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
        "#,
    )
    .bind(&public_id)
    .bind(label)
    .bind(token_hash)
    .execute(pool)
    .await
    .map_err(|err| {
        let message = err.to_string();
        if message.contains("UNIQUE constraint failed: integration_tokens.label") {
            ApiError::bad_request("token label must be unique")
        } else {
            internal_error(err)
        }
    })?;

    let token = get_integration_token(pool, &public_id).await?;
    Ok(CreatedIntegrationTokenResource {
        token,
        plaintext_token,
    })
}

pub async fn disable_integration_token(
    pool: &SqlitePool,
    token_id: &str,
) -> Result<IntegrationTokenResource, ApiError> {
    let result = sqlx::query(
        r#"
        UPDATE integration_tokens
        SET is_enabled = 0, updated_at = CURRENT_TIMESTAMP
        WHERE public_id = ?1
        "#,
    )
    .bind(token_id)
    .execute(pool)
    .await
    .map_err(internal_error)?;

    if result.rows_affected() == 0 {
        return Err(ApiError::not_found("integration token", token_id));
    }

    get_integration_token(pool, token_id).await
}

pub async fn delete_integration_token(pool: &SqlitePool, token_id: &str) -> Result<(), ApiError> {
    let result = sqlx::query("DELETE FROM integration_tokens WHERE public_id = ?1")
        .bind(token_id)
        .execute(pool)
        .await
        .map_err(internal_error)?;

    if result.rows_affected() == 0 {
        return Err(ApiError::not_found("integration token", token_id));
    }

    Ok(())
}

fn current_admin_identity(
    state: &AppState,
    headers: &axum::http::HeaderMap,
) -> Result<String, ApiError> {
    match state.config().auth.admin_mode {
        AdminMode::Local => current_local_session(state, headers)?
            .map(|session| session.username)
            .ok_or_else(|| unauthorized("missing or expired admin session")),
        AdminMode::Proxy => headers
            .get(
                state
                    .config()
                    .auth
                    .proxy_user_header
                    .as_deref()
                    .ok_or_else(|| unauthorized("proxy auth is not configured"))?,
            )
            .and_then(|value| value.to_str().ok())
            .map(str::trim)
            .filter(|value| !value.is_empty())
            .map(str::to_string)
            .ok_or_else(|| unauthorized("missing proxy-authenticated user")),
    }
}

fn current_local_session(
    state: &AppState,
    headers: &axum::http::HeaderMap,
) -> Result<Option<AdminSession>, ApiError> {
    let Some(token) = extract_cookie(headers, ADMIN_SESSION_COOKIE) else {
        return Ok(None);
    };

    let mut sessions = state
        .admin_sessions()
        .lock()
        .map_err(|_| unauthorized("admin session state is unavailable"))?;
    let Some(session) = sessions.get(&token).cloned() else {
        return Ok(None);
    };

    if session.expires_at_unix <= unix_now() {
        sessions.remove(&token);
        return Ok(None);
    }

    Ok(Some(session))
}

fn verify_local_password(state: &AppState, password: &str) -> Result<(), ApiError> {
    let hash_path = state
        .config()
        .auth
        .owner_password_hash_file
        .as_ref()
        .ok_or_else(|| unauthorized("local admin password configuration is missing"))?;
    let expected = fs::read_to_string(hash_path)
        .map_err(|_| unauthorized("failed to read admin password configuration"))?;
    let expected = expected.trim();
    let expected = expected.strip_prefix("sha256:").unwrap_or(expected).trim();
    let actual = hash_secret(password);
    if actual == expected {
        Ok(())
    } else {
        Err(unauthorized("invalid admin credentials"))
    }
}

async fn get_integration_token(
    pool: &SqlitePool,
    token_id: &str,
) -> Result<IntegrationTokenResource, ApiError> {
    let row = sqlx::query(
        r#"
        SELECT public_id, label, is_enabled, created_at, updated_at
        FROM integration_tokens
        WHERE public_id = ?1
        "#,
    )
    .bind(token_id)
    .fetch_optional(pool)
    .await
    .map_err(internal_error)?
    .ok_or_else(|| ApiError::not_found("integration token", token_id))?;

    Ok(IntegrationTokenResource {
        id: row.get("public_id"),
        label: row.get("label"),
        enabled: row.get::<i64, _>("is_enabled") == 1,
        created_at: timestamp_to_rfc3339(&row.get::<String, _>("created_at")),
        updated_at: timestamp_to_rfc3339(&row.get::<String, _>("updated_at")),
    })
}

fn build_session_cookie(token: &str, expires_at_unix: u64) -> Result<HeaderValue, ApiError> {
    let max_age = expires_at_unix.saturating_sub(unix_now());
    HeaderValue::from_str(&format!(
        "{ADMIN_SESSION_COOKIE}={token}; HttpOnly; Path=/; SameSite=Lax; Max-Age={max_age}"
    ))
    .map_err(|_| unauthorized("failed to build admin session cookie"))
}

fn build_expired_cookie() -> Result<HeaderValue, ApiError> {
    HeaderValue::from_str(&format!(
        "{ADMIN_SESSION_COOKIE}=deleted; HttpOnly; Path=/; SameSite=Lax; Max-Age=0"
    ))
    .map_err(|_| unauthorized("failed to clear admin session cookie"))
}

fn extract_cookie(headers: &axum::http::HeaderMap, name: &str) -> Option<String> {
    headers
        .get(COOKIE)
        .and_then(|value| value.to_str().ok())
        .and_then(|value| {
            value
                .split(';')
                .map(str::trim)
                .find_map(|part| part.strip_prefix(&format!("{name}=")))
        })
        .map(str::to_string)
}

fn parse_bearer_token(value: &str) -> Option<&str> {
    value
        .strip_prefix("Bearer ")
        .map(str::trim)
        .filter(|v| !v.is_empty())
}

fn hash_secret(value: &str) -> String {
    hex_encode(Sha256::digest(value.as_bytes()))
}

fn unix_now() -> u64 {
    SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .unwrap_or_default()
        .as_secs()
}

fn unauthorized(message: &str) -> ApiError {
    ApiError::new(
        axum::http::StatusCode::UNAUTHORIZED,
        "unauthorized",
        message,
    )
}

fn internal_error(err: impl std::fmt::Display) -> ApiError {
    ApiError::new(
        axum::http::StatusCode::INTERNAL_SERVER_ERROR,
        "internal_error",
        err.to_string(),
    )
}

pub fn set_cookie_header_name() -> &'static axum::http::HeaderName {
    &SET_COOKIE
}
