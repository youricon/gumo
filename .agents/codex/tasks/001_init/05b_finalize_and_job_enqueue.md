# Task 05B: Finalize And Job Enqueue

## Status

Not started

## Parent Task

- [05_uploads_and_jobs.md](/home/isaac/workspace/gumo/.agents/codex/tasks/001_init/05_uploads_and_jobs.md)

## Goal

Implement idempotent upload finalization and background job creation.

## Depends On

- 05A Upload State Machine

## Deliverables

- finalize endpoint
- idempotency handling
- durable job creation
- upload-to-job linkage

## Steps

- Implement `POST /uploads/:id/finalize`.
- Validate upload is in a finalizable state.
- Verify declared size and checksum when available.
- Create a durable job record with the correct `kind`.
- Transition upload state into `queued`.
- Ensure retries do not create duplicate jobs.
- Return `upload_id`, `job_id`, and current status.

## Acceptance Criteria

- Finalize is idempotent.
- Finalize accepted once continues to completion even if the client disconnects.
- Game payload uploads create `import_archive` jobs.
- Save snapshot uploads create `save_snapshot_archive` jobs.

## Tracking Checklist

- [ ] Finalize endpoint
- [ ] Finalize validation
- [ ] Job creation
- [ ] Upload/job linking
- [ ] Idempotency handling

## Notes

- Keep `jobs.upload_id` as the authoritative FK.
