# Task 08a: Install From Archive Set

## Status

In progress

## Goal

Install Gumo game versions from the uploaded archive set directly instead of assuming a server-generated replacement archive.

## Depends On

- 07a Import Session API
- 08 Installation Flow

## Deliverables

- install manifest with ordered archive parts
- plugin download pipeline for archive sets
- extraction flow for archive-set installs

## Steps

- Update the install manifest contract to return the stored archive parts in order.
- Download all required parts for the selected version.
- Reconstruct or extract the archive set according to its format.
- Preserve checksum verification for each part or the whole set as appropriate.

## Acceptance Criteria

- Installation works from the same archive set that was uploaded.
- The install flow does not depend on backend re-archiving.
- Archive-part ordering is explicit and reliable.

## Tracking Checklist

- [x] Install manifest updated
- [x] Archive-part download added
- [x] Archive-set extraction added
- [x] Checksum verification updated

## Notes

- Keep the plugin-side install logic format-aware, but start with the formats Gumo actually supports.
- Current implementation status:
  - backend manifests now expose stored artifact parts
  - the plugin downloads, verifies, and extracts all parts in order
  - Windows runtime validation is still required
