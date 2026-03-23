use std::path::{Path, PathBuf};
use std::sync::Mutex;
use std::sync::Arc;

use axum::Router;
use sqlx::SqlitePool;
use tower_http::services::{ServeDir, ServeFile};

use crate::config::AppConfig;

#[derive(Clone)]
pub struct AppState {
    pub shared: Arc<SharedState>,
}

pub struct SharedState {
    pub config: AppConfig,
    pub db: SqlitePool,
    pub admin_sessions: Mutex<std::collections::HashMap<String, AdminSession>>,
}

#[derive(Debug, Clone)]
pub struct AdminSession {
    pub username: String,
    pub expires_at_unix: u64,
}

impl AppState {
    pub fn new(config: AppConfig, db: SqlitePool) -> Self {
        Self {
            shared: Arc::new(SharedState {
                config,
                db,
                admin_sessions: Mutex::new(std::collections::HashMap::new()),
            }),
        }
    }

    pub fn config(&self) -> &AppConfig {
        &self.shared.config
    }

    pub fn db(&self) -> &SqlitePool {
        &self.shared.db
    }

    pub fn admin_sessions(
        &self,
    ) -> &Mutex<std::collections::HashMap<String, AdminSession>> {
        &self.shared.admin_sessions
    }
}

pub fn frontend_router() -> Option<Router> {
    let dir = frontend_dir()?;
    let index = dir.join("index.html");
    if !index.exists() {
        return None;
    }

    Some(
        Router::new().fallback_service(
            ServeDir::new(dir).not_found_service(ServeFile::new(index)),
        ),
    )
}

fn frontend_dir() -> Option<PathBuf> {
    if let Ok(path) = std::env::var("GUMO_WEB_DIR") {
        let path = PathBuf::from(path);
        if path.exists() {
            return Some(path);
        }
    }

    let exe = std::env::current_exe().ok()?;
    let root = exe.parent()?.parent()?;
    let packaged = root.join("share/gumo/web");
    if packaged.exists() {
        return Some(packaged);
    }

    let repo_root = std::env::current_dir().ok()?;
    let dev_dist = repo_root.join("web/dist");
    if dev_dist.exists() {
        return Some(dev_dist);
    }

    None
}

#[allow(dead_code)]
fn _is_dir(path: &Path) -> bool {
    path.is_dir()
}
