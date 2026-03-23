use std::fs;
use std::io::{Read, Write};
use std::path::{Path, PathBuf};
use std::time::Duration;

use anyhow::{anyhow, Context, Result};
use axum::body::Bytes;
use serde::{Deserialize, Serialize};
use sha2::{Digest, Sha256};
use sqlx::{Row, SqlitePool};
use tokio::task::JoinHandle;
use uuid::Uuid;
use zip::write::SimpleFileOptions;

use crate::api::error::ApiError;
use crate::api::state::AppState;
use crate::api::types::{JobProgress, JobResource, ResourceError, UploadResource};

const ACTIVE_UPLOAD_RETENTION_HOURS: i64 = 24;
const COMPLETED_UPLOAD_RETENTION_HOURS: i64 = 72;

#[derive(Debug, Clone, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct CreateGamePayloadUploadRequest {
    pub library_id: String,
    pub platform: String,
    pub game: GameUploadTarget,
    pub version: VersionUploadTarget,
    pub file: UploadFileDescriptor,
    #[serde(default)]
    pub idempotency_key: Option<String>,
}

#[derive(Debug, Clone, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct CreateSaveSnapshotUploadRequest {
    pub game_version_id: String,
    pub name: String,
    pub file: UploadFileDescriptor,
    #[serde(default)]
    pub notes: Option<String>,
    #[serde(default)]
    pub idempotency_key: Option<String>,
}

#[derive(Debug, Clone, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct UploadFileDescriptor {
    pub filename: String,
    pub size_bytes: u64,
    #[serde(default)]
    pub checksum: Option<String>,
}

#[derive(Debug, Clone, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct VersionUploadTarget {
    pub version_name: String,
    #[serde(default)]
    pub version_code: Option<String>,
    #[serde(default)]
    pub notes: Option<String>,
}

#[derive(Debug, Clone, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct GameUploadTarget {
    #[serde(default)]
    pub id: Option<String>,
    #[serde(default)]
    pub create: Option<NewGameTarget>,
}

#[derive(Debug, Clone, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct NewGameTarget {
    pub name: String,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(tag = "kind", rename_all = "snake_case")]
enum UploadIntent {
    GamePayload {
        library_public_id: String,
        platform: String,
        game: GameUploadTarget,
        version: VersionUploadTarget,
        file: UploadFileDescriptor,
    },
    SaveSnapshot {
        game_version_public_id: String,
        name: String,
        file: UploadFileDescriptor,
        notes: Option<String>,
    },
}

#[derive(Debug, Clone)]
struct UploadRow {
    id: i64,
    public_id: String,
    kind: String,
    library_id: i64,
    platform_id: Option<i64>,
    game_id: Option<i64>,
    game_version_id: Option<i64>,
    state: String,
    filename: String,
    declared_size_bytes: i64,
    received_size_bytes: i64,
    checksum: Option<String>,
    temp_path: String,
    job_id: Option<i64>,
    expires_at: Option<String>,
    error_code: Option<String>,
    error_message: Option<String>,
    created_at: String,
    updated_at: String,
    intent_payload: String,
}

#[derive(Debug, Clone)]
struct JobRow {
    id: i64,
    public_id: String,
    kind: String,
    state: String,
    upload_id: Option<i64>,
    game_id: Option<i64>,
    game_version_id: Option<i64>,
    progress_phase: Option<String>,
    progress_percent: Option<i64>,
    result_payload: Option<String>,
    error_code: Option<String>,
    error_message: Option<String>,
    retryable: Option<i64>,
    created_at: String,
    updated_at: String,
}

#[derive(Debug, Clone, Deserialize)]
pub struct ListQuery {
    #[serde(default)]
    pub scope: Option<String>,
}

pub fn spawn_background_worker(state: AppState) -> JoinHandle<()> {
    tokio::spawn(async move {
        if let Err(err) = recover_incomplete_jobs(state.db()).await {
            tracing::error!(error = %err, "failed to recover incomplete jobs");
        }

        let mut interval = tokio::time::interval(Duration::from_secs(1));
        loop {
            interval.tick().await;

            if let Err(err) = run_queued_jobs_once(&state).await {
                tracing::error!(error = %err, "job dispatch iteration failed");
            }

            if let Err(err) = cleanup_stale_uploads(&state).await {
                tracing::error!(error = %err, "upload cleanup iteration failed");
            }
        }
    })
}

pub async fn create_game_payload_upload(
    state: &AppState,
    request: CreateGamePayloadUploadRequest,
) -> Result<UploadResource, ApiError> {
    if request.file.size_bytes == 0 {
        return Err(ApiError::bad_request("file.size_bytes must be greater than 0"));
    }
    validate_game_target(&request.game)?;

    if let Some(idempotency_key) = &request.idempotency_key {
        if let Some(existing) =
            find_upload_by_idempotency_key(state.db(), "game_payload", idempotency_key).await?
        {
            return Ok(upload_to_resource(existing)?);
        }
    }

    let library = lookup_library(state.db(), &request.library_id).await?;
    let platform = lookup_platform(state.db(), &request.platform).await?;
    let public_id = prefixed_id("upl");
    let temp_path = upload_temp_path(state, &public_id);
    let intent = UploadIntent::GamePayload {
        library_public_id: request.library_id.clone(),
        platform: request.platform.clone(),
        game: request.game.clone(),
        version: request.version.clone(),
        file: request.file.clone(),
    };

    let row = sqlx::query(
        r#"
        INSERT INTO uploads (
          public_id, kind, library_id, platform_id, game_id, game_version_id, state, filename,
          declared_size_bytes, received_size_bytes, checksum, temp_path, job_id, idempotency_key,
          expires_at, error_code, error_message, created_at, updated_at, intent_payload
        )
        VALUES (?1, 'game_payload', ?2, ?3, NULL, NULL, 'created', ?4, ?5, 0, ?6, ?7, NULL, ?8,
                datetime('now', '+24 hours'), NULL, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, ?9)
        RETURNING *
        "#,
    )
    .bind(&public_id)
    .bind(library.id)
    .bind(platform.id)
    .bind(&request.file.filename)
    .bind(i64::try_from(request.file.size_bytes).map_err(|_| ApiError::bad_request("file.size_bytes is too large"))?)
    .bind(&request.file.checksum)
    .bind(temp_path.to_string_lossy().to_string())
    .bind(&request.idempotency_key)
    .bind(serde_json::to_string(&intent).map_err(internal_error)?)
    .fetch_one(state.db())
    .await
    .map_err(internal_error)?;

    upload_to_resource(upload_from_row(row))
}

