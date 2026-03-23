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
