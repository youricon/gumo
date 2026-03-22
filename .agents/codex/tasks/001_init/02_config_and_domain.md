# Task 02: Config And Domain

## Status

Completed

## Goal

Implement the app-native configuration schema and core domain model boundaries agreed during planning.

## Depends On

- 01 Repo Foundation

## Deliverables

- config schema representation for TOML
- config loading and validation boundary
- domain types for libraries, games, versions, artifacts, save snapshots, uploads, and jobs
- enums and validation rules aligned with the architecture document

## Steps

- Implement the TOML config model with sections for `server`, `storage`, `auth`, `integrations`, `libraries`, and `platforms`.
- Define strict validation for required paths, visibility values, auth modes, upload kinds, and job states.
- Model Playnite-aligned game metadata fields such as `name`, `sorting_name`, `release_date`, `genres`, `developers`, `publishers`, and `links`.
- Model version-specific save snapshot concepts as first-class domain types.
- Define serialization boundaries between internal domain models and API resource shapes.
- Keep `public_id` as the external API identity for domain resources.

## Acceptance Criteria

- Config shape matches [docs/architecture.md](/home/isaac/workspace/gumo/docs/architecture.md).
- Invalid config is rejected with clear validation errors.
- Domain model covers games, versions, artifacts, uploads, jobs, and save snapshots.
- Public metadata fields align with Playnite terminology.

## Tracking Checklist

- [x] Config types defined
- [x] Config validation defined
- [x] Domain enums defined
- [x] Core domain entities defined
- [x] Playnite metadata alignment implemented

## Blockers

- none

## Notes

- Keep public and integration-facing names aligned with Playnite unless there is a strong reason not to.
- Implemented in `backend/src/config.rs` and `backend/src/domain.rs` with TOML loading, validation, and Playnite-aligned metadata names.
- Example config now includes `integrations.playnite` and `[[platforms]]` so local development uses the same schema as the app.
