# Gumo Architecture

## Goal

Gumo manages a personal game library stored on a NAS and publishes a browsable web catalog for that library.

The system needs to:

- ingest games into managed storage
- store managed games as archives, including split archives for large payloads
- track multiple versions of the same game
- store version-specific save snapshots as archives
- attach metadata, artwork, and platform information
- let the owner curate entries and visibility
- serve a fast web UI for browsing the collection
- integrate with Playnite
- stay simple enough to run as a single self-hosted service

## Recommended Architecture

Use a **single deployable backend service in Rust** with a **separate web frontend** that is built to static assets and served by the backend, inside a **Nix-first monorepo**.

This gives:

- a good fit with the existing Rust-oriented repo setup
- low operational complexity for self-hosting
- strong filesystem and background job support
- clear separation between domain/backend logic and UI
- reproducible development and build workflows through Nix

The Playnite plugin should stay in the same monorepo as a separate client project under `playnite-plugin/`.

Plugin build policy:

- try Linux-side compilation only as a convenience for iteration
- do not require NixOS to produce release-grade plugin artifacts
- treat Windows builds as the source of truth for Playnite development and release packaging

## System Shape

### 1. Backend

Rust service with:

- `axum` for HTTP API and static file serving
- `sqlx` with SQLite for persistence
- `tokio` for async runtime
- `tracing` for logs

Responsibilities:

- metadata ingestion and caching
- admin actions
- public read API for the website
- thumbnail/artwork proxy or local cache serving

### 2. Frontend

Use a small SPA built with:

- React
- Vite
- TypeScript

Responsibilities:

- public game catalog pages
- search/filter/sort UI
- game detail pages
- admin screens later if needed

The backend should serve the built frontend from `/`.

### 3. Database

Use **SQLite** first, accessed through `sqlx`.

Why:

- single-user, self-hosted workload
- easy backup and migration
- no separate database service
- enough for tens of thousands of indexed files

If the project later grows into multi-user sync or high write concurrency, Postgres can be introduced, but that is not justified now.

### 4. Managed Storage as Source of Truth

Gumo manages its own library storage.

Game payloads are imported into Gumo-controlled storage and tracked through application metadata.

Gumo stores:

- archive and part metadata
- version metadata
- save snapshot metadata
- metadata
- artwork cache
- curation state

Do not design around arbitrary external library paths for v1.

## Data Model

Start with these core entities:

### `libraries`

Configured managed library roots.

Fields:

- `id`
- `name`
- `root_path`
- `platform_hint` nullable
- `is_enabled`

### `games`

Logical game records shown on the site.

Fields:

- `id`
- `name`
- `sorting_name`
- `description`
- `release_date`
- `platforms`
- `genres`
- `developers`
- `publishers`
- `links`
- `cover_image` nullable
- `background_image` nullable
- `icon` nullable
- `source_slug` nullable
- `visibility`
- `created_at`
- `updated_at`

### `game_versions`

Versioned releases of the same logical game.

Fields:

- `id`
- `game_id`
- `version_name`
- `version_code` nullable
- `release_date` nullable
- `is_latest`
- `storage_mode`
- `notes` nullable
- `created_at`
- `updated_at`

### `version_artifacts`

Concrete stored payloads for a game version.

Fields:

- `id`
- `game_version_id`
- `artifact_kind`
- `archive_format`
- `relative_path`
- `size_bytes`
- `checksum`
- `is_managed`
- `created_at`

### `artifact_parts`

Split archive parts for large managed payloads.

Fields:

- `id`
- `version_artifact_id`
- `part_index`
- `relative_path`
- `size_bytes`
- `checksum`

### `save_snapshots`

Archived full-state save captures tied to a specific game version.

Fields:

- `id`
- `game_id`
- `game_version_id`
- `library_id`
- `name`
- `captured_at`
- `archive_type`
- `size_bytes`
- `checksum`
- `notes` nullable
- `created_at`

### `save_snapshot_parts`

Split archive parts for large save snapshot payloads.

Fields:

- `id`
- `save_snapshot_id`
- `part_index`
- `relative_path`
- `size_bytes`
- `checksum`

### `assets`

Cached local images and derived thumbnails.

Fields:

- `id`
- `kind`
- `storage_path`
- `source_url` nullable
- `width` nullable
- `height` nullable
- `checksum` nullable

### `metadata_sources`

Traceability for imported metadata.

Fields:

- `id`
- `game_id`
- `provider`
- `provider_key`
- `raw_payload`
- `fetched_at`

### `overrides`

Manual and Playnite-provided metadata win over later imported metadata.

Fields:

- `game_id`
- `field_name`
- `value`
- `updated_at`

## Import Strategy

Gumo does not support arbitrary external library scanning in v1.

All game payloads enter the system through managed imports.

Managed import flow:

1. Receive a game payload from an admin workflow or external integration.
2. Create or match the logical `game`.
3. Create a `game_version`.
4. Archive the payload into managed storage.
5. Split the archive into parts when it exceeds the configured size threshold.
6. Persist artifact and part metadata.

Save import and restore are related but separate flows.

Save snapshot flow:

1. Playnite identifies the local save state for an installed game version.
2. The plugin creates a save snapshot upload targeting a specific `game_version`.
3. Gumo stores the uploaded save state as one full-state archive.
4. If necessary, Gumo splits the archive into parts.
5. Gumo creates the snapshot record and returns the resulting metadata.

## Metadata Strategy

Treat Playnite as the primary metadata source in v1.

Suggested order:

1. Use Playnite-provided metadata during import and metadata updates.
2. Persist those fields directly in Gumo's game/version records.
3. Store manual or plugin edits as authoritative values.
4. Later, if built-in metadata providers are added, treat provider data as secondary to explicit user or Playnite edits.

This prevents later metadata refreshes from overwriting trusted values coming from the main client integration.

### Metadata Alignment With Playnite

Gumo metadata should align closely with Playnite field names and concepts where practical.

Reason:

- reduces transformation logic in the plugin
- makes metadata sync simpler
- keeps future bidirectional updates predictable

For game-level metadata, favor fields compatible with Playnite concepts such as:

- `name`
- `sorting_name`
- `description`
- `release_date`
- `genres`
- `developers`
- `publishers`
- `links`
- `cover_image`
- `background_image`
- `icon`

For version-level metadata, favor:

- `version_name`
- `version_code`
- `release_date`
- `install_notes` if needed later

Gumo does not need to mirror every Playnite field immediately, but the schema should avoid inventing incompatible names without a clear reason.

Release year, if needed for display, should be derived from `release_date` rather than stored as the primary metadata field.

## Platform Architecture

Platform support must be modular from the start.

The core app should define a platform interface covering:

- import validation rules
- naming and parsing heuristics
- version detection heuristics where relevant
- metadata normalization
- archive/import rules where platform-specific behavior is needed
- future launch/install integration hooks

`pc` should be implemented as the first built-in platform module.

Do not build dynamic runtime plugins for platform support in v1.

Instead:

- keep platform modules internal to the repo
- keep the platform boundary explicit
- allow future extraction into separate crates or build-time composition through Nix

This preserves extensibility without introducing dynamic loading complexity.

## External Integrations

Playnite integration is a first-class requirement.

Treat the Playnite plugin as a separate client of the Gumo API.

It should be implemented as a proper Playnite `LibraryPlugin`.

The Playnite plugin should support:

- browsing or searching Gumo libraries
- linking local Playnite entries to Gumo records
- uploading game payloads into Gumo-managed storage
- selecting or creating a target game/version during upload
- updating game and version metadata where authorized

This implies the backend will need authenticated integration endpoints in addition to the public catalog API.

The integration should not require special backend runtime behavior beyond normal API and storage operations.

Recommended Playnite plugin capabilities:

- customized game import
- install actions for Gumo-managed versions
- play actions for locally installed games
- save backup and restore actions tied to a selected game version

This maps well to Playnite's library plugin model and avoids inventing a more awkward generic plugin workflow.

## Playnite Import/Install Lifecycle

The Playnite plugin is the primary client for managed imports and local installs.

The contract should be simple:

- Gumo stores versioned archived payloads
- Playnite uploads payloads into Gumo
- Playnite may update metadata for linked games and versions
- Playnite installs payloads by downloading and extracting them locally
- Playnite may upload and restore save snapshots for a specific game version

### Import Lifecycle

Recommended upload flow:

1. The user selects a game or payload in Playnite.
2. The plugin authenticates with an API token.
3. The plugin either:
   - selects an existing `game`, or
   - creates a new logical `game`
