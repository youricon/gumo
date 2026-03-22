# Task 03: Database And Migrations

## Status

Not started

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

- [ ] Reference migrations created
- [ ] Domain migrations created
- [ ] Metadata migrations created
- [ ] Artifact/save migrations created
- [ ] Upload/job migrations created
- [ ] Indexes created
- [ ] Seed data created
- [ ] SQLite caveats resolved

## Blockers

- Version uniqueness strategy must be chosen concretely during implementation.

## Notes

- Keep `jobs.upload_id` as the actual FK if the circular `uploads.job_id` relationship becomes awkward.
