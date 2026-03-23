use axum::extract::{Path, Query, State};
use axum::http::{HeaderMap, StatusCode};
use axum::middleware;
use axum::routing::{get, patch, post};
use axum::{Json, Router};

use crate::api::auth::{self, LoginRequest};
use crate::api::error::ApiError;
use crate::api::state::AppState;
use crate::api::types::{
    json, AdminSessionResource, GameSummaryResource, GameVersionResource, JobResource,
    ListResponse, SaveSnapshotResource, UploadResource,
};
use crate::playnite::{self, PatchGameRequest, PatchVersionRequest};
use crate::upload_jobs::{self, ListQuery};

pub fn router(state: AppState) -> Router<AppState> {
    let protected = Router::new()
        .route("/games", get(list_games))
        .route("/games/{id}", get(get_game).patch(patch_game))
        .route("/games/{id}/versions", get(list_versions))
        .route("/versions/{id}", patch(patch_version))
        .route("/versions/{id}/save-snapshots", get(list_save_snapshots))
        .route("/uploads", get(list_uploads))
        .route("/uploads/{id}", get(get_upload))
        .route("/jobs", get(list_jobs))
        .route("/jobs/{id}", get(get_job))
        .route_layer(middleware::from_fn_with_state(
            state.clone(),
            auth::require_admin_session,
        ));

    Router::new()
        .route("/session", get(get_session))
        .route("/session/login", post(login))
        .route("/session/logout", post(logout))
        .merge(protected)
}

async fn get_session(
    State(state): State<AppState>,
    headers: HeaderMap,
) -> Result<Json<AdminSessionResource>, ApiError> {
    Ok(Json(auth::current_session_resource(&state, &headers)?))
}

async fn login(
    State(state): State<AppState>,
    Json(payload): Json<LoginRequest>,
) -> Result<(StatusCode, HeaderMap, Json<AdminSessionResource>), ApiError> {
    let mut headers = HeaderMap::new();
    if let Some(cookie) = auth::login_local(&state, &payload.password)? {
        headers.insert(auth::set_cookie_header_name().clone(), cookie);
    }
    let body = AdminSessionResource {
        authenticated: true,
        mode: "local".to_string(),
        username: Some("owner".to_string()),
    };
    Ok((StatusCode::OK, headers, Json(body)))
}

async fn logout(
    State(state): State<AppState>,
    headers: HeaderMap,
) -> Result<(StatusCode, HeaderMap, Json<AdminSessionResource>), ApiError> {
    let mut response_headers = HeaderMap::new();
    if let Some(cookie) = auth::logout_local(&state, &headers)? {
        response_headers.insert(auth::set_cookie_header_name().clone(), cookie);
    }
    Ok((
        StatusCode::OK,
        response_headers,
        Json(AdminSessionResource {
            authenticated: false,
            mode: match state.config().auth.admin_mode {
                crate::domain::AdminMode::Local => "local".to_string(),
                crate::domain::AdminMode::Proxy => "proxy".to_string(),
            },
            username: None,
        }),
    ))
}

async fn list_games(
    State(state): State<AppState>,
) -> Result<Json<ListResponse<GameSummaryResource>>, ApiError> {
    let items = playnite::list_games(&state).await?;
    Ok(json(ListResponse {
        items,
        next_cursor: None,
    }))
}

async fn get_game(
    Path(id): Path<String>,
    State(state): State<AppState>,
) -> Result<Json<GameSummaryResource>, ApiError> {
    Ok(Json(playnite::get_game(&state, &id).await?))
}

async fn patch_game(
    Path(id): Path<String>,
    State(state): State<AppState>,
    Json(payload): Json<PatchGameRequest>,
) -> Result<Json<GameSummaryResource>, ApiError> {
    Ok(Json(playnite::patch_game(&state, &id, payload).await?))
}

async fn list_versions(
    Path(id): Path<String>,
    State(state): State<AppState>,
) -> Result<Json<ListResponse<GameVersionResource>>, ApiError> {
    let items = playnite::list_versions_for_game(&state, &id).await?;
    Ok(json(ListResponse {
        items,
        next_cursor: None,
    }))
}

async fn patch_version(
    Path(id): Path<String>,
    State(state): State<AppState>,
    Json(payload): Json<PatchVersionRequest>,
) -> Result<Json<GameVersionResource>, ApiError> {
    Ok(Json(playnite::patch_version(&state, &id, payload).await?))
}

async fn list_save_snapshots(
    Path(id): Path<String>,
    State(state): State<AppState>,
) -> Result<Json<ListResponse<SaveSnapshotResource>>, ApiError> {
    let items = playnite::list_save_snapshots(&state, &id).await?;
    Ok(json(ListResponse {
        items,
        next_cursor: None,
    }))
}

async fn list_uploads(
    State(state): State<AppState>,
    Query(query): Query<ListQuery>,
) -> Result<Json<ListResponse<UploadResource>>, ApiError> {
    let items = upload_jobs::list_uploads(&state, query).await?;
    Ok(json(ListResponse {
        items,
        next_cursor: None,
    }))
}

async fn get_upload(
    Path(id): Path<String>,
    State(state): State<AppState>,
) -> Result<Json<UploadResource>, ApiError> {
    Ok(Json(upload_jobs::get_upload(&state, &id).await?))
}

async fn list_jobs(
    State(state): State<AppState>,
    Query(query): Query<ListQuery>,
) -> Result<Json<ListResponse<JobResource>>, ApiError> {
    let items = upload_jobs::list_jobs(&state, query).await?;
    Ok(json(ListResponse {
        items,
        next_cursor: None,
    }))
}

async fn get_job(
    Path(id): Path<String>,
    State(state): State<AppState>,
) -> Result<Json<JobResource>, ApiError> {
    Ok(Json(upload_jobs::get_job(&state, &id).await?))
}
