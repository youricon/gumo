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
2. Initialize local state with `nix run .#dev-init` or `just dev-init`.
3. Install frontend dependencies with `npm --prefix web install`.
4. Run the backend with `nix run .#backend` or `just backend`.
5. Run the frontend dev server with `nix run .#frontend` or `just frontend`.

The backend and frontend should remain runnable independently in development.

The frontend dev server now reads its development settings from `./.local/gumo/config.toml`.

- `server.listen_address` and `server.port` define the backend origin used for `/api` proxying.
- the same backend origin is also used for `/media` proxying so artwork loads in Vite dev mode
- `frontend.dev_port` defines the Vite dev server port.
- `frontend.dev_listen_address` is optional and falls back to `server.listen_address`.

Quickstart:

```bash
nix develop
just dev-init
npm --prefix web install
just backend
just frontend
```

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

Logging verbosity is also configured there:

```toml
[logging]
level = "debug"
```

Valid practical values are `trace`, `debug`, `info`, `warn`, and `error`. `RUST_LOG` still overrides the config if you set it explicitly.

For local admin auth, `dev-init` now creates `./.local/gumo/secrets/admin-password-hash` automatically if it is missing, using the default development password `admin`.

To replace it with your own password, use either:

- `just admin-password your-password`
- `just admin-password-prompt`

Typical password-management flow:

```bash
just admin-password-prompt
```

or:

```bash
just admin-password my-dev-password
```

Playnite integration tokens are stored in SQLite, not in config files.

Generate them from the admin UI after signing in:

1. Open `http://127.0.0.1:4173/admin` in Vite dev mode, or `http://127.0.0.1:8080/admin` when running the packaged frontend from the backend.
2. Sign in with the local owner password. If you did not override it, the default dev password is `admin`.
3. Use the `API tokens` panel to create a token for Playnite.
4. Copy the plaintext token immediately and paste it into the Playnite plugin settings.

The plaintext token is only shown once. If you lose it, disable that token and create a new one.

## Cache Directory

`storage.cache_dir` is reserved for local cached/generated runtime files such as downloaded artwork, thumbnails, image derivatives, and future metadata cache files.

At the moment it is mostly a forward-looking runtime path rather than a heavily used feature path, but it is still useful to keep it explicit so packaged deployments do not rely on writing into the install directory.
