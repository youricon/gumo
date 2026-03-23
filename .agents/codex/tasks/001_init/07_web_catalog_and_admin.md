# Task 07: Web Catalog And Admin

## Status

Completed

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

- [x] Public catalog scaffolded
- [x] Game detail pages implemented
- [x] Admin auth flow implemented
- [x] Upload/job admin views implemented
- [x] Metadata review/edit views implemented
- [x] Packaged serving verified

## Blockers

- none

## Notes

- Keep the public surface small in v1. Prioritize correctness over breadth.
- Implemented real public/admin API handlers in `backend/src/api/routes/public.rs` and `backend/src/api/routes/admin.rs`.
- Added owner session handling in `backend/src/api/auth.rs` using local password verification and cookie-backed in-memory sessions.
- Replaced the placeholder frontend with a real catalog/admin app in `web/src/App.tsx` and `web/src/styles.css`.
- Verified with `nix develop --command cargo test --manifest-path backend/Cargo.toml`.
- Verified with `nix develop --command bash -lc 'cd web && npm install && npm run build'`.
- Verified with `nix build .#gumo-web` and `nix build .#gumo`.
