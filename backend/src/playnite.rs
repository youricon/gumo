use std::path::PathBuf;

use anyhow::{Context, Result};
use axum::body::Body;
use axum::http::{header, HeaderValue, StatusCode};
use axum::response::Response;
use serde::Deserialize;
use sqlx::{Row, SqlitePool, Transaction};

use crate::api::error::ApiError;
use crate::api::state::AppState;
use crate::api::types::{
    ArtifactPartResource, GameSummaryResource, GameVersionResource, InstallArtifactResource,
    InstallGameResource, InstallManifestResource, InstallVersionResource, LinkResource,
    SaveRestoreManifestResource, SaveSnapshotManifestResource, SaveSnapshotResource,
};

#[derive(Debug, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct PatchGameRequest {
    #[serde(default)]
    pub name: Option<String>,
    #[serde(default)]
    pub sorting_name: Option<Option<String>>,
    #[serde(default)]
    pub description: Option<Option<String>>,
    #[serde(default)]
    pub release_date: Option<Option<String>>,
    #[serde(default)]
    pub genres: Option<Vec<String>>,
    #[serde(default)]
    pub developers: Option<Vec<String>>,
    #[serde(default)]
    pub publishers: Option<Vec<String>>,
    #[serde(default)]
    pub links: Option<Vec<LinkResource>>,
    #[serde(default)]
    pub cover_image: Option<Option<String>>,
    #[serde(default)]
    pub background_image: Option<Option<String>>,
    #[serde(default)]
    pub icon: Option<Option<String>>,
    #[serde(default)]
    pub visibility: Option<String>,
}

#[derive(Debug, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct PatchVersionRequest {
    #[serde(default)]
    pub version_name: Option<String>,
    #[serde(default)]
    pub version_code: Option<Option<String>>,
    #[serde(default)]
    pub notes: Option<Option<String>>,
    #[serde(default)]
    pub release_date: Option<Option<String>>,
}

pub async fn list_games(state: &AppState) -> Result<Vec<GameSummaryResource>, ApiError> {
    let rows = sqlx::query(
        r#"
        SELECT public_id, name, sorting_name, description, release_date, visibility,
               cover_image, background_image, icon, created_at, updated_at
        FROM games
        ORDER BY updated_at DESC
        LIMIT 100
        "#,
    )
    .fetch_all(state.db())
    .await
    .map_err(internal_error)?;

    let mut items = Vec::with_capacity(rows.len());
    for row in rows {
        let game_id: String = row.get("public_id");
        items.push(load_game_resource(state.db(), &game_id).await?);
    }
    Ok(items)
}

pub async fn get_game(state: &AppState, game_id: &str) -> Result<GameSummaryResource, ApiError> {
    load_game_resource(state.db(), game_id).await
}

