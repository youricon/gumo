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

Current result for Gumo:

- attempted Linux-side build with `mono`, `msbuild`, and `nuget`
- build failed immediately on the SDK-style project system
- `Microsoft.NET.Sdk.WindowsDesktop` could not be resolved in this workflow
- no useful partial compile feedback was gained

Decision:

- do not add Playnite plugin build tooling to the main Nix flake right now
- keep plugin work Windows-first
- revisit Linux-side compilation only if there is a strong reason later

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

## Windows Quickstart

Use this when setting up a fresh Windows VM.

1. Install Visual Studio 2022 Community.
2. During installation, enable:
   - `.NET desktop development`
   - `Desktop development with C++` is not required
3. Confirm the `.NET Framework 4.6.2` targeting pack is available.
4. Install Git.
5. Install Playnite.
6. Install 7-Zip.
7. Clone the repo to `C:\dev\gumo\`.
8. Open `playnite-plugin\src\Gumo.Playnite\Gumo.Playnite.csproj` in Visual Studio.
9. Restore and build in `Debug`.
10. Start Gumo, sign in to `/admin`, and generate a Playnite integration token from the `API tokens` panel.
11. Copy the build output into the Playnite extensions directory or use the install helper script.

If any of those steps fail, fix the Windows toolchain first. Do not debug plugin logic until the project builds and Playnite sees the extension files.

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
- Playnite extension target: `%APPDATA%\Playnite\Extensions\Gumo`

Suggested VM snapshots:

- clean Windows base
- Windows with toolchain installed
- Windows with Playnite installed
- Windows with repo cloned and plugin building

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

Also verify:

```powershell
Test-NetConnection HOST_IP -Port 8080
```

If that fails:

- check the Gumo bind address
- check VM networking mode
- check host firewall
- check that the backend is actually listening on the host-reachable interface

## Windows Development Workflow

### Initial Setup

1. Clone the repo in Windows.
2. Open the plugin project in Visual Studio.
3. Set the build configuration to `Debug`.
4. Restore dependencies.
5. Build once.
6. Confirm the built assembly and manifest/package files land where expected.
7. Install or copy the plugin into Playnite.
8. Start Playnite and confirm the plugin is visible.
9. Paste the generated API token into the plugin settings.

Expected output area:

- `playnite-plugin\src\Gumo.Playnite\bin\Debug\`

At minimum, that directory should contain:

- `Gumo.Playnite.dll`
- `extension.yaml`

Before testing auth-dependent flows:

1. Start the Gumo backend and frontend.
2. Open `/admin`.
3. Sign in with the owner password. In local development, `dev-init` creates the default password `admin` unless you replace it with `just admin-password ...`.
4. Generate a new integration token from the `API tokens` panel.
5. Paste that token into the Playnite plugin settings.

Current identity/linkage rule:

- imported Gumo games use the stable Gumo game ID as Playnite `GameId`
- version display currently maps the preferred Gumo version into Playnite's `Version` field
- metadata edits can be pushed back to Gumo through the game menu for Gumo-managed titles
- current implementation uploads a local payload file into Gumo as a new game/version pair
- pending uploads persist `upload_id` and `job_id` locally so the plugin can resume job tracking after restart
- startup recovery is non-destructive: it resumes job polling/finalization when possible, but it does not automatically re-upload file content from disk
- target architecture is broader than the current implementation:
  - the plugin should normalize folder, file, and multipart-archive selections into one archive-set model
  - the plugin should package non-archive inputs on the client before upload
  - the backend should store uploaded archive parts directly instead of re-archiving them

### Daily Iteration

1. Start or verify the Gumo backend is running.
2. Open Playnite.
3. Rebuild the plugin in Visual Studio.
4. Copy/install the updated build into the Playnite extensions directory if your workflow requires it.
5. Reload Playnite or restart it if needed.
6. Watch Playnite logs and your plugin logs.
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

Typical attach flow:

1. Start Playnite.
2. In Visual Studio, use `Debug -> Attach to Process`.
3. Select the Playnite process.
4. Set breakpoints in the plugin project.
5. Trigger the plugin action from Playnite.

If the plugin fails before attach is useful, inspect the Playnite logs first.

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

Typical extension path:

```powershell
$env:APPDATA\Playnite\Extensions\Gumo
```

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

For the current scaffold, the simplest path is still copy-based installation until the extension layout is stable.

## Local Windows Commands

From `C:\dev\gumo\playnite-plugin`:

Build with MSBuild:

```powershell
msbuild .\src\Gumo.Playnite\Gumo.Playnite.csproj /t:Build /p:Configuration=Debug
```

Install development build into Playnite:

```powershell
.\scripts\install-dev.ps1 -Configuration Debug
```

Run the packaging script:

```powershell
.\scripts\package.ps1 -Configuration Release
```

The packaging script now:

- builds the plugin in the requested configuration
- stages the manifest and required assemblies from `bin\<Configuration>\`
- uses Playnite's `Toolbox.exe` to create a versioned `.pext` archive in `playnite-plugin\artifacts\`

`install-dev.ps1` remains the fast local iteration path.

## Windows Packaging Workflow

Release packaging should happen on Windows.

Recommended flow:

1. Build in `Release`.
2. Run `.\scripts\package.ps1 -Configuration Release`.
3. Collect the generated `.pext` artifact from `playnite-plugin\artifacts\`.
4. Install that packaged artifact into a clean Playnite environment.
5. Validate the core flows against a real Gumo instance.

Example:

```powershell
cd C:\dev\gumo\playnite-plugin
.\scripts\package.ps1 -Configuration Release
```

If `Toolbox.exe` is not discovered automatically, pass it explicitly:

```powershell
.\scripts\package.ps1 -Configuration Release -ToolboxPath "$env:LOCALAPPDATA\Programs\Playnite\Toolbox.exe"
```

Use `-Configuration`, not `--Configuration`, when invoking the PowerShell script.

Release validation checklist:

- plugin loads in a clean Playnite install
- Gumo server URL can be configured
- token auth succeeds
- games can be fetched
- install manifest flow works
- save snapshot flow works
- no local debug-only assumptions remain

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
- for the current scaffold, treat Linux-side compilation as unsupported

Long-term:

- keep Windows CI as the release path
- keep Nix focused on the Rust/web/service side unless the plugin Linux workflow proves genuinely useful
