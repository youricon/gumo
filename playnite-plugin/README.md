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

## Save Backup Configuration

Gumo stores save-backup configuration per game version on the backend.

From the Playnite game menu, `Configure save backup` lets the user set:

- a save path
- whether that path is `relative` to the install directory or `absolute`
- an optional file-matching pattern

For relative paths, the install directory itself is stored as `.` and child folders are stored relative to that root.

If the save path points at the game root, a matching pattern is strongly recommended, and is required during local-game upload so the plugin does not exclude the whole install from the game payload.

### Pattern Format

The matching pattern uses regular expressions.

- if the regex contains no `/`, it matches filenames only
- if the regex contains `/`, it matches the relative path under the configured save folder
- matching is case-insensitive

When a save folder is excluded from a local game upload, the regex is evaluated inside that save folder. The folder path is not prepended to the regex.

Examples:

- `^.*\.sav$`
- `^profile.\.json$`
- `^SaveData/.*\.dat$`
- `^.*/slot.*\.bin$`

Standard .NET regex syntax is supported. Invalid regex patterns are rejected when configuring the save backup.

### Local Upload Behavior

When uploading a local Playnite game to Gumo, the plugin now asks how to handle saves before packaging the game payload:

- `Configure save folder`
  - records the save config on the created Gumo version
  - excludes those save files from the game archive upload
  - uploads matching save files as a separate initial save snapshot after the game upload completes
- `Skip save upload`
  - uploads only the game payload
- `Cancel upload`
  - aborts the local upload flow

If the configured save folder currently has no matching files, the plugin still saves the configuration on the backend and skips the initial save snapshot upload.

## Layout

- `Gumo/`
  - Toolbox-generated Playnite plugin project
  - current Windows build and packaging target
  - `BuildInclude.txt` allowlist for Toolbox packaging
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

1. Open `Gumo\Gumo.sln` in Visual Studio.
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
The script uses Playnite's `Toolbox.exe` to generate the package. If Toolbox is not on `PATH`, pass `-ToolboxPath` explicitly.

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
- backend-backed save backup configuration per version
- save snapshot backup and restore using configured relative or absolute save paths
- optional regex-based save file filtering for backup, restore, and local upload exclusion
- local game upload flow that can split save files into a separate save snapshot upload
- durable upload/job tracking persisted in plugin settings
- startup recovery that resumes pending Gumo uploads after Playnite restarts

Release validation still pending:

- clean install validation from the packaged `.pext` in Playnite
