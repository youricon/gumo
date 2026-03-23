# Task 08: Installation Flow

## Status

Not started

## Goal

Install Gumo-managed game versions from Playnite using Gumo install manifests and local extraction.

## Depends On

- 05 API Client And Auth
- 06 Library Sync And Metadata

## Deliverables

- install manifest handling
- archive download flow
- checksum verification
- extraction into a Playnite-managed install directory
- Playnite install/play actions

## Steps

- Request install manifests for selected versions.
- Download archive artifacts and parts in order.
- Verify checksums before extraction.
- Extract into the configured install location.
- Wire Playnite install/play actions around the managed install lifecycle.

## Acceptance Criteria

- A user can install a selected Gumo game version from Playnite.
- The plugin does not guess backend state that is already present in the manifest.
- Install failures are diagnosable.

## Tracking Checklist

- [ ] Install manifest handling added
- [ ] Download flow added
- [ ] Checksum verification added
- [ ] Extraction flow added
- [ ] Install/play actions wired

## Notes

- Keep local install state owned by Playnite.