pub async fn create_save_snapshot_upload(
    state: &AppState,
    request: CreateSaveSnapshotUploadRequest,
) -> Result<UploadResource, ApiError> {
    if request.file.size_bytes == 0 {
        return Err(ApiError::bad_request("file.size_bytes must be greater than 0"));
    }

    if let Some(idempotency_key) = &request.idempotency_key {
        if let Some(existing) =
            find_upload_by_idempotency_key(state.db(), "save_snapshot", idempotency_key).await?
        {
            return Ok(upload_to_resource(existing)?);
        }
    }

    let game_version =
        lookup_game_version_with_library(state.db(), &request.game_version_id).await?;
    let platform_id = lookup_platform_id_for_library(state.db(), game_version.library_id).await?;
    let public_id = prefixed_id("upl");
    let temp_path = upload_temp_path(state, &public_id);
    let intent = UploadIntent::SaveSnapshot {
        game_version_public_id: request.game_version_id.clone(),
        name: request.name.clone(),
        file: request.file.clone(),
        notes: request.notes.clone(),
    };

    let row = sqlx::query(
        r#"
        INSERT INTO uploads (
          public_id, kind, library_id, platform_id, game_id, game_version_id, state, filename,
          declared_size_bytes, received_size_bytes, checksum, temp_path, job_id, idempotency_key,
          expires_at, error_code, error_message, created_at, updated_at, intent_payload
        )
        VALUES (?1, 'save_snapshot', ?2, ?3, ?4, ?5, 'created', ?6, ?7, 0, ?8, ?9, NULL, ?10,
                datetime('now', '+24 hours'), NULL, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, ?11)
        RETURNING *
        "#,
    )
    .bind(&public_id)
    .bind(game_version.library_id)
    .bind(platform_id)
    .bind(game_version.game_id)
    .bind(game_version.id)
    .bind(&request.file.filename)
    .bind(i64::try_from(request.file.size_bytes).map_err(|_| ApiError::bad_request("file.size_bytes is too large"))?)
    .bind(&request.file.checksum)
    .bind(temp_path.to_string_lossy().to_string())
    .bind(&request.idempotency_key)
    .bind(serde_json::to_string(&intent).map_err(internal_error)?)
    .fetch_one(state.db())
    .await
    .map_err(internal_error)?;

    upload_to_resource(upload_from_row(row))
}

pub async fn put_upload_content(
    state: &AppState,
    upload_public_id: &str,
    body: Bytes,
) -> Result<UploadResource, ApiError> {
    let upload = get_upload_row(state.db(), upload_public_id).await?;
    validate_upload_state(&upload.state, &["created", "abandoned"])?;

    sqlx::query(
        "UPDATE uploads SET state = 'uploading', updated_at = CURRENT_TIMESTAMP, error_code = NULL, error_message = NULL WHERE id = ?1",
    )
    .bind(upload.id)
    .execute(state.db())
    .await
    .map_err(internal_error)?;

    let temp_path = PathBuf::from(&upload.temp_path);
    if let Some(parent) = temp_path.parent() {
        fs::create_dir_all(parent).map_err(internal_error)?;
    }

    if let Err(err) = fs::write(&temp_path, &body) {
        mark_upload_abandoned(state.db(), upload.id, "write_failed", &err.to_string()).await?;
        return Err(ApiError::new(
            axum::http::StatusCode::INTERNAL_SERVER_ERROR,
            "upload_write_failed",
            "failed to persist upload content",
        ));
    }

    let received_size = i64::try_from(body.len()).map_err(|_| ApiError::bad_request("upload body is too large"))?;
    if received_size != upload.declared_size_bytes {
        mark_upload_abandoned(
            state.db(),
            upload.id,
            "size_mismatch",
            "received upload size does not match declared size",
        )
        .await?;
        return Err(ApiError::bad_request(
            "received upload size does not match declared size",
        ));
    }

    let row = sqlx::query(
        r#"
        UPDATE uploads
        SET state = 'uploaded', received_size_bytes = ?2, updated_at = CURRENT_TIMESTAMP
        WHERE id = ?1
        RETURNING *
        "#,
    )
    .bind(upload.id)
    .bind(received_size)
    .fetch_one(state.db())
    .await
    .map_err(internal_error)?;

    upload_to_resource(upload_from_row(row))
}

