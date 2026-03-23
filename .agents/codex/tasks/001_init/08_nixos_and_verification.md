# Task 08: NixOS And Verification

## Status

Completed

## Goal

Package the application through Nix, provide the NixOS module wrapper, and add verification coverage.

## Depends On

- 01 Repo Foundation
- 03 Database And Migrations
- 04 Backend API Scaffold
- 05 Uploads And Jobs
- 07 Web Catalog And Admin

## Deliverables

- buildable backend and frontend package outputs
- combined app package
- `nixosModules.gumo`
- NixOS VM tests
- core checks for formatting, linting, and tests
- detailed subtask breakdown for execution tracking

## Subtasks

- [08a_nix_packages.md](/home/isaac/workspace/gumo/.agents/codex/tasks/001_init/08a_nix_packages.md)
- [08b_nixos_module.md](/home/isaac/workspace/gumo/.agents/codex/tasks/001_init/08b_nixos_module.md)
- [08c_dev_shell_and_apps.md](/home/isaac/workspace/gumo/.agents/codex/tasks/001_init/08c_dev_shell_and_apps.md)
- [08d_vm_tests.md](/home/isaac/workspace/gumo/.agents/codex/tasks/001_init/08d_vm_tests.md)
- [08e_checks_and_ci_surface.md](/home/isaac/workspace/gumo/.agents/codex/tasks/001_init/08e_checks_and_ci_surface.md)

## Steps

- Package backend, frontend, and combined app outputs.
- Implement the NixOS module wrapper and config rendering.
- Ensure local dev shell and run apps are usable.
- Add NixOS VM tests for the deployment path.
- Expose build and verification checks through the flake.

## Acceptance Criteria

- `nix build` can produce the app package.
- `nixosModules.gumo` can enable and run the service.
- VM tests validate module behavior without real deployment.
- Flake checks cover the critical build and runtime wiring.

## Tracking Checklist

- [x] Backend package defined
- [x] Frontend package defined
- [x] Combined package defined
- [x] NixOS module implemented
- [x] Config rendering implemented
- [x] VM tests implemented
- [x] Flake checks implemented
- [x] OCI path kept viable

## Blockers

- none

## Notes

- The NixOS module must stay a wrapper, not a separate runtime definition.
- Completed through `nix/packages.nix`, `nix/module.nix`, `nix/vm-test.nix`, `nix/checks.nix`, `nix/apps.nix`, and `nix/devshell.nix`.
- Verified with `nix run .#dev-init`, `nix develop --command ...`, `nix build .#checks.x86_64-linux.vm-module`, and `nix flake check`.
