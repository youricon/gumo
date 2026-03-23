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