pub async fn finalize_upload(
    state: &AppState,
    upload_public_id: &str,
) -> Result<JobResource, ApiError> {
    let upload = get_upload_row(state.db(), upload_public_id).await?;
    if let Some(job_id) = upload.job_id {
        let job = get_job_by_internal_id(state.db(), job_id)
            .await
            .map_err(internal_error)?;
        return Ok(job_to_resource(job));
    }

    validate_upload_state(&upload.state, &["uploaded"])?;
    verify_uploaded_content(&upload)?;

    let kind = if upload.kind == "game_payload" {
        "import_archive"
    } else {
        "save_snapshot_archive"
    };
    let job_public_id = prefixed_id("job");

    let mut tx = state.db().begin().await.map_err(internal_error)?;
    let job_row = sqlx::query(
        r#"
        INSERT INTO jobs (
          public_id, kind, state, upload_id, game_id, game_version_id, progress_phase, progress_percent,
          result_payload, error_code, error_message, retryable, created_at, updated_at
        )
        VALUES (?1, ?2, 'pending', ?3, ?4, ?5, 'queued', 0, NULL, NULL, NULL, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
        RETURNING *
        "#,
    )
    .bind(&job_public_id)
    .bind(kind)
    .bind(upload.id)
    .bind(upload.game_id)
    .bind(upload.game_version_id)
    .fetch_one(&mut *tx)
    .await
    .map_err(internal_error)?;
    let job = job_from_row(job_row);

    sqlx::query(
        "UPDATE uploads SET state = 'queued', job_id = ?2, updated_at = CURRENT_TIMESTAMP WHERE id = ?1",
    )
    .bind(upload.id)
    .bind(job.id)
    .execute(&mut *tx)
    .await
    .map_err(internal_error)?;
    tx.commit().await.map_err(internal_error)?;

    Ok(job_to_resource(job))
}

pub async fn get_upload(state: &AppState, upload_public_id: &str) -> Result<UploadResource, ApiError> {
    let row = get_upload_row(state.db(), upload_public_id).await?;
    upload_to_resource(row)
}

pub async fn list_uploads(
    state: &AppState,
    query: ListQuery,
) -> Result<Vec<UploadResource>, ApiError> {
    let scope = query.scope.as_deref().unwrap_or("recent");
    let clause = upload_scope_clause(scope)?;
    let sql = format!("SELECT * FROM uploads WHERE {clause} ORDER BY updated_at DESC LIMIT 50");
    let rows = sqlx::query(&sql)
        .fetch_all(state.db())
        .await
        .map_err(internal_error)?;
    rows.into_iter()
        .map(|row| upload_to_resource(upload_from_row(row)))
        .collect()
}

pub async fn get_job(state: &AppState, job_public_id: &str) -> Result<JobResource, ApiError> {
    let row = get_job_row(state.db(), job_public_id).await?;
    Ok(job_to_resource(row))
}

pub async fn list_jobs(
    state: &AppState,
    query: ListQuery,
) -> Result<Vec<JobResource>, ApiError> {
    let scope = query.scope.as_deref().unwrap_or("recent");
    let clause = job_scope_clause(scope)?;
    let sql = format!("SELECT * FROM jobs WHERE {clause} ORDER BY updated_at DESC LIMIT 50");
    let rows = sqlx::query(&sql)
        .fetch_all(state.db())
        .await
        .map_err(internal_error)?;
    Ok(rows.into_iter().map(job_from_row).map(job_to_resource).collect())
}

pub async fn cleanup_stale_uploads(state: &AppState) -> Result<()> {
    let rows = sqlx::query(
        r#"
        SELECT * FROM uploads
        WHERE
          (state IN ('created', 'uploading', 'abandoned', 'uploaded') AND updated_at < datetime('now', ?1))
          OR
          (state IN ('completed', 'failed', 'expired') AND updated_at < datetime('now', ?2))
        "#,
    )
    .bind(format!("-{} hours", ACTIVE_UPLOAD_RETENTION_HOURS))
    .bind(format!("-{} hours", COMPLETED_UPLOAD_RETENTION_HOURS))
    .fetch_all(state.db())
    .await?;

    for row in rows {
        let upload = upload_from_row(row);
        let temp_path = PathBuf::from(&upload.temp_path);
        if temp_path.exists() {
            let _ = fs::remove_file(&temp_path);
        }
        sqlx::query(
            "UPDATE uploads SET state = 'expired', error_code = COALESCE(error_code, 'expired'), error_message = COALESCE(error_message, 'upload expired during cleanup'), updated_at = CURRENT_TIMESTAMP WHERE id = ?1",
        )
        .bind(upload.id)
        .execute(state.db())
        .await?;
    }

    Ok(())
}

pub async fn run_queued_jobs_once(state: &AppState) -> Result<()> {
    let rows = sqlx::query("SELECT * FROM jobs WHERE state = 'pending' ORDER BY created_at ASC LIMIT 10")
        .fetch_all(state.db())
        .await?;

    for row in rows {
        let job = job_from_row(row);
        if !claim_job(state.db(), job.id).await? {
            continue;
        }

        if let Err(err) = execute_job(state, job.id).await {
            tracing::error!(error = %err, job_id = job.id, "job execution failed");
            let _ = mark_job_failed(state.db(), &job, &err.to_string()).await;
        }
    }

    Ok(())
}

async fn execute_job(state: &AppState, job_id: i64) -> Result<()> {
    let job = get_job_by_internal_id(state.db(), job_id).await?;
    let upload_id = job.upload_id.ok_or_else(|| anyhow!("job {} missing upload_id", job.public_id))?;
    let upload = get_upload_by_internal_id(state.db(), upload_id).await?;
    let intent: UploadIntent = serde_json::from_str(&upload.intent_payload)?;

    set_job_progress(state.db(), job.id, "archiving", 25).await?;
    sqlx::query("UPDATE uploads SET state = 'processing', updated_at = CURRENT_TIMESTAMP WHERE id = ?1")
        .bind(upload.id)
        .execute(state.db())
        .await?;

    let result = match intent {
        UploadIntent::GamePayload {
            library_public_id,
            platform,
            game,
            version,
            file,
        } => {
            let created =
                process_game_payload_job(state, &upload, &library_public_id, &platform, &game, &version, &file)
                    .await?;
            serde_json::json!({
                "game_id": created.game_public_id,
                "game_version_id": created.game_version_public_id,
                "artifact_ids": [created.artifact_public_id],
            })
        }
        UploadIntent::SaveSnapshot {
            game_version_public_id,
            name,
            file,
            notes,
        } => {
            let created = process_save_snapshot_job(
                state,
                &upload,
                &game_version_public_id,
                &name,
                &file,
                notes.as_deref(),
            )
            .await?;
            serde_json::json!({
                "game_id": created.game_public_id,
                "game_version_id": created.game_version_public_id,
                "save_snapshot_id": created.snapshot_public_id,
            })
        }
    };

    sqlx::query(
        r#"
        UPDATE jobs
        SET state = 'completed', progress_phase = 'completed', progress_percent = 100,
            result_payload = ?2, error_code = NULL, error_message = NULL, retryable = 0,
            updated_at = CURRENT_TIMESTAMP
        WHERE id = ?1
        "#,
    )
    .bind(job.id)
    .bind(result.to_string())
    .execute(state.db())
    .await?;

    sqlx::query(
        "UPDATE uploads SET state = 'completed', error_code = NULL, error_message = NULL, updated_at = CURRENT_TIMESTAMP WHERE id = ?1",
    )
    .bind(upload.id)
    .execute(state.db())
    .await?;

    let temp_path = PathBuf::from(&upload.temp_path);
    if temp_path.exists() {
        let _ = fs::remove_file(temp_path);
    }

    Ok(())
}

async fn process_game_payload_job(
    state: &AppState,
    upload: &UploadRow,
    library_public_id: &str,
    platform: &str,
    game_target: &GameUploadTarget,
    version_target: &VersionUploadTarget,
    file: &UploadFileDescriptor,
) -> Result<GamePayloadResult> {
    let library = lookup_library(state.db(), library_public_id)
        .await
        .map_err(|err| anyhow!(err.message))?;
    let platform_row = lookup_platform(state.db(), platform)
        .await
        .map_err(|err| anyhow!(err.message))?;
    let mut tx = state.db().begin().await?;

    let game = match (&game_target.id, &game_target.create) {
        (Some(existing_id), _) => lookup_game_in_tx(&mut tx, existing_id).await?,
        (None, Some(create)) => {
            let public_id = prefixed_id("game");
            let visibility = visibility_str_from_row(library.visibility);
            let row = sqlx::query(
                r#"
                INSERT INTO games (
                  public_id, library_id, name, sorting_name, description, release_date,
                  cover_image, background_image, icon, source_slug, visibility, created_at, updated_at
                )
                VALUES (?1, ?2, ?3, ?3, NULL, NULL, NULL, NULL, NULL, NULL, ?4, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
                RETURNING id, public_id
                "#,
            )
            .bind(&public_id)
            .bind(library.id)
            .bind(&create.name)
            .bind(visibility)
            .fetch_one(&mut *tx)
            .await?;

            sqlx::query(
                "INSERT OR IGNORE INTO game_platforms (game_id, platform_id) VALUES (?1, ?2)",
            )
            .bind(row.get::<i64, _>("id"))
            .bind(platform_row.id)
            .execute(&mut *tx)
            .await?;

            BasicEntity {
                id: row.get("id"),
                public_id: row.get("public_id"),
            }
        }
        _ => return Err(anyhow!("game upload target must specify either id or create")),
    };

    sqlx::query("UPDATE game_versions SET is_latest = 0, updated_at = CURRENT_TIMESTAMP WHERE game_id = ?1")
        .bind(game.id)
        .execute(&mut *tx)
        .await?;

    let version_public_id = prefixed_id("ver");
    let version_row = sqlx::query(
        r#"
        INSERT INTO game_versions (
          public_id, game_id, library_id, version_name, version_code, release_date, notes,
          is_latest, storage_mode, created_at, updated_at
        )
        VALUES (?1, ?2, ?3, ?4, ?5, NULL, ?6, 1, 'managed_archive', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
        RETURNING id, public_id
        "#,
    )
    .bind(&version_public_id)
    .bind(game.id)
    .bind(library.id)
    .bind(&version_target.version_name)
    .bind(&version_target.version_code)
    .bind(&version_target.notes)
    .fetch_one(&mut *tx)
    .await?;

    let version_entity = BasicEntity {
        id: version_row.get("id"),
        public_id: version_row.get("public_id"),
    };
    let archive = write_archive_for_upload(
        &library.root_path,
        "games",
        &version_entity.public_id,
        &upload.filename,
        &upload.temp_path,
    )
    .await?;

    let artifact_public_id = prefixed_id("art");
    sqlx::query(
        r#"
        INSERT INTO version_artifacts (
          public_id, game_version_id, artifact_kind, archive_type, relative_path, size_bytes,
          checksum, part_count, is_managed, created_at
        )
        VALUES (?1, ?2, 'game_payload', 'zip', ?3, ?4, ?5, 1, 1, CURRENT_TIMESTAMP)
        "#,
    )
    .bind(&artifact_public_id)
    .bind(version_entity.id)
    .bind(&archive.relative_path)
    .bind(archive.size_bytes)
    .bind(&archive.checksum)
    .execute(&mut *tx)
    .await?;

    tx.commit().await?;

    let _ = file;
    Ok(GamePayloadResult {
        game_public_id: game.public_id,
        game_version_public_id: version_entity.public_id,
        artifact_public_id,
    })
}

async fn process_save_snapshot_job(
    state: &AppState,
    upload: &UploadRow,
    game_version_public_id: &str,
    name: &str,
    _file: &UploadFileDescriptor,
    notes: Option<&str>,
) -> Result<SaveSnapshotResult> {
    let game_version = lookup_game_version_with_library(state.db(), game_version_public_id)
        .await
        .map_err(|err| anyhow!(err.message))?;
    let library = get_library_by_internal_id(state.db(), game_version.library_id).await?;
    let snapshot_public_id = prefixed_id("save");

    let archive = write_archive_for_upload(
        &library.root_path,
        "saves",
        game_version_public_id,
        &upload.filename,
        &upload.temp_path,
    )
    .await?;

    sqlx::query(
        r#"
        INSERT INTO save_snapshots (
          public_id, game_id, game_version_id, library_id, name, captured_at, archive_type,
          size_bytes, checksum, notes, created_at
        )
        VALUES (?1, ?2, ?3, ?4, ?5, CURRENT_TIMESTAMP, 'zip', ?6, ?7, ?8, CURRENT_TIMESTAMP)
        "#,
    )
    .bind(&snapshot_public_id)
    .bind(game_version.game_id)
    .bind(game_version.id)
    .bind(game_version.library_id)
    .bind(name)
    .bind(archive.size_bytes)
    .bind(&archive.checksum)
    .bind(notes)
    .execute(state.db())
    .await?;

    Ok(SaveSnapshotResult {
        game_public_id: game_version.game_public_id,
        game_version_public_id: game_version.public_id,
        snapshot_public_id,
    })
}

async fn recover_incomplete_jobs(pool: &SqlitePool) -> Result<()> {
    sqlx::query(
        "UPDATE jobs SET state = 'pending', progress_phase = 'recovered', updated_at = CURRENT_TIMESTAMP WHERE state = 'processing'",
    )
    .execute(pool)
    .await?;
    sqlx::query(
        "UPDATE uploads SET state = 'queued', updated_at = CURRENT_TIMESTAMP WHERE state = 'processing'",
    )
    .execute(pool)
    .await?;
    Ok(())
}

async fn claim_job(pool: &SqlitePool, job_id: i64) -> Result<bool> {
    let updated = sqlx::query(
        "UPDATE jobs SET state = 'processing', progress_phase = 'processing', progress_percent = 5, updated_at = CURRENT_TIMESTAMP WHERE id = ?1 AND state = 'pending'",
    )
    .bind(job_id)
    .execute(pool)
    .await?;
    Ok(updated.rows_affected() == 1)
}

async fn set_job_progress(pool: &SqlitePool, job_id: i64, phase: &str, percent: u8) -> Result<()> {
    if job_id == 0 {
        return Ok(());
    }
    sqlx::query(
        "UPDATE jobs SET progress_phase = ?2, progress_percent = ?3, updated_at = CURRENT_TIMESTAMP WHERE id = ?1",
    )
    .bind(job_id)
    .bind(phase)
    .bind(i64::from(percent))
    .execute(pool)
    .await?;
    Ok(())
}

async fn mark_upload_abandoned(pool: &SqlitePool, upload_id: i64, code: &str, message: &str) -> Result<(), ApiError> {
    sqlx::query(
        "UPDATE uploads SET state = 'abandoned', error_code = ?2, error_message = ?3, updated_at = CURRENT_TIMESTAMP WHERE id = ?1",
    )
    .bind(upload_id)
    .bind(code)
    .bind(message)
    .execute(pool)
    .await
    .map_err(internal_error)?;
    Ok(())
}

async fn find_upload_by_idempotency_key(
    pool: &SqlitePool,
    kind: &str,
    idempotency_key: &str,
) -> Result<Option<UploadRow>, ApiError> {
    let row = sqlx::query(
        "SELECT * FROM uploads WHERE kind = ?1 AND idempotency_key = ?2 ORDER BY created_at DESC LIMIT 1",
    )
    .bind(kind)
    .bind(idempotency_key)
    .fetch_optional(pool)
    .await
    .map_err(internal_error)?;

    Ok(row.map(upload_from_row))
}

async fn lookup_library(pool: &SqlitePool, public_id: &str) -> Result<LibraryLookup, ApiError> {
    let row = sqlx::query("SELECT id, root_path, visibility FROM libraries WHERE public_id = ?1")
        .bind(public_id)
        .fetch_optional(pool)
        .await
        .map_err(internal_error)?
        .ok_or_else(|| ApiError::not_found("library", public_id))?;

    Ok(LibraryLookup {
        id: row.get("id"),
        root_path: row.get("root_path"),
        visibility: row.get("visibility"),
    })
}

async fn get_library_by_internal_id(pool: &SqlitePool, library_id: i64) -> Result<LibraryLookup> {
    let row = sqlx::query("SELECT id, root_path, visibility FROM libraries WHERE id = ?1")
        .bind(library_id)
        .fetch_one(pool)
        .await?;
    Ok(LibraryLookup {
        id: row.get("id"),
        root_path: row.get("root_path"),
        visibility: row.get("visibility"),
    })
}

async fn lookup_platform(pool: &SqlitePool, platform: &str) -> Result<BasicEntity, ApiError> {
    let row = sqlx::query("SELECT id, public_id FROM platforms WHERE name = ?1")
        .bind(platform)
        .fetch_optional(pool)
        .await
        .map_err(internal_error)?
        .ok_or_else(|| ApiError::not_found("platform", platform))?;

    Ok(BasicEntity {
        id: row.get("id"),
        public_id: row.get("public_id"),
    })
}

async fn lookup_platform_id_for_library(pool: &SqlitePool, library_id: i64) -> Result<Option<i64>, ApiError> {
    let row = sqlx::query(
        r#"
        SELECT p.id
        FROM libraries l
        LEFT JOIN platforms p ON p.name = l.platform_hint
        WHERE l.id = ?1
        "#,
    )
    .bind(library_id)
    .fetch_optional(pool)
    .await
    .map_err(internal_error)?;
    Ok(row.map(|row| row.get("id")))
}

async fn lookup_game_version_with_library(
    pool: &SqlitePool,
    public_id: &str,
) -> Result<GameVersionLookup, ApiError> {
    let row = sqlx::query(
        r#"
        SELECT gv.id, gv.public_id, gv.game_id, gv.library_id, g.public_id AS game_public_id
        FROM game_versions gv
        INNER JOIN games g ON g.id = gv.game_id
        WHERE gv.public_id = ?1
        "#,
    )
    .bind(public_id)
    .fetch_optional(pool)
    .await
    .map_err(internal_error)?
    .ok_or_else(|| ApiError::not_found("game version", public_id))?;

    Ok(GameVersionLookup {
        id: row.get("id"),
        public_id: row.get("public_id"),
        game_id: row.get("game_id"),
        library_id: row.get("library_id"),
        game_public_id: row.get("game_public_id"),
    })
}

async fn lookup_game_in_tx(
    tx: &mut sqlx::Transaction<'_, sqlx::Sqlite>,
    public_id: &str,
) -> Result<BasicEntity> {
    let row = sqlx::query("SELECT id, public_id FROM games WHERE public_id = ?1")
        .bind(public_id)
        .fetch_one(&mut **tx)
        .await?;
    Ok(BasicEntity {
        id: row.get("id"),
        public_id: row.get("public_id"),
    })
}

async fn get_upload_row(pool: &SqlitePool, public_id: &str) -> Result<UploadRow, ApiError> {
    let row = sqlx::query("SELECT * FROM uploads WHERE public_id = ?1")
        .bind(public_id)
        .fetch_optional(pool)
        .await
        .map_err(internal_error)?
        .ok_or_else(|| ApiError::not_found("upload", public_id))?;
    Ok(upload_from_row(row))
}

async fn get_upload_by_internal_id(pool: &SqlitePool, upload_id: i64) -> Result<UploadRow> {
    let row = sqlx::query("SELECT * FROM uploads WHERE id = ?1")
        .bind(upload_id)
        .fetch_one(pool)
        .await?;
    Ok(upload_from_row(row))
}

async fn get_job_row(pool: &SqlitePool, public_id: &str) -> Result<JobRow, ApiError> {
    let row = sqlx::query("SELECT * FROM jobs WHERE public_id = ?1")
        .bind(public_id)
        .fetch_optional(pool)
        .await
        .map_err(internal_error)?
        .ok_or_else(|| ApiError::not_found("job", public_id))?;
    Ok(job_from_row(row))
}

async fn get_job_by_internal_id(pool: &SqlitePool, job_id: i64) -> Result<JobRow> {
    let row = sqlx::query("SELECT * FROM jobs WHERE id = ?1")
        .bind(job_id)
        .fetch_one(pool)
        .await?;
    Ok(job_from_row(row))
}

fn upload_from_row(row: sqlx::sqlite::SqliteRow) -> UploadRow {
    UploadRow {
        id: row.get("id"),
        public_id: row.get("public_id"),
        kind: row.get("kind"),
        library_id: row.get("library_id"),
        platform_id: row.get("platform_id"),
        game_id: row.get("game_id"),
        game_version_id: row.get("game_version_id"),
        state: row.get("state"),
        filename: row.get("filename"),
        declared_size_bytes: row.get("declared_size_bytes"),
        received_size_bytes: row.get("received_size_bytes"),
        checksum: row.get("checksum"),
        temp_path: row.get("temp_path"),
        job_id: row.get("job_id"),
        expires_at: row.get("expires_at"),
        error_code: row.get("error_code"),
        error_message: row.get("error_message"),
        created_at: row.get("created_at"),
        updated_at: row.get("updated_at"),
        intent_payload: row.get("intent_payload"),
    }
}

fn job_from_row(row: sqlx::sqlite::SqliteRow) -> JobRow {
    JobRow {
        id: row.get("id"),
        public_id: row.get("public_id"),
        kind: row.get("kind"),
        state: row.get("state"),
        upload_id: row.get("upload_id"),
        game_id: row.get("game_id"),
        game_version_id: row.get("game_version_id"),
        progress_phase: row.get("progress_phase"),
        progress_percent: row.get("progress_percent"),
        result_payload: row.get("result_payload"),
        error_code: row.get("error_code"),
        error_message: row.get("error_message"),
        retryable: row.get("retryable"),
        created_at: row.get("created_at"),
        updated_at: row.get("updated_at"),
    }
}

fn upload_to_resource(upload: UploadRow) -> Result<UploadResource, ApiError> {
    let state = upload.state.clone();
    let retryable = matches!(state.as_str(), "abandoned" | "failed");
    let platform = upload.platform_id.map(|_| "pc".to_string()).unwrap_or_else(|| "pc".to_string());
    Ok(UploadResource {
        id: upload.public_id,
        kind: upload.kind,
        library_id: library_public_id(upload.library_id),
        platform,
        game_id: upload.game_id.map(game_public_id),
        game_version_id: upload.game_version_id.map(game_version_public_id),
        state,
        filename: upload.filename,
        declared_size_bytes: u64::try_from(upload.declared_size_bytes).map_err(internal_error)?,
        received_size_bytes: u64::try_from(upload.received_size_bytes).map_err(internal_error)?,
        checksum: upload.checksum,
        job_id: upload.job_id.map(job_public_id),
        created_at: timestamp_to_rfc3339(&upload.created_at),
        updated_at: timestamp_to_rfc3339(&upload.updated_at),
        expires_at: upload.expires_at.map(|value| timestamp_to_rfc3339(&value)),
        error: upload.error_code.map(|code| ResourceError {
            code,
            message: upload
                .error_message
                .unwrap_or_else(|| "upload failed".to_string()),
            retryable: Some(retryable),
        }),
    })
}

fn job_to_resource(job: JobRow) -> JobResource {
    JobResource {
        id: job.public_id,
        kind: job.kind,
        state: job.state.clone(),
        upload_id: job.upload_id.map(upload_public_id),
        game_id: job.game_id.map(game_public_id),
        game_version_id: job.game_version_id.map(game_version_public_id),
        progress: job.progress_phase.map(|phase| JobProgress {
            phase,
            percent: u8::try_from(job.progress_percent.unwrap_or(0)).unwrap_or(0),
        }),
        result: job
            .result_payload
            .and_then(|value| serde_json::from_str(&value).ok()),
        error: job.error_code.map(|code| ResourceError {
            code,
            message: job.error_message.unwrap_or_else(|| "job failed".to_string()),
            retryable: job.retryable.map(|value| value == 1),
        }),
        created_at: timestamp_to_rfc3339(&job.created_at),
        updated_at: timestamp_to_rfc3339(&job.updated_at),
    }
}

fn validate_game_target(target: &GameUploadTarget) -> Result<(), ApiError> {
    match (&target.id, &target.create) {
        (Some(_), None) | (None, Some(_)) => Ok(()),
        _ => Err(ApiError::bad_request(
            "game target must specify either game.id or game.create",
        )),
    }
}

fn validate_upload_state(current: &str, allowed: &[&str]) -> Result<(), ApiError> {
    if allowed.contains(&current) {
        Ok(())
    } else {
        Err(ApiError::bad_request(format!(
            "upload cannot transition from state '{current}'"
        )))
    }
}

fn verify_uploaded_content(upload: &UploadRow) -> Result<(), ApiError> {
    let metadata = fs::metadata(&upload.temp_path)
        .with_context(|| format!("failed to stat upload temp file {}", upload.temp_path))
        .map_err(internal_error)?;
    let size = i64::try_from(metadata.len()).map_err(internal_error)?;
    if size != upload.declared_size_bytes || size != upload.received_size_bytes {
        return Err(ApiError::bad_request(
            "upload file size does not match recorded upload metadata",
        ));
    }

    if let Some(expected) = &upload.checksum {
        let actual = compute_sha256(Path::new(&upload.temp_path)).map_err(internal_error)?;
        if expected != &actual {
            return Err(ApiError::bad_request("upload checksum verification failed"));
        }
    }

    Ok(())
}

async fn write_archive_for_upload(
    library_root: &str,
    prefix: &str,
    record_public_id: &str,
    original_filename: &str,
    temp_path: &str,
) -> Result<ArchiveResult> {
    let library_root = PathBuf::from(library_root);
    let relative = PathBuf::from(prefix)
        .join(record_public_id)
        .join("payload.zip");
    let final_path = library_root.join(&relative);
    if let Some(parent) = final_path.parent() {
        fs::create_dir_all(parent)?;
    }

    let temp_path = PathBuf::from(temp_path);
    let final_path_for_task = final_path.clone();
    let original_filename = original_filename.to_string();
    let temp_path_for_task = temp_path.clone();
    tokio::task::spawn_blocking(move || {
        let input = fs::read(&temp_path_for_task)?;
        let file = fs::File::create(&final_path_for_task)?;
        let mut archive = zip::ZipWriter::new(file);
        let options = SimpleFileOptions::default()
            .compression_method(zip::CompressionMethod::Deflated);
        archive.start_file(original_filename, options)?;
        archive.write_all(&input)?;
        archive.finish()?;
        Result::<(), anyhow::Error>::Ok(())
    })
    .await??;

    let metadata = fs::metadata(&final_path)?;
    let checksum = compute_sha256(&final_path)?;
    Ok(ArchiveResult {
        relative_path: relative.to_string_lossy().to_string(),
        size_bytes: i64::try_from(metadata.len())?,
        checksum,
    })
}

async fn mark_job_failed(pool: &SqlitePool, job: &JobRow, message: &str) -> Result<()> {
    sqlx::query(
        r#"
        UPDATE jobs
        SET state = 'failed', error_code = 'job_execution_failed', error_message = ?2,
            retryable = 1, updated_at = CURRENT_TIMESTAMP
        WHERE id = ?1
        "#,
    )
    .bind(job.id)
    .bind(message)
    .execute(pool)
    .await?;

    if let Some(upload_id) = job.upload_id {
        sqlx::query(
            "UPDATE uploads SET state = 'failed', error_code = 'job_execution_failed', error_message = ?2, updated_at = CURRENT_TIMESTAMP WHERE id = ?1",
        )
        .bind(upload_id)
        .bind(message)
        .execute(pool)
        .await?;
    }

    Ok(())
}

fn compute_sha256(path: &Path) -> Result<String> {
    let mut file = fs::File::open(path)?;
    let mut hasher = Sha256::new();
    let mut buffer = [0_u8; 16 * 1024];
    loop {
        let read = file.read(&mut buffer)?;
        if read == 0 {
            break;
        }
        hasher.update(&buffer[..read]);
    }
    Ok(format!("sha256:{}", hex::encode(hasher.finalize())))
}

fn upload_temp_path(state: &AppState, upload_public_id: &str) -> PathBuf {
    let root = state
        .config()
        .storage
        .temp_dir
        .clone()
        .unwrap_or_else(|| {
            state
                .config()
                .libraries
                .first()
                .map(|library| library.root_path.join("tmp"))
                .unwrap_or_else(|| PathBuf::from("./tmp"))
        });
    root.join(format!("{upload_public_id}.upload"))
}

fn upload_scope_clause(scope: &str) -> Result<&'static str, ApiError> {
    match scope {
        "active" => Ok("state IN ('created', 'uploading', 'uploaded', 'queued', 'processing')"),
        "failed" => Ok("state IN ('failed', 'abandoned', 'expired')"),
        "recent" | "all" => Ok("1 = 1"),
        _ => Err(ApiError::bad_request("invalid upload scope")),
    }
}

fn job_scope_clause(scope: &str) -> Result<&'static str, ApiError> {
    match scope {
        "active" => Ok("state IN ('pending', 'processing')"),
        "failed" => Ok("state = 'failed'"),
        "recent" | "all" => Ok("1 = 1"),
        _ => Err(ApiError::bad_request("invalid job scope")),
    }
}

fn prefixed_id(prefix: &str) -> String {
    format!("{prefix}_{}", Uuid::new_v4().simple())
}

fn timestamp_to_rfc3339(value: &str) -> String {
    value.replace(' ', "T") + "Z"
}

fn visibility_str_from_row(value: String) -> &'static str {
    match value.as_str() {
        "public" => "public",
        _ => "private",
    }
}