4. The plugin specifies the target library and platform.
5. The plugin provides version metadata such as:
   - `version_name`
   - optional `version_code`
   - optional notes
6. The plugin uploads the payload to Gumo.
7. Gumo stores the upload as a managed archive in the target library.
8. If the payload exceeds the configured threshold, Gumo splits the archive into parts.
9. Gumo creates:
   - or updates the `game`
   - a `game_version`
   - one or more artifact records
10. Gumo returns the created game/version identifiers and resulting metadata.

Important import rules:

- imports must always target a specific library
- version creation must be explicit, not guessed implicitly from file names alone
- imports should be idempotent where practical, using checksums and version metadata to avoid accidental duplicates
- archive creation happens server-side, not in the Playnite plugin

Playnite-side implementation note:

- customized import should be implemented through the library plugin import flow rather than a separate ad hoc UI pathway if possible

### Metadata Update Lifecycle

Recommended metadata update flow:

1. The plugin fetches the current game or version metadata from Gumo.
2. The user edits metadata in Playnite.
3. The plugin submits an authenticated update request.
4. Gumo validates the request against editable fields.
5. Gumo writes the changes as normal field updates or manual overrides.
6. Gumo returns the updated record.

Important rules:

- Playnite should only update fields that are explicitly editable through the integration
- manual/plugin edits should take precedence over imported metadata refreshes
- game-level fields and version-level fields should be updated through distinct endpoints

### Install Lifecycle

Recommended install flow:

1. The user selects a Gumo-managed game version in Playnite.
2. The plugin requests install metadata from Gumo.
3. Gumo returns:
   - game metadata
   - selected version metadata
   - artifact identifiers
   - download information for the archive or archive parts
   - checksum information
4. The plugin downloads the archive payload.
5. The plugin verifies checksums.
6. The plugin extracts the archive into a Playnite-managed install directory.
7. The plugin records local install state on the Playnite side.
8. Playnite launches the installed game using its own local game configuration.

Important install rules:

- Gumo manages storage and artifact metadata
- Playnite manages local installation state and launch configuration
- Gumo should not assume ownership of the installed files on the client machine
- installs should target a version explicitly, not an implicit "latest" unless the client requests that behavior

### Save Lifecycle

Recommended save backup flow:

1. The user selects a locally installed game version in Playnite.
2. The plugin locates the save data for that installation.
3. The plugin creates a save upload targeting a specific `game_version`.
4. The plugin uploads the save payload.
5. Gumo stores the payload as one full-state save archive.
6. Gumo returns the created save snapshot metadata.

Recommended save restore flow:

1. The user selects a save snapshot for a specific game version.
2. The plugin requests a restore manifest from Gumo.
3. Gumo returns archive download and checksum metadata.
4. The plugin downloads and verifies the snapshot archive.
5. The plugin extracts the snapshot into the configured local save location.

Important save rules:

- saves are always tied to a specific `game_version`
- one save snapshot represents one full save state at one point in time
- Gumo stores snapshots; Playnite performs local backup and restore actions
- save restore should require explicit user intent and target path handling on the client side

### Division Of Responsibility

Gumo should own:

- logical game records
- version records
- archive creation
- multipart archive management
- artifact checksums and download metadata
- save snapshot records and archive metadata
- catalog metadata and artwork

Playnite should own:

- local installation path choice
- local extraction
- local installed/uninstalled state
- local play actions and launch parameters
- client-side UX for version selection during install
- local save path discovery and restore behavior

### API Shape For Playnite

The integration likely needs endpoints in three groups:

- catalog/query endpoints
- import endpoints
- install/download endpoints

Representative endpoints:

- `GET /api/integrations/playnite/games`
- `GET /api/integrations/playnite/games/:id`
- `POST /api/integrations/playnite/imports`
- `PATCH /api/integrations/playnite/games/:id`
- `PATCH /api/integrations/playnite/versions/:id`
- `GET /api/integrations/playnite/versions/:id/install`
- `GET /api/integrations/playnite/artifacts/:id/download`
- `POST /api/integrations/playnite/versions/:id/save-uploads`
- `GET /api/integrations/playnite/versions/:id/save-snapshots`
- `GET /api/integrations/playnite/save-snapshots/:id/restore`
- `GET /api/integrations/playnite/save-snapshots/:id/download`

For multipart archives, the install metadata endpoint should list parts in order rather than forcing the plugin to discover them separately.

### Version Selection Policy

The plugin should not assume that one logical game maps to one installable payload.

Install requests should work like this:

- if the user selected a specific version, install that version
- if no version is selected, the plugin may request the library default or latest version explicitly

Do not hide versioning behind a single "best guess" behavior in the backend.

### Failure Model

Expected failure points:

- upload interrupted
- archive creation fails
- duplicate version conflict
- download interrupted
- checksum mismatch
- extraction fails on the Playnite side
- save backup upload interrupted
- save restore extraction fails

The architecture should treat upload state and archive processing state as explicit job state rather than implicit transient behavior.

## Upload Protocol Recommendation

Recommended approach: **two-step upload with a created upload resource and explicit finalize step**

Use one shared upload infrastructure with distinct upload kinds:

- `game_payload`
- `save_snapshot`

Shared flow:

1. create an upload session
2. stream the upload content
3. finalize the upload
4. enqueue background processing

Game payload flow:

1. `POST /api/integrations/playnite/uploads/game-payloads`
   - create an upload session
   - declare target library, platform, game/version intent, and file metadata
2. `PUT /api/integrations/playnite/uploads/:id/content`
   - stream the payload bytes
3. `POST /api/integrations/playnite/uploads/:id/finalize`
   - verify the upload
   - create archive artifacts
   - attach the resulting payload to the selected or created game/version

Save snapshot flow:

1. `POST /api/integrations/playnite/uploads/save-snapshots`
   - create an upload session
   - declare target game version and save metadata
2. `PUT /api/integrations/playnite/uploads/:id/content`
   - stream the payload bytes
3. `POST /api/integrations/playnite/uploads/:id/finalize`
   - verify the upload
   - create save snapshot artifacts
   - attach the resulting snapshot to the selected game version

Why this is the right default:

- simpler than full resumable chunk protocols
- safer than a single giant multipart request
- gives explicit server-side state for validation and recovery
- works well with background archive processing
- leaves room to add resumable/chunked uploads later without replacing the whole model

Do not start with:

- one giant multipart form request for the full payload
- a custom chunking protocol in v1 unless file sizes force it immediately

Recommended v1 upload behavior:

- one upload resource per incoming payload
- streamed request body for content transfer
- checksum provided by the client when possible
- explicit server-side size validation
- explicit finalize call to transition from "uploaded" to "processing"

If large unreliable uploads become a problem later, extend this model into resumable chunked uploads rather than replacing it.

Finalize should enqueue a background job rather than performing archive work inline.

The finalize response should include:

- `upload_id`
- `job_id`
- current status

The Playnite plugin should poll for job completion.

## Upload And Job State Model

Playnite may be terminated during upload, finalize, polling, or install preparation.

The backend must therefore treat upload and import processing as durable state machines rather than assuming a continuously connected client.

### Upload States

Recommended upload states:

- `created`
- `uploading`
- `uploaded`
- `finalizing`
- `queued`
- `processing`
- `completed`
- `failed`
- `abandoned`
- `expired`

State meanings:

- `created`
  - upload session exists but no content has been received yet
- `uploading`
  - content transfer is in progress
- `uploaded`
  - content transfer completed and server-side size/checksum validation has passed enough to allow finalize
- `finalizing`
  - finalize request accepted and transition into job creation is underway
- `queued`
  - background import/archive job has been created but not started
- `processing`
  - background import/archive job is running
- `completed`
  - import/archive job finished successfully and produced durable game/version/artifact records
- `failed`
  - upload or processing encountered a terminal error
- `abandoned`
  - client disappeared before completing the flow and the upload is eligible for cleanup or retry logic
- `expired`
  - stale upload was cleaned up after a retention timeout

### Job States

Recommended job states:

- `pending`
- `running`
- `completed`
- `failed`
- `cancelled`

Job records should be durable and separate from the upload record, but linked to it.

Recommended job kinds:

- `import_archive`
- `save_snapshot_archive`

### State Transitions

Recommended main path:

1. `created`
2. `uploading`
3. `uploaded`
4. `finalizing`
5. `queued`
6. `processing`
7. `completed`

Failure paths:

- `uploading -> abandoned`
- `uploading -> failed`
- `uploaded -> expired`
- `finalizing -> failed`
- `queued -> failed`
- `processing -> failed`
- `created -> expired`
- `abandoned -> expired`

Do not silently collapse these states into generic "error" behavior. The plugin and admin UI should be able to distinguish them.

### Interruption Handling

The system must handle Playnite termination gracefully in every phase.

#### During `created`

If the client creates an upload but never starts sending content:

- keep the record for a short retention window
- mark it `expired` after timeout
- remove any temporary resources

#### During `uploading`

If the client disconnects mid-transfer:

- do not mark the upload as completed
- keep partial data only if it is needed for diagnostic or future resumable support
- otherwise mark the upload `abandoned` and schedule cleanup

For v1, I recommend:

- no resumable upload support
- partial payloads are discarded during cleanup
- the client retries by creating a new upload

#### During `uploaded`

If content upload finishes but Playnite exits before finalize:

- preserve the uploaded payload for a retention window
- allow the same client or a later retry flow to finalize it if appropriate
- otherwise expire and clean it up automatically

This avoids wasting a successful upload when only the final API call was interrupted.

#### During `finalizing`

If the client disconnects after sending finalize:

- treat finalize as authoritative once accepted
- continue creating the job server-side
- make the operation idempotent so retrying finalize does not duplicate jobs

This is a critical rule. The backend must not require the client to stay connected after finalize is accepted.

#### During `queued` or `processing`

If Playnite disappears while the background job runs:

- continue processing normally
- persist progress and terminal status in the database
- allow a later client session to poll the existing job and discover the result

The job lifecycle must be independent of the client lifecycle.

#### After `completed`

If Playnite never comes back to read the result:

- keep the completed records normally
- retain upload/job history according to cleanup policy

#### On `failed`

Failures should include structured diagnostics such as:

- failure code
- human-readable message
- failed phase
- retryability flag

The plugin should be able to present useful errors without scraping logs.

### Idempotency Rules

To survive retries and client termination, these operations should be idempotent where practical:

- create upload, if a client-generated idempotency key is supplied
- finalize upload
- metadata patch requests

At minimum:

- `finalize` must not create duplicate jobs for the same completed upload
- repeating a successful metadata patch should be harmless

### Cleanup Policy

The backend should run cleanup as a background responsibility.

Cleanup rules:

- remove stale `created` uploads after a short timeout
- remove `abandoned` partial uploads after a short timeout
- remove unfinalized `uploaded` payloads after a longer timeout
- keep `failed` uploads long enough for diagnosis
- keep `completed` job records longer than temporary upload blobs

This policy should be configurable but conservative by default.

### Polling Contract

Playnite should recover from interruption by polling server state rather than assuming in-memory client state is still valid.

Recommended endpoints:

- `GET /api/integrations/playnite/uploads/:id`
- `GET /api/integrations/playnite/jobs/:id`

These responses should include:

- current state
- timestamps
- linked game/version ids when available
- structured error info on failure
- whether retry is possible

### Recovery Strategy

Support both client-side persistence and server-side rediscovery.

Playnite should:

- persist `upload_id` and `job_id` locally when available
- resume polling those identifiers after restart

The backend should also support rediscovery in case the client loses local state.

Recommended recovery endpoints:

- `GET /api/integrations/playnite/uploads`
- `GET /api/integrations/playnite/jobs`

These should support filtering for states such as:

- active or incomplete uploads
- recent failed uploads
- recent completed jobs

Recommended rule:

- local identifier persistence is the fast path
- server-side rediscovery is the fallback path

This gives graceful recovery whether Playnite crashes before saving state or after saving only part of it.

## Managed Storage Model

Gumo is not only an indexer. It also needs a managed storage mode.

Managed storage requirements:

- store imported games as archives
- support split archives for large payloads
- support multiple versions of the same game
- store save snapshots as full-state archives tied to specific versions
- keep storage metadata separate from logical game metadata

Recommended model:

- `games` represent the logical title
- `game_versions` represent release variants or revisions
- `version_artifacts` represent stored payloads for a version
- `artifact_parts` represent split pieces of a large archive
- `save_snapshots` represent archived save states for a version
- `save_snapshot_parts` represent split pieces of a large save archive

Important rule:

- versioning belongs below the logical game record

That keeps the public catalog clean while preserving storage-level detail.

Another important rule:

- archive management should be a storage concern, not the main identity of the game

The app should be able to say "this game has multiple versions" without exposing archive internals unless needed.

The same applies to saves: the app should expose save snapshots as user-facing history without exposing low-level archive layout unless required for recovery.

## API Design

Use a JSON HTTP API. Do not start with GraphQL.

Public endpoints:

- `GET /api/games`
- `GET /api/games/:id`
- `GET /api/platforms`
- `GET /api/genres`
- `GET /assets/...`

Admin endpoints:

- `PATCH /api/admin/games/:id`
- `POST /api/admin/games/:id/match`
- `POST /api/admin/assets/refresh`
- `POST /api/admin/imports`

Keep the API versionless initially unless a public integration need appears.

## API Resource Shapes

The API should use stable JSON resource shapes across:

- public catalog endpoints
- admin endpoints
- Playnite integration endpoints

The same core entities should appear consistently, with only context-specific field subsets added where needed.

### Common Conventions

Recommended conventions:

- use string ids at the API boundary
- use RFC 3339 timestamps
- use explicit enums as strings
- return nullable fields explicitly when they are part of the schema
- prefer full resource objects in responses over ambiguous partial blobs

For write requests:

- accept only explicitly writable fields
- reject unknown fields in strict mode if practical

For errors:

- return a machine-readable error code
- return a human-readable message
- include field-level validation errors where relevant

### `Game` Resource

Represents a logical title.

Suggested shape:

```json
{
  "id": "game_01",
  "name": "Example Game",
  "sorting_name": "Example Game",
  "platforms": ["pc"],
  "description": null,
  "release_date": null,
  "genres": [],
  "developers": [],
  "publishers": [],
  "links": [],
  "visibility": "private",
  "cover_image": null,
  "background_image": null,
  "icon": null,
  "created_at": "2026-03-22T20:00:00Z",
  "updated_at": "2026-03-22T20:00:00Z"
}
```

### `GameVersion` Resource

Represents one installable version of a logical game.

Suggested shape:

```json
{
  "id": "ver_01",
  "game_id": "game_01",
  "library_id": "lib_01",
  "version_name": "1.0.0",
  "version_code": null,
  "release_date": null,
  "is_latest": true,
  "notes": null,
  "created_at": "2026-03-22T20:00:00Z",
  "updated_at": "2026-03-22T20:00:00Z"
}
```

### `Artifact` Resource

Represents one archived payload attached to a version.

Suggested shape:

```json
{
  "id": "art_01",
  "game_version_id": "ver_01",
  "archive_type": "zip",
  "size_bytes": 123456789,
  "checksum": "sha256:...",
  "part_count": 1,
  "created_at": "2026-03-22T20:00:00Z"
}
```

### `SaveSnapshot` Resource

Represents one archived full save-state capture for a specific game version.

Suggested shape:

```json
{
  "id": "save_01",
  "game_id": "game_01",
  "game_version_id": "ver_01",
  "library_id": "lib_01",
  "name": "Before patch 1.0.1",
  "captured_at": "2026-03-22T20:00:00Z",
  "archive_type": "zip",
  "size_bytes": 1234567,
  "checksum": "sha256:...",
  "notes": null,
  "created_at": "2026-03-22T20:00:00Z"
}
```

If multipart:

```json
{
  "id": "art_02",
  "game_version_id": "ver_02",
  "archive_type": "zip",
  "size_bytes": 9876543210,
  "checksum": "sha256:...",
  "part_count": 3,
  "parts": [
    {
      "part_index": 0,
      "size_bytes": 2147483648,
      "checksum": "sha256:..."
    },
    {
      "part_index": 1,
      "size_bytes": 2147483648,
      "checksum": "sha256:..."
    },
    {
      "part_index": 2,
      "size_bytes": 12345,
      "checksum": "sha256:..."
    }
  ],
  "created_at": "2026-03-22T20:00:00Z"
}
```

### `Library` Resource

Represents a managed storage root.

Suggested shape:

```json
{
  "id": "lib_01",
  "name": "managed-pc",
  "platform": "pc",
  "visibility": "private",
  "enabled": true
}
```

Do not expose internal filesystem paths to untrusted public clients unless there is a concrete need.

### `Upload` Resource

Represents one inbound payload upload session.

