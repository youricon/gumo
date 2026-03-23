# Task 04: Plugin Scaffold And Configuration

## Status

In progress

## Goal

Create the Playnite `LibraryPlugin` scaffold and the plugin configuration model.

## Depends On

- 01 Repo And Project Setup
- 02 Windows Dev Environment

## Deliverables

- initial Playnite plugin project
- plugin manifest/package metadata
- settings UI or config model for Gumo server URL and API token
- logging baseline

## Steps

- Create the plugin project targeting the required Playnite-compatible framework.
- Implement the minimal `LibraryPlugin` structure.
- Add plugin configuration for Gumo server URL and API token.
- Add logging around startup, connectivity, and failures.
- Ensure the plugin loads cleanly in Playnite before implementing deeper flows.

## Acceptance Criteria

- The plugin loads in Playnite.
- Basic configuration can be set for server URL and token.
- Logs are good enough to debug startup and API issues.

## Tracking Checklist

- [x] LibraryPlugin scaffold created
- [x] Manifest/package metadata added
- [x] Server URL setting added
- [x] API token setting added
- [x] Logging baseline added

## Notes

- Keep the initial scaffold small and observable.
- Initial scaffold created under `playnite-plugin/src/Gumo.Playnite/`, but Playnite runtime validation on Windows is still pending.