fn library_public_id(id: i64) -> String {
    format!("library_row_{id}")
}

fn game_public_id(id: i64) -> String {
    format!("game_row_{id}")
}

fn game_version_public_id(id: i64) -> String {
    format!("version_row_{id}")
}

fn upload_public_id(id: i64) -> String {
    format!("upload_row_{id}")
}

fn job_public_id(id: i64) -> String {
    format!("job_row_{id}")
}

fn internal_error(err: impl std::fmt::Display) -> ApiError {
    ApiError::new(
        axum::http::StatusCode::INTERNAL_SERVER_ERROR,
        "internal_error",
        err.to_string(),
    )
}

#[derive(Debug, Clone)]
struct BasicEntity {
    id: i64,
    public_id: String,
}

#[derive(Debug, Clone)]
struct LibraryLookup {
    id: i64,
    root_path: String,
    visibility: String,
}

#[derive(Debug, Clone)]
struct GameVersionLookup {
    id: i64,
    public_id: String,
    game_id: i64,
    library_id: i64,
    game_public_id: String,
}

#[derive(Debug)]
struct ArchiveResult {
    relative_path: String,
    size_bytes: i64,
    checksum: String,
}

struct GamePayloadResult {
    game_public_id: String,
    game_version_public_id: String,
    artifact_public_id: String,
}

