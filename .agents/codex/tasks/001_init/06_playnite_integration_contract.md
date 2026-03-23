# Task 06: Playnite Integration Contract

## Status

Completed

## Goal

Finalize and support the Gumo API contract expected by the future Playnite `LibraryPlugin`.

## Depends On

- 02 Config And Domain
- 04 Backend API Scaffold
- 05 Uploads And Jobs

## Deliverables

- stable Playnite-facing API resource shapes
- metadata patching behavior aligned with Playnite terminology
- install manifest and save restore manifest endpoints
- token-authenticated integration boundary
- explicit editability rules for game and version metadata

## Steps

- Implement Playnite-facing resource serialization for games, versions, artifacts, uploads, jobs, and save snapshots.
- Implement metadata patch endpoints for game-level and version-level editable fields.
- Implement install manifest endpoint for version-specific installs.
- Implement save snapshot listing and restore manifest endpoints.
- Implement integration token auth and request authorization.
- Validate that Playnite-facing resource names remain aligned with verified SDK terminology.

## Acceptance Criteria

- The API surface matches the planned Playnite integration contract.
- Game metadata names align with `GameMetadata` concepts.
- Install and save restore manifests provide enough information for the plugin to act without backend guesswork.
- Integration auth works independently of browser sessions.

## Tracking Checklist

- [x] Playnite resource serializers implemented
- [x] Metadata patch endpoints implemented
- [x] Install manifest implemented
- [x] Save restore manifest implemented
- [x] Token auth implemented
- [x] Editability rules enforced

## Blockers

- none

## Notes

- Treat Playnite as the primary metadata source in v1.
- Implemented in `backend/src/playnite.rs`, `backend/src/api/auth.rs`, and `backend/src/api/routes/integration.rs`.
- Verified with `nix develop --command cargo test --manifest-path backend/Cargo.toml`.
- Verified with `nix build .#gumo-backend`.
