# Task 06: Library Sync And Metadata

## Status

Not started

## Goal

Sync Gumo-managed games into Playnite and support metadata updates using the Playnite-aligned API contract.

## Depends On

- 05 API Client And Auth

## Deliverables

- game import/listing integration
- mapping between Gumo game/version resources and Playnite models
- metadata patch support
- local record linkage strategy

## Steps

- Fetch game and version data from Gumo.
- Represent Gumo-managed titles as Playnite library entries.
- Implement metadata refresh/update flow using the integration endpoints.
- Decide and document how local Playnite entries map back to Gumo IDs.

## Acceptance Criteria

- Gumo games can appear in Playnite through the plugin.
- Metadata updates from Playnite reach Gumo through the intended contract.
- Game/version identity is stable across syncs.

## Tracking Checklist

- [ ] Game sync flow added
- [ ] Version mapping added
- [ ] Metadata patch flow added
- [ ] Identity/linkage strategy implemented

## Notes

- Follow Playnite field naming and semantics closely.