struct SaveSnapshotResult {
    game_public_id: String,
    game_version_public_id: String,
    snapshot_public_id: String,
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::config::AppConfig;
    use crate::db::{connect_and_migrate, sync_config_reference_data};

    fn temp_test_root() -> PathBuf {
        let unique = format!("gumo-upload-test-{}-{}", std::process::id(), Uuid::new_v4());
        std::env::temp_dir().join(unique)
    }

    fn test_config(root: &Path) -> AppConfig {
        let raw = format!(
            r#"
[server]
listen_address = "127.0.0.1"
port = 8080

[storage]
database_path = "{}"
cache_dir = "{}"
temp_dir = "{}"

[auth]
admin_mode = "local"
session_secret_file = "{}/session-secret"
owner_password_hash_file = "{}/password-hash"
api_tokens_file = "{}/api-tokens.toml"

[integrations.playnite]
enabled = true
allow_uploads = true
default_platform = "pc"

[[platforms]]
id = "pc"
enabled = true
match_priority = 100

[[libraries]]
name = "primary"
root_path = "{}"
platform = "pc"
visibility = "private"
enabled = true
"#,
            root.join("data/gumo.db").display(),
            root.join("cache").display(),
            root.join("tmp").display(),
            root.join("secrets").display(),
            root.join("secrets").display(),
            root.join("secrets").display(),
            root.join("storage").display(),
        );
        let config: AppConfig = toml::from_str(&raw).expect("test config should parse");
        config.validate().expect("test config should validate");
        config
    }

