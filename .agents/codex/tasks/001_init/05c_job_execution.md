# Task 05C: Job Execution

## Status

Not started

## Parent Task

- [05_uploads_and_jobs.md](/home/isaac/workspace/gumo/.agents/codex/tasks/001_init/05_uploads_and_jobs.md)

## Goal

Run background archive jobs for imports and save snapshots to completion with durable progress.

## Depends On

- 05B Finalize And Job Enqueue

## Deliverables

- job runner loop or executor
- progress updates
- terminal result persistence
- archive creation for payloads and save snapshots

## Steps

- Implement background polling/dispatch for queued jobs.
- Process `import_archive` jobs into `version_artifacts` and `artifact_parts`.
- Process `save_snapshot_archive` jobs into `save_snapshots` and `save_snapshot_parts`.
- Persist progress phase and progress percent during execution.
- Mark jobs `completed` or `failed` with structured error info.
- Transition uploads in sync with job progress and outcome.

## Acceptance Criteria

- Queued jobs are processed without client participation.
- Successful jobs create the expected domain records.
- Failed jobs preserve enough structured diagnostics for client display.
- Upload and job terminal states stay consistent.

## Tracking Checklist

- [ ] Job dispatcher
- [ ] Import archive execution
- [ ] Save snapshot archive execution
- [ ] Progress persistence
- [ ] Structured failure handling

## Notes

- Keep archive type fixed to `zip` in v1.
