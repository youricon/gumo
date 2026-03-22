# Task 05D: Recovery And Cleanup

## Status

Not started

## Parent Task

- [05_uploads_and_jobs.md](/home/isaac/workspace/gumo/.agents/codex/tasks/001_init/05_uploads_and_jobs.md)

## Goal

Support Playnite restart recovery and server-side cleanup of stale upload/job state.

## Depends On

- 05A Upload State Machine
- 05B Finalize And Job Enqueue
- 05C Job Execution

## Deliverables

- polling endpoints for uploads and jobs
- list/recovery endpoints
- cleanup routines
- expiry policy enforcement

## Steps

- Implement `GET /uploads/:id` and `GET /jobs/:id`.
- Implement list endpoints for recent/active uploads and jobs.
- Support filtering by active, failed, and recent completed states.
- Implement expiry handling for stale `created`, `abandoned`, and unfinalized `uploaded` uploads.
- Implement retention policy for failed and completed history.
- Remove temporary files during cleanup when safe.

## Acceptance Criteria

- Playnite can recover using saved ids.
- Playnite can recover even if local ids are lost.
- Stale temporary uploads are cleaned without affecting durable completed records.
- Cleanup policy is conservative and explicit.

## Tracking Checklist

- [ ] Upload polling endpoint
- [ ] Job polling endpoint
- [ ] Recovery list endpoints
- [ ] Expiry handling
- [ ] Temp file cleanup
- [ ] Retention policy implementation

## Notes

- Recovery must work after abrupt Playnite termination.
