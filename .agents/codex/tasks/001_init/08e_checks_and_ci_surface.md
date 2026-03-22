# Task 08E: Checks And CI Surface

## Status

Not started

## Parent Task

- [08_nixos_and_verification.md](/home/isaac/workspace/gumo/.agents/codex/tasks/001_init/08_nixos_and_verification.md)

## Goal

Expose the verification surface through flake checks so build, test, and VM validation are easy to run.

## Depends On

- 08A Nix Packages
- 08C Dev Shell And Apps
- 08D VM Tests

## Deliverables

- flake checks for package builds
- flake checks for backend/frontend tests
- flake checks for NixOS VM tests
- documented check surface

## Steps

- Add package build checks to the flake.
- Add backend test and frontend test checks where practical.
- Add NixOS VM test checks.
- Expose the intended `nix flake check` surface cleanly.
- Keep the check structure compatible with later CI integration.

## Acceptance Criteria

- `nix flake check` exercises the important build and runtime validation path.
- Build, test, and VM checks are grouped coherently.
- The verification surface is suitable for local development and future CI.

## Tracking Checklist

- [ ] Package build checks
- [ ] Backend test checks
- [ ] Frontend test checks
- [ ] VM test checks
- [ ] Flake check surface reviewed

## Notes

- Keep checks practical; do not bloat the default verification path with low-value work.
