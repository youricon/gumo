CREATE TABLE platforms (
  id INTEGER PRIMARY KEY,
  public_id TEXT NOT NULL UNIQUE,
  name TEXT NOT NULL UNIQUE,
  is_enabled INTEGER NOT NULL CHECK (is_enabled IN (0, 1)),
  match_priority INTEGER NOT NULL,
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL
);

CREATE TABLE libraries (
  id INTEGER PRIMARY KEY,
  public_id TEXT NOT NULL UNIQUE,
  name TEXT NOT NULL UNIQUE,
  root_path TEXT NOT NULL UNIQUE,
  platform_hint TEXT,
  visibility TEXT NOT NULL CHECK (visibility IN ('public', 'private')),
  is_enabled INTEGER NOT NULL CHECK (is_enabled IN (0, 1)),
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL
);

CREATE TABLE games (
  id INTEGER PRIMARY KEY,
  public_id TEXT NOT NULL UNIQUE,
  library_id INTEGER NOT NULL REFERENCES libraries(id) ON DELETE RESTRICT,
  name TEXT NOT NULL,
  sorting_name TEXT,
  description TEXT,
  release_date TEXT,
  cover_image TEXT,
  background_image TEXT,
  icon TEXT,
  source_slug TEXT,
  visibility TEXT NOT NULL CHECK (visibility IN ('public', 'private')),
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL
);

CREATE TABLE game_versions (
  id INTEGER PRIMARY KEY,
  public_id TEXT NOT NULL UNIQUE,
  game_id INTEGER NOT NULL REFERENCES games(id) ON DELETE RESTRICT,
  library_id INTEGER NOT NULL REFERENCES libraries(id) ON DELETE RESTRICT,
  version_name TEXT NOT NULL,
  version_code TEXT,
  original_source_name TEXT,
  release_date TEXT,
  notes TEXT,
  save_path TEXT,
  save_path_type TEXT,
  save_file_pattern TEXT,
  is_latest INTEGER NOT NULL CHECK (is_latest IN (0, 1)),
  storage_mode TEXT NOT NULL CHECK (storage_mode IN ('managed_archive')),
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL
);

CREATE TABLE game_platforms (
  game_id INTEGER NOT NULL REFERENCES games(id) ON DELETE CASCADE,
  platform_id INTEGER NOT NULL REFERENCES platforms(id) ON DELETE RESTRICT,
  PRIMARY KEY (game_id, platform_id)
);

CREATE TABLE genres (
  id INTEGER PRIMARY KEY,
  name TEXT NOT NULL UNIQUE
);

CREATE TABLE developers (
  id INTEGER PRIMARY KEY,
  name TEXT NOT NULL UNIQUE
);

CREATE TABLE publishers (
  id INTEGER PRIMARY KEY,
  name TEXT NOT NULL UNIQUE
);

CREATE TABLE links (
  id INTEGER PRIMARY KEY,
  game_id INTEGER NOT NULL REFERENCES games(id) ON DELETE CASCADE,
  name TEXT NOT NULL,
  url TEXT NOT NULL
);

CREATE TABLE game_genres (
  game_id INTEGER NOT NULL REFERENCES games(id) ON DELETE CASCADE,
  genre_id INTEGER NOT NULL REFERENCES genres(id) ON DELETE RESTRICT,
  PRIMARY KEY (game_id, genre_id)
);

CREATE TABLE game_developers (
  game_id INTEGER NOT NULL REFERENCES games(id) ON DELETE CASCADE,
  developer_id INTEGER NOT NULL REFERENCES developers(id) ON DELETE RESTRICT,
  PRIMARY KEY (game_id, developer_id)
);

CREATE TABLE game_publishers (
  game_id INTEGER NOT NULL REFERENCES games(id) ON DELETE CASCADE,
  publisher_id INTEGER NOT NULL REFERENCES publishers(id) ON DELETE RESTRICT,
  PRIMARY KEY (game_id, publisher_id)
);


CREATE TABLE version_artifacts (
  id INTEGER PRIMARY KEY,
  public_id TEXT NOT NULL UNIQUE,
  game_version_id INTEGER NOT NULL REFERENCES game_versions(id) ON DELETE RESTRICT,
  artifact_kind TEXT NOT NULL CHECK (artifact_kind IN ('game_payload', 'save_snapshot')),
  archive_type TEXT NOT NULL CHECK (archive_type IN ('zip')),
  relative_path TEXT NOT NULL,
  size_bytes INTEGER NOT NULL CHECK (size_bytes >= 0),
  checksum TEXT NOT NULL,
  part_count INTEGER NOT NULL CHECK (part_count >= 1),
  is_managed INTEGER NOT NULL CHECK (is_managed IN (0, 1)),
  created_at TEXT NOT NULL
);

