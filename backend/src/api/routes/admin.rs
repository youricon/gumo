use axum::extract::Path;
use axum::routing::{patch, post};
use axum::{Json, Router};
use serde::Deserialize;

use crate::api::error::ApiError;
use crate::api::state::AppState;
use crate::api::types::{json, AcknowledgedResponse};

pub fn router() -> Router<AppState> {
    Router::new()
        .route("/games/:id", patch(patch_game))
        .route("/games/:id/match", post(match_game))
        .route("/assets/refresh", post(refresh_assets))
        .route("/imports", post(create_import))
}

#[derive(Debug, Deserialize)]
struct AdminPayload {
    #[serde(flatten)]
    fields: serde_json::Map<String, serde_json::Value>,
}

async fn patch_game(
    Path(_id): Path<String>,
    Json(payload): Json<AdminPayload>,
) -> Result<Json<AcknowledgedResponse>, ApiError> {
    let _ = payload.fields.len();
    Err(ApiError::not_implemented("admin game patch"))
}

async fn match_game(Path(_id): Path<String>) -> Result<Json<AcknowledgedResponse>, ApiError> {
    Err(ApiError::not_implemented("admin game match"))
}

async fn refresh_assets() -> Result<Json<AcknowledgedResponse>, ApiError> {
    Err(ApiError::not_implemented("admin asset refresh"))
}

async fn create_import() -> Result<Json<AcknowledgedResponse>, ApiError> {
    Ok(json(AcknowledgedResponse { status: "accepted" }))
}
