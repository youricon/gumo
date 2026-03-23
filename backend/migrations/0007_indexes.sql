CREATE INDEX idx_games_library_name ON games (library_id, name);
CREATE INDEX idx_games_library_sorting_name ON games (library_id, sorting_name);
CREATE INDEX idx_game_versions_game_latest ON game_versions (game_id, is_latest);
CREATE INDEX idx_game_versions_library_created_at ON game_versions (library_id, created_at);
CREATE UNIQUE INDEX idx_game_versions_identity
ON game_versions (game_id, version_name, version_code);
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
