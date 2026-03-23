# Task 10: Packaging And Release

## Status

Not started

## Goal

Create a reliable Windows-side packaging flow for distributable Playnite plugin artifacts.

## Depends On

- 04 Plugin Scaffold And Configuration
- 08 Installation Flow
- 09 Save Backup And Restore

## Deliverables

- release build instructions
- packaging script
- packaged extension artifact
- clean install validation steps

## Steps

- Implement a Windows packaging script for the plugin.
- Build in `Release`.
- Package the plugin in the format expected by Playnite.
- Validate installation in a clean Playnite environment.
- Document the release checklist.

## Acceptance Criteria

- Packaging is repeatable on Windows.
- The release artifact is installable in Playnite.
- Packaging logic is kept in-repo.

## Tracking Checklist

- [ ] Packaging script added
- [ ] Release build documented
- [ ] Artifact format validated
- [ ] Clean install validation documented

## Notes

- Windows is the only required release path.
