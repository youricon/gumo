# Gumo Playnite Plugin

This directory contains the Playnite library plugin for Gumo.

## Scope

The plugin is a separate client of the Gumo HTTP API. It must not depend on the Rust backend internals or share runtime code with the web app.

Responsibilities:

- authenticate to Gumo using integration tokens
- import and synchronize Gumo-managed games into Playnite
- upload game payloads and save snapshots
- poll upload/archive jobs
- install archived game versions locally
- restore version-specific save snapshots

## Layout

- `src/Gumo.Playnite/`
  - Playnite plugin project
  - plugin source code
  - `extension.yaml` copied into build output
- `scripts/`
  - Windows-oriented helper scripts for packaging and local developer installation
- `packaging/`
  - packaging notes and future release assets

## Development

Primary development environment:

- Windows
- Visual Studio 2022 or Rider
- Playnite installed locally

Reference:

- [playnite-plugin-development.md](/home/isaac/workspace/gumo/docs/playnite-plugin-development.md)

## Current State

This is an initial scaffold only.

Implemented:

- `LibraryPlugin` project targeting `.NET Framework 4.6.2`
- manifest file
- settings object and settings view
- logging baseline
- placeholder library import methods

Not implemented yet:

- Gumo API client
- authentication flow
- import/upload/install/save behavior
- packaging validation in Playnite
