# Task 08A: Nix Packages

## Status

Completed

## Parent Task

- [08_nixos_and_verification.md](/home/isaac/workspace/gumo/.agents/codex/tasks/001_init/08_nixos_and_verification.md)

## Goal

Define the flake package outputs for backend, frontend, and combined app artifacts.

## Depends On

- 01 Repo Foundation
- 04 Backend API Scaffold
- 07 Web Catalog And Admin

## Deliverables

- backend package output
- frontend package output
- combined package output
- package wiring for packaged frontend assets served by the backend

## Steps

- Package the backend binary through Nix.
- Package the frontend static build through Nix.
- Create a combined package that includes backend plus built frontend assets.
- Ensure package outputs are addressable from the flake.
- Verify package boundaries are compatible with future OCI packaging.

## Acceptance Criteria

- `nix build` can build backend and frontend package outputs.
- The combined package includes the backend and the packaged frontend assets.
- Package outputs do not assume writable install directories.

## Tracking Checklist

- [x] Backend package
- [x] Frontend package
- [x] Combined package
- [x] Flake package outputs exposed

## Notes

- Keep the package layout compatible with backend-served frontend assets.
- `nix/packages.nix` now builds the frontend with `buildNpmPackage`, installs `dist` into `share/gumo/web`, and combines it with the backend package.
- Verified with `nix build .#gumo-backend`, `nix build .#gumo-web`, and `nix build .#gumo`.