    async fn make_state() -> (AppState, PathBuf) {
        let root = temp_test_root();
        let config = test_config(&root);
        let pool = connect_and_migrate(&config.storage).await.expect("db setup");
        sync_config_reference_data(&pool, &config)
            .await
            .expect("reference sync");
        (AppState::new(config, pool), root)
    }

    #[tokio::test]
    async fn game_payload_upload_happy_path() {
        let (state, root) = make_state().await;
        let upload = create_game_payload_upload(
            &state,
            CreateGamePayloadUploadRequest {
                library_id: "library_primary".to_string(),
                platform: "pc".to_string(),
                game: GameUploadTarget {
                    id: None,
                    create: Some(NewGameTarget {
                        name: "Example".to_string(),
                    }),
                },
                version: VersionUploadTarget {
                    version_name: "1.0.0".to_string(),
                    version_code: None,
                    notes: None,
                },
                file: UploadFileDescriptor {
                    filename: "setup.exe".to_string(),
                    size_bytes: 5,
                    checksum: None,
                },
                idempotency_key: Some("key-1".to_string()),
            },
        )
        .await
        .expect("upload create");

        put_upload_content(&state, &upload.id, Bytes::from_static(b"hello"))
            .await
            .expect("content write");
        let job = finalize_upload(&state, &upload.id).await.expect("finalize");
        let second = finalize_upload(&state, &upload.id).await.expect("idempotent finalize");
        assert_eq!(job.id, second.id);

        run_queued_jobs_once(&state).await.expect("job run");
        let stored_job = get_job(&state, &job.id).await.expect("job fetch");
        assert_eq!(stored_job.state, "completed");

        let artifacts: i64 = sqlx::query_scalar("SELECT COUNT(*) FROM version_artifacts")
            .fetch_one(state.db())
            .await
            .expect("artifact count");
        assert_eq!(artifacts, 1);

        state.db().close().await;
        let _ = fs::remove_dir_all(root);
    }

