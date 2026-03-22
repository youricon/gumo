# Task 08D: NixOS VM Tests

## Status

Not started

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

- [ ] Startup VM test
- [ ] Config rendering test
- [ ] Data path test
- [ ] DB creation test
- [ ] Managed storage path test

## Notes

- VM tests should be lightweight but cover the critical deployment path.
