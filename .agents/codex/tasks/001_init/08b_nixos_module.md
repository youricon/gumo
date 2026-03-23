# Task 08B: NixOS Module

## Status

Completed

## Parent Task

- [08_nixos_and_verification.md](/home/isaac/workspace/gumo/.agents/codex/tasks/001_init/08_nixos_and_verification.md)

## Goal

Implement the `nixosModules.gumo` wrapper around the app-native config and package.

## Depends On

- 01 Repo Foundation
- 08A Nix Packages

## Deliverables

- `nixosModules.gumo`
- module options for package, user/group, data dir, firewall, and app settings
- config rendering from Nix to TOML
- systemd service wiring

## Steps

- Define module options: `enable`, `package`, `user`, `group`, `dataDir`, `openFirewall`, `settings`.
- Render the TOML config from `settings`.
- Derive runtime paths from `dataDir`.
- Create systemd service wiring for the Gumo backend.
- Create required writable directories for DB, assets, storage, and temp data if needed.
- Keep module behavior aligned with the app-native runtime contract.

## Acceptance Criteria

- The module can enable the service with app-native settings.
- Runtime paths are derived from `dataDir`.
- The module does not invent alternate runtime semantics.
- The systemd service can start the packaged app with rendered config.

## Tracking Checklist

- [x] Module option surface
- [x] TOML rendering
- [x] Directory creation
- [x] Systemd service wiring
- [x] Firewall option wiring

## Notes

- The module is a wrapper, not the source of truth for app behavior.
- `nix/module.nix` now derives storage paths from `dataDir`, renders `/etc/gumo/gumo.toml`, creates writable directories through tmpfiles, and wires the systemd unit around the packaged app.
- The module keeps app-native config in `settings` and only wraps deployment concerns.