    #[tokio::test]
    async fn save_snapshot_upload_happy_path() {
        let (state, root) = make_state().await;
        let game_id = prefixed_id("game");
        let version_id = prefixed_id("ver");
        sqlx::query(
            "INSERT INTO games (public_id, library_id, name, sorting_name, description, release_date, cover_image, background_image, icon, source_slug, visibility, created_at, updated_at) VALUES (?1, 1, 'Example', 'Example', NULL, NULL, NULL, NULL, NULL, NULL, 'private', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)",
        )
        .bind(&game_id)
        .execute(state.db())
        .await
        .expect("insert game");
        let game_row_id: i64 = sqlx::query_scalar("SELECT id FROM games WHERE public_id = ?1")
            .bind(&game_id)
            .fetch_one(state.db())
            .await
            .expect("game id");
        sqlx::query(
            "INSERT INTO game_versions (public_id, game_id, library_id, version_name, version_code, release_date, notes, is_latest, storage_mode, created_at, updated_at) VALUES (?1, ?2, 1, '1.0.0', NULL, NULL, NULL, 1, 'managed_archive', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)",
        )
        .bind(&version_id)
        .bind(game_row_id)
        .execute(state.db())
        .await
        .expect("insert version");

        let upload = create_save_snapshot_upload(
            &state,
            CreateSaveSnapshotUploadRequest {
                game_version_id: version_id.clone(),
                name: "Before patch".to_string(),
                file: UploadFileDescriptor {
                    filename: "save.dat".to_string(),
                    size_bytes: 4,
                    checksum: None,
                },
                notes: Some("baseline".to_string()),
                idempotency_key: Some("save-1".to_string()),
            },
        )
        .await
        .expect("save upload create");

        put_upload_content(&state, &upload.id, Bytes::from_static(b"save"))
            .await
            .expect("save content");
        let job = finalize_upload(&state, &upload.id).await.expect("save finalize");
        run_queued_jobs_once(&state).await.expect("save job run");
        let stored_job = get_job(&state, &job.id).await.expect("save job fetch");
        assert_eq!(stored_job.state, "completed");

        let snapshots: i64 = sqlx::query_scalar("SELECT COUNT(*) FROM save_snapshots")
            .fetch_one(state.db())
            .await
            .expect("snapshot count");
        assert_eq!(snapshots, 1);

        state.db().close().await;
        let _ = fs::remove_dir_all(root);
    }

