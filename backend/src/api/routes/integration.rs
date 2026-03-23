use axum::body::Bytes;
use axum::extract::DefaultBodyLimit;
use axum::extract::{Path, Query, State};
use axum::http::StatusCode;
use axum::middleware;
use axum::response::Response;
use axum::routing::{get, patch, post, put};
use axum::{Json, Router};

use crate::api::auth;
use crate::api::error::ApiError;
use crate::api::state::AppState;
use crate::api::types::{
    json, GameSummaryResource, GameVersionResource, ImportSessionResource,
    InstallManifestResource, JobResource, LibraryResource, ListResponse, SaveRestoreManifestResource,
    SaveSnapshotResource, UploadPartResource, UploadResource,
};
use crate::playnite::{self, PatchGameRequest, PatchVersionRequest};
use crate::upload_jobs::{
    self, CreateGamePayloadImportSessionRequest, CreateGamePayloadUploadRequest,
    CreateImportPartRequest, CreateSaveSnapshotImportSessionRequest, CreateSaveSnapshotUploadRequest,
    ListQuery,
};

pub fn router(state: AppState) -> Router<AppState> {
    Router::new()
        .route("/libraries", get(list_libraries))
        .route("/games", get(list_games))
        .route("/games/{id}", get(get_game).patch(patch_game))
        .route("/games/{id}/versions", get(list_versions))
        .route("/versions/{id}", patch(patch_version))
        .route("/versions/{id}/install", get(get_install_manifest))
        .route("/versions/{id}/save-snapshots", get(list_save_snapshots))
        .route("/versions/{id}/save-uploads", post(create_save_upload))
        .route("/artifacts/{id}/download", get(download_artifact))
        .route("/artifacts/{id}/parts/{part_index}/download", get(download_artifact_part))
        .route("/save-snapshots/{id}/restore", get(get_save_restore_manifest))
        .route("/save-snapshots/{id}/download", get(download_save_snapshot))
        .route(
            "/save-snapshots/{id}/parts/{part_index}/download",
            get(download_save_snapshot_part),
        )
        .route("/uploads", get(list_uploads))
        .route("/uploads/{id}", get(get_upload))
        .route("/import-sessions", get(list_import_sessions))
        .route("/import-sessions/{id}", get(get_import_session))
        .route("/import-sessions/{id}/parts", get(list_import_parts).post(create_import_part))
        .route("/import-sessions/{id}/finalize", post(finalize_import_session))
        .route("/import-sessions/game-payloads", post(create_game_payload_import_session))
        .route("/import-sessions/save-snapshots", post(create_save_snapshot_import_session))
        .route(
            "/upload-parts/{id}/content",
            put(put_import_part_content).layer(DefaultBodyLimit::disable()),
        )
        .route(
            "/uploads/{id}/content",
            put(put_upload_content).layer(DefaultBodyLimit::disable()),
        )
        .route("/uploads/{id}/finalize", post(finalize_upload))
        .route("/uploads/game-payloads", post(create_game_payload_upload))
        .route("/uploads/save-snapshots", post(create_save_upload))
        .route("/jobs", get(list_jobs))
        .route("/jobs/{id}", get(get_job))
        .route_layer(middleware::from_fn_with_state(
            state,
            auth::require_integration_token,
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

async fn list_libraries(
    State(state): State<AppState>,
) -> Result<Json<ListResponse<LibraryResource>>, ApiError> {
    Ok(json(ListResponse {
        items: playnite::list_libraries(&state),
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
) -> Result<Json<crate::api::types::GameVersionResource>, ApiError> {
    Ok(Json(playnite::patch_version(&state, &id, payload).await?))
}

async fn get_install_manifest(
    Path(id): Path<String>,
    State(state): State<AppState>,
) -> Result<Json<InstallManifestResource>, ApiError> {
    Ok(Json(playnite::get_install_manifest(&state, &id).await?))
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

async fn create_game_payload_upload(
    State(state): State<AppState>,
    Json(payload): Json<CreateGamePayloadUploadRequest>,
) -> Result<(StatusCode, Json<UploadResource>), ApiError> {
    let upload = upload_jobs::create_game_payload_upload(&state, payload).await?;
    Ok((StatusCode::ACCEPTED, Json(upload)))
}

async fn create_game_payload_import_session(
    State(state): State<AppState>,
    Json(payload): Json<CreateGamePayloadImportSessionRequest>,
) -> Result<(StatusCode, Json<ImportSessionResource>), ApiError> {
    let session = upload_jobs::create_game_payload_import_session(&state, payload).await?;
    Ok((StatusCode::ACCEPTED, Json(session)))
}

async fn create_save_upload(
    State(state): State<AppState>,
    Json(payload): Json<CreateSaveSnapshotUploadRequest>,
) -> Result<(StatusCode, Json<UploadResource>), ApiError> {
    let upload = upload_jobs::create_save_snapshot_upload(&state, payload).await?;
    Ok((StatusCode::ACCEPTED, Json(upload)))
}

async fn create_save_snapshot_import_session(
    State(state): State<AppState>,
    Json(payload): Json<CreateSaveSnapshotImportSessionRequest>,
) -> Result<(StatusCode, Json<ImportSessionResource>), ApiError> {
    let session = upload_jobs::create_save_snapshot_import_session(&state, payload).await?;
    Ok((StatusCode::ACCEPTED, Json(session)))
}

async fn list_import_sessions(
    State(state): State<AppState>,
    Query(query): Query<ListQuery>,
) -> Result<Json<ListResponse<ImportSessionResource>>, ApiError> {
    let items = upload_jobs::list_import_sessions(&state, query).await?;
    Ok(json(ListResponse {
        items,
        next_cursor: None,
    }))
}

async fn get_import_session(
    Path(id): Path<String>,
    State(state): State<AppState>,
) -> Result<Json<ImportSessionResource>, ApiError> {
    Ok(Json(upload_jobs::get_import_session(&state, &id).await?))
}

async fn create_import_part(
    Path(id): Path<String>,
    State(state): State<AppState>,
    Json(payload): Json<CreateImportPartRequest>,
) -> Result<(StatusCode, Json<UploadPartResource>), ApiError> {
    let part = upload_jobs::create_import_part(&state, &id, payload).await?;
    Ok((StatusCode::ACCEPTED, Json(part)))
}

async fn list_import_parts(
    Path(id): Path<String>,
    State(state): State<AppState>,
) -> Result<Json<ListResponse<UploadPartResource>>, ApiError> {
    let items = upload_jobs::list_import_parts(&state, &id).await?;
    Ok(json(ListResponse {
        items,
        next_cursor: None,
    }))
}

async fn put_import_part_content(
    Path(id): Path<String>,
    State(state): State<AppState>,
    body: Bytes,
) -> Result<Json<UploadPartResource>, ApiError> {
    let part = upload_jobs::put_import_part_content(&state, &id, body).await?;
    Ok(Json(part))
}

async fn finalize_import_session(
    Path(id): Path<String>,
    State(state): State<AppState>,
) -> Result<(StatusCode, Json<JobResource>), ApiError> {
    let job = upload_jobs::finalize_import_session(&state, &id).await?;
    Ok((StatusCode::ACCEPTED, Json(job)))
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

async fn download_artifact(
    Path(id): Path<String>,
    State(state): State<AppState>,
) -> Result<Response, ApiError> {
    playnite::download_artifact(&state, &id).await
}

async fn download_artifact_part(
    Path((id, part_index)): Path<(String, i32)>,
    State(state): State<AppState>,
) -> Result<Response, ApiError> {
    playnite::download_artifact_part(&state, &id, part_index).await
}

async fn get_save_restore_manifest(
    Path(id): Path<String>,
    State(state): State<AppState>,
) -> Result<Json<SaveRestoreManifestResource>, ApiError> {
    Ok(Json(playnite::get_save_restore_manifest(&state, &id).await?))
}

async fn download_save_snapshot(
    Path(id): Path<String>,
    State(state): State<AppState>,
) -> Result<Response, ApiError> {
    playnite::download_save_snapshot(&state, &id).await
}

async fn download_save_snapshot_part(
    Path((id, part_index)): Path<(String, i32)>,
    State(state): State<AppState>,
) -> Result<Response, ApiError> {
    playnite::download_save_snapshot_part(&state, &id, part_index).await
}
