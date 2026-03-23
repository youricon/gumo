# Repository Guidelines

## Project Structure & Module Organization

`backend/` contains the Rust service, SQLite migrations in `backend/migrations/`, and tests alongside the source in `backend/src/`. `web/` is the React + Vite frontend; app code lives in `web/src/`. `playnite-plugin/` contains the Windows-first Playnite plugin, including the C# project, packaging helpers, and install scripts. Nix definitions live in `nix/`, example config in `config/`, and longer design notes in `docs/`. Task tracking is kept under `.agents/codex/tasks/`.

## Build, Test, and Development Commands

- `nix develop` opens the full dev shell.
- `just dev-init` creates `./.local/gumo/` and a usable local config.
- `just backend` runs the Rust server with the local config.
- `just frontend` runs the Vite dev server.
- `nix flake check` runs the main Nix checks, including packaging and VM coverage.
- `nix develop --command cargo test --manifest-path backend/Cargo.toml` runs backend tests.
- `nix develop --command bash -lc 'cd web && npm run build'` verifies the frontend build.

## Coding Style & Naming Conventions

Use 4-space indentation in Rust, TypeScript, XAML, and C#. Prefer explicit, descriptive names over abbreviations. Keep Rust modules small and SQL-oriented; use `snake_case` for Rust/JSON fields and `PascalCase` for C# types and properties. Follow existing patterns before introducing new abstractions. Use `apply_patch` for manual edits and keep comments brief and high-signal.

## Testing Guidelines

Backend tests use Rust’s built-in test framework with `cargo test`. Add tests for new database flows, upload/job transitions, and API edge cases when behavior changes. Frontend changes should at minimum pass `npm run build`. Subtree-specific validation rules, including Playnite plugin guidance, live in nested `AGENTS.md` files.

## Commit & Pull Request Guidelines

Recent history favors short, imperative commit subjects such as `Finish 09`, `Fix image upload`, and `Cleanup db migration chain`. Keep commits focused and descriptive. Pull requests should summarize the user-visible change, note verification steps, and include screenshots for UI changes when relevant. Link the relevant task file when the change maps to `.agents/codex/tasks/`.

## Security & Configuration Tips

Do not commit secrets from `./.local/gumo/`. Generate admin passwords and integration tokens locally. Treat `config/gumo.example.toml` as the baseline for new environments, and prefer storing runtime-generated media and temporary files under `storage.cache_dir` and `storage.temp_dir`.