pub async fn patch_game(
    state: &AppState,
    game_id: &str,
    request: PatchGameRequest,
) -> Result<GameSummaryResource, ApiError> {
    if request.visibility.as_deref().is_some_and(|v| v != "public" && v != "private") {
        return Err(ApiError::bad_request("visibility must be 'public' or 'private'"));
    }

    let game_row_id: i64 = sqlx::query_scalar("SELECT id FROM games WHERE public_id = ?1")
        .bind(game_id)
        .fetch_optional(state.db())
        .await
        .map_err(internal_error)?
        .ok_or_else(|| ApiError::not_found("game", game_id))?;

    let mut tx = state.db().begin().await.map_err(internal_error)?;
    sqlx::query(
        r#"
        UPDATE games
        SET
          name = COALESCE(?2, name),
          sorting_name = CASE WHEN ?3 THEN ?4 ELSE sorting_name END,
          description = CASE WHEN ?5 THEN ?6 ELSE description END,
          release_date = CASE WHEN ?7 THEN ?8 ELSE release_date END,
          cover_image = CASE WHEN ?9 THEN ?10 ELSE cover_image END,
          background_image = CASE WHEN ?11 THEN ?12 ELSE background_image END,
          icon = CASE WHEN ?13 THEN ?14 ELSE icon END,
          visibility = COALESCE(?15, visibility),
          updated_at = CURRENT_TIMESTAMP
        WHERE public_id = ?1
        "#,
    )
    .bind(game_id)
    .bind(request.name.as_deref())
    .bind(request.sorting_name.is_some())
    .bind(request.sorting_name.flatten())
    .bind(request.description.is_some())
    .bind(request.description.flatten())
    .bind(request.release_date.is_some())
    .bind(request.release_date.flatten())
    .bind(request.cover_image.is_some())
    .bind(request.cover_image.flatten())
    .bind(request.background_image.is_some())
    .bind(request.background_image.flatten())
    .bind(request.icon.is_some())
    .bind(request.icon.flatten())
    .bind(request.visibility.as_deref())
    .execute(&mut *tx)
    .await
    .map_err(internal_error)?;

    if let Some(genres) = request.genres {
        replace_named_set(&mut tx, game_row_id, "genres", "game_genres", "genre_id", genres).await?;
    }
    if let Some(developers) = request.developers {
        replace_named_set(
            &mut tx,
            game_row_id,
            "developers",
            "game_developers",
            "developer_id",
            developers,
        )
        .await?;
    }
    if let Some(publishers) = request.publishers {
        replace_named_set(
            &mut tx,
            game_row_id,
            "publishers",
            "game_publishers",
            "publisher_id",
            publishers,
        )
        .await?;
    }
    if let Some(links) = request.links {
        sqlx::query("DELETE FROM links WHERE game_id = ?1")
            .bind(game_row_id)
            .execute(&mut *tx)
            .await
            .map_err(internal_error)?;
        for link in links {
            sqlx::query("INSERT INTO links (game_id, name, url) VALUES (?1, ?2, ?3)")
                .bind(game_row_id)
                .bind(link.name)
                .bind(link.url)
                .execute(&mut *tx)
                .await
                .map_err(internal_error)?;
        }
    }

    tx.commit().await.map_err(internal_error)?;
    load_game_resource(state.db(), game_id).await
}

pub async fn patch_version(
    state: &AppState,
    version_id: &str,
    request: PatchVersionRequest,
) -> Result<GameVersionResource, ApiError> {
    sqlx::query(
        r#"
        UPDATE game_versions
        SET
          version_name = COALESCE(?2, version_name),
          version_code = CASE WHEN ?3 THEN ?4 ELSE version_code END,
          notes = CASE WHEN ?5 THEN ?6 ELSE notes END,
          release_date = CASE WHEN ?7 THEN ?8 ELSE release_date END,
          updated_at = CURRENT_TIMESTAMP
        WHERE public_id = ?1
        "#,
    )
    .bind(version_id)
    .bind(request.version_name.as_deref())
    .bind(request.version_code.is_some())
    .bind(request.version_code.flatten())
    .bind(request.notes.is_some())
    .bind(request.notes.flatten())
    .bind(request.release_date.is_some())
    .bind(request.release_date.flatten())
    .execute(state.db())
    .await
    .map_err(internal_error)?;

    load_version_resource(state.db(), version_id).await
}

pub async fn get_install_manifest(
    state: &AppState,
    version_id: &str,
) -> Result<InstallManifestResource, ApiError> {
    let version = load_version_resource(state.db(), version_id).await?;
    let game = load_game_resource(state.db(), &version.game_id).await?;
    let artifact = load_primary_artifact(state.db(), version_id).await?;
    Ok(InstallManifestResource {
        game: InstallGameResource {
            id: game.id,
            name: game.name,
            platforms: game.platforms,
        },
        version: InstallVersionResource {
            id: version.id,
            version_name: version.version_name,
            is_latest: version.is_latest,
        },
        artifact: InstallArtifactResource {
            id: artifact.id,
            archive_type: artifact.archive_type,
            size_bytes: artifact.size_bytes,
            checksum: artifact.checksum,
            parts: artifact.parts,
        },
    })
}

