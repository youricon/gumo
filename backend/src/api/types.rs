use axum::Json;
use serde::{Deserialize, Serialize};

#[derive(Debug, Clone, Serialize)]
pub struct ListResponse<T> {
    pub items: Vec<T>,
    pub next_cursor: Option<String>,
}

#[derive(Debug, Clone, Serialize)]
pub struct HealthResponse {
    pub status: &'static str,
}

#[derive(Debug, Clone, Serialize)]
pub struct AdminSessionResource {
    pub authenticated: bool,
    pub mode: String,
    pub username: Option<String>,
}

#[derive(Debug, Clone, Serialize)]
pub struct IntegrationTokenResource {
    pub id: String,
    pub label: String,
    pub enabled: bool,
    pub created_at: String,
    pub updated_at: String,
}

#[derive(Debug, Clone, Serialize)]
pub struct CreatedIntegrationTokenResource {
    pub token: IntegrationTokenResource,
    pub plaintext_token: String,
}

#[derive(Debug, Clone, Serialize)]
pub struct PlatformResource {
    pub id: String,
    pub enabled: bool,
    pub match_priority: i32,
}

#[derive(Debug, Clone, Serialize)]
pub struct LibraryResource {
    pub id: String,
    pub name: String,
    pub platform: String,
    pub visibility: String,
    pub enabled: bool,
}

#[derive(Debug, Clone, Serialize)]
pub struct GameSummaryResource {
    pub id: String,
    pub name: String,
    pub sorting_name: Option<String>,
    pub platforms: Vec<String>,
    pub description: Option<String>,
    pub release_date: Option<String>,
    pub genres: Vec<String>,
    pub developers: Vec<String>,
    pub publishers: Vec<String>,
    pub links: Vec<LinkResource>,
    pub visibility: String,
    pub cover_image: Option<String>,
    pub background_image: Option<String>,
    pub icon: Option<String>,
    pub created_at: String,
    pub updated_at: String,
}

#[derive(Debug, Clone, Serialize)]
pub struct GameVersionResource {
    pub id: String,
    pub game_id: String,
    pub library_id: String,
    pub version_name: String,
    pub version_code: Option<String>,
    pub release_date: Option<String>,
    pub is_latest: bool,
    pub notes: Option<String>,
    pub created_at: String,
    pub updated_at: String,
}

#[derive(Debug, Clone, Serialize)]
pub struct ArtifactPartResource {
    pub part_index: i32,
    pub download_url: String,
    pub size_bytes: u64,
    pub checksum: String,
}

#[derive(Debug, Clone, Serialize)]
pub struct ArtifactResource {
    pub id: String,
    pub game_version_id: String,
    pub archive_type: String,
    pub size_bytes: u64,
    pub checksum: String,
    pub part_count: u32,
    #[serde(skip_serializing_if = "Vec::is_empty")]
    pub parts: Vec<ArtifactPartResource>,
    pub created_at: String,
}

#[derive(Debug, Clone, Serialize)]
pub struct SaveSnapshotResource {
    pub id: String,
    pub game_id: String,
    pub game_version_id: String,
    pub library_id: String,
    pub name: String,
    pub captured_at: String,
    pub archive_type: String,
    pub size_bytes: u64,
    pub checksum: String,
    pub notes: Option<String>,
    pub created_at: String,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct LinkResource {
    pub name: String,
    pub url: String,
}

#[derive(Debug, Clone, Serialize)]
pub struct UploadResource {
    pub id: String,
    pub kind: String,
    pub library_id: String,
    pub platform: String,
    pub game_id: Option<String>,
    pub game_version_id: Option<String>,
    pub state: String,
    pub filename: String,
    pub declared_size_bytes: u64,
    pub received_size_bytes: u64,
    pub checksum: Option<String>,
    pub job_id: Option<String>,
    pub created_at: String,
    pub updated_at: String,
    pub expires_at: Option<String>,
    pub error: Option<ResourceError>,
}

#[derive(Debug, Clone, Serialize)]
pub struct JobResource {
    pub id: String,
    pub kind: String,
    pub state: String,
    pub upload_id: Option<String>,
    pub game_id: Option<String>,
    pub game_version_id: Option<String>,
    pub progress: Option<JobProgress>,
    pub result: Option<serde_json::Value>,
    pub error: Option<ResourceError>,
    pub created_at: String,
    pub updated_at: String,
}

#[derive(Debug, Clone, Serialize)]
pub struct JobProgress {
    pub phase: String,
    pub percent: u8,
}

#[derive(Debug, Clone, Serialize)]
pub struct ResourceError {
    pub code: String,
    pub message: String,
    pub retryable: Option<bool>,
}

#[derive(Debug, Clone, Serialize)]
pub struct AcknowledgedResponse {
    pub status: &'static str,
}

#[derive(Debug, Clone, Serialize)]
pub struct InstallManifestResource {
    pub game: InstallGameResource,
    pub version: InstallVersionResource,
    pub artifact: InstallArtifactResource,
}

#[derive(Debug, Clone, Serialize)]
pub struct InstallGameResource {
    pub id: String,
    pub name: String,
    pub platforms: Vec<String>,
}

#[derive(Debug, Clone, Serialize)]
pub struct InstallVersionResource {
    pub id: String,
    pub version_name: String,
    pub is_latest: bool,
}

#[derive(Debug, Clone, Serialize)]
pub struct InstallArtifactResource {
    pub id: String,
    pub archive_type: String,
    pub size_bytes: u64,
    pub checksum: String,
    pub parts: Vec<ArtifactPartResource>,
}

#[derive(Debug, Clone, Serialize)]
pub struct SaveRestoreManifestResource {
    pub game_id: String,
    pub game_version_id: String,
    pub save_snapshot: SaveSnapshotManifestResource,
    pub parts: Vec<ArtifactPartResource>,
}

#[derive(Debug, Clone, Serialize)]
pub struct SaveSnapshotManifestResource {
    pub id: String,
    pub name: String,
    pub captured_at: String,
    pub archive_type: String,
    pub size_bytes: u64,
    pub checksum: String,
}

pub fn json<T: Serialize>(value: T) -> Json<T> {
    Json(value)
}