    #[tokio::test]
    async fn cleanup_expires_stale_upload() {
        let (state, root) = make_state().await;
        let upload = create_game_payload_upload(
            &state,
            CreateGamePayloadUploadRequest {
                library_id: "library_primary".to_string(),
                platform: "pc".to_string(),
                game: GameUploadTarget {
                    id: None,
                    create: Some(NewGameTarget {
                        name: "Cleanup".to_string(),
                    }),
                },
                version: VersionUploadTarget {
                    version_name: "1.0.0".to_string(),
                    version_code: None,
                    notes: None,
                },
                file: UploadFileDescriptor {
                    filename: "cleanup.bin".to_string(),
                    size_bytes: 3,
                    checksum: None,
                },
                idempotency_key: None,
            },
        )
        .await
        .expect("upload create");

        let temp_path: String = sqlx::query_scalar("SELECT temp_path FROM uploads WHERE public_id = ?1")
            .bind(&upload.id)
            .fetch_one(state.db())
            .await
            .expect("temp path");
        fs::write(&temp_path, b"tmp").expect("write temp file");

        sqlx::query("UPDATE uploads SET updated_at = datetime('now', '-48 hours') WHERE public_id = ?1")
            .bind(&upload.id)
            .execute(state.db())
            .await
            .expect("age upload");

        cleanup_stale_uploads(&state).await.expect("cleanup");
        let state_value: String = sqlx::query_scalar("SELECT state FROM uploads WHERE public_id = ?1")
            .bind(&upload.id)
            .fetch_one(state.db())
            .await
            .expect("fetch state");
        assert_eq!(state_value, "expired");
        assert!(!Path::new(&temp_path).exists());

        state.db().close().await;
        let _ = fs::remove_dir_all(root);
    }
}
