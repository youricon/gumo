# Task 08c: Directory Upload UX

## Status

Not started

## Goal

Expose folder uploads cleanly in the Playnite plugin once the archive-set pipeline exists.

## Depends On

- 07b Client Packaging Pipeline
- 07c Folder Partitioning

## Deliverables

- folder selection UX
- source-type-aware import prompts
- progress reporting for packaging and upload phases

## Steps

- Add folder selection in the upload flow.
- Make the upload UI clear about whether the source is a file, archive set, or folder.
- Show separate progress for packaging and network upload when both occur.
- Keep cancellation behavior explicit during long packaging operations.

## Acceptance Criteria

- Users can intentionally select a directory for upload.
- Packaging and upload progress are distinguishable.
- Folder uploads do not feel like a broken file-only workflow.

## Tracking Checklist

- [ ] Folder picker added
- [ ] Source-type-aware prompts added
- [ ] Packaging progress added
- [ ] Upload progress integrated

## Notes

- This should land only after the packaging pipeline exists.
