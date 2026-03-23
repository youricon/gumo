# Gumo Playnite Plugin

This directory contains the Playnite library plugin for Gumo.

## Scope

The plugin is a separate client of the Gumo HTTP API. It must not depend on the Rust backend internals or share runtime code with the web app.

Responsibilities:

- authenticate to Gumo using integration tokens
- import and synchronize Gumo-managed games into Playnite
- upload game payloads and save snapshots
- poll upload/archive jobs
- install archived game versions locally
- restore version-specific save snapshots

## Layout

- `src/Gumo.Playnite/`
  - Playnite plugin project
  - plugin source code
  - `extension.yaml` copied into build output
- `scripts/`
  - Windows-oriented helper scripts for packaging and local developer installation
- `packaging/`
  - packaging notes and future release assets

## Development

Primary development environment:

- Windows
- Visual Studio 2022 or Rider
- Playnite installed locally

Linux/Nix status:

- a minimal Mono/MSBuild attempt was tried
- the current SDK-style WPF project does not build usefully on NixOS
- Windows remains the only supported plugin build environment for now

Windows quickstart:

1. Open `Gumo.Playnite.sln` in Visual Studio.
2. Build `Debug`.
3. Start Gumo and generate an integration token from the admin UI.
4. Copy the build output into `%APPDATA%\Playnite\Extensions\Gumo` or run `scripts\install-dev.ps1`.
5. Start Playnite.
6. Confirm the extension loads and accepts the token in settings.

Release packaging:

```powershell
.\scripts\package.ps1 -Configuration Release
```

This produces a `.pext` artifact under `playnite-plugin\artifacts\`.

Reference:

- [playnite-plugin-development.md](/home/isaac/workspace/gumo/docs/playnite-plugin-development.md)

## Current State

This is an early integration build.

Implemented:

- `LibraryPlugin` project targeting `.NET Framework 4.6.2`
- manifest file
- settings object and settings view
- logging baseline
- token-authenticated Gumo API client wrapper
- typed models for games, versions, uploads, jobs, install manifests, and save manifests
- startup connectivity probe with structured API error logging
- game sync through `GetGames`
- latest-version mapping into Playnite metadata
- basic game-menu action to push edited metadata back to Gumo
- custom import flow that uploads a local payload file into Gumo
- durable upload/job tracking persisted in plugin settings
- startup recovery that resumes pending Gumo uploads after Playnite restarts

Release validation still pending:

- clean install validation from the packaged `.pext` in Playnite
