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
  release_date TEXT,
  notes TEXT,
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