pub async fn list_save_snapshots(
    state: &AppState,
    version_id: &str,
) -> Result<Vec<SaveSnapshotResource>, ApiError> {
    let rows = sqlx::query(
        r#"
        SELECT s.public_id, g.public_id AS game_public_id, gv.public_id AS version_public_id,
               l.public_id AS library_public_id, s.name, s.captured_at, s.archive_type,
               s.size_bytes, s.checksum, s.notes, s.created_at
        FROM save_snapshots s
        INNER JOIN games g ON g.id = s.game_id
        INNER JOIN game_versions gv ON gv.id = s.game_version_id
        INNER JOIN libraries l ON l.id = s.library_id
        WHERE gv.public_id = ?1
        ORDER BY s.captured_at DESC
        "#,
    )
    .bind(version_id)
    .fetch_all(state.db())
    .await
    .map_err(internal_error)?;

    Ok(rows
        .into_iter()
        .map(|row| SaveSnapshotResource {
            id: row.get("public_id"),
            game_id: row.get("game_public_id"),
            game_version_id: row.get("version_public_id"),
            library_id: row.get("library_public_id"),
            name: row.get("name"),
            captured_at: timestamp_to_rfc3339(&row.get::<String, _>("captured_at")),
            archive_type: row.get("archive_type"),
            size_bytes: row.get::<i64, _>("size_bytes") as u64,
            checksum: row.get("checksum"),
            notes: row.get("notes"),
            created_at: timestamp_to_rfc3339(&row.get::<String, _>("created_at")),
        })
        .collect())
}

pub async fn get_save_restore_manifest(
    state: &AppState,
    snapshot_id: &str,
) -> Result<SaveRestoreManifestResource, ApiError> {
    let row = sqlx::query(
        r#"
        SELECT s.public_id, s.name, s.captured_at, s.archive_type, s.size_bytes, s.checksum,
               g.public_id AS game_public_id, gv.public_id AS version_public_id
        FROM save_snapshots s
        INNER JOIN games g ON g.id = s.game_id
        INNER JOIN game_versions gv ON gv.id = s.game_version_id
        WHERE s.public_id = ?1
        "#,
    )
    .bind(snapshot_id)
    .fetch_optional(state.db())
    .await
    .map_err(internal_error)?
    .ok_or_else(|| ApiError::not_found("save snapshot", snapshot_id))?;

    Ok(SaveRestoreManifestResource {
        game_id: row.get("game_public_id"),
        game_version_id: row.get("version_public_id"),
        save_snapshot: SaveSnapshotManifestResource {
            id: row.get("public_id"),
            name: row.get("name"),
            captured_at: timestamp_to_rfc3339(&row.get::<String, _>("captured_at")),
            archive_type: row.get("archive_type"),
            size_bytes: row.get::<i64, _>("size_bytes") as u64,
            checksum: row.get("checksum"),
        },
        parts: vec![ArtifactPartResource {
            part_index: 0,
            download_url: format!("/api/integrations/playnite/save-snapshots/{snapshot_id}/download"),
            size_bytes: row.get::<i64, _>("size_bytes") as u64,
            checksum: row.get("checksum"),
        }],
    })
}

pub async fn download_artifact(state: &AppState, artifact_id: &str) -> Result<Response, ApiError> {
    let row = sqlx::query(
        r#"
        SELECT a.relative_path, l.root_path
        FROM version_artifacts a
        INNER JOIN game_versions gv ON gv.id = a.game_version_id
        INNER JOIN libraries l ON l.id = gv.library_id
        WHERE a.public_id = ?1
        "#,
    )
    .bind(artifact_id)
    .fetch_optional(state.db())
    .await
    .map_err(internal_error)?
    .ok_or_else(|| ApiError::not_found("artifact", artifact_id))?;

    let path = PathBuf::from(row.get::<String, _>("root_path")).join(row.get::<String, _>("relative_path"));
    file_response(path).await
}

pub async fn download_save_snapshot(
    state: &AppState,
    snapshot_id: &str,
) -> Result<Response, ApiError> {
    let row = sqlx::query(
        r#"
        SELECT gv.public_id AS version_public_id, l.root_path
        FROM save_snapshots s
        INNER JOIN game_versions gv ON gv.id = s.game_version_id
        INNER JOIN libraries l ON l.id = s.library_id
        WHERE s.public_id = ?1
        "#,
    )
    .bind(snapshot_id)
    .fetch_optional(state.db())
    .await
    .map_err(internal_error)?
    .ok_or_else(|| ApiError::not_found("save snapshot", snapshot_id))?;

    let relative = PathBuf::from("saves")
        .join(row.get::<String, _>("version_public_id"))
        .join("payload.zip");
    let path = PathBuf::from(row.get::<String, _>("root_path")).join(relative);
    file_response(path).await
}

