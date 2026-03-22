# Task 04: Backend API Scaffold

## Status

Not started

## Goal

Set up the Rust backend service, HTTP routing, config wiring, and baseline API surface.

## Depends On

- 01 Repo Foundation
- 02 Config And Domain
- 03 Database And Migrations

## Deliverables

- `axum` service entrypoint
- config loading and app state wiring
- health/basic routes
- public catalog route scaffold
- integration/admin route scaffold
- static asset serving for packaged frontend

## Steps

- Create backend app initialization with config, DB pool, logging, and router setup.
- Add public route groups for games, platforms, and assets.
- Add Playnite integration route groups for uploads, jobs, metadata patching, install manifests, and save manifests.
- Add admin route groups for owner-only actions.
- Add consistent JSON error responses with machine-readable error codes.
- Ensure route modules match the planned resource shapes and authentication boundaries.

## Acceptance Criteria

- Backend starts with config and DB connectivity.
- Route layout matches the architecture plan.
- Responses use stable JSON envelopes and error format.
- Static frontend serving is supported in packaged mode.

## Tracking Checklist

- [ ] Backend bootstrap created
- [ ] App state wiring created
- [ ] Public routes scaffolded
- [ ] Integration routes scaffolded
- [ ] Admin routes scaffolded
- [ ] JSON error model implemented

## Blockers

- none

## Notes

- Keep API ids externalized through `public_id`.
