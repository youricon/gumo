# Task 05: Uploads And Jobs

## Status

Completed

## Goal

Implement the durable upload and background job system for game payloads and save snapshots.

## Depends On

- 03 Database And Migrations
- 04 Backend API Scaffold

## Deliverables

- upload creation/content/finalize flows
- shared upload infrastructure with kinds `game_payload` and `save_snapshot`
- background job execution for archive creation
- polling and recovery endpoints
- cleanup policy for stale uploads
- detailed subtask breakdown for execution tracking

## Subtasks

- [05a_upload_state_machine.md](/home/isaac/workspace/gumo/.agents/codex/tasks/001_init/05a_upload_state_machine.md)
- [05b_finalize_and_job_enqueue.md](/home/isaac/workspace/gumo/.agents/codex/tasks/001_init/05b_finalize_and_job_enqueue.md)
- [05c_job_execution.md](/home/isaac/workspace/gumo/.agents/codex/tasks/001_init/05c_job_execution.md)
- [05d_recovery_and_cleanup.md](/home/isaac/workspace/gumo/.agents/codex/tasks/001_init/05d_recovery_and_cleanup.md)
- [05e_uploads_and_jobs_testing.md](/home/isaac/workspace/gumo/.agents/codex/tasks/001_init/05e_uploads_and_jobs_testing.md)

## Steps

- Implement the shared upload state machine.
- Implement finalize and durable job enqueue semantics.
- Implement background job execution for both upload kinds.
- Implement polling, rediscovery, and cleanup behavior.
- Add focused automated lifecycle tests.

## Acceptance Criteria

- Uploads survive Playnite termination safely.
- Finalize is idempotent and does not create duplicate jobs.
- Jobs continue independently of the client lifecycle.
- Both local identifier persistence and server-side rediscovery are supported.
- Save snapshot uploads use the same infrastructure without conflating resource kinds.

## Tracking Checklist

- [x] Game payload upload flow implemented
- [x] Save snapshot upload flow implemented
- [x] Streamed content handling implemented
- [x] Finalize idempotency implemented
- [x] Background job execution implemented
- [x] Polling endpoints implemented
- [x] Recovery listing endpoints implemented
- [x] Cleanup policy implemented

## Blockers

- Archive write strategy and temp file layout must be chosen during implementation.

## Notes

- This task is the highest-risk integration area and should be tested heavily.
- Implemented in `backend/src/upload_jobs.rs` with shared upload intent payloads, temp-file storage, idempotent finalize, background polling, and cleanup/recovery support.
- Added migration `0009_upload_intent_payload.sql` to persist upload intent metadata needed for durable processing.
