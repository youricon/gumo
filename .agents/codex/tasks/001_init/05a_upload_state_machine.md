# Task 05A: Upload State Machine

## Status

Not started

## Parent Task

- [05_uploads_and_jobs.md](/home/isaac/workspace/gumo/.agents/codex/tasks/001_init/05_uploads_and_jobs.md)

## Goal

Implement the durable upload state machine shared by game payload and save snapshot uploads.

## Depends On

- 03 Database And Migrations
- 04 Backend API Scaffold

## Deliverables

- upload creation endpoints
- upload content streaming endpoint
- upload persistence model
- state transition enforcement
- interruption-safe recovery behavior

## Steps

- Implement upload creation for `game_payload`.
- Implement upload creation for `save_snapshot`.
- Persist upload records with `kind`, `state`, declared size, checksum, and expiry.
- Implement content streaming into temporary storage.
- Update `received_size_bytes` during transfer.
- Enforce valid state transitions: `created -> uploading -> uploaded`.
- Mark interrupted transfers as `abandoned` when appropriate.
- Preserve fully uploaded content for later finalize retries.
- Implement fetch endpoints for upload state inspection.

## Acceptance Criteria

- Both upload kinds use the same state machine.
- Interrupted client uploads do not produce false `uploaded` states.
- Uploaded content can survive client termination until expiry/finalize.
- Invalid state transitions are rejected explicitly.

## Tracking Checklist

- [ ] Game payload upload creation
- [ ] Save snapshot upload creation
- [ ] Temporary storage handling
- [ ] Streaming and byte accounting
- [ ] State transition validation
- [ ] Upload state fetch endpoint

## Notes

- No resumable chunk uploads in v1.