CREATE TABLE artifact_parts (
  id INTEGER PRIMARY KEY,
  version_artifact_id INTEGER NOT NULL REFERENCES version_artifacts(id) ON DELETE CASCADE,
  part_index INTEGER NOT NULL CHECK (part_index >= 0),
  relative_path TEXT NOT NULL,
  size_bytes INTEGER NOT NULL CHECK (size_bytes >= 0),
  checksum TEXT NOT NULL,
  UNIQUE (version_artifact_id, part_index)
);

CREATE TABLE save_snapshots (
  id INTEGER PRIMARY KEY,
  public_id TEXT NOT NULL UNIQUE,
  game_id INTEGER NOT NULL REFERENCES games(id) ON DELETE RESTRICT,
  game_version_id INTEGER NOT NULL REFERENCES game_versions(id) ON DELETE RESTRICT,
  library_id INTEGER NOT NULL REFERENCES libraries(id) ON DELETE RESTRICT,
  name TEXT NOT NULL,
  captured_at TEXT NOT NULL,
  archive_type TEXT NOT NULL CHECK (archive_type IN ('zip')),
  size_bytes INTEGER NOT NULL CHECK (size_bytes >= 0),
  checksum TEXT NOT NULL,
  notes TEXT,
  created_at TEXT NOT NULL
);

CREATE TABLE save_snapshot_parts (
  id INTEGER PRIMARY KEY,
  save_snapshot_id INTEGER NOT NULL REFERENCES save_snapshots(id) ON DELETE CASCADE,
  part_index INTEGER NOT NULL CHECK (part_index >= 0),
  relative_path TEXT NOT NULL,
  size_bytes INTEGER NOT NULL CHECK (size_bytes >= 0),
  checksum TEXT NOT NULL,
  UNIQUE (save_snapshot_id, part_index)
);

CREATE TABLE metadata_sources (
  id INTEGER PRIMARY KEY,
  game_id INTEGER NOT NULL REFERENCES games(id) ON DELETE CASCADE,
  provider TEXT NOT NULL,
  provider_key TEXT NOT NULL,
  raw_payload TEXT NOT NULL,
  fetched_at TEXT NOT NULL
);

CREATE TABLE overrides (
  id INTEGER PRIMARY KEY,
  game_id INTEGER REFERENCES games(id) ON DELETE CASCADE,
  game_version_id INTEGER REFERENCES game_versions(id) ON DELETE CASCADE,
  field_name TEXT NOT NULL,
  value TEXT,
  source TEXT NOT NULL,
  updated_at TEXT NOT NULL,
  CHECK (
    (game_id IS NOT NULL AND game_version_id IS NULL) OR
    (game_id IS NULL AND game_version_id IS NOT NULL)
  )
);

CREATE TABLE uploads (
  id INTEGER PRIMARY KEY,
  public_id TEXT NOT NULL UNIQUE,
  kind TEXT NOT NULL CHECK (kind IN ('game_payload', 'save_snapshot')),
  library_id INTEGER NOT NULL REFERENCES libraries(id) ON DELETE RESTRICT,
  platform_id INTEGER REFERENCES platforms(id) ON DELETE RESTRICT,
  game_id INTEGER REFERENCES games(id) ON DELETE RESTRICT,
  game_version_id INTEGER REFERENCES game_versions(id) ON DELETE RESTRICT,
  state TEXT NOT NULL CHECK (
    state IN ('created', 'uploading', 'uploaded', 'finalizing', 'queued', 'processing', 'completed', 'failed', 'abandoned', 'expired')
  ),
  filename TEXT NOT NULL,
  declared_size_bytes INTEGER NOT NULL CHECK (declared_size_bytes >= 0),
  received_size_bytes INTEGER NOT NULL DEFAULT 0 CHECK (received_size_bytes >= 0),
  checksum TEXT,
  temp_path TEXT NOT NULL,
  job_id INTEGER,
  idempotency_key TEXT,
  intent_payload TEXT NOT NULL DEFAULT '{}',
  expires_at TEXT,
  error_code TEXT,
  error_message TEXT,
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL
);

CREATE TABLE jobs (
  id INTEGER PRIMARY KEY,
  public_id TEXT NOT NULL UNIQUE,
  kind TEXT NOT NULL CHECK (kind IN ('import_archive', 'save_snapshot_archive')),
  state TEXT NOT NULL CHECK (state IN ('pending', 'processing', 'completed', 'failed')),
  upload_id INTEGER REFERENCES uploads(id) ON DELETE SET NULL,
  game_id INTEGER REFERENCES games(id) ON DELETE SET NULL,
  game_version_id INTEGER REFERENCES game_versions(id) ON DELETE SET NULL,
  progress_phase TEXT,
  progress_percent INTEGER CHECK (progress_percent IS NULL OR (progress_percent >= 0 AND progress_percent <= 100)),
  result_payload TEXT,
  error_code TEXT,
  error_message TEXT,
  retryable INTEGER CHECK (retryable IS NULL OR retryable IN (0, 1)),
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL
);

