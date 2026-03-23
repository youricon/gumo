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

CREATE INDEX idx_import_sessions_kind_created_at ON import_sessions(kind, created_at DESC);
CREATE INDEX idx_import_sessions_job_id ON import_sessions(job_id);
CREATE INDEX idx_upload_parts_import_session_id ON upload_parts(import_session_id, part_index);
