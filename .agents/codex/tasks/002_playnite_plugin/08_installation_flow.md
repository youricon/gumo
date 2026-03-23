# Task 08: Installation Flow

## Status

In progress

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
- Follow-up work is tracked in:
  - [08a_install_from_archive_set.md](/home/isaac/workspace/gumo/.agents/codex/tasks/002_playnite_plugin/08a_install_from_archive_set.md)
  - [08b_install_cleanup_and_flattening.md](/home/isaac/workspace/gumo/.agents/codex/tasks/002_playnite_plugin/08b_install_cleanup_and_flattening.md)
  - [08c_directory_upload_ux.md](/home/isaac/workspace/gumo/.agents/codex/tasks/002_playnite_plugin/08c_directory_upload_ux.md)