Suggested shape:

```json
{
  "id": "upl_01",
  "kind": "game_payload",
  "library_id": "lib_01",
  "platform": "pc",
  "game_id": "game_01",
  "game_version_id": null,
  "state": "uploaded",
  "filename": "example-installer.exe",
  "declared_size_bytes": 123456789,
  "received_size_bytes": 123456789,
  "checksum": "sha256:...",
  "job_id": null,
  "created_at": "2026-03-22T20:00:00Z",
  "updated_at": "2026-03-22T20:10:00Z",
  "expires_at": "2026-03-23T20:10:00Z",
  "error": null
}
```

For save snapshots:

```json
{
  "id": "upl_02",
  "kind": "save_snapshot",
  "library_id": "lib_01",
  "platform": "pc",
  "game_id": "game_01",
  "game_version_id": "ver_01",
  "state": "uploaded",
  "filename": "save-backup.zip",
  "declared_size_bytes": 1234567,
  "received_size_bytes": 1234567,
  "checksum": "sha256:...",
  "job_id": null,
  "created_at": "2026-03-22T20:00:00Z",
  "updated_at": "2026-03-22T20:10:00Z",
  "expires_at": "2026-03-23T20:10:00Z",
  "error": null
}
```

### `Job` Resource

Represents durable background processing state.

Suggested shape:

```json
{
  "id": "job_01",
  "kind": "import_archive",
  "state": "running",
  "upload_id": "upl_01",
  "game_id": "game_01",
  "game_version_id": null,
  "progress": {
    "phase": "archiving",
    "percent": 42
  },
  "result": null,
  "error": null,
  "created_at": "2026-03-22T20:10:00Z",
  "updated_at": "2026-03-22T20:11:00Z"
}
```

For save snapshot processing:

```json
{
  "id": "job_02",
  "kind": "save_snapshot_archive",
  "state": "running",
  "upload_id": "upl_02",
  "game_id": "game_01",
  "game_version_id": "ver_01",
  "progress": {
    "phase": "archiving",
    "percent": 42
  },
  "result": null,
  "error": null,
  "created_at": "2026-03-22T20:10:00Z",
  "updated_at": "2026-03-22T20:11:00Z"
}
```

On success:

```json
{
  "id": "job_01",
  "kind": "import_archive",
  "state": "completed",
  "upload_id": "upl_01",
  "game_id": "game_01",
  "game_version_id": "ver_01",
  "progress": {
    "phase": "completed",
    "percent": 100
  },
  "result": {
    "artifact_ids": ["art_01"]
  },
  "error": null,
  "created_at": "2026-03-22T20:10:00Z",
  "updated_at": "2026-03-22T20:12:00Z"
}
```

On failure:

```json
{
  "id": "job_01",
  "kind": "import_archive",
  "state": "failed",
  "upload_id": "upl_01",
  "game_id": "game_01",
  "game_version_id": null,
  "progress": {
    "phase": "archiving",
    "percent": 42
  },
  "result": null,
  "error": {
    "code": "archive_write_failed",
    "message": "Failed to write archive",
    "retryable": true
  },
  "created_at": "2026-03-22T20:10:00Z",
  "updated_at": "2026-03-22T20:12:00Z"
}
```

### `InstallManifest` Resource

Represents everything Playnite needs to install one version.

Suggested shape:

```json
{
  "game": {
    "id": "game_01",
    "name": "Example Game",
    "platforms": ["pc"]
  },
  "version": {
    "id": "ver_01",
    "version_name": "1.0.0",
    "is_latest": true
  },
  "artifact": {
    "id": "art_01",
    "archive_type": "zip",
    "size_bytes": 123456789,
    "checksum": "sha256:...",
    "parts": [
      {
        "part_index": 0,
        "download_url": "/api/integrations/playnite/artifacts/art_01/download",
        "size_bytes": 123456789,
        "checksum": "sha256:..."
      }
    ]
  }
}
```

### `SaveRestoreManifest` Resource

Represents everything Playnite needs to restore one save snapshot.

Suggested shape:

```json
{
  "game_id": "game_01",
  "game_version_id": "ver_01",
  "save_snapshot": {
    "id": "save_01",
    "name": "Before patch 1.0.1",
    "captured_at": "2026-03-22T20:00:00Z",
    "archive_type": "zip",
    "size_bytes": 1234567,
    "checksum": "sha256:..."
  },
  "parts": [
    {
      "part_index": 0,
      "download_url": "/api/integrations/playnite/save-snapshots/save_01/download",
      "size_bytes": 1234567,
      "checksum": "sha256:..."
    }
  ]
}
```

### List Response Shape

For collection endpoints, use an explicit envelope.

Suggested shape:

```json
{
  "items": [],
  "next_cursor": null
}
```

This leaves room for future pagination without changing the top-level response format.

## API Request Shapes

### Create Game Payload Upload

`POST /api/integrations/playnite/uploads/game-payloads`

Suggested request:

```json
{
  "library_id": "lib_01",
  "platform": "pc",
  "game": {
    "id": "game_01"
  },
  "version": {
    "version_name": "1.0.0",
    "version_code": null,
    "notes": null
  },
  "file": {
    "filename": "example-installer.exe",
    "size_bytes": 123456789,
    "checksum": "sha256:..."
  },
  "idempotency_key": "client-generated-key"
}
```

Game selection may also allow create-on-upload:

```json
{
  "library_id": "lib_01",
  "platform": "pc",
  "game": {
    "create": {
      "name": "Example Game"
    }
  },
  "version": {
    "version_name": "1.0.0"
  },
  "file": {
    "filename": "example-installer.exe",
    "size_bytes": 123456789
  }
}
```

### Finalize Upload

`POST /api/integrations/playnite/uploads/:id/finalize`

Suggested request:

```json
{
  "idempotency_key": "client-generated-key-finalize"
}
```

### Create Save Snapshot Upload

`POST /api/integrations/playnite/uploads/save-snapshots`

Suggested request:

```json
{
  "game_version_id": "ver_01",
  "name": "Before patch 1.0.1",
  "file": {
    "filename": "save-backup.zip",
    "size_bytes": 1234567,
    "checksum": "sha256:..."
  },
  "notes": null,
  "idempotency_key": "client-generated-key"
}
```

Suggested response:

```json
{
  "upload_id": "upl_01",
  "job_id": "job_01",
  "state": "queued"
}
```

### Patch Game

`PATCH /api/integrations/playnite/games/:id`

Suggested request:

```json
{
  "name": "Example Game",
  "sorting_name": "Example Game",
  "description": "Updated description",
  "release_date": null,
  "genres": ["Action"],
  "developers": ["Example Studio"],
  "publishers": ["Example Publisher"],
  "visibility": "private"
}
```

### Patch Version

`PATCH /api/integrations/playnite/versions/:id`

Suggested request:

```json
{
  "version_name": "1.0.1",
  "version_code": "build-101",
  "notes": "Hotfix release"
}
```

## Validation Rules

Minimum validation rules to enforce at the API boundary:

### Game Payload Upload Rules

- game payload uploads must reference an enabled library
- library platform and requested platform must match
- exactly one of `game.id` or `game.create` must be supplied on upload creation
- version fields required for version creation must be present

### Save Snapshot Upload Rules

- save snapshot uploads must target an existing game version
- the targeted game version must belong to an enabled library
- the game version platform must match the requested platform context if one is supplied

### Shared Upload Rules

- finalize is only valid from `uploaded` or an equivalent idempotent retry state
- content size and checksum must match declared values when provided
- unknown or expired upload ids must fail explicitly

### Metadata Update Rules

- patch endpoints may only update fields explicitly allowed for integrations
- game-level and version-level metadata must be patched through their respective endpoints

### Install And Restore Rules

- install manifests must resolve to exactly one version
- save restore manifests must resolve to exactly one save snapshot tied to the requested version

Do not let the plugin depend on ambiguous backend heuristics for identity or version resolution.

## Database Schema

Use SQLite with a normalized relational schema.

The API may expose arrays for convenience, but the database should normalize fields that are likely to be queried or reused across games.

Recommended normalization:

- normalize libraries, games, versions, artifacts, uploads, jobs, and save snapshots
- normalize multi-value metadata such as genres, developers, publishers, and links
- keep large raw provider payloads and some error/result payloads as JSON text where strict relational structure is not worth it

### Conventions

Recommended conventions:

