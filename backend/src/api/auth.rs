use std::fs;

use axum::extract::{Request, State};
use axum::http::header::AUTHORIZATION;
use axum::middleware::Next;
use axum::response::Response;
use serde::Deserialize;

use crate::api::error::ApiError;
use crate::api::state::AppState;

#[derive(Debug, Deserialize)]
struct TokenFile {
    #[serde(default)]
    tokens: Vec<TokenEntry>,
}

#[derive(Debug, Deserialize)]
struct TokenEntry {
    token: String,
    #[serde(default)]
    enabled: Option<bool>,
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

    let tokens_path = state
        .config()
        .auth
        .api_tokens_file
        .clone()
        .ok_or_else(|| unauthorized("integration token authentication is not configured"))?;
    let contents = fs::read_to_string(&tokens_path)
        .map_err(|_| unauthorized("failed to read integration token configuration"))?;
    let file: TokenFile = toml::from_str(&contents)
        .map_err(|_| unauthorized("failed to parse integration token configuration"))?;

    let valid = file.tokens.iter().any(|entry| {
        entry.token == provided && entry.enabled.unwrap_or(true)
    });

    if !valid {
        return Err(unauthorized("invalid integration token"));
    }

    Ok(next.run(request).await)
}

fn parse_bearer_token(value: &str) -> Option<&str> {
    value.strip_prefix("Bearer ").map(str::trim).filter(|v| !v.is_empty())
}

fn unauthorized(message: &str) -> ApiError {
    ApiError::new(axum::http::StatusCode::UNAUTHORIZED, "unauthorized", message)
}
