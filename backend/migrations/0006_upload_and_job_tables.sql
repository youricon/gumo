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
