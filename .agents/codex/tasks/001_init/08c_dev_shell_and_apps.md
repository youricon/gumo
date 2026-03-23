# Task 08C: Dev Shell And Apps

## Status

Completed

## Parent Task

- [08_nixos_and_verification.md](/home/isaac/workspace/gumo/.agents/codex/tasks/001_init/08_nixos_and_verification.md)

## Goal

Provide the Nix development workflow for running backend and frontend directly with local mock storage.

## Depends On

- 01 Repo Foundation
- 02 Config And Domain
- 04 Backend API Scaffold

## Deliverables

- development shell
- flake apps or run targets
- local mock storage conventions
- development-friendly config path

## Steps

- Define the default dev shell toolchain.
- Expose flake apps for backend and frontend development runs.
- Ensure the dev workflow points to `./.local/gumo/` storage paths.
- Verify the backend can run without NixOS-specific assumptions.
- Verify the frontend can run in dev mode against the backend API.

## Acceptance Criteria

- `nix develop` provides the expected tools.
- Backend and frontend can be run directly in development.
- Local mock storage is sufficient for development workflows.

## Tracking Checklist

- [x] Dev shell defined
- [x] Backend dev app defined
- [x] Frontend dev app defined
- [x] Local storage workflow verified

## Notes

- Keep development workflow fast and independent of the NixOS module.
- Verified with `nix run .#dev-init` and `nix develop --command ...` checks for shell env/tooling.
- `nix/apps.nix` and `nix/devshell.nix` keep the development path pointed at `./.local/gumo/`.
