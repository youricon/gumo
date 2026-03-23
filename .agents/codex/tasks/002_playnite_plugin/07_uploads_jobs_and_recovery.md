# Task 07: Uploads Jobs And Recovery

## Status

In progress

## Goal

Support game payload uploads and job polling with durable recovery after Playnite termination.

## Depends On

- 05 API Client And Auth
- 06 Library Sync And Metadata

## Deliverables

- create/upload/finalize flow for game payloads
- job polling UI or status handling
- local persistence of upload/job IDs
- recovery logic after restart

## Steps

- Implement game payload upload creation.
- Implement content upload and finalize calls.
- Poll job state until completion or failure.
- Persist upload/job identifiers locally.
- Restore and resume pending work after Playnite restart.

## Acceptance Criteria

- Uploads can continue to be tracked after plugin interruption.
- The plugin can rediscover active work if local state is incomplete.
- Users can see actionable failure states.

## Tracking Checklist

- [x] Game upload flow added
- [x] Finalize flow added
- [x] Job polling added
- [x] Local recovery state added
- [x] Restart recovery added

## Notes

- Treat interruption handling as a first-class requirement.
- Current upload scope supports creating new Gumo games from local payload files through the Playnite custom import flow.
- Pending upload state is persisted in plugin settings using `upload_id`, `job_id`, source file path, and upload metadata so startup recovery can resume the backend state machine.
