use axum::extract::{Path, State};
use axum::routing::get;
use axum::{Json, Router};

use crate::api::error::ApiError;
use crate::api::state::AppState;
use crate::api::types::{json, GameSummaryResource, LibraryResource, LinkResource, ListResponse, PlatformResource};

pub fn router() -> Router<AppState> {
    Router::new()
        .route("/games", get(list_games))
        .route("/games/:id", get(get_game))
        .route("/platforms", get(list_platforms))
        .route("/genres", get(list_genres))
        .route("/libraries", get(list_libraries))
}

async fn list_games(State(_state): State<AppState>) -> Json<ListResponse<GameSummaryResource>> {
    json(ListResponse {
        items: vec![],
        next_cursor: None,
    })
}

async fn get_game(Path(id): Path<String>) -> Result<Json<GameSummaryResource>, ApiError> {
    Err(ApiError::not_found("game", &id))
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

async fn list_genres() -> Json<ListResponse<String>> {
    json(ListResponse {
        items: vec![],
        next_cursor: None,
    })
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

#[allow(dead_code)]
fn _placeholder_game(id: String) -> GameSummaryResource {
    GameSummaryResource {
        id,
        name: "Example Game".to_string(),
        sorting_name: Some("Example Game".to_string()),
        platforms: vec!["pc".to_string()],
        description: None,
        release_date: None,
        genres: vec![],
        developers: vec![],
        publishers: vec![],
        links: vec![LinkResource {
            name: "homepage".to_string(),
            url: "https://example.invalid".to_string(),
        }],
        visibility: "private".to_string(),
        cover_image: None,
        background_image: None,
        icon: None,
        created_at: "2026-03-22T20:00:00Z".to_string(),
        updated_at: "2026-03-22T20:00:00Z".to_string(),
    }
}
