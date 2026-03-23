use axum::extract::{Path, State};
use axum::http::StatusCode;
use axum::routing::{get, patch, post, put};
use axum::{Json, Router};
use serde::Deserialize;

use crate::api::error::ApiError;
use crate::api::state::AppState;
use crate::api::types::{json, AcknowledgedResponse, JobResource, ListResponse, ResourceError, UploadResource};

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

async fn create_game_payload_upload() -> Result<(StatusCode, Json<UploadResource>), ApiError> {
    Ok((StatusCode::ACCEPTED, Json(sample_upload("game_payload"))))
}

async fn create_save_upload() -> Result<(StatusCode, Json<UploadResource>), ApiError> {
    Ok((StatusCode::ACCEPTED, Json(sample_upload("save_snapshot"))))
}

async fn put_upload_content(Path(_id): Path<String>) -> Json<AcknowledgedResponse> {
    json(AcknowledgedResponse {
        status: "accepted",
    })
}

async fn finalize_upload(Path(_id): Path<String>) -> Result<(StatusCode, Json<JobResource>), ApiError> {
    Ok((StatusCode::ACCEPTED, Json(sample_job())))
}

async fn list_uploads() -> Json<ListResponse<UploadResource>> {
    json(ListResponse {
        items: vec![],
        next_cursor: None,
    })
}

async fn get_upload(Path(id): Path<String>) -> Result<Json<UploadResource>, ApiError> {
    Ok(Json(sample_upload_with_id(&id, "game_payload")))
}

async fn list_jobs() -> Json<ListResponse<JobResource>> {
    json(ListResponse {
        items: vec![],
        next_cursor: None,
    })
}

async fn get_job(Path(id): Path<String>) -> Result<Json<JobResource>, ApiError> {
    let mut job = sample_job();
    job.id = id;
    Ok(Json(job))
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

fn sample_upload(kind: &str) -> UploadResource {
    sample_upload_with_id("upl_scaffold", kind)
}

fn sample_upload_with_id(id: &str, kind: &str) -> UploadResource {
    UploadResource {
        id: id.to_string(),
        kind: kind.to_string(),
        library_id: "library_primary".to_string(),
        platform: "pc".to_string(),
        game_id: Some("game_scaffold".to_string()),
        game_version_id: None,
        state: "created".to_string(),
        filename: "placeholder.bin".to_string(),
        declared_size_bytes: 0,
        received_size_bytes: 0,
        checksum: None,
        job_id: Some("job_scaffold".to_string()),
        created_at: "2026-03-22T20:00:00Z".to_string(),
        updated_at: "2026-03-22T20:00:00Z".to_string(),
        expires_at: None,
        error: None,
    }
}

fn sample_job() -> JobResource {
    JobResource {
        id: "job_scaffold".to_string(),
        kind: "import_archive".to_string(),
        state: "pending".to_string(),
        upload_id: Some("upl_scaffold".to_string()),
        game_id: Some("game_scaffold".to_string()),
        game_version_id: None,
        progress: Some(crate::api::types::JobProgress {
            phase: "queued".to_string(),
            percent: 0,
        }),
        result: None,
        error: Some(ResourceError {
            code: "not_implemented".to_string(),
            message: "background processing not implemented yet".to_string(),
            retryable: Some(false),
        }),
        created_at: "2026-03-22T20:00:00Z".to_string(),
        updated_at: "2026-03-22T20:00:00Z".to_string(),
    }
}
