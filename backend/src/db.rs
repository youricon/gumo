use std::fs;
use std::path::Path;

use anyhow::{Context, Result};
use sqlx::migrate::Migrator;
use sqlx::sqlite::{SqliteConnectOptions, SqliteJournalMode, SqlitePoolOptions, SqliteSynchronous};
use sqlx::{Row, SqlitePool};

use crate::config::{AppConfig, StorageConfig};
use crate::domain::Visibility;

pub static MIGRATOR: Migrator = sqlx::migrate!("./migrations");

pub async fn connect_and_migrate(storage: &StorageConfig) -> Result<SqlitePool> {
    ensure_runtime_dirs(storage)?;

    let options = SqliteConnectOptions::new()
        .filename(&storage.database_path)
        .create_if_missing(true)
        .journal_mode(SqliteJournalMode::Wal)
        .synchronous(SqliteSynchronous::Normal)
        .foreign_keys(true);

    let pool = SqlitePoolOptions::new()
        .max_connections(5)
        .connect_with(options)
        .await
        .with_context(|| {
            format!(
                "failed to connect to sqlite database at {}",
                storage.database_path.display()
            )
        })?;

    MIGRATOR
        .run(&pool)
        .await
        .context("failed to run database migrations")?;

    Ok(pool)
}

pub async fn sync_config_reference_data(pool: &SqlitePool, config: &AppConfig) -> Result<()> {
    for platform in &config.platforms {
        sqlx::query(
            r#"
            INSERT INTO platforms (public_id, name, is_enabled, match_priority, created_at, updated_at)
            VALUES (?1, ?2, ?3, ?4, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
            ON CONFLICT(name) DO UPDATE SET
              public_id = excluded.public_id,
              is_enabled = excluded.is_enabled,
              match_priority = excluded.match_priority,
              updated_at = CURRENT_TIMESTAMP
            "#,
        )
        .bind(format!("platform_{}", platform.id.0))
        .bind(&platform.id.0)
        .bind(bool_to_int(platform.enabled))
        .bind(platform.match_priority)
        .execute(pool)
        .await
        .with_context(|| format!("failed to sync platform '{}'", platform.id.0))?;
    }

    for library in &config.libraries {
        sqlx::query(
            r#"
            INSERT INTO libraries (public_id, name, root_path, platform_hint, visibility, is_enabled, created_at, updated_at)
            VALUES (?1, ?2, ?3, ?4, ?5, ?6, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
            ON CONFLICT(name) DO UPDATE SET
              public_id = excluded.public_id,
              root_path = excluded.root_path,
              platform_hint = excluded.platform_hint,
              visibility = excluded.visibility,
              is_enabled = excluded.is_enabled,
              updated_at = CURRENT_TIMESTAMP
            "#,
        )
        .bind(format!("library_{}", slugify(&library.name)))
        .bind(&library.name)
        .bind(library.root_path.to_string_lossy().to_string())
        .bind(&library.platform.0)
        .bind(visibility_str(&library.visibility))
        .bind(bool_to_int(library.enabled))
        .execute(pool)
        .await
        .with_context(|| format!("failed to sync library '{}'", library.name))?;
    }

    Ok(())
}

pub async fn foreign_keys_enabled(pool: &SqlitePool) -> Result<bool> {
    let row = sqlx::query("PRAGMA foreign_keys;")
        .fetch_one(pool)
        .await
        .context("failed to query PRAGMA foreign_keys")?;
    let enabled: i64 = row
        .try_get(0)
        .context("failed to decode foreign_keys pragma")?;
    Ok(enabled == 1)
}

fn ensure_runtime_dirs(storage: &StorageConfig) -> Result<()> {
    ensure_parent_dir(&storage.database_path)?;
    ensure_dir(&storage.cache_dir)?;
    if let Some(temp_dir) = &storage.temp_dir {
        ensure_dir(temp_dir)?;
    }
    Ok(())
}

fn ensure_parent_dir(path: &Path) -> Result<()> {
    if let Some(parent) = path.parent() {
        ensure_dir(parent)?;
    }
    Ok(())
}

fn ensure_dir(path: &Path) -> Result<()> {
    fs::create_dir_all(path)
        .with_context(|| format!("failed to create directory {}", path.display()))
}

fn bool_to_int(value: bool) -> i64 {
    if value {
        1
    } else {
        0
    }
}

fn visibility_str(value: &Visibility) -> &'static str {
    match value {
        Visibility::Public => "public",
        Visibility::Private => "private",
    }
}

fn slugify(input: &str) -> String {
    input
        .chars()
        .map(|ch| {
            if ch.is_ascii_alphanumeric() {
                ch.to_ascii_lowercase()
            } else {
                '_'
            }
        })
        .collect()
}

#[cfg(test)]
mod tests {
    use std::fs;
    use std::path::{Path, PathBuf};

    use super::{connect_and_migrate, foreign_keys_enabled, sync_config_reference_data};
    use crate::config::AppConfig;

    fn temp_test_root() -> PathBuf {
        let unique = format!(
            "gumo-db-test-{}-{}",
            std::process::id(),
            std::time::SystemTime::now()
                .duration_since(std::time::UNIX_EPOCH)
                .expect("system time should be after epoch")
                .as_nanos()
        );
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
owner_password_hash_file = "{}/password-hash"

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
            root.join("library").display(),
        );

        let config: AppConfig = toml::from_str(&raw).expect("test config should parse");
        config.validate().expect("test config should validate");
        config
    }

    #[tokio::test]
    async fn migrations_apply_and_seed_platforms() {
        let root = temp_test_root();
        let config = test_config(&root);

        let pool = connect_and_migrate(&config.storage)
            .await
            .expect("migrations should apply");
        sync_config_reference_data(&pool, &config)
            .await
            .expect("config reference data should sync");

        assert!(
            foreign_keys_enabled(&pool)
                .await
                .expect("pragma query should succeed"),
            "foreign keys should be enabled"
        );

        let tables: i64 = sqlx::query_scalar(
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name IN ('platforms', 'libraries', 'games', 'game_versions', 'uploads', 'jobs');",
        )
        .fetch_one(&pool)
        .await
        .expect("schema query should succeed");
        assert_eq!(tables, 6, "expected core schema tables to exist");

        let seeded_platforms: i64 =
            sqlx::query_scalar("SELECT COUNT(*) FROM platforms WHERE name = 'pc';")
                .fetch_one(&pool)
                .await
                .expect("seed query should succeed");
        assert_eq!(
            seeded_platforms, 1,
            "pc platform should be seeded exactly once"
        );

        let synced_libraries: i64 =
            sqlx::query_scalar("SELECT COUNT(*) FROM libraries WHERE name = 'primary';")
                .fetch_one(&pool)
                .await
                .expect("library sync query should succeed");
        assert_eq!(synced_libraries, 1, "config library should be synchronized");

        pool.close().await;
        let _ = fs::remove_dir_all(root);
    }
}
