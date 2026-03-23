# Task 03: Database And Migrations

## Status

Completed

## Goal

Implement the initial SQLite schema and forward-only SQL migrations.

## Depends On

- 01 Repo Foundation
- 02 Config And Domain

## Deliverables

- `sqlx` migration set
- SQLite schema matching the architecture doc
- schema initialization path for development and production
- basic DB access layer scaffolding

## Steps

- Create migrations for reference tables: `platforms`, `libraries`.
- Create migrations for primary domain tables: `games`, `game_versions`, `game_platforms`.
- Create migrations for normalized metadata tables and join tables.
- Create migrations for artifact and save snapshot tables.
- Create migrations for metadata provenance tables.
- Create migrations for durable `uploads` and `jobs` tables.
- Add indexes and seed the `pc` platform row.
- Ensure SQLite foreign keys are enabled in application startup and tests.
- Decide the concrete approach for `game_versions` uniqueness when `version_code` is null.

## Acceptance Criteria

- Migrations apply cleanly to an empty SQLite database.
- Schema matches the planned relational model.
- Seed data is idempotent.
- Upload/job tables support the planned durable lifecycle.
- Known SQLite caveats are addressed deliberately.

## Tracking Checklist

- [x] Reference migrations created
- [x] Domain migrations created
- [x] Metadata migrations created
- [x] Artifact/save migrations created
- [x] Upload/job migrations created
- [x] Indexes created
- [x] Seed data created
- [x] SQLite caveats resolved

## Blockers

- Version uniqueness strategy must be chosen concretely during implementation.

## Notes

- Keep `jobs.upload_id` as the actual FK if the circular `uploads.job_id` relationship becomes awkward.
- Implemented with `sqlx` migrations under `backend/migrations/` and a small database bootstrap layer in `backend/src/db.rs`.
- Chosen uniqueness strategy: keep the nullable SQLite unique index on `(game_id, version_name, version_code)` and rely on application-level duplicate checks when `version_code IS NULL`.
