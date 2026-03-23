# Task 07a: Import Session API

## Status

In progress

## Goal

Replace the single-payload upload assumption with a logical import session that can own multiple uploaded archive parts.

## Depends On

- 07 Uploads Jobs And Recovery

## Deliverables

- backend import-session resource shape
- upload-part resource shape
- finalize semantics for archive sets
- plugin API client adjustments for the new contract

## Steps

- Define the backend API for creating a game-payload import session.
- Define the backend API for registering ordered upload parts within a session.
- Define the finalize contract for a completed archive set.
- Update plugin-side request/response models to match.
- Preserve durable job polling and recovery semantics after the contract change.

## Acceptance Criteria

- One logical import can contain one or more uploaded archive parts.
- Finalize operates on the import session rather than a single uploaded file.
- Recovery can resume from persisted session and job identifiers.

## Tracking Checklist

- [x] Import-session API defined
- [x] Upload-part API defined
- [x] Finalize contract updated
- [x] Plugin API client updated
- [ ] Recovery contract updated

## Notes

- This is the core protocol change needed for folder uploads and multipart archive sets.
- Keep the contract idempotent and durable across Playnite termination.
- Current bridge behavior:
  - import sessions and upload parts are now first-class API resources
  - finalize currently materializes a legacy single-payload upload/job internally
  - sessions with more than one uploaded part are rejected until the archive-set execution path lands
