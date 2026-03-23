# Task 08D: NixOS VM Tests

## Status

Completed

## Parent Task

- [08_nixos_and_verification.md](/home/isaac/workspace/gumo/.agents/codex/tasks/001_init/08_nixos_and_verification.md)

## Goal

Test the NixOS module and packaged runtime using local VM tests rather than real deployment.

## Depends On

- 08A Nix Packages
- 08B NixOS Module

## Deliverables

- VM test for service startup
- VM test for config rendering and data paths
- VM test for DB creation
- VM test for managed storage path wiring

## Steps

- Add a minimal VM test that enables the module and boots the service.
- Assert the service starts successfully.
- Assert the rendered config exists and contains the expected values.
- Assert writable directories are created with the expected paths.
- Assert the SQLite DB file is created.
- Add a fixture managed storage path and verify the service wiring accepts it.

## Acceptance Criteria

- Module changes can be validated locally without real deployment.
- VM tests cover service startup and path/config wiring.
- Failing module changes are caught by automated checks.

## Tracking Checklist

- [x] Startup VM test
- [x] Config rendering test
- [x] Data path test
- [x] DB creation test
- [x] Managed storage path test

## Notes

- VM tests should be lightweight but cover the critical deployment path.
- `nix/vm-test.nix` boots a NixOS VM with `services.gumo.enable = true`, waits for the service and port, checks rendered config contents, writable directories, and SQLite creation, then hits `/api/health`.
- Verified with `nix build .#checks.x86_64-linux.vm-module`.
