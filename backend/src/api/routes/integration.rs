use axum::body::Bytes;
use axum::extract::{Path, Query, State};
use axum::http::StatusCode;
use axum::routing::{get, patch, post, put};
use axum::{Json, Router};
use serde::Deserialize;

use crate::api::error::ApiError;
use crate::api::state::AppState;
use crate::api::types::{json, AcknowledgedResponse, JobResource, ListResponse, UploadResource};
use crate::upload_jobs::{
    self, CreateGamePayloadUploadRequest, CreateSaveSnapshotUploadRequest, ListQuery,
};

pub fn router() -> Router<AppState> {
    Router::new()
        .route("/games", get(list_games))
        .route("/games/:id", get(get_game).patch(patch_game))
        .route("/versions/:id", patch(patch_version))
        .route("/versions/:id/install", get(get_install_manifest))
        .route("/versions/:id/save-snapshots", get(list_save_snapshots))
        .route("/versions/:id/save-uploads", post(create_save_upload))
        .route("/artifacts/:id/download", get(download_artifact))
        .route("/save-snapshots/:id/restore", get(get_save_restore_manifest))
        .route("/save-snapshots/:id/download", get(download_save_snapshot))
        .route("/uploads", get(list_uploads))
        .route("/uploads/:id", get(get_upload))
        .route("/uploads/:id/content", put(put_upload_content))
        .route("/uploads/:id/finalize", post(finalize_upload))
        .route("/uploads/game-payloads", post(create_game_payload_upload))
        .route("/uploads/save-snapshots", post(create_save_upload))
        .route("/jobs", get(list_jobs))
        .route("/jobs/:id", get(get_job))
}

#[derive(Debug, Deserialize)]
struct PatchPayload {
    #[serde(flatten)]
    fields: serde_json::Map<String, serde_json::Value>,
}

async fn list_games() -> Json<ListResponse<serde_json::Value>> {
    json(ListResponse {
        items: vec![],
        next_cursor: None,
    })
}

async fn get_game(Path(id): Path<String>) -> Result<Json<serde_json::Value>, ApiError> {
    Err(ApiError::not_found("game", &id))
}

async fn patch_game(
    Path(_id): Path<String>,
    State(_state): State<AppState>,
    Json(payload): Json<PatchPayload>,
) -> Result<Json<AcknowledgedResponse>, ApiError> {
    let _ = payload.fields.len();
    Err(ApiError::not_implemented("playnite game patch"))
}

async fn patch_version(
    Path(_id): Path<String>,
    Json(payload): Json<PatchPayload>,
) -> Result<Json<AcknowledgedResponse>, ApiError> {
    let _ = payload.fields.len();
    Err(ApiError::not_implemented("playnite version patch"))
}

async fn get_install_manifest(Path(_id): Path<String>) -> Result<Json<serde_json::Value>, ApiError> {
    Err(ApiError::not_implemented("install manifest"))
}

async fn list_save_snapshots(Path(_id): Path<String>) -> Json<ListResponse<serde_json::Value>> {
    json(ListResponse {
        items: vec![],
        next_cursor: None,
    })
}

async fn create_game_payload_upload(
    State(state): State<AppState>,
    Json(payload): Json<CreateGamePayloadUploadRequest>,
) -> Result<(StatusCode, Json<UploadResource>), ApiError> {
    let upload = upload_jobs::create_game_payload_upload(&state, payload).await?;
    Ok((StatusCode::ACCEPTED, Json(upload)))
}

async fn create_save_upload(
    State(state): State<AppState>,
    Json(payload): Json<CreateSaveSnapshotUploadRequest>,
) -> Result<(StatusCode, Json<UploadResource>), ApiError> {
    let upload = upload_jobs::create_save_snapshot_upload(&state, payload).await?;
    Ok((StatusCode::ACCEPTED, Json(upload)))
}

async fn put_upload_content(
    Path(id): Path<String>,
    State(state): State<AppState>,
    body: Bytes,
) -> Result<Json<UploadResource>, ApiError> {
    let upload = upload_jobs::put_upload_content(&state, &id, body).await?;
    Ok(Json(upload))
}

async fn finalize_upload(
    Path(id): Path<String>,
    State(state): State<AppState>,
) -> Result<(StatusCode, Json<JobResource>), ApiError> {
    let job = upload_jobs::finalize_upload(&state, &id).await?;
    Ok((StatusCode::ACCEPTED, Json(job)))
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

async fn download_artifact(Path(_id): Path<String>) -> Result<Json<AcknowledgedResponse>, ApiError> {
    Err(ApiError::not_implemented("artifact download"))
}

async fn get_save_restore_manifest(
    Path(_id): Path<String>,
) -> Result<Json<serde_json::Value>, ApiError> {
    Err(ApiError::not_implemented("save restore manifest"))
}

async fn download_save_snapshot(
    Path(_id): Path<String>,
) -> Result<Json<AcknowledgedResponse>, ApiError> {
    Err(ApiError::not_implemented("save snapshot download"))
}
