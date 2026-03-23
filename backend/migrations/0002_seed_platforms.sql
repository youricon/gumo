INSERT INTO platforms (public_id, name, is_enabled, match_priority, created_at, updated_at)
VALUES ('platform_pc', 'pc', 1, 100, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
ON CONFLICT(name) DO UPDATE SET
  is_enabled = excluded.is_enabled,
  match_priority = excluded.match_priority,
  updated_at = CURRENT_TIMESTAMP;
