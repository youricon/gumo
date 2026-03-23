# Task 01: Repo And Project Setup

## Status

Not started

## Goal

Create the monorepo location and project boundaries for the Playnite plugin without coupling it to the Rust or web runtime.

## Depends On

- none

## Deliverables

- `playnite-plugin/` directory in the monorepo
- initial solution/project layout
- plugin-specific README
- clear separation between plugin code, packaging assets, and scripts

## Steps

- Create `playnite-plugin/` in the repo root.
- Define the project structure for source, manifest, packaging assets, and scripts.
- Keep the plugin isolated as an HTTP client of Gumo rather than shared runtime code.
- Add minimal plugin documentation in the plugin directory.

## Acceptance Criteria

- The plugin has a dedicated place in the monorepo.
- The project layout is suitable for Windows development and CI.
- Plugin code does not introduce accidental coupling to backend implementation details.

## Tracking Checklist

- [ ] Plugin directory created
- [ ] Project layout defined
- [ ] Plugin-local README added
- [ ] Boundary with Gumo API documented

## Notes

- Treat the plugin as a separate client project inside the monorepo.
