# Task 05E: Uploads And Jobs Testing

## Status

Completed

## Parent Task

- [05_uploads_and_jobs.md](/home/isaac/workspace/gumo/.agents/codex/tasks/001_init/05_uploads_and_jobs.md)

## Goal

Verify upload, finalize, job, recovery, and cleanup behavior with focused automated tests.

## Depends On

- 05A Upload State Machine
- 05B Finalize And Job Enqueue
- 05C Job Execution
- 05D Recovery And Cleanup

## Deliverables

- upload lifecycle tests
- finalize idempotency tests
- interrupted client tests
- job execution tests
- recovery tests
- cleanup tests

## Steps

- Test game payload upload happy path.
- Test save snapshot upload happy path.
- Test interrupted upload behavior.
- Test finalize retry behavior.
- Test job success and failure paths.
- Test upload/job rediscovery after simulated client restart.
- Test cleanup of expired and abandoned uploads.

## Acceptance Criteria

- High-risk lifecycle paths are covered by automated tests.
- Interrupted and retried client flows are validated explicitly.
- Save snapshot handling is covered alongside game payload handling.

## Tracking Checklist

- [x] Happy path tests
- [x] Interrupted upload tests
- [x] Finalize retry tests
- [x] Job execution tests
- [x] Recovery tests
- [x] Cleanup tests

## Notes

- This task should complete before considering the upload/job system stable.