CREATE TABLE integration_tokens (
  id INTEGER PRIMARY KEY,
  public_id TEXT NOT NULL UNIQUE,
  label TEXT NOT NULL UNIQUE,
  token_hash TEXT NOT NULL UNIQUE,
  is_enabled INTEGER NOT NULL CHECK (is_enabled IN (0, 1)),
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL
);

CREATE TABLE import_sessions (
  id INTEGER PRIMARY KEY,
  public_id TEXT NOT NULL UNIQUE,
  kind TEXT NOT NULL CHECK (kind IN ('game_payload', 'save_snapshot')),
  library_id INTEGER NOT NULL REFERENCES libraries(id) ON DELETE RESTRICT,
  platform_id INTEGER REFERENCES platforms(id) ON DELETE RESTRICT,
  game_id INTEGER REFERENCES games(id) ON DELETE RESTRICT,
  game_version_id INTEGER REFERENCES game_versions(id) ON DELETE RESTRICT,
  state TEXT NOT NULL CHECK (
    state IN ('created', 'uploading', 'uploaded', 'finalizing', 'queued', 'processing', 'completed', 'failed', 'abandoned', 'expired')
  ),
  job_id INTEGER REFERENCES jobs(id) ON DELETE SET NULL,
  idempotency_key TEXT,
  intent_payload TEXT NOT NULL DEFAULT '{}',
  expires_at TEXT,
  error_code TEXT,
  error_message TEXT,
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL
);

CREATE TABLE upload_parts (
  id INTEGER PRIMARY KEY,
  public_id TEXT NOT NULL UNIQUE,
  import_session_id INTEGER NOT NULL REFERENCES import_sessions(id) ON DELETE CASCADE,
  part_index INTEGER NOT NULL CHECK (part_index >= 0),
  state TEXT NOT NULL CHECK (state IN ('created', 'uploading', 'uploaded', 'abandoned', 'expired')),
  filename TEXT NOT NULL,
  declared_size_bytes INTEGER NOT NULL CHECK (declared_size_bytes >= 0),
  received_size_bytes INTEGER NOT NULL DEFAULT 0 CHECK (received_size_bytes >= 0),
  checksum TEXT,
  temp_path TEXT NOT NULL,
  error_code TEXT,
  error_message TEXT,
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL,
  UNIQUE (import_session_id, part_index)
);

CREATE INDEX idx_games_library_name ON games (library_id, name);
CREATE INDEX idx_games_library_sorting_name ON games (library_id, sorting_name);
CREATE INDEX idx_game_versions_game_latest ON game_versions (game_id, is_latest);
CREATE INDEX idx_game_versions_library_created_at ON game_versions (library_id, created_at);
CREATE UNIQUE INDEX idx_game_versions_identity ON game_versions (game_id, version_name, version_code);
CREATE INDEX idx_version_artifacts_game_version ON version_artifacts (game_version_id);
CREATE INDEX idx_version_artifacts_checksum ON version_artifacts (checksum);
CREATE INDEX idx_save_snapshots_version_captured_at ON save_snapshots (game_version_id, captured_at DESC);
CREATE INDEX idx_save_snapshots_library_captured_at ON save_snapshots (library_id, captured_at DESC);
CREATE INDEX idx_links_game_id ON links (game_id);
CREATE INDEX idx_metadata_sources_game_provider ON metadata_sources (game_id, provider);
CREATE INDEX idx_metadata_sources_provider_key ON metadata_sources (provider, provider_key);
CREATE INDEX idx_uploads_state_updated_at ON uploads (state, updated_at);
CREATE INDEX idx_uploads_job_id ON uploads (job_id);
CREATE INDEX idx_uploads_idempotency_key ON uploads (idempotency_key);
CREATE INDEX idx_jobs_state_updated_at ON jobs (state, updated_at);
CREATE INDEX idx_jobs_kind_state ON jobs (kind, state);
CREATE INDEX idx_jobs_upload_id ON jobs (upload_id);
CREATE INDEX idx_integration_tokens_enabled ON integration_tokens (is_enabled);
CREATE INDEX idx_import_sessions_kind_created_at ON import_sessions (kind, created_at DESC);
CREATE INDEX idx_import_sessions_job_id ON import_sessions (job_id);
CREATE INDEX idx_upload_parts_import_session_id ON upload_parts (import_session_id, part_index);
