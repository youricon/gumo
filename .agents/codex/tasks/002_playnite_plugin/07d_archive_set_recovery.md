# Task 07d: Archive Set Recovery

## Status

Not started

## Goal

Extend recovery logic from single uploaded files to multi-part archive-set imports.

## Depends On

- 07a Import Session API
- 07b Client Packaging Pipeline

## Deliverables

- persisted import-session state
- persisted uploaded-part progress
- restart recovery for partially uploaded archive sets
- cleanup rules for abandoned local temporary packaging output

## Steps

- Persist import-session identifiers and expected part metadata locally.
- Record which archive parts were already uploaded successfully.
- Resume polling/finalization after restart when the server state allows it.
- Avoid blindly regenerating or re-uploading all local parts on startup.
- Clean up stale local temporary packaging output conservatively.

## Acceptance Criteria

- Restart recovery works for archive-set imports, not only one-file uploads.
- Recovery decisions are based on server session state and local part state.
- The plugin does not unexpectedly duplicate uploads after restart.

## Tracking Checklist

- [ ] Session-state persistence expanded
- [ ] Uploaded-part tracking added
- [ ] Restart recovery updated
- [ ] Duplicate-upload protection added
- [ ] Temporary cleanup rules added

## Notes

- Keep recovery non-destructive by default.
