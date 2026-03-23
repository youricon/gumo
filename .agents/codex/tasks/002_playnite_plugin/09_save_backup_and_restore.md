# Task 09: Save Backup And Restore

## Status

Not started

## Goal

Support version-specific save snapshot backup and restore flows through the plugin.

## Depends On

- 05 API Client And Auth
- 06 Library Sync And Metadata

## Deliverables

- save upload flow
- save restore manifest handling
- local save location configuration or detection strategy
- restore extraction flow

## Steps

- Define how the plugin discovers or configures local save paths.
- Implement save snapshot uploads for a selected game version.
- Implement save snapshot listing and restore manifest requests.
- Download, verify, and extract save snapshots into the configured location.

## Acceptance Criteria

- Save snapshots are tied to the intended game version.
- Backup and restore flows are explicit and debuggable.
- Save handling remains a plugin-side local filesystem concern.

## Tracking Checklist

- [ ] Save path strategy defined
- [ ] Save upload flow added
- [ ] Save listing flow added
- [ ] Save restore flow added

## Notes

- Keep save backup and restore semantics conservative and explicit.
