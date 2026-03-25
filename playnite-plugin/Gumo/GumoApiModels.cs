using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Gumo.Playnite
{
    [DataContract]
    public sealed class GumoJobResult
    {
        [DataMember(Name = "game_id")]
        public string GameId { get; set; }

        [DataMember(Name = "game_version_id")]
        public string GameVersionId { get; set; }

        [DataMember(Name = "save_snapshot_id")]
        public string SaveSnapshotId { get; set; }
    }

    [DataContract]
    public sealed class GumoListResponse<T>
    {
        [DataMember(Name = "items")]
        public List<T> Items { get; set; } = new List<T>();

        [DataMember(Name = "next_cursor")]
        public string NextCursor { get; set; }
    }

    [DataContract]
    public sealed class GumoApiErrorEnvelope
    {
        [DataMember(Name = "error")]
        public GumoApiErrorBody Error { get; set; }
    }

    [DataContract]
    public sealed class GumoApiErrorBody
    {
        [DataMember(Name = "code")]
        public string Code { get; set; }

        [DataMember(Name = "message")]
        public string Message { get; set; }
    }

    [DataContract]
    public sealed class GumoLink
    {
        [DataMember(Name = "name")]
        public string Name { get; set; }

        [DataMember(Name = "url")]
        public string Url { get; set; }
    }

    [DataContract]
    public sealed class GumoMediaAsset
    {
        [DataMember(Name = "url")]
        public string Url { get; set; }
    }

    [DataContract]
    public sealed class GumoPatchGameRequest
    {
        [DataMember(Name = "name")]
        public string Name { get; set; }

        [DataMember(Name = "sorting_name")]
        public string SortingName { get; set; }

        [DataMember(Name = "description")]
        public string Description { get; set; }

        [DataMember(Name = "release_date")]
        public string ReleaseDate { get; set; }

        [DataMember(Name = "genres")]
        public List<string> Genres { get; set; } = new List<string>();

        [DataMember(Name = "developers")]
        public List<string> Developers { get; set; } = new List<string>();

        [DataMember(Name = "publishers")]
        public List<string> Publishers { get; set; } = new List<string>();

        [DataMember(Name = "tags")]
        public List<string> Tags { get; set; } = new List<string>();

        [DataMember(Name = "links")]
        public List<GumoLink> Links { get; set; } = new List<GumoLink>();

        [DataMember(Name = "cover_image")]
        public string CoverImage { get; set; }

        [DataMember(Name = "background_image")]
        public string BackgroundImage { get; set; }

        [DataMember(Name = "icon")]
        public string Icon { get; set; }
    }

    [DataContract]
    public sealed class GumoGame
    {
        [DataMember(Name = "id")]
        public string Id { get; set; }

        [DataMember(Name = "name")]
        public string Name { get; set; }

        [DataMember(Name = "sorting_name")]
        public string SortingName { get; set; }

        [DataMember(Name = "platforms")]
        public List<string> Platforms { get; set; } = new List<string>();

        [DataMember(Name = "description")]
        public string Description { get; set; }

        [DataMember(Name = "release_date")]
        public string ReleaseDate { get; set; }

        [DataMember(Name = "genres")]
        public List<string> Genres { get; set; } = new List<string>();

        [DataMember(Name = "developers")]
        public List<string> Developers { get; set; } = new List<string>();

        [DataMember(Name = "publishers")]
        public List<string> Publishers { get; set; } = new List<string>();

        [DataMember(Name = "tags")]
        public List<string> Tags { get; set; } = new List<string>();

        [DataMember(Name = "links")]
        public List<GumoLink> Links { get; set; } = new List<GumoLink>();

        [DataMember(Name = "visibility")]
        public string Visibility { get; set; }

        [DataMember(Name = "cover_image")]
        public string CoverImage { get; set; }

        [DataMember(Name = "background_image")]
        public string BackgroundImage { get; set; }

        [DataMember(Name = "icon")]
        public string Icon { get; set; }

        [DataMember(Name = "created_at")]
        public string CreatedAt { get; set; }

        [DataMember(Name = "updated_at")]
        public string UpdatedAt { get; set; }
    }

    [DataContract]
    public sealed class GumoLibrary
    {
        [DataMember(Name = "id")]
        public string Id { get; set; }

        [DataMember(Name = "name")]
        public string Name { get; set; }

        [DataMember(Name = "platform")]
        public string Platform { get; set; }

        [DataMember(Name = "visibility")]
        public string Visibility { get; set; }

        [DataMember(Name = "enabled")]
        public bool Enabled { get; set; }
    }

    [DataContract]
    public sealed class GumoGameVersion
    {
        [DataMember(Name = "id")]
        public string Id { get; set; }

        [DataMember(Name = "game_id")]
        public string GameId { get; set; }

        [DataMember(Name = "library_id")]
        public string LibraryId { get; set; }

        [DataMember(Name = "version_name")]
        public string VersionName { get; set; }

        [DataMember(Name = "version_code")]
        public string VersionCode { get; set; }

        [DataMember(Name = "original_source_name")]
        public string OriginalSourceName { get; set; }

        [DataMember(Name = "release_date")]
        public string ReleaseDate { get; set; }

        [DataMember(Name = "is_latest")]
        public bool IsLatest { get; set; }

        [DataMember(Name = "notes")]
        public string Notes { get; set; }

        [DataMember(Name = "save_path")]
        public string SavePath { get; set; }

        [DataMember(Name = "save_path_type")]
        public string SavePathType { get; set; }

        [DataMember(Name = "save_file_pattern")]
        public string SaveFilePattern { get; set; }

        [DataMember(Name = "created_at")]
        public string CreatedAt { get; set; }

        [DataMember(Name = "updated_at")]
        public string UpdatedAt { get; set; }
    }

    [DataContract]
    public sealed class GumoResourceError
    {
        [DataMember(Name = "code")]
        public string Code { get; set; }

        [DataMember(Name = "message")]
        public string Message { get; set; }

        [DataMember(Name = "retryable")]
        public bool? Retryable { get; set; }
    }

    [DataContract]
    public sealed class GumoUpload
    {
        [DataMember(Name = "id")]
        public string Id { get; set; }

        [DataMember(Name = "kind")]
        public string Kind { get; set; }

        [DataMember(Name = "library_id")]
        public string LibraryId { get; set; }

        [DataMember(Name = "platform")]
        public string Platform { get; set; }

        [DataMember(Name = "game_id")]
        public string GameId { get; set; }

        [DataMember(Name = "game_version_id")]
        public string GameVersionId { get; set; }

        [DataMember(Name = "state")]
        public string State { get; set; }

        [DataMember(Name = "filename")]
        public string Filename { get; set; }

        [DataMember(Name = "declared_size_bytes")]
        public long DeclaredSizeBytes { get; set; }

        [DataMember(Name = "received_size_bytes")]
        public long ReceivedSizeBytes { get; set; }

        [DataMember(Name = "checksum")]
        public string Checksum { get; set; }

        [DataMember(Name = "job_id")]
        public string JobId { get; set; }

        [DataMember(Name = "created_at")]
        public string CreatedAt { get; set; }

        [DataMember(Name = "updated_at")]
        public string UpdatedAt { get; set; }

        [DataMember(Name = "expires_at")]
        public string ExpiresAt { get; set; }

        [DataMember(Name = "error")]
        public GumoResourceError Error { get; set; }
    }

    [DataContract]
    public sealed class GumoImportSession
    {
        [DataMember(Name = "id")]
        public string Id { get; set; }

        [DataMember(Name = "kind")]
        public string Kind { get; set; }

        [DataMember(Name = "library_id")]
        public string LibraryId { get; set; }

        [DataMember(Name = "platform")]
        public string Platform { get; set; }

        [DataMember(Name = "game_id")]
        public string GameId { get; set; }

        [DataMember(Name = "game_version_id")]
        public string GameVersionId { get; set; }

        [DataMember(Name = "state")]
        public string State { get; set; }

        [DataMember(Name = "part_count")]
        public int PartCount { get; set; }

        [DataMember(Name = "uploaded_part_count")]
        public int UploadedPartCount { get; set; }

        [DataMember(Name = "declared_size_bytes")]
        public long DeclaredSizeBytes { get; set; }

        [DataMember(Name = "received_size_bytes")]
        public long ReceivedSizeBytes { get; set; }

        [DataMember(Name = "job_id")]
        public string JobId { get; set; }

        [DataMember(Name = "created_at")]
        public string CreatedAt { get; set; }

        [DataMember(Name = "updated_at")]
        public string UpdatedAt { get; set; }

        [DataMember(Name = "expires_at")]
        public string ExpiresAt { get; set; }

        [DataMember(Name = "error")]
        public GumoResourceError Error { get; set; }
    }

    [DataContract]
    public sealed class GumoUploadPart
    {
        [DataMember(Name = "id")]
        public string Id { get; set; }

        [DataMember(Name = "import_session_id")]
        public string ImportSessionId { get; set; }

        [DataMember(Name = "part_index")]
        public int PartIndex { get; set; }

        [DataMember(Name = "state")]
        public string State { get; set; }

        [DataMember(Name = "filename")]
        public string Filename { get; set; }

        [DataMember(Name = "declared_size_bytes")]
        public long DeclaredSizeBytes { get; set; }

        [DataMember(Name = "received_size_bytes")]
        public long ReceivedSizeBytes { get; set; }

        [DataMember(Name = "checksum")]
        public string Checksum { get; set; }

        [DataMember(Name = "created_at")]
        public string CreatedAt { get; set; }

        [DataMember(Name = "updated_at")]
        public string UpdatedAt { get; set; }

        [DataMember(Name = "error")]
        public GumoResourceError Error { get; set; }
    }

    [DataContract]
    public sealed class GumoJobProgress
    {
        [DataMember(Name = "phase")]
        public string Phase { get; set; }

        [DataMember(Name = "percent")]
        public int Percent { get; set; }
    }

    [DataContract]
    public sealed class GumoJob
    {
        [DataMember(Name = "id")]
        public string Id { get; set; }

        [DataMember(Name = "kind")]
        public string Kind { get; set; }

        [DataMember(Name = "state")]
        public string State { get; set; }

        [DataMember(Name = "upload_id")]
        public string UploadId { get; set; }

        [DataMember(Name = "game_id")]
        public string GameId { get; set; }

        [DataMember(Name = "game_version_id")]
        public string GameVersionId { get; set; }

        [DataMember(Name = "progress")]
        public GumoJobProgress Progress { get; set; }

        [DataMember(Name = "result")]
        public GumoJobResult Result { get; set; }

        [DataMember(Name = "error")]
        public GumoResourceError Error { get; set; }

        [DataMember(Name = "created_at")]
        public string CreatedAt { get; set; }

        [DataMember(Name = "updated_at")]
        public string UpdatedAt { get; set; }
    }

    [DataContract]
    public sealed class GumoArtifactPart
    {
        [DataMember(Name = "part_index")]
        public int PartIndex { get; set; }

        [DataMember(Name = "download_url")]
        public string DownloadUrl { get; set; }

        [DataMember(Name = "size_bytes")]
        public long SizeBytes { get; set; }

        [DataMember(Name = "checksum")]
        public string Checksum { get; set; }
    }

    [DataContract]
    public sealed class GumoInstallGame
    {
        [DataMember(Name = "id")]
        public string Id { get; set; }

        [DataMember(Name = "name")]
        public string Name { get; set; }

        [DataMember(Name = "platforms")]
        public List<string> Platforms { get; set; } = new List<string>();
    }

    [DataContract]
    public sealed class GumoInstallVersion
    {
        [DataMember(Name = "id")]
        public string Id { get; set; }

        [DataMember(Name = "version_name")]
        public string VersionName { get; set; }

        [DataMember(Name = "is_latest")]
        public bool IsLatest { get; set; }
    }

    [DataContract]
    public sealed class GumoInstallArtifact
    {
        [DataMember(Name = "id")]
        public string Id { get; set; }

        [DataMember(Name = "archive_type")]
        public string ArchiveType { get; set; }

        [DataMember(Name = "size_bytes")]
        public long SizeBytes { get; set; }

        [DataMember(Name = "checksum")]
        public string Checksum { get; set; }

        [DataMember(Name = "parts")]
        public List<GumoArtifactPart> Parts { get; set; } = new List<GumoArtifactPart>();
    }

    [DataContract]
    public sealed class GumoInstallManifest
    {
        [DataMember(Name = "game")]
        public GumoInstallGame Game { get; set; }

        [DataMember(Name = "version")]
        public GumoInstallVersion Version { get; set; }

        [DataMember(Name = "artifact")]
        public GumoInstallArtifact Artifact { get; set; }
    }

    [DataContract]
    public sealed class GumoSaveSnapshot
    {
        [DataMember(Name = "id")]
        public string Id { get; set; }

        [DataMember(Name = "game_id")]
        public string GameId { get; set; }

        [DataMember(Name = "game_version_id")]
        public string GameVersionId { get; set; }

        [DataMember(Name = "library_id")]
        public string LibraryId { get; set; }

        [DataMember(Name = "name")]
        public string Name { get; set; }

        [DataMember(Name = "captured_at")]
        public string CapturedAt { get; set; }

        [DataMember(Name = "archive_type")]
        public string ArchiveType { get; set; }

        [DataMember(Name = "size_bytes")]
        public long SizeBytes { get; set; }

        [DataMember(Name = "checksum")]
        public string Checksum { get; set; }

        [DataMember(Name = "notes")]
        public string Notes { get; set; }

        [DataMember(Name = "created_at")]
        public string CreatedAt { get; set; }
    }

    [DataContract]
    public sealed class GumoPatchVersionRequest
    {
        [DataMember(Name = "save_path")]
        public string SavePath { get; set; }

        [DataMember(Name = "save_path_type")]
        public string SavePathType { get; set; }

        [DataMember(Name = "save_file_pattern")]
        public string SaveFilePattern { get; set; }
    }

    [DataContract]
    public sealed class GumoSaveSnapshotManifest
    {
        [DataMember(Name = "id")]
        public string Id { get; set; }

        [DataMember(Name = "name")]
        public string Name { get; set; }

        [DataMember(Name = "captured_at")]
        public string CapturedAt { get; set; }

        [DataMember(Name = "archive_type")]
        public string ArchiveType { get; set; }

        [DataMember(Name = "size_bytes")]
        public long SizeBytes { get; set; }

        [DataMember(Name = "checksum")]
        public string Checksum { get; set; }
    }

    [DataContract]
    public sealed class GumoSaveRestoreManifest
    {
        [DataMember(Name = "game_id")]
        public string GameId { get; set; }

        [DataMember(Name = "game_version_id")]
        public string GameVersionId { get; set; }

        [DataMember(Name = "save_snapshot")]
        public GumoSaveSnapshotManifest SaveSnapshot { get; set; }

        [DataMember(Name = "parts")]
        public List<GumoArtifactPart> Parts { get; set; } = new List<GumoArtifactPart>();
    }

    [DataContract]
    public sealed class GumoUploadFileDescriptor
    {
        [DataMember(Name = "filename")]
        public string Filename { get; set; }

        [DataMember(Name = "size_bytes")]
        public long SizeBytes { get; set; }

        [DataMember(Name = "checksum")]
        public string Checksum { get; set; }
    }

    [DataContract]
    public sealed class GumoUploadNewGameTarget
    {
        [DataMember(Name = "name")]
        public string Name { get; set; }
    }

    [DataContract]
    public sealed class GumoUploadGameTarget
    {
        [DataMember(Name = "id")]
        public string Id { get; set; }

        [DataMember(Name = "create")]
        public GumoUploadNewGameTarget Create { get; set; }
    }

    [DataContract]
    public sealed class GumoUploadVersionTarget
    {
        [DataMember(Name = "version_name")]
        public string VersionName { get; set; }

        [DataMember(Name = "version_code")]
        public string VersionCode { get; set; }

        [DataMember(Name = "original_source_name")]
        public string OriginalSourceName { get; set; }

        [DataMember(Name = "notes")]
        public string Notes { get; set; }
    }

    [DataContract]
    public sealed class GumoCreateGamePayloadUploadRequest
    {
        [DataMember(Name = "library_id")]
        public string LibraryId { get; set; }

        [DataMember(Name = "platform")]
        public string Platform { get; set; }

        [DataMember(Name = "game")]
        public GumoUploadGameTarget Game { get; set; }

        [DataMember(Name = "version")]
        public GumoUploadVersionTarget Version { get; set; }

        [DataMember(Name = "file")]
        public GumoUploadFileDescriptor File { get; set; }

        [DataMember(Name = "idempotency_key")]
        public string IdempotencyKey { get; set; }
    }

    [DataContract]
    public sealed class GumoCreateGamePayloadImportSessionRequest
    {
        [DataMember(Name = "library_id")]
        public string LibraryId { get; set; }

        [DataMember(Name = "platform")]
        public string Platform { get; set; }

        [DataMember(Name = "game")]
        public GumoUploadGameTarget Game { get; set; }

        [DataMember(Name = "version")]
        public GumoUploadVersionTarget Version { get; set; }

        [DataMember(Name = "idempotency_key")]
        public string IdempotencyKey { get; set; }
    }

    [DataContract]
    public sealed class GumoCreateSaveSnapshotImportSessionRequest
    {
        [DataMember(Name = "game_version_id")]
        public string GameVersionId { get; set; }

        [DataMember(Name = "name")]
        public string Name { get; set; }

        [DataMember(Name = "notes")]
        public string Notes { get; set; }

        [DataMember(Name = "idempotency_key")]
        public string IdempotencyKey { get; set; }
    }

    [DataContract]
    public sealed class GumoCreateImportPartRequest
    {
        [DataMember(Name = "part_index")]
        public int? PartIndex { get; set; }

        [DataMember(Name = "file")]
        public GumoUploadFileDescriptor File { get; set; }
    }
}
