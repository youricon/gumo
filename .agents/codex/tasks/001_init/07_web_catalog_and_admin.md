# Task 07: Web Catalog And Admin

## Status

Not started

## Goal

Build the initial web experience for the public catalog and basic owner workflows.

## Depends On

- 04 Backend API Scaffold
- 05 Uploads And Jobs

## Deliverables

- public catalog UI
- game detail pages
- basic admin/auth flow
- upload/job visibility for owner workflows
- minimal save snapshot visibility where appropriate

## Steps

- Scaffold the React/Vite frontend according to the monorepo plan.
- Implement public listing and detail pages against the public API.
- Implement admin login flow using session auth.
- Add owner-facing pages for monitoring uploads and jobs.
- Add owner-facing pages for game/version metadata review and edits.
- Add basic visibility into save snapshots and version-specific state.
- Ensure the frontend works both in Vite dev mode and backend-served packaged mode.

## Acceptance Criteria

- Public catalog is browsable and backed by the real API.
- Admin login works with the chosen auth model.
- Owner can inspect uploads/jobs and core game metadata.
- Packaged frontend can be served by the backend.

## Tracking Checklist

- [ ] Public catalog scaffolded
- [ ] Game detail pages implemented
- [ ] Admin auth flow implemented
- [ ] Upload/job admin views implemented
- [ ] Metadata review/edit views implemented
- [ ] Packaged serving verified

## Blockers

- none

## Notes

- Keep the public surface small in v1. Prioritize correctness over breadth.
