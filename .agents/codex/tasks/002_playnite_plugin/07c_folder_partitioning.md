# Task 07c: Folder Partitioning

## Status

Not started

## Goal

Support very large folder uploads by partitioning source files into archive groups without creating one giant temporary archive.

## Depends On

- 07b Client Packaging Pipeline

## Deliverables

- configurable target part size
- stable directory walk and grouping algorithm
- archive-part creation loop
- handling for single files larger than the target part size

## Steps

- Walk selected directories in stable sorted order.
- Accumulate files into a group until the estimated size reaches the target threshold.
- Start a new group when needed.
- Archive each group into one part sequentially.
- Allow an oversized single file to exceed the target instead of splitting it.

## Acceptance Criteria

- Folder uploads do not require one monolithic temporary archive.
- File grouping is deterministic across runs.
- Large single files are handled without undefined behavior.

## Tracking Checklist

- [ ] Target part size defined
- [ ] Stable file walk added
- [ ] Grouping logic added
- [ ] Sequential archive generation added
- [ ] Oversized-file rule implemented

## Notes

- Do not split individual files in the first implementation.
- Keep the grouping rules simple and debuggable.
