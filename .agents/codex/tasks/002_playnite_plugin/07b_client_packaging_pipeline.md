# Task 07b: Client Packaging Pipeline

## Status

Not started

## Goal

Teach the Playnite plugin to normalize supported input types into one archive-set upload model.

## Depends On

- 07a Import Session API

## Deliverables

- source-type detection for file, multipart file set, and folder inputs
- client-side packaging for non-archive inputs
- archive-set manifest generation
- cleanup of temporary packaging artifacts

## Steps

- Detect whether the selected input is:
  - a single archive file
  - a multipart archive set
  - a single non-archive file
  - a folder
- Pass archive files through unchanged when appropriate.
- Wrap non-archive files into archive parts on the client.
- Package folder inputs into archive parts using deterministic grouping.
- Delete temporary artifacts after successful upload or cancellation.

## Acceptance Criteria

- The plugin can produce one archive set from any supported source mode.
- Temporary packaging output does not linger after successful upload.
- The resulting archive-part order is deterministic.

## Tracking Checklist

- [ ] Source-mode detection added
- [ ] Single-file packaging added
- [ ] Multipart archive passthrough added
- [ ] Folder packaging added
- [ ] Temporary artifact cleanup added

## Notes

- Client packaging should become the norm; server-side re-archiving is only a bridge implementation.
