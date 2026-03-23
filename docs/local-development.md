# Local Development

Gumo development should run directly from `nix develop` against repo-local state.

## Local State Layout

Use `./.local/gumo/` for development-only writable state:

- `./.local/gumo/config.toml`
- `./.local/gumo/data/`
- `./.local/gumo/cache/`
- `./.local/gumo/library/`
- `./.local/gumo/secrets/`
- `./.local/gumo/tmp/`

These paths are local-only and must not be assumed by packaged deployments.

`./.local/gumo/library/` is typically used as the `root_path` of the default development library.

## Development Workflow

1. Enter the shell with `nix develop`.
2. Initialize local state with `nix run .#dev-init`.
3. Install frontend dependencies with `npm --prefix web install`.
4. Run the backend with `nix run .#backend`.
5. Run the frontend dev server with `nix run .#frontend`.

The backend and frontend should remain runnable independently in development.

## Playnite Plugin

The Playnite plugin should live in the same monorepo under `playnite-plugin/`.

Plugin workflow policy:

- NixOS-side plugin compilation is optional and best-effort only
- Windows is the primary plugin development environment
- Windows CI or a Windows VM should be used for release builds

Detailed guidance is in [playnite-plugin-development.md](/home/isaac/workspace/gumo/docs/playnite-plugin-development.md).

## Configuration

The example development config lives at `config/gumo.example.toml`.

Copy it into `./.local/gumo/config.toml` before running the backend. The dev init app performs that copy automatically if the config file is absent.

## Cache Directory

`storage.cache_dir` is reserved for local cached/generated runtime files such as downloaded artwork, thumbnails, image derivatives, and future metadata cache files.

At the moment it is mostly a forward-looking runtime path rather than a heavily used feature path, but it is still useful to keep it explicit so packaged deployments do not rely on writing into the install directory.