- internal primary keys may be integer autoincrement ids
- external API ids should be stable string ids stored separately or derived consistently
- all timestamps stored as UTC
- use foreign keys with `ON DELETE` behavior chosen deliberately
- enable SQLite foreign key enforcement explicitly

Suggested common columns:

- `id INTEGER PRIMARY KEY`
- `public_id TEXT NOT NULL UNIQUE`
- `created_at TEXT NOT NULL`
- `updated_at TEXT NOT NULL`

### `libraries`

Purpose:

- managed storage roots

Suggested columns:

- `id`
- `public_id`
- `name TEXT NOT NULL UNIQUE`
- `root_path TEXT NOT NULL UNIQUE`
- `platform_hint TEXT`
- `visibility TEXT NOT NULL`
- `is_enabled INTEGER NOT NULL`
- `created_at`
- `updated_at`

### `games`

Purpose:

- logical titles

Suggested columns:

- `id`
- `public_id`
- `library_id INTEGER NOT NULL`
- `name TEXT NOT NULL`
- `sorting_name TEXT`
- `description TEXT`
- `release_date TEXT`
- `cover_image TEXT`
- `background_image TEXT`
- `icon TEXT`
- `source_slug TEXT`
- `visibility TEXT NOT NULL`
- `created_at`
- `updated_at`

Recommended indexes:

- `(library_id, name)`
- `(library_id, sorting_name)`

Note:

- `platforms` should not be stored as a JSON array in the main table even though the API exposes an array

### `game_platforms`

Purpose:

- associate games with one or more platforms

Suggested columns:

- `game_id INTEGER NOT NULL`
- `platform_id INTEGER NOT NULL`

Constraints:

- primary key `(game_id, platform_id)`

### `platforms`

Purpose:

- supported platform values

Suggested columns:

- `id`
- `public_id`
- `name TEXT NOT NULL UNIQUE`
- `is_enabled INTEGER NOT NULL`
- `match_priority INTEGER NOT NULL`
- `created_at`
- `updated_at`

For v1 this will likely only contain `pc`, but it should still be a real table.

### `game_versions`

Purpose:

- installable versions of a game

Suggested columns:

- `id`
- `public_id`
- `game_id INTEGER NOT NULL`
- `library_id INTEGER NOT NULL`
- `version_name TEXT NOT NULL`
- `version_code TEXT`
- `release_date TEXT`
- `notes TEXT`
- `is_latest INTEGER NOT NULL`
- `storage_mode TEXT NOT NULL`
- `created_at`
- `updated_at`

Recommended constraints:

- unique `(game_id, version_name, COALESCE(version_code, ''))` in logical terms

Because SQLite cannot express that exact uniqueness cleanly without care, use either:

- a generated normalized column, or
- application-level duplicate checks plus a practical unique index strategy

Recommended indexes:

- `(game_id, is_latest)`
- `(library_id, created_at)`

### `version_artifacts`

Purpose:

- archived payloads attached to a version

Suggested columns:

- `id`
- `public_id`
- `game_version_id INTEGER NOT NULL`
- `artifact_kind TEXT NOT NULL`
- `archive_type TEXT NOT NULL`
- `relative_path TEXT NOT NULL`
- `size_bytes INTEGER NOT NULL`
- `checksum TEXT NOT NULL`
- `part_count INTEGER NOT NULL`
- `is_managed INTEGER NOT NULL`
- `created_at`

Recommended indexes:

- `(game_version_id)`
- `(checksum)`

### `artifact_parts`

Purpose:

- split archive pieces for a version artifact

Suggested columns:

- `id`
- `version_artifact_id INTEGER NOT NULL`
- `part_index INTEGER NOT NULL`
- `relative_path TEXT NOT NULL`
- `size_bytes INTEGER NOT NULL`
- `checksum TEXT NOT NULL`

Constraints:

- unique `(version_artifact_id, part_index)`

### `save_snapshots`

Purpose:

- full save-state captures tied to a specific version

Suggested columns:

- `id`
- `public_id`
- `game_id INTEGER NOT NULL`
- `game_version_id INTEGER NOT NULL`
- `library_id INTEGER NOT NULL`
- `name TEXT NOT NULL`
- `captured_at TEXT NOT NULL`
- `archive_type TEXT NOT NULL`
- `size_bytes INTEGER NOT NULL`
- `checksum TEXT NOT NULL`
- `notes TEXT`
- `created_at`

Recommended indexes:

- `(game_version_id, captured_at DESC)`
- `(library_id, captured_at DESC)`

### `save_snapshot_parts`

Purpose:

- split archive pieces for a save snapshot

Suggested columns:

- `id`
- `save_snapshot_id INTEGER NOT NULL`
- `part_index INTEGER NOT NULL`
- `relative_path TEXT NOT NULL`
- `size_bytes INTEGER NOT NULL`
- `checksum TEXT NOT NULL`

Constraints:

- unique `(save_snapshot_id, part_index)`

### `genres`

Suggested columns:

- `id`
- `name TEXT NOT NULL UNIQUE`

### `developers`

Suggested columns:

- `id`
- `name TEXT NOT NULL UNIQUE`

### `publishers`

Suggested columns:

- `id`
- `name TEXT NOT NULL UNIQUE`

### `links`

Purpose:

- external links associated with games

Suggested columns:

- `id`
- `game_id INTEGER NOT NULL`
- `name TEXT NOT NULL`
- `url TEXT NOT NULL`

Recommended indexes:

- `(game_id)`

### Join Tables

Use join tables for many-to-many metadata:

- `game_genres(game_id, genre_id)`
- `game_developers(game_id, developer_id)`
- `game_publishers(game_id, publisher_id)`

Each should use a composite primary key over both foreign keys.

### `metadata_sources`

Purpose:

- trace imported metadata payloads

Suggested columns:

- `id`
- `game_id INTEGER NOT NULL`
- `provider TEXT NOT NULL`
- `provider_key TEXT NOT NULL`
- `raw_payload TEXT NOT NULL`
- `fetched_at TEXT NOT NULL`

Recommended indexes:

- `(game_id, provider)`
- `(provider, provider_key)`

### `overrides`

Purpose:

- authoritative field-level overrides

Suggested columns:

- `id`
- `game_id INTEGER`
- `game_version_id INTEGER`
- `field_name TEXT NOT NULL`
- `value TEXT`
- `source TEXT NOT NULL`
- `updated_at TEXT NOT NULL`

Recommended constraints:

- exactly one of `game_id` or `game_version_id` must be non-null

This is a good place for a `CHECK` constraint.

### `uploads`

Purpose:

- durable inbound upload sessions for game payloads and save snapshots

Suggested columns:

- `id`
- `public_id`
- `kind TEXT NOT NULL`
- `library_id INTEGER NOT NULL`
- `platform_id INTEGER`
- `game_id INTEGER`
- `game_version_id INTEGER`
- `state TEXT NOT NULL`
- `filename TEXT NOT NULL`
- `declared_size_bytes INTEGER NOT NULL`
- `received_size_bytes INTEGER NOT NULL`
- `checksum TEXT`
- `temp_path TEXT NOT NULL`
- `job_id INTEGER`
- `idempotency_key TEXT`
- `expires_at TEXT`
- `error_code TEXT`
- `error_message TEXT`
- `created_at`
- `updated_at`

Recommended indexes:

- `(state, updated_at)`
- `(job_id)`
- `(idempotency_key)`

### `jobs`

Purpose:

- durable background work state

Suggested columns:

- `id`
- `public_id`
- `kind TEXT NOT NULL`
- `state TEXT NOT NULL`
- `upload_id INTEGER`
- `game_id INTEGER`
- `game_version_id INTEGER`
- `progress_phase TEXT`
- `progress_percent INTEGER`
- `result_payload TEXT`
- `error_code TEXT`
- `error_message TEXT`
- `retryable INTEGER`
- `created_at`
- `updated_at`

Recommended indexes:

- `(state, updated_at)`
- `(upload_id)`
- `(kind, state)`

### Suggested Foreign Key Behavior

Recommended defaults:

- deleting a `game` should be restricted if versions exist
- deleting a `game_version` should be restricted if artifacts or save snapshots exist
- deleting an artifact should cascade to its parts
- deleting a save snapshot should cascade to its parts
- deleting a library should be restricted while dependent records exist

This keeps destructive operations explicit.

### Arrays In API vs Relational Storage

Keep these as arrays in the API but normalized in the database:

- `platforms`
- `genres`
- `developers`
- `publishers`
- `links`

Do not store those as JSON arrays in `games` unless query requirements turn out to be trivial and intentionally limited.

