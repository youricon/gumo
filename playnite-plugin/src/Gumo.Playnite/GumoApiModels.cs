using System.Collections.Generic;
using Newtonsoft.Json;

namespace Gumo.Playnite
{
    public sealed class GumoListResponse<T>
    {
        [JsonProperty("items")]
        public List<T> Items { get; set; } = new List<T>();

        [JsonProperty("next_cursor")]
        public string NextCursor { get; set; }
    }

    public sealed class GumoApiErrorEnvelope
    {
        [JsonProperty("error")]
        public GumoApiErrorBody Error { get; set; }
    }

    public sealed class GumoApiErrorBody
    {
        [JsonProperty("code")]
        public string Code { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }
    }

    public sealed class GumoLink
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }
    }

    public sealed class GumoGame
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("sorting_name")]
        public string SortingName { get; set; }

        [JsonProperty("platforms")]
        public List<string> Platforms { get; set; } = new List<string>();

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("release_date")]
        public string ReleaseDate { get; set; }

        [JsonProperty("genres")]
        public List<string> Genres { get; set; } = new List<string>();

        [JsonProperty("developers")]
        public List<string> Developers { get; set; } = new List<string>();

        [JsonProperty("publishers")]
        public List<string> Publishers { get; set; } = new List<string>();

        [JsonProperty("links")]
        public List<GumoLink> Links { get; set; } = new List<GumoLink>();

        [JsonProperty("visibility")]
        public string Visibility { get; set; }

        [JsonProperty("cover_image")]
        public string CoverImage { get; set; }

        [JsonProperty("background_image")]
        public string BackgroundImage { get; set; }

        [JsonProperty("icon")]
        public string Icon { get; set; }

        [JsonProperty("created_at")]
        public string CreatedAt { get; set; }

        [JsonProperty("updated_at")]
        public string UpdatedAt { get; set; }
    }

    public sealed class GumoGameVersion
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("game_id")]
        public string GameId { get; set; }

        [JsonProperty("library_id")]
        public string LibraryId { get; set; }

        [JsonProperty("version_name")]
        public string VersionName { get; set; }

        [JsonProperty("version_code")]
        public string VersionCode { get; set; }

        [JsonProperty("release_date")]
        public string ReleaseDate { get; set; }

        [JsonProperty("is_latest")]
        public bool IsLatest { get; set; }

        [JsonProperty("notes")]
        public string Notes { get; set; }

        [JsonProperty("created_at")]
        public string CreatedAt { get; set; }

        [JsonProperty("updated_at")]
        public string UpdatedAt { get; set; }
    }

    public sealed class GumoResourceError
    {
        [JsonProperty("code")]
        public string Code { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("retryable")]
        public bool? Retryable { get; set; }
    }

    public sealed class GumoUpload
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("kind")]
        public string Kind { get; set; }

        [JsonProperty("library_id")]
        public string LibraryId { get; set; }

        [JsonProperty("platform")]
        public string Platform { get; set; }

        [JsonProperty("game_id")]
        public string GameId { get; set; }

        [JsonProperty("game_version_id")]
        public string GameVersionId { get; set; }

        [JsonProperty("state")]
        public string State { get; set; }

        [JsonProperty("filename")]
        public string Filename { get; set; }

        [JsonProperty("declared_size_bytes")]
        public long DeclaredSizeBytes { get; set; }

        [JsonProperty("received_size_bytes")]
        public long ReceivedSizeBytes { get; set; }

        [JsonProperty("checksum")]
        public string Checksum { get; set; }

        [JsonProperty("job_id")]
        public string JobId { get; set; }

        [JsonProperty("created_at")]
        public string CreatedAt { get; set; }

        [JsonProperty("updated_at")]
        public string UpdatedAt { get; set; }

        [JsonProperty("expires_at")]
        public string ExpiresAt { get; set; }

        [JsonProperty("error")]
        public GumoResourceError Error { get; set; }
    }

    public sealed class GumoJobProgress
    {
        [JsonProperty("phase")]
        public string Phase { get; set; }

        [JsonProperty("percent")]
        public int Percent { get; set; }
    }

    public sealed class GumoJob
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("kind")]
        public string Kind { get; set; }

        [JsonProperty("state")]
        public string State { get; set; }

        [JsonProperty("upload_id")]
        public string UploadId { get; set; }

        [JsonProperty("game_id")]
        public string GameId { get; set; }

        [JsonProperty("game_version_id")]
        public string GameVersionId { get; set; }

        [JsonProperty("progress")]
        public GumoJobProgress Progress { get; set; }

        [JsonProperty("result")]
        public object Result { get; set; }

        [JsonProperty("error")]
        public GumoResourceError Error { get; set; }

        [JsonProperty("created_at")]
        public string CreatedAt { get; set; }

        [JsonProperty("updated_at")]
        public string UpdatedAt { get; set; }
    }

    public sealed class GumoArtifactPart
    {
        [JsonProperty("part_index")]
        public int PartIndex { get; set; }

        [JsonProperty("download_url")]
        public string DownloadUrl { get; set; }

        [JsonProperty("size_bytes")]
        public long SizeBytes { get; set; }

        [JsonProperty("checksum")]
        public string Checksum { get; set; }
    }

    public sealed class GumoInstallGame
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("platforms")]
        public List<string> Platforms { get; set; } = new List<string>();
    }

    public sealed class GumoInstallVersion
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("version_name")]
        public string VersionName { get; set; }

        [JsonProperty("is_latest")]
        public bool IsLatest { get; set; }
    }

    public sealed class GumoInstallArtifact
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("archive_type")]
        public string ArchiveType { get; set; }

        [JsonProperty("size_bytes")]
        public long SizeBytes { get; set; }

        [JsonProperty("checksum")]
        public string Checksum { get; set; }

        [JsonProperty("parts")]
        public List<GumoArtifactPart> Parts { get; set; } = new List<GumoArtifactPart>();
    }

    public sealed class GumoInstallManifest
    {
        [JsonProperty("game")]
        public GumoInstallGame Game { get; set; }

        [JsonProperty("version")]
        public GumoInstallVersion Version { get; set; }

        [JsonProperty("artifact")]
        public GumoInstallArtifact Artifact { get; set; }
    }

    public sealed class GumoSaveSnapshot
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("game_id")]
        public string GameId { get; set; }

        [JsonProperty("game_version_id")]
        public string GameVersionId { get; set; }

        [JsonProperty("library_id")]
        public string LibraryId { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("captured_at")]
        public string CapturedAt { get; set; }

        [JsonProperty("archive_type")]
        public string ArchiveType { get; set; }

        [JsonProperty("size_bytes")]
        public long SizeBytes { get; set; }

        [JsonProperty("checksum")]
        public string Checksum { get; set; }

        [JsonProperty("notes")]
        public string Notes { get; set; }

        [JsonProperty("created_at")]
        public string CreatedAt { get; set; }
    }

    public sealed class GumoSaveSnapshotManifest
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("captured_at")]
        public string CapturedAt { get; set; }

        [JsonProperty("archive_type")]
        public string ArchiveType { get; set; }

        [JsonProperty("size_bytes")]
        public long SizeBytes { get; set; }

        [JsonProperty("checksum")]
        public string Checksum { get; set; }
    }

    public sealed class GumoSaveRestoreManifest
    {
        [JsonProperty("game_id")]
        public string GameId { get; set; }

        [JsonProperty("game_version_id")]
        public string GameVersionId { get; set; }

        [JsonProperty("save_snapshot")]
        public GumoSaveSnapshotManifest SaveSnapshot { get; set; }

        [JsonProperty("parts")]
        public List<GumoArtifactPart> Parts { get; set; } = new List<GumoArtifactPart>();
    }
}
