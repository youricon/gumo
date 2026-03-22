# Task 06: Playnite Integration Contract

## Status

Not started

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

- [ ] Playnite resource serializers implemented
- [ ] Metadata patch endpoints implemented
- [ ] Install manifest implemented
- [ ] Save restore manifest implemented
- [ ] Token auth implemented
- [ ] Editability rules enforced

## Blockers

- none

## Notes

- Treat Playnite as the primary metadata source in v1.
