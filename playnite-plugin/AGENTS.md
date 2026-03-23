# Repository Guidelines

## Scope

These instructions apply only to work under `playnite-plugin/`. Keep plugin-specific guidance here so repository-wide work does not carry unnecessary Playnite context.

## Project Structure

`src/Gumo.Playnite/` contains the plugin source, XAML views, and the `.csproj`. `scripts/` contains Windows helper scripts for install and packaging. `packaging/` is reserved for release assets. Open `Gumo.Playnite.sln` in Visual Studio for normal development.

## Build and Validation

- Build on Windows with Visual Studio or `msbuild`.
- Install the dev build with `playnite-plugin/scripts/install-dev.ps1`.
- Package release artifacts with `playnite-plugin/scripts/package.ps1`.

NixOS-side plugin builds are not a supported workflow. Treat Windows as the source of truth for compile and runtime behavior.

## UI Thread Rules

Be strict about Playnite UI-thread boundaries.

- Keep dialogs, pickers, and confirmation prompts on the UI thread.
- Move HTTP calls, archive packaging, upload/download work, checksum verification, and extraction into `PlayniteApi.Dialogs.ActivateGlobalProgress(...)`.
- Gather all user input before starting background work.
- Be suspicious of any `GetAwaiter().GetResult()` call in menu handlers, settings button handlers, or dialog callbacks.

If a new action freezes Playnite, first check whether network or filesystem work is still happening before the progress dialog starts.

## State and Persistence

Persist plugin-owned state through `GumoLibrarySettings`, including installed-game state and pending upload state. Keep recovery conservative: never blindly re-upload or overwrite local files without explicit confirmation.

## Testing Notes

Manual Windows validation is required for plugin changes. Record what was tested, especially for:
- upload/import flows
- install/uninstall behavior
- save backup/restore
- artwork sync
- menu and settings actions
