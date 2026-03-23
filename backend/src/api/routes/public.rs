use axum::extract::{Path, State};
use axum::routing::get;
use axum::{Json, Router};

use crate::api::error::ApiError;
use crate::api::state::AppState;
use crate::api::types::{
    json, GameSummaryResource, GameVersionResource, LibraryResource, ListResponse,
    PlatformResource,
};
use crate::playnite;

pub fn router() -> Router<AppState> {
    Router::new()
        .route("/games", get(list_games))
        .route("/games/:id", get(get_game))
        .route("/games/:id/versions", get(list_game_versions))
        .route("/platforms", get(list_platforms))
        .route("/genres", get(list_genres))
        .route("/libraries", get(list_libraries))
}

async fn list_games(
    State(state): State<AppState>,
) -> Result<Json<ListResponse<GameSummaryResource>>, ApiError> {
    let items = playnite::list_games_filtered(&state, Some("public")).await?;
    Ok(json(ListResponse {
        items,
        next_cursor: None,
    }))
}

async fn get_game(
    Path(id): Path<String>,
    State(state): State<AppState>,
) -> Result<Json<GameSummaryResource>, ApiError> {
    let game = playnite::get_game(&state, &id).await?;
    if game.visibility != "public" {
        return Err(ApiError::not_found("game", &id));
    }
    Ok(Json(game))
}

async fn list_game_versions(
    Path(id): Path<String>,
    State(state): State<AppState>,
) -> Result<Json<ListResponse<GameVersionResource>>, ApiError> {
    let game = playnite::get_game(&state, &id).await?;
    if game.visibility != "public" {
        return Err(ApiError::not_found("game", &id));
    }
    let items = playnite::list_versions_for_game(&state, &id).await?;
    Ok(json(ListResponse {
        items,
        next_cursor: None,
    }))
}

async fn list_platforms(State(state): State<AppState>) -> Json<ListResponse<PlatformResource>> {
    let items = state
        .config()
        .platforms
        .iter()
        .map(|platform| PlatformResource {
            id: platform.id.0.clone(),
            enabled: platform.enabled,
            match_priority: platform.match_priority,
        })
        .collect();
    json(ListResponse {
        items,
        next_cursor: None,
    })
}

async fn list_genres(
    State(state): State<AppState>,
) -> Result<Json<ListResponse<String>>, ApiError> {
    let items = playnite::list_genres(&state).await?;
    Ok(json(ListResponse {
        items,
        next_cursor: None,
    }))
}

async fn list_libraries(State(state): State<AppState>) -> Json<ListResponse<LibraryResource>> {
    let items = state
        .config()
        .libraries
        .iter()
        .map(|library| LibraryResource {
            id: format!("library_{}", library.name),
            name: library.name.clone(),
            platform: library.platform.0.clone(),
            visibility: match library.visibility {
                crate::domain::Visibility::Public => "public".to_string(),
                crate::domain::Visibility::Private => "private".to_string(),
            },
            enabled: library.enabled,
        })
        .collect();

    json(ListResponse {
        items,
        next_cursor: None,
    })
}
