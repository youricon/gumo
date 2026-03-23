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
9. [07a_import_session_api.md](/home/isaac/workspace/gumo/.agents/codex/tasks/002_playnite_plugin/07a_import_session_api.md)
10. [07b_client_packaging_pipeline.md](/home/isaac/workspace/gumo/.agents/codex/tasks/002_playnite_plugin/07b_client_packaging_pipeline.md)
11. [07c_folder_partitioning.md](/home/isaac/workspace/gumo/.agents/codex/tasks/002_playnite_plugin/07c_folder_partitioning.md)
12. [07d_archive_set_recovery.md](/home/isaac/workspace/gumo/.agents/codex/tasks/002_playnite_plugin/07d_archive_set_recovery.md)
13. [08a_install_from_archive_set.md](/home/isaac/workspace/gumo/.agents/codex/tasks/002_playnite_plugin/08a_install_from_archive_set.md)
14. [08b_install_cleanup_and_flattening.md](/home/isaac/workspace/gumo/.agents/codex/tasks/002_playnite_plugin/08b_install_cleanup_and_flattening.md)
15. [08c_directory_upload_ux.md](/home/isaac/workspace/gumo/.agents/codex/tasks/002_playnite_plugin/08c_directory_upload_ux.md)
16. [09_save_backup_and_restore.md](/home/isaac/workspace/gumo/.agents/codex/tasks/002_playnite_plugin/09_save_backup_and_restore.md)
17. [10_packaging_and_release.md](/home/isaac/workspace/gumo/.agents/codex/tasks/002_playnite_plugin/10_packaging_and_release.md)
18. [11_windows_ci.md](/home/isaac/workspace/gumo/.agents/codex/tasks/002_playnite_plugin/11_windows_ci.md)

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
| 07a | Import Session API | In progress | 07 |
| 07b | Client Packaging Pipeline | In progress | 07a |
| 07c | Folder Partitioning | In progress | 07b |
| 07d | Archive Set Recovery | Not started | 07a, 07b |
| 08a | Install From Archive Set | In progress | 07a, 08 |
| 08b | Install Cleanup And Flattening | In progress | 08a |
| 08c | Directory Upload UX | In progress | 07b, 07c |
| 09 | Save Backup And Restore | Completed | 05, 06 |
| 10 | Packaging And Release | Completed | 04, 08a, 09 |
| 11 | Windows CI | In progress | 10 |

## Notes

- Architecture source of truth: [docs/architecture.md](/home/isaac/workspace/gumo/docs/architecture.md)
- Plugin development guidance: [docs/playnite-plugin-development.md](/home/isaac/workspace/gumo/docs/playnite-plugin-development.md)
- Windows is the source of truth for plugin development and release builds.
- NixOS-side compilation is best-effort only and should be dropped if it becomes expensive to maintain.
- The current single-payload upload path is a bridge implementation; future upload/install work should follow the archive-set model documented in the architecture notes.
