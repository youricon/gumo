# Playnite Plugin Development

## Goal

Keep the Playnite plugin in the Gumo monorepo, support best-effort plugin compilation on NixOS for iteration, and treat Windows as the authoritative development and release environment.

## Repo Placement

The plugin should live in:

- `playnite-plugin/`

Recommended top-level repo layout:

- `backend/`
- `web/`
- `playnite-plugin/`
- `nix/`
- `docs/`

Reasoning:

- the plugin and Gumo API will change together early on
- keeping both in one repo reduces cross-repo version skew
- API and plugin changes can be reviewed and tested in the same branch
- the plugin can still stay logically isolated as a separate client of the Gumo API

## Build Policy

Use this policy from the start:

- NixOS plugin builds are best-effort and only meant to help iteration
- Windows builds are the source of truth
- release artifacts should be produced on Windows CI or a Windows VM
- if Linux compilation becomes too fragile, drop the Nix plugin toolchain without affecting the rest of the repo

This avoids wasting time trying to force fully reproducible `.NET Framework` release builds on Linux.

## Development Modes

### 1. NixOS Iteration

This mode is optional.

Use it only if it provides useful compile feedback for the plugin project.

What success looks like:

- the plugin project restores dependencies
- the plugin project compiles far enough to catch common API/type errors
- no promise is made that the resulting artifact is release-ready

What failure looks like:

- Mono/MSBuild or reference assembly issues waste time
- packaging becomes brittle
- Linux-specific workarounds start dominating plugin work

If that happens, remove plugin build tooling from the flake and do plugin work only on Windows.

### 2. Windows Development

This is the primary workflow.

Use it for:

- daily plugin development
- debugging against Playnite itself
- packaging
- release validation

## Suggested Windows Environment

Use a dedicated Windows VM if your primary workstation is NixOS.

Recommended setup:

- Windows 11 VM
- Visual Studio 2022 Community or Build Tools with `.NET desktop development`
- .NET Framework 4.6.2 targeting pack if not already included
- Git
- Playnite installed
- 7-Zip

Optional:

- PowerShell 7
- VS Code or Rider, if you prefer them over full Visual Studio

## Suggested Windows VM Layout

Inside the VM:

1. clone the repo
2. open `playnite-plugin/` in Visual Studio
3. build the plugin
4. install or link the plugin into Playnite's extensions directory
5. run Playnite and test against a locally reachable Gumo instance

Recommended directories:

- repo checkout: `C:\dev\gumo\`
- plugin project: `C:\dev\gumo\playnite-plugin\`
- local plugin build output: under the project `bin\` tree

## Connecting Windows Playnite To Gumo On NixOS

If Gumo runs on your NixOS host and Playnite runs in a Windows VM:

- bind Gumo to an address reachable from the VM
- make sure the VM can reach the host over the VM network
- use an explicit base API URL in the plugin config

Typical approaches:

- bridged networking if you want the VM to behave like another machine on the LAN
- host-only or NAT with host reachability if your VM platform supports it cleanly

Before plugin debugging, verify from Windows:

```powershell
curl http://HOST_IP:8080/api/health
```

Do not start plugin debugging until that works.

## Windows Development Workflow

### Initial Setup

1. Clone the repo in Windows.
2. Open the plugin solution or project in Visual Studio.
3. Restore dependencies.
4. Build once in `Debug`.
5. Confirm the built assembly and manifest/package files land where expected.
6. Install or symlink the plugin into Playnite.

### Daily Iteration

1. Start or verify the Gumo backend is running.
2. Open Playnite.
3. Rebuild the plugin in Visual Studio.
4. Reload Playnite or restart it if needed.
5. Test:
   - authentication
   - listing games from Gumo
   - metadata updates
   - import/upload flow
   - install flow
   - save backup/restore flow

### Debugging

Preferred approach:

- run Playnite normally
- attach Visual Studio to the Playnite process
- use plugin logs aggressively

Debug the flows separately:

- API auth and connectivity
- metadata sync
- upload create/upload/finalize
- job polling and recovery after termination
- install/extract behavior
- save snapshot backup and restore

## Plugin Installation During Development

Pick one of these approaches:

### Option A: Copy on Build

After build, copy the plugin output into the Playnite extensions directory.

Pros:

- simple

Cons:

- slower feedback
- easy to get stale files

### Option B: Symlink/Junction Into Playnite Extensions

Point Playnite's extension loading path at a working tree or build output directory.

Pros:

- faster iteration

Cons:

- setup is slightly more fragile

This is usually the better development mode if the plugin layout allows it.

## Windows Packaging Workflow

Release packaging should happen on Windows.

Recommended flow:

1. Build in `Release`.
2. Collect the plugin DLL and required manifest/package metadata.
3. Produce the Playnite extension package format expected by Playnite.
4. Install that packaged artifact into a clean Playnite environment.
5. Validate the core flows against a real Gumo instance.

Keep packaging logic in-repo, for example:

- `playnite-plugin/scripts/package.ps1`

This makes local Windows builds and CI builds use the same process.

## Windows CI Recommendation

When you add the plugin:

- create a separate Windows workflow
- trigger it on changes under `playnite-plugin/` and relevant shared docs/config
- build the plugin in `Release`
- package the extension artifact
- upload it as a workflow artifact

Keep the Windows CI separate from the main Nix workflow. They solve different problems.

## NixOS Plugin Tooling Recommendation

If you try Linux-side compilation, keep it minimal and disposable.

Reasonable candidate toolchain:

- `mono`
- `msbuild`
- `nuget`
- `zip`

Do not let the repo depend on this succeeding.

Use a separate dev shell or app target, not the main Gumo shell by default, unless the overhead is trivial.

## Decision Rules

Use these rules to avoid wasting time:

- if NixOS compilation works with modest effort, keep it as a convenience
- if NixOS compilation requires ongoing hacks, drop it
- if a bug only appears in Playnite itself, debug on Windows
- if packaging differs between Linux and Windows, trust Windows
- if release confidence depends on Playnite runtime behavior, validate on Windows

## Practical Recommendation For Gumo

Near-term:

- keep the plugin in the monorepo
- add `playnite-plugin/`
- document Windows as the primary workflow
- optionally try a Linux compile shell once
- do not block plugin work on Linux compilation success

Long-term:

- keep Windows CI as the release path
- keep Nix focused on the Rust/web/service side unless the plugin Linux workflow proves genuinely useful
