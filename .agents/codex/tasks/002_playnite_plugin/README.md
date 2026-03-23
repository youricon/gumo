# 002 Playnite Plugin Tasks

This directory tracks the Playnite plugin work for Gumo.

## Usage

- Update each task file directly as work progresses.
- Keep status accurate.
- Record scope changes and blockers in the relevant task file.
- Do not mark a task complete until its acceptance criteria are met.

## Task Order

1. [01_repo_and_project_setup.md](/home/isaac/workspace/gumo/.agents/codex/tasks/002_playnite_plugin/01_repo_and_project_setup.md)
2. [02_windows_dev_environment.md](/home/isaac/workspace/gumo/.agents/codex/tasks/002_playnite_plugin/02_windows_dev_environment.md)
3. [03_optional_nix_iteration_tooling.md](/home/isaac/workspace/gumo/.agents/codex/tasks/002_playnite_plugin/03_optional_nix_iteration_tooling.md)
4. [04_plugin_scaffold_and_configuration.md](/home/isaac/workspace/gumo/.agents/codex/tasks/002_playnite_plugin/04_plugin_scaffold_and_configuration.md)
5. [05_api_client_and_auth.md](/home/isaac/workspace/gumo/.agents/codex/tasks/002_playnite_plugin/05_api_client_and_auth.md)
6. [06_library_sync_and_metadata.md](/home/isaac/workspace/gumo/.agents/codex/tasks/002_playnite_plugin/06_library_sync_and_metadata.md)
7. [07_uploads_jobs_and_recovery.md](/home/isaac/workspace/gumo/.agents/codex/tasks/002_playnite_plugin/07_uploads_jobs_and_recovery.md)
8. [08_installation_flow.md](/home/isaac/workspace/gumo/.agents/codex/tasks/002_playnite_plugin/08_installation_flow.md)
9. [09_save_backup_and_restore.md](/home/isaac/workspace/gumo/.agents/codex/tasks/002_playnite_plugin/09_save_backup_and_restore.md)
10. [10_packaging_and_release.md](/home/isaac/workspace/gumo/.agents/codex/tasks/002_playnite_plugin/10_packaging_and_release.md)
11. [11_windows_ci.md](/home/isaac/workspace/gumo/.agents/codex/tasks/002_playnite_plugin/11_windows_ci.md)

## Current Status

| Task | Title | Status | Depends On |
| --- | --- | --- | --- |
| 01 | Repo And Project Setup | Completed | - |
| 02 | Windows Dev Environment | Completed | 01 |
| 03 | Optional Nix Iteration Tooling | Completed | 01 |
| 04 | Plugin Scaffold And Configuration | Completed | 01, 02 |
| 05 | API Client And Auth | Completed | 04 |
| 06 | Library Sync And Metadata | Completed | 05 |
| 07 | Uploads Jobs And Recovery | Completed | 05, 06 |
| 08 | Installation Flow | In progress | 05, 06 |
| 09 | Save Backup And Restore | Not started | 05, 06 |
| 10 | Packaging And Release | Not started | 04, 08, 09 |
| 11 | Windows CI | Not started | 10 |

## Notes

- Architecture source of truth: [docs/architecture.md](/home/isaac/workspace/gumo/docs/architecture.md)
- Plugin development guidance: [docs/playnite-plugin-development.md](/home/isaac/workspace/gumo/docs/playnite-plugin-development.md)
- Windows is the source of truth for plugin development and release builds.
- NixOS-side compilation is best-effort only and should be dropped if it becomes expensive to maintain.
