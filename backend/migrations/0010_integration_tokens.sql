CREATE TABLE integration_tokens (
  id INTEGER PRIMARY KEY,
  public_id TEXT NOT NULL UNIQUE,
  label TEXT NOT NULL UNIQUE,
  token_hash TEXT NOT NULL UNIQUE,
  is_enabled INTEGER NOT NULL CHECK (is_enabled IN (0, 1)),
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL
);

CREATE INDEX idx_integration_tokens_enabled
ON integration_tokens (is_enabled);
