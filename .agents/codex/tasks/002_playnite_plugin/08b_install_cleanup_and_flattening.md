# Task 08b: Install Cleanup And Flattening

## Status

Not started

## Goal

Finish the local install UX by cleaning up temporary artifacts and flattening redundant archive folder structures reliably.

## Depends On

- 08a Install From Archive Set

## Deliverables

- cleanup of temporary downloaded archives
- cleanup of nested temporary extraction directories
- flattening of redundant single-root directory layers
- post-install executable selection rules

## Steps

- Remove temporary downloaded archive files after successful install.
- Remove temporary extraction directories after content is moved into place.
- Flatten redundant top-level directory nesting when safe.
- Keep executable selection predictable after flattening.

## Acceptance Criteria

- Successful installs do not leave avoidable temporary junk behind.
- Redundant one-folder nesting does not remain in the final install layout.
- Executable selection still works after cleanup and flattening.

## Tracking Checklist

- [ ] Temporary archive cleanup added
- [ ] Temporary extraction cleanup added
- [ ] Redundant nesting flattening completed
- [ ] Executable selection revalidated

## Notes

- This task is about local install ergonomics, not upload protocol changes.
