use serde::{Deserialize, Serialize};

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "snake_case")]
pub enum Visibility {
    Public,
    Private,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "snake_case")]
pub enum AdminMode {
    Local,
    Proxy,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "snake_case")]
pub enum StorageMode {
    ManagedArchive,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "snake_case")]
pub enum ArtifactKind {
    GamePayload,
    SaveSnapshot,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "snake_case")]
pub enum ArchiveType {
    Zip,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "snake_case")]
pub enum UploadKind {
    GamePayload,
    SaveSnapshot,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "snake_case")]
pub enum UploadState {
    Created,
    Uploading,
    Uploaded,
    Finalizing,
    Queued,
    Processing,
    Completed,
    Failed,
    Abandoned,
    Expired,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "snake_case")]
pub enum JobKind {
    ImportArchive,
    SaveSnapshotArchive,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "snake_case")]
pub enum JobState {
    Pending,
    Processing,
    Completed,
    Failed,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
pub struct PublicId(pub String);

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq, PartialOrd, Ord)]
pub struct PlatformId(pub String);

impl std::fmt::Display for PlatformId {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.write_str(&self.0)
    }
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
pub struct AuditTimestamps {
    pub created_at: String,
    pub updated_at: String,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
pub struct Library {
    pub id: i64,
    pub public_id: PublicId,
    pub name: String,
    pub root_path: String,
    pub platform: PlatformId,
    pub visibility: Visibility,
    pub enabled: bool,
    pub timestamps: AuditTimestamps,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
pub struct Platform {
    pub id: i64,
    pub public_id: PublicId,
    pub code: PlatformId,
    pub enabled: bool,
    pub match_priority: i32,
    pub timestamps: AuditTimestamps,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
pub struct Link {
    pub name: String,
    pub url: String,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
pub struct GameMetadata {
    pub name: String,
    pub sorting_name: Option<String>,
    pub description: Option<String>,
    pub release_date: Option<String>,
    pub platforms: Vec<PlatformId>,
    pub genres: Vec<String>,
    pub developers: Vec<String>,
    pub publishers: Vec<String>,
    pub links: Vec<Link>,
    pub cover_image: Option<String>,
    pub background_image: Option<String>,
    pub icon: Option<String>,
    pub source_slug: Option<String>,
    pub visibility: Visibility,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
pub struct Game {
    pub id: i64,
    pub public_id: PublicId,
    pub library_id: i64,
    pub metadata: GameMetadata,
    pub timestamps: AuditTimestamps,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
pub struct GameVersion {
    pub id: i64,
    pub public_id: PublicId,
    pub game_id: i64,
    pub library_id: i64,
    pub version_name: String,
    pub version_code: Option<String>,
    pub release_date: Option<String>,
    pub notes: Option<String>,
    pub is_latest: bool,
    pub storage_mode: StorageMode,
    pub timestamps: AuditTimestamps,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
pub struct ArtifactPart {
    pub part_index: i32,
    pub relative_path: String,
    pub size_bytes: u64,
    pub checksum: String,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
pub struct VersionArtifact {
    pub id: i64,
    pub public_id: PublicId,
    pub game_version_id: i64,
    pub artifact_kind: ArtifactKind,
    pub archive_type: ArchiveType,
    pub relative_path: String,
    pub size_bytes: u64,
    pub checksum: String,
    pub part_count: u32,
    pub is_managed: bool,
    pub created_at: String,
    pub parts: Vec<ArtifactPart>,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
pub struct SaveSnapshot {
    pub id: i64,
    pub public_id: PublicId,
    pub game_id: i64,
    pub game_version_id: i64,
    pub library_id: i64,
    pub name: String,
    pub captured_at: String,
    pub archive_type: ArchiveType,
    pub size_bytes: u64,
    pub checksum: String,
    pub notes: Option<String>,
    pub created_at: String,
    pub parts: Vec<ArtifactPart>,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
pub struct Upload {
    pub id: i64,
    pub public_id: PublicId,
    pub kind: UploadKind,
    pub library_id: i64,
    pub platform_id: Option<i64>,
    pub game_id: Option<i64>,
    pub game_version_id: Option<i64>,
    pub state: UploadState,
    pub filename: String,
    pub declared_size_bytes: u64,
    pub received_size_bytes: u64,
    pub checksum: Option<String>,
    pub temp_path: String,
    pub job_id: Option<i64>,
    pub idempotency_key: Option<String>,
    pub expires_at: Option<String>,
    pub error_code: Option<String>,
    pub error_message: Option<String>,
    pub timestamps: AuditTimestamps,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
pub struct Job {
    pub id: i64,
    pub public_id: PublicId,
    pub kind: JobKind,
    pub state: JobState,
    pub upload_id: Option<i64>,
    pub game_id: Option<i64>,
    pub game_version_id: Option<i64>,
    pub progress_phase: Option<String>,
    pub progress_percent: Option<u8>,
    pub result_payload: Option<String>,
    pub error_code: Option<String>,
    pub error_message: Option<String>,
    pub retryable: bool,
    pub timestamps: AuditTimestamps,
}
