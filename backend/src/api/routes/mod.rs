pub mod admin;
pub mod integration;
pub mod public;

use axum::routing::get;
use axum::{extract::State, Router};
use tower_http::services::ServeDir;

use crate::api::state::AppState;
use crate::api::types::{json, HealthResponse};

pub fn router(state: AppState) -> Router {
    let media_dir = state.config().storage.cache_dir.join("media");
    Router::new()
        .route("/api/health", get(health))
        .nest_service("/media", ServeDir::new(media_dir))
        .nest("/api", public::router())
        .nest("/api/integrations/playnite", integration::router(state.clone()))
        .nest("/api/admin", admin::router(state.clone()))
        .with_state(state)
}

async fn health(State(state): State<AppState>) -> axum::Json<HealthResponse> {
    let _ = state.db();
    json(HealthResponse { status: "ok" })
}
