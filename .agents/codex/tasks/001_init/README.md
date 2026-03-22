# 001 Init Tasks

This directory tracks the initial implementation plan for Gumo.

## Usage

- Update each task file directly as work progresses.
- Keep status accurate.
- Record scope changes and blockers in the relevant task file.
- Do not mark a task complete until its acceptance criteria are met.

## Task Order

1. [01_repo_foundation.md](/home/isaac/workspace/gumo/.agents/codex/tasks/001_init/01_repo_foundation.md)
2. [02_config_and_domain.md](/home/isaac/workspace/gumo/.agents/codex/tasks/001_init/02_config_and_domain.md)
3. [03_database_and_migrations.md](/home/isaac/workspace/gumo/.agents/codex/tasks/001_init/03_database_and_migrations.md)
4. [04_backend_api_scaffold.md](/home/isaac/workspace/gumo/.agents/codex/tasks/001_init/04_backend_api_scaffold.md)
5. [05_uploads_and_jobs.md](/home/isaac/workspace/gumo/.agents/codex/tasks/001_init/05_uploads_and_jobs.md)
6. [06_playnite_integration_contract.md](/home/isaac/workspace/gumo/.agents/codex/tasks/001_init/06_playnite_integration_contract.md)
7. [07_web_catalog_and_admin.md](/home/isaac/workspace/gumo/.agents/codex/tasks/001_init/07_web_catalog_and_admin.md)
8. [08_nixos_and_verification.md](/home/isaac/workspace/gumo/.agents/codex/tasks/001_init/08_nixos_and_verification.md)

## Current Status

| Task | Title | Status | Depends On |
| --- | --- | --- | --- |
| 01 | Repo Foundation | Completed | - |
| 02 | Config And Domain | Not started | 01 |
| 03 | Database And Migrations | Not started | 01, 02 |
| 04 | Backend API Scaffold | Not started | 01, 02, 03 |
| 05 | Uploads And Jobs | Not started | 03, 04 |
| 06 | Playnite Integration Contract | Not started | 02, 04, 05 |
| 07 | Web Catalog And Admin | Not started | 04, 05 |
| 08 | NixOS And Verification | Not started | 01, 03, 04, 05, 07 |

## Notes

- Architecture source of truth: [docs/architecture.md](/home/isaac/workspace/gumo/docs/architecture.md)
- Primary deployment target: NixOS module
- Secondary deployment target: OCI image built by Nix
- Current implementation scope: managed libraries only, no external scanning