### Migration Strategy

Start with forward-only SQL migrations through `sqlx`.

Recommended migration ordering:

1. core reference tables: `platforms`, `libraries`
2. primary domain tables: `games`, `game_versions`
3. artifact and save tables
4. metadata tables
5. upload/job tables
6. indexes and data backfills

### Schema Notes

A few fields should stay intentionally simple in v1:

- `result_payload` in `jobs` can be JSON text
- `raw_payload` in `metadata_sources` can be JSON text
- `value` in `overrides` can be text-encoded and interpreted by the application layer

This keeps the schema practical without over-normalizing low-value internals.

## Initial Migration Design

This section describes the first-pass SQL migration layout for SQLite.

It is not meant to be final SQL syntax, but it should be close enough to implement directly with `sqlx` migrations.

### Migration Ordering

Recommended initial migration sequence:

1. create reference tables
2. create primary domain tables
3. create normalized metadata tables
4. create artifact and save tables
5. create upload and job tables
6. create indexes
7. seed initial platform rows

### Common Constraint Strategy

Use `CHECK` constraints for enum-like values in SQLite.

Recommended enum-like domains for v1:

- `visibility IN ('public', 'private')`
- `upload.kind IN ('game_payload', 'save_snapshot')`
- `upload.state IN ('created', 'uploading', 'uploaded', 'finalizing', 'queued', 'processing', 'completed', 'failed', 'abandoned', 'expired')`
- `job.kind IN ('import_archive', 'save_snapshot_archive')`
- `job.state IN ('pending', 'running', 'completed', 'failed', 'cancelled')`
- `game_version.storage_mode IN ('managed')`
- `artifact.archive_type IN ('zip')`
- `save_snapshot.archive_type IN ('zip')`

### Migration 001: Reference Tables

Create:

- `platforms`
- `libraries`

Suggested DDL outline:

```sql
CREATE TABLE platforms (
  id INTEGER PRIMARY KEY,
  public_id TEXT NOT NULL UNIQUE,
  name TEXT NOT NULL UNIQUE,
  is_enabled INTEGER NOT NULL CHECK (is_enabled IN (0, 1)),
  match_priority INTEGER NOT NULL,
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL
);

CREATE TABLE libraries (
  id INTEGER PRIMARY KEY,
  public_id TEXT NOT NULL UNIQUE,
  name TEXT NOT NULL UNIQUE,
  root_path TEXT NOT NULL UNIQUE,
  platform_hint TEXT,
  visibility TEXT NOT NULL CHECK (visibility IN ('public', 'private')),
  is_enabled INTEGER NOT NULL CHECK (is_enabled IN (0, 1)),
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL
);
```

### Migration 002: Primary Domain Tables

Create:

- `games`
- `game_versions`
- `game_platforms`

Suggested DDL outline:

```sql
CREATE TABLE games (
  id INTEGER PRIMARY KEY,
  public_id TEXT NOT NULL UNIQUE,
  library_id INTEGER NOT NULL REFERENCES libraries(id) ON DELETE RESTRICT,
  name TEXT NOT NULL,
  sorting_name TEXT,
  description TEXT,
  release_date TEXT,
  cover_image TEXT,
  background_image TEXT,
  icon TEXT,
  source_slug TEXT,
  visibility TEXT NOT NULL CHECK (visibility IN ('public', 'private')),
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL
);

CREATE TABLE game_versions (
  id INTEGER PRIMARY KEY,
  public_id TEXT NOT NULL UNIQUE,
  game_id INTEGER NOT NULL REFERENCES games(id) ON DELETE RESTRICT,
  library_id INTEGER NOT NULL REFERENCES libraries(id) ON DELETE RESTRICT,
  version_name TEXT NOT NULL,
  version_code TEXT,
  release_date TEXT,
  notes TEXT,
  is_latest INTEGER NOT NULL CHECK (is_latest IN (0, 1)),
  storage_mode TEXT NOT NULL CHECK (storage_mode IN ('managed')),
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL
);

CREATE TABLE game_platforms (
  game_id INTEGER NOT NULL REFERENCES games(id) ON DELETE CASCADE,
  platform_id INTEGER NOT NULL REFERENCES platforms(id) ON DELETE RESTRICT,
  PRIMARY KEY (game_id, platform_id)
);
```

Version uniqueness strategy for v1:

- use a unique index on `(game_id, version_name, version_code)`
- accept SQLite's handling of `NULL` in `version_code`
- add application-level duplicate checks when `version_code IS NULL`

If this becomes painful later, introduce a normalized generated column for uniqueness.

### Migration 003: Normalized Metadata Tables

Create:

- `genres`
- `developers`
- `publishers`
- `links`
- `game_genres`
- `game_developers`
- `game_publishers`

Suggested DDL outline:

```sql
CREATE TABLE genres (
  id INTEGER PRIMARY KEY,
  name TEXT NOT NULL UNIQUE
);

CREATE TABLE developers (
  id INTEGER PRIMARY KEY,
  name TEXT NOT NULL UNIQUE
);

CREATE TABLE publishers (
  id INTEGER PRIMARY KEY,
  name TEXT NOT NULL UNIQUE
);

CREATE TABLE links (
  id INTEGER PRIMARY KEY,
  game_id INTEGER NOT NULL REFERENCES games(id) ON DELETE CASCADE,
  name TEXT NOT NULL,
  url TEXT NOT NULL
);

CREATE TABLE game_genres (
  game_id INTEGER NOT NULL REFERENCES games(id) ON DELETE CASCADE,
  genre_id INTEGER NOT NULL REFERENCES genres(id) ON DELETE RESTRICT,
  PRIMARY KEY (game_id, genre_id)
);

CREATE TABLE game_developers (
  game_id INTEGER NOT NULL REFERENCES games(id) ON DELETE CASCADE,
  developer_id INTEGER NOT NULL REFERENCES developers(id) ON DELETE RESTRICT,
  PRIMARY KEY (game_id, developer_id)
);

CREATE TABLE game_publishers (
  game_id INTEGER NOT NULL REFERENCES games(id) ON DELETE CASCADE,
  publisher_id INTEGER NOT NULL REFERENCES publishers(id) ON DELETE RESTRICT,
  PRIMARY KEY (game_id, publisher_id)
);
```

### Migration 004: Artifact And Save Tables

Create:

- `version_artifacts`
- `artifact_parts`
- `save_snapshots`
- `save_snapshot_parts`

Suggested DDL outline:

```sql
CREATE TABLE version_artifacts (
  id INTEGER PRIMARY KEY,
  public_id TEXT NOT NULL UNIQUE,
  game_version_id INTEGER NOT NULL REFERENCES game_versions(id) ON DELETE RESTRICT,
  artifact_kind TEXT NOT NULL,
  archive_type TEXT NOT NULL CHECK (archive_type IN ('zip')),
  relative_path TEXT NOT NULL,
  size_bytes INTEGER NOT NULL,
  checksum TEXT NOT NULL,
  part_count INTEGER NOT NULL CHECK (part_count >= 1),
  is_managed INTEGER NOT NULL CHECK (is_managed IN (0, 1)),
  created_at TEXT NOT NULL
);

CREATE TABLE artifact_parts (
  id INTEGER PRIMARY KEY,
  version_artifact_id INTEGER NOT NULL REFERENCES version_artifacts(id) ON DELETE CASCADE,
  part_index INTEGER NOT NULL CHECK (part_index >= 0),
  relative_path TEXT NOT NULL,
  size_bytes INTEGER NOT NULL,
  checksum TEXT NOT NULL,
  UNIQUE (version_artifact_id, part_index)
);

CREATE TABLE save_snapshots (
  id INTEGER PRIMARY KEY,
  public_id TEXT NOT NULL UNIQUE,
  game_id INTEGER NOT NULL REFERENCES games(id) ON DELETE RESTRICT,
  game_version_id INTEGER NOT NULL REFERENCES game_versions(id) ON DELETE RESTRICT,
  library_id INTEGER NOT NULL REFERENCES libraries(id) ON DELETE RESTRICT,
  name TEXT NOT NULL,
  captured_at TEXT NOT NULL,
  archive_type TEXT NOT NULL CHECK (archive_type IN ('zip')),
  size_bytes INTEGER NOT NULL,
  checksum TEXT NOT NULL,
  notes TEXT,
  created_at TEXT NOT NULL
);

CREATE TABLE save_snapshot_parts (
  id INTEGER PRIMARY KEY,
  save_snapshot_id INTEGER NOT NULL REFERENCES save_snapshots(id) ON DELETE CASCADE,
  part_index INTEGER NOT NULL CHECK (part_index >= 0),
  relative_path TEXT NOT NULL,
  size_bytes INTEGER NOT NULL,
  checksum TEXT NOT NULL,
  UNIQUE (save_snapshot_id, part_index)
);
```