async fn load_game_resource(pool: &SqlitePool, game_id: &str) -> Result<GameSummaryResource, ApiError> {
    let row = sqlx::query(
        r#"
        SELECT public_id, name, sorting_name, description, release_date, visibility,
               cover_image, background_image, icon, created_at, updated_at
        FROM games
        WHERE public_id = ?1
        "#,
    )
    .bind(game_id)
    .fetch_optional(pool)
    .await
    .map_err(internal_error)?
    .ok_or_else(|| ApiError::not_found("game", game_id))?;

    Ok(GameSummaryResource {
        id: row.get("public_id"),
        name: row.get("name"),
        sorting_name: row.get("sorting_name"),
        description: row.get("description"),
        release_date: row.get("release_date"),
        platforms: load_game_platforms(pool, row.get("public_id")).await?,
        genres: load_named_set(pool, "genres", "game_genres", "genre_id", row.get("public_id")).await?,
        developers: load_named_set(pool, "developers", "game_developers", "developer_id", row.get("public_id")).await?,
        publishers: load_named_set(pool, "publishers", "game_publishers", "publisher_id", row.get("public_id")).await?,
        links: load_links(pool, row.get("public_id")).await?,
        visibility: row.get("visibility"),
        cover_image: row.get("cover_image"),
        background_image: row.get("background_image"),
        icon: row.get("icon"),
        created_at: timestamp_to_rfc3339(&row.get::<String, _>("created_at")),
        updated_at: timestamp_to_rfc3339(&row.get::<String, _>("updated_at")),
    })
}

async fn load_version_resource(pool: &SqlitePool, version_id: &str) -> Result<GameVersionResource, ApiError> {
    let row = sqlx::query(
        r#"
        SELECT gv.public_id, g.public_id AS game_public_id, l.public_id AS library_public_id,
               gv.version_name, gv.version_code, gv.release_date, gv.is_latest, gv.notes,
               gv.created_at, gv.updated_at
        FROM game_versions gv
        INNER JOIN games g ON g.id = gv.game_id
        INNER JOIN libraries l ON l.id = gv.library_id
        WHERE gv.public_id = ?1
        "#,
    )
    .bind(version_id)
    .fetch_optional(pool)
    .await
    .map_err(internal_error)?
    .ok_or_else(|| ApiError::not_found("game version", version_id))?;

    Ok(GameVersionResource {
        id: row.get("public_id"),
        game_id: row.get("game_public_id"),
        library_id: row.get("library_public_id"),
        version_name: row.get("version_name"),
        version_code: row.get("version_code"),
        release_date: row.get("release_date"),
        is_latest: row.get::<i64, _>("is_latest") == 1,
        notes: row.get("notes"),
        created_at: timestamp_to_rfc3339(&row.get::<String, _>("created_at")),
        updated_at: timestamp_to_rfc3339(&row.get::<String, _>("updated_at")),
    })
}

async fn load_primary_artifact(pool: &SqlitePool, version_id: &str) -> Result<crate::api::types::ArtifactResource, ApiError> {
    let row = sqlx::query(
        r#"
        SELECT a.public_id, gv.public_id AS version_public_id, a.archive_type, a.size_bytes,
               a.checksum, a.part_count, a.created_at
        FROM version_artifacts a
        INNER JOIN game_versions gv ON gv.id = a.game_version_id
        WHERE gv.public_id = ?1
        ORDER BY a.created_at DESC
        LIMIT 1
        "#,
    )
    .bind(version_id)
    .fetch_optional(pool)
    .await
    .map_err(internal_error)?
    .ok_or_else(|| ApiError::not_found("artifact for version", version_id))?;

    let artifact_id: String = row.get("public_id");
    Ok(crate::api::types::ArtifactResource {
        id: artifact_id.clone(),
        game_version_id: row.get("version_public_id"),
        archive_type: row.get("archive_type"),
        size_bytes: row.get::<i64, _>("size_bytes") as u64,
        checksum: row.get("checksum"),
        part_count: row.get::<i64, _>("part_count") as u32,
        parts: vec![ArtifactPartResource {
            part_index: 0,
            download_url: format!("/api/integrations/playnite/artifacts/{artifact_id}/download"),
            size_bytes: row.get::<i64, _>("size_bytes") as u64,
            checksum: row.get("checksum"),
        }],
        created_at: timestamp_to_rfc3339(&row.get::<String, _>("created_at")),
    })
}

