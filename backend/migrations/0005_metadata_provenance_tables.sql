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