### Migration 005: Metadata Provenance Tables

Create:

- `metadata_sources`
- `overrides`

Suggested DDL outline:

```sql
CREATE TABLE metadata_sources (
  id INTEGER PRIMARY KEY,
  game_id INTEGER NOT NULL REFERENCES games(id) ON DELETE CASCADE,
  provider TEXT NOT NULL,
  provider_key TEXT NOT NULL,
  raw_payload TEXT NOT NULL,
  fetched_at TEXT NOT NULL
);

CREATE TABLE overrides (
  id INTEGER PRIMARY KEY,
  game_id INTEGER REFERENCES games(id) ON DELETE CASCADE,
  game_version_id INTEGER REFERENCES game_versions(id) ON DELETE CASCADE,
  field_name TEXT NOT NULL,
  value TEXT,
  source TEXT NOT NULL,
  updated_at TEXT NOT NULL,
  CHECK (
    (game_id IS NOT NULL AND game_version_id IS NULL) OR
    (game_id IS NULL AND game_version_id IS NOT NULL)
  )
);
```

### Migration 006: Upload And Job Tables

Create:

- `uploads`
- `jobs`

Suggested DDL outline:

```sql
CREATE TABLE uploads (
  id INTEGER PRIMARY KEY,
  public_id TEXT NOT NULL UNIQUE,
  kind TEXT NOT NULL CHECK (kind IN ('game_payload', 'save_snapshot')),
  library_id INTEGER NOT NULL REFERENCES libraries(id) ON DELETE RESTRICT,
  platform_id INTEGER REFERENCES platforms(id) ON DELETE RESTRICT,
  game_id INTEGER REFERENCES games(id) ON DELETE RESTRICT,
  game_version_id INTEGER REFERENCES game_versions(id) ON DELETE RESTRICT,
  state TEXT NOT NULL CHECK (
    state IN ('created', 'uploading', 'uploaded', 'finalizing', 'queued', 'processing', 'completed', 'failed', 'abandoned', 'expired')
  ),
  filename TEXT NOT NULL,
  declared_size_bytes INTEGER NOT NULL,
  received_size_bytes INTEGER NOT NULL DEFAULT 0,
  checksum TEXT,
  temp_path TEXT NOT NULL,
  job_id INTEGER,
  idempotency_key TEXT,
  expires_at TEXT,
  error_code TEXT,
  error_message TEXT,
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL
);

CREATE TABLE jobs (
  id INTEGER PRIMARY KEY,
  public_id TEXT NOT NULL UNIQUE,
  kind TEXT NOT NULL CHECK (kind IN ('import_archive', 'save_snapshot_archive')),
  state TEXT NOT NULL CHECK (state IN ('pending', 'running', 'completed', 'failed', 'cancelled')),
  upload_id INTEGER REFERENCES uploads(id) ON DELETE SET NULL,
  game_id INTEGER REFERENCES games(id) ON DELETE SET NULL,
  game_version_id INTEGER REFERENCES game_versions(id) ON DELETE SET NULL,
  progress_phase TEXT,
  progress_percent INTEGER CHECK (progress_percent IS NULL OR (progress_percent >= 0 AND progress_percent <= 100)),
  result_payload TEXT,
  error_code TEXT,
  error_message TEXT,
  retryable INTEGER CHECK (retryable IS NULL OR retryable IN (0, 1)),
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL
);
```

Implementation note:

- `uploads.job_id` and `jobs.upload_id` create a circular relationship if both are declared as hard foreign keys immediately

Recommended approach:

- keep `jobs.upload_id` as the real foreign key
- make `uploads.job_id` an indexed nullable column without a hard foreign key, or add it later if you decide it is worth the complexity

### Migration 007: Indexes

Create indexes after core tables exist.

Suggested indexes:

```sql
CREATE INDEX idx_games_library_name ON games (library_id, name);
CREATE INDEX idx_games_library_sorting_name ON games (library_id, sorting_name);
CREATE INDEX idx_game_versions_game_latest ON game_versions (game_id, is_latest);
CREATE INDEX idx_game_versions_library_created_at ON game_versions (library_id, created_at);
CREATE INDEX idx_version_artifacts_game_version ON version_artifacts (game_version_id);
CREATE INDEX idx_version_artifacts_checksum ON version_artifacts (checksum);
CREATE INDEX idx_save_snapshots_version_captured_at ON save_snapshots (game_version_id, captured_at DESC);
CREATE INDEX idx_save_snapshots_library_captured_at ON save_snapshots (library_id, captured_at DESC);
CREATE INDEX idx_links_game_id ON links (game_id);
CREATE INDEX idx_metadata_sources_game_provider ON metadata_sources (game_id, provider);
CREATE INDEX idx_metadata_sources_provider_key ON metadata_sources (provider, provider_key);
CREATE INDEX idx_uploads_state_updated_at ON uploads (state, updated_at);
CREATE INDEX idx_uploads_idempotency_key ON uploads (idempotency_key);
CREATE INDEX idx_jobs_state_updated_at ON jobs (state, updated_at);
CREATE INDEX idx_jobs_kind_state ON jobs (kind, state);
CREATE INDEX idx_jobs_upload_id ON jobs (upload_id);
```

Also add a practical uniqueness index:

```sql
CREATE UNIQUE INDEX idx_game_versions_identity
ON game_versions (game_id, version_name, version_code);
```

### Migration 008: Seed Data

Seed the `pc` platform row in an idempotent way.

Suggested outline:

```sql
INSERT INTO platforms (public_id, name, is_enabled, match_priority, created_at, updated_at)
VALUES ('platform_pc', 'pc', 1, 100, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
ON CONFLICT(name) DO NOTHING;
```

### Important Consistency Checks

These are worth validating in application code even if the DB cannot enforce all of them cleanly:

- a `game_version.library_id` should match its parent game's library
- a `save_snapshot.library_id` should match its version's library
- a save snapshot's `game_id` should match the parent version's `game_id`
- `is_latest = 1` should be unique per game within active versions

For `is_latest`, the cleanest approach is:

- maintain it transactionally in application code
- optionally add a partial unique index later if SQLite support and migration shape make it worthwhile

### SQLx Implementation Notes

Practical notes for later implementation:

- enable `PRAGMA foreign_keys = ON`
- prefer explicit SQL migrations instead of schema generation
- keep timestamps generated by the application for consistency across inserts and updates
- use transactions for multi-row operations like import finalization and latest-version updates

## Background Jobs

Run background work inside the backend process at first.

Jobs:

- imports and archive processing
- metadata fetches
- image downloads/resizing
- stale cache cleanup

Persist job state in SQLite so progress survives restarts.

Do not split out a separate worker service yet. That adds operational cost without enough benefit for a single-node personal deployment.

## Authentication

Split the product into two access modes:

- public read-only catalog
- owner-only admin actions and integrations

Recommended auth model for v1:

- no auth for the public catalog
- local username/password with session cookie for the admin UI
- API tokens for Playnite and other integrations
- optional reverse-proxy auth support for admin access

Do not use one global auth mode for every client type.

Treat these separately:

- interactive admin authentication
- programmatic integration authentication

This keeps local development simple and avoids coupling the Playnite plugin to browser-oriented auth flows.

### Auth Modes

For interactive admin access, support:

- `local`
- `proxy`

For integrations, support:

- `token`

Recommended default:

- admin auth mode: `local`
- integration auth mode: `token`

### Admin Auth

`local` mode should provide:

- one owner account in v1
- password hash stored in a secret file or equivalent protected config input
- session cookie authentication for the admin UI

`proxy` mode should provide:

- trusted reverse-proxy header based authentication
- explicit trusted header configuration
- optional fallback disablement for local login if desired later

Do not make reverse-proxy auth mandatory.

### Integration Auth

Playnite should authenticate with API tokens.

Token auth should be:

- scoped to admin/integration operations as needed
- revocable
- independently manageable from browser sessions

This avoids awkward session handling in external clients and keeps integration behavior deployment-neutral.

## Nix Support

Nix is a first-class project constraint from the start.

The repo should be flake-first and expose:

- `devShells` for reproducible local development
- `packages` for the backend, frontend bundle, and combined app artifact
- `checks` for formatting, linting, tests, and NixOS VM tests
- `apps` for running the backend and frontend directly in development
- `nixosModules.gumo` for production deployment
- optional OCI image outputs for future container deployment

Nix should be the source of truth for development, packaging, and deployment wiring.

## Deployment

Primary deployment target: **NixOS module**

Secondary deployment target: **OCI/container image built by Nix**

The app itself must remain platform-neutral. It should run the same way:

- directly from the dev shell
- under the NixOS module
- inside a future OCI container

The NixOS module is a thin wrapper around the app runtime. It should:

- install the package
- render app config
- create state and cache directories
- define the `systemd` service
- expose host-level settings such as firewall and service user

It should not introduce alternate runtime semantics.

For deployment, mounted paths should be enough:

- writable data directory for SQLite, managed storage, and asset cache

Typical runtime paths:

- `/var/lib/gumo/gumo.db`
- `/var/lib/gumo/cache`
- `/var/lib/gumo/library`

OCI support should remain possible by keeping the config model app-native rather than module-native.

## Local Development

The backend and frontend should both be runnable directly from `nix develop`.

Development workflow assumptions:

- backend runs directly from the shell
- frontend runs as a separate Vite dev server
- a repo-local mock storage tree is used for managed library data

Suggested local paths:

- `./.local/gumo/data/`
- `./.local/gumo/cache/`
- `./.local/gumo/library/`

This allows realistic local testing without deploying the NixOS module or touching production storage.

## App Runtime Contract

The application runtime must be independent of NixOS.

The app should accept configuration through a single primary config file.

Recommended format: **TOML**

Why:

- easy to read and hand-edit
- comments are allowed
- straightforward to render from the NixOS module
- well-supported by `serde`
- less error-prone than YAML for long-lived service config

Do not use environment variables as the primary configuration surface.

Use them only for:

- small local overrides in development
- injected secrets where needed

The app should accept configuration for:

- server settings
- storage paths
- managed storage behavior
- configured libraries
- enabled platforms
- authentication settings
- integration settings

The install directory must never be assumed writable.

The frontend should:

- use a dev server in development
- be built to static assets in production
- be served by the backend in packaged deployments

## Config Schema

The config should be app-native and portable across:

- direct local development
- NixOS module deployment
- future OCI/container deployment

Recommended top-level sections:

- `[server]`
- `[storage]`
- `[auth]`
- `[integrations]`
- `[[libraries]]`
- `[[platforms]]`

### `server`

Purpose:

- network and URL behavior

Fields:

- `listen_address`
- `port`
- `trusted_proxies` optional

### `storage`

Purpose:

- runtime paths and managed payload behavior

Fields:

- `database_path`
- `cache_dir`
- `temp_dir` optional
- `archive_format`
- `split_part_size_bytes`
- `deduplicate_by_checksum`

Important rules:

- `database_path` and `cache_dir` must be explicit
- each library `root_path` is its managed storage root
- split archive behavior is a storage policy, not a platform policy by default

### `auth`

Purpose:

- admin and integration authentication

Fields:

- `admin_mode`
- `owner_password_hash_file` optional
- `proxy_user_header` optional
- `proxy_email_header` optional
- `trusted_proxy_headers` optional

The public catalog should not require auth by default.

Recommended values:

- `admin_mode = "local"` or `admin_mode = "proxy"`

Important rules:

- session cookies are only for the admin UI
- API tokens are for Playnite and future automation
- token auth should not depend on interactive login state
- integration tokens should be stored in the database, not in config files

### `integrations`

Purpose:

- external client behavior and feature toggles

Suggested subsections:

- `[integrations.playnite]`

Fields for Playnite:

- `enabled`
- `allow_uploads`
- `default_platform`
- `token_label_prefix` optional

### `libraries`

Purpose:

- define managed library roots

This should be an array of library entries.

Fields:

- `name`
- `root_path`
- `platform`
- `enabled`
- `visibility`

Important rule:

- libraries are managed storage roots, not scanned external paths

### `platforms`

Purpose:

- enable platform modules and platform-specific settings

This should be an array of platform entries.

Fields:

- `id`
- `enabled`
- `match_priority`

Optional platform-specific settings can live in nested tables where needed.

For now, only `pc` needs to exist, but the schema should assume more platforms will be added later.

## Example Config

Illustrative shape:

```toml
[server]
listen_address = "127.0.0.1"
port = 8080

[logging]
level = "debug"

[frontend]
dev_port = 4173

[storage]
database_path = "./.local/gumo/data/gumo.db"
cache_dir = "./.local/gumo/cache"
split_part_size_bytes = 2147483648
deduplicate_by_checksum = true

[auth]
admin_mode = "local"
owner_password_hash_file = "./.local/gumo/secrets/admin-password-hash"

[integrations.playnite]
enabled = true
allow_uploads = true
default_platform = "pc"

[[platforms]]
id = "pc"
enabled = true
match_priority = 100

[[libraries]]
name = "managed-pc"
root_path = "./.local/gumo/library"
platform = "pc"
enabled = true
visibility = "private"
```

## Config Ownership Boundaries

For local development, `nix run .#dev-init` or `just dev-init` should create the repo-local state tree and a default local admin password hash using the password `admin` if that file does not already exist.

App config should own:

- server behavior
- logging behavior
- frontend dev behavior
- storage behavior
- library definitions
- platform enablement
- integration behavior

The NixOS module should own:

- whether the service is enabled
- which package is used
- service user/group
- host firewall behavior
- derivation of writable paths from `dataDir`

The module should pass most app behavior through a `settings` option that maps closely to the TOML schema.

## NixOS Module Contract

The NixOS module should be a wrapper around the app runtime contract.

Likely module options:

- `enable`
- `package`
- `user`
- `group`
- `dataDir`
- `openFirewall`
- `settings`

The module should derive concrete runtime paths from `dataDir`, for example:

- database path under `${dataDir}`
- asset cache under `${dataDir}/assets`

Host-level concerns belong in the module. App behavior belongs in the app config.

## NixOS Module Testing

Do not rely on real deployments to test module changes.

Use NixOS VM tests in flake `checks`.

These tests should boot ephemeral VMs that enable `nixosModules.gumo` and verify:

- service starts successfully
- rendered configuration is correct
- writable state directories exist with correct permissions
- the configured port is listening
- the SQLite database is created in the expected location
- a fixture managed storage root can be passed into the service

This should be complemented by:

- package build checks
- normal backend/frontend test suites
- evaluation-level validation for module options where useful

## Internal Project Layout

Recommended repo structure:

```text
gumo/
  flake.nix
  nix/
    devshell.nix
    packages.nix
    checks.nix
    module.nix
    container.nix
  backend/
    Cargo.toml
    src/
      main.rs
      api/
      domain/
      db/
      metadata/
      jobs/
      cache/
      auth/
  web/
    package.json
    src/
      routes/
      components/
      lib/
  docs/
    architecture.md
```

If you want tighter Rust modularity later, convert `backend/` into a Cargo workspace. Do not start with multiple crates unless the boundaries become painful.

## Key Decisions

These are the architecture decisions I recommend locking in now:

1. Flake-first monorepo with first-class Nix support
2. Rust backend with `axum`
3. React + Vite frontend served as static assets by the backend in packaged deployments
4. SQLite as the only database for v1, accessed through `sqlx`
5. Gumo supports self-managed libraries only in v1
6. Managed game payloads are stored as archives, with split parts when necessary
7. Logical games support multiple versions
8. External library scanning is out of scope for v1
9. Single-process app with in-process background jobs
10. Manual overrides always win over imported metadata
11. Platform support is modular, but not dynamic-plugin-based in v1
12. Public catalog is unauthenticated; admin UI uses local session auth by default
13. Playnite plugin uses API token auth
14. Reverse-proxy auth remains an optional admin mode
15. NixOS module is the primary deployment wrapper
16. OCI/container deployment remains a supported future option
17. Backend and frontend must run directly from the dev shell using local mock storage

## Things To Avoid Early

- microservices
- separate queue infrastructure
- Postgres by default
- SurrealDB
- Diesel
- GraphQL
- dynamic runtime plugin loading in v1
- external library scanning in v1
- over-modeling emulator/launcher behavior before cataloging works

## Recommended First Milestones

1. Managed import pipeline that stores archives into SQLite-backed library metadata
2. Read-only API for listing and viewing games
3. Basic public web catalog
4. Manual metadata editing/overrides
5. Playnite upload integration
6. Metadata provider integration