async fn load_game_platforms(pool: &SqlitePool, game_id: String) -> Result<Vec<String>, ApiError> {
    let rows = sqlx::query(
        r#"
        SELECT p.name
        FROM game_platforms gp
        INNER JOIN games g ON g.id = gp.game_id
        INNER JOIN platforms p ON p.id = gp.platform_id
        WHERE g.public_id = ?1
        ORDER BY p.match_priority DESC, p.name ASC
        "#,
    )
    .bind(game_id)
    .fetch_all(pool)
    .await
    .map_err(internal_error)?;
    Ok(rows.into_iter().map(|row| row.get("name")).collect())
}

async fn load_named_set(
    pool: &SqlitePool,
    table: &str,
    join_table: &str,
    foreign_column: &str,
    game_id: String,
) -> Result<Vec<String>, ApiError> {
    let sql = format!(
        "SELECT t.name FROM {join_table} j INNER JOIN games g ON g.id = j.game_id INNER JOIN {table} t ON t.id = j.{foreign_column} WHERE g.public_id = ?1 ORDER BY t.name ASC"
    );
    let rows = sqlx::query(&sql)
        .bind(game_id)
        .fetch_all(pool)
        .await
        .map_err(internal_error)?;
    Ok(rows.into_iter().map(|row| row.get("name")).collect())
}

async fn load_links(pool: &SqlitePool, game_id: String) -> Result<Vec<LinkResource>, ApiError> {
    let rows = sqlx::query(
        r#"
        SELECT l.name, l.url
        FROM links l
        INNER JOIN games g ON g.id = l.game_id
        WHERE g.public_id = ?1
        ORDER BY l.id ASC
        "#,
    )
    .bind(game_id)
    .fetch_all(pool)
    .await
    .map_err(internal_error)?;
    Ok(rows
        .into_iter()
        .map(|row| LinkResource {
            name: row.get("name"),
            url: row.get("url"),
        })
        .collect())
}

async fn replace_named_set(
    tx: &mut Transaction<'_, sqlx::Sqlite>,
    game_row_id: i64,
    table: &str,
    join_table: &str,
    foreign_column: &str,
    values: Vec<String>,
) -> Result<(), ApiError>
{
    let delete_sql = format!("DELETE FROM {join_table} WHERE game_id = ?1");
    sqlx::query(&delete_sql)
        .bind(game_row_id)
        .execute(&mut **tx)
        .await
        .map_err(internal_error)?;

    for value in values {
        let insert_named_sql = format!("INSERT INTO {table} (name) VALUES (?1) ON CONFLICT(name) DO NOTHING");
        sqlx::query(&insert_named_sql)
            .bind(&value)
            .execute(&mut **tx)
            .await
            .map_err(internal_error)?;

        let lookup_sql = format!("SELECT id FROM {table} WHERE name = ?1");
        let named_id: i64 = sqlx::query_scalar(&lookup_sql)
            .bind(&value)
            .fetch_one(&mut **tx)
            .await
            .map_err(internal_error)?;

        let attach_sql = format!(
            "INSERT INTO {join_table} (game_id, {foreign_column}) VALUES (?1, ?2)"
        );
        sqlx::query(&attach_sql)
            .bind(game_row_id)
            .bind(named_id)
            .execute(&mut **tx)
            .await
            .map_err(internal_error)?;
    }

    Ok(())
}

async fn file_response(path: PathBuf) -> Result<Response, ApiError> {
    let bytes = tokio::fs::read(&path)
        .await
        .with_context(|| format!("failed to read {}", path.display()))
        .map_err(internal_error)?;
    let mut response = Response::new(Body::from(bytes));
    response.headers_mut().insert(
        header::CONTENT_TYPE,
        HeaderValue::from_static("application/zip"),
    );
    Ok(response)
}

fn timestamp_to_rfc3339(value: &str) -> String {
    value.replace(' ', "T") + "Z"
}

fn internal_error(err: impl std::fmt::Display) -> ApiError {
    ApiError::new(
        StatusCode::INTERNAL_SERVER_ERROR,
        "internal_error",
        err.to_string(),
    )
}
