# Task 01: Repo Foundation

## Status

Not started

## Goal

Create the initial repo structure and Nix-first development foundation for the monorepo.

## Depends On

- none

## Deliverables

- `flake.nix`
- `nix/` structure for packages, dev shell, checks, and module
- `backend/` Rust project scaffold
- `web/` frontend project scaffold
- shared local development conventions under `./.local/gumo/`

## Steps

- Create the top-level monorepo layout from the architecture document.
- Define flake outputs for `devShells`, `packages`, `checks`, `apps`, and `nixosModules`.
- Add a default dev shell containing Rust, Node, `sqlx-cli`, and supporting tools.
- Add app entrypoints for running backend and frontend directly in development.
- Define local mock storage conventions for data, assets, storage, and secrets.
- Ensure the repo can enter the dev shell without depending on production paths.

## Acceptance Criteria

- `nix develop` provides the expected toolchain.
- Repo layout matches the intended monorepo structure.
- There is a documented local development path using `./.local/gumo/`.
- Backend and frontend entrypoints are represented in flake outputs.

## Tracking Checklist

- [ ] Repo layout created
- [ ] Flake outputs defined
- [ ] Dev shell defined
- [ ] Local dev path documented
- [ ] Apps/run targets defined

## Blockers

- none

## Notes

- Keep the NixOS module a wrapper around app-native config.
- Do not couple local development to NixOS-specific paths.
