# Task 11: Windows CI

## Status

Not started

## Goal

Automate Windows-side build and packaging for the Playnite plugin.

## Depends On

- 10 Packaging And Release

## Deliverables

- Windows CI workflow
- release or artifact upload path
- path filters to avoid unnecessary Windows runs
- CI documentation

## Steps

- Add a Windows workflow for the plugin.
- Build the plugin in `Release`.
- Run the packaging script.
- Upload the packaged artifact.
- Restrict the workflow to relevant file changes where practical.

## Acceptance Criteria

- The plugin can be built and packaged automatically on Windows.
- CI output is useful for testing and future release automation.
- The Windows pipeline stays separate from the main Nix workflow.

## Tracking Checklist

- [ ] Windows workflow added
- [ ] Release build automated
- [ ] Packaging automated
- [ ] Artifact upload added
- [ ] Path filters reviewed

## Notes

- Keep plugin CI isolated from the Nix-first service pipeline.
