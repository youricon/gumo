# Task 05: API Client And Auth

## Status

Not started

## Goal

Implement the plugin-side HTTP client for the Gumo integration API and the token auth flow.

## Depends On

- 04 Plugin Scaffold And Configuration

## Deliverables

- API client wrapper
- token-authenticated requests
- typed handling for integration resources
- useful error mapping for plugin UI/logging

## Steps

- Implement a small API client around the Gumo integration endpoints.
- Send bearer-token auth for all integration requests.
- Define client-side models for games, versions, uploads, jobs, installs, and saves.
- Map transport and API errors into user-visible plugin errors and logs.

## Acceptance Criteria

- The plugin can authenticate to Gumo successfully.
- The API client is structured enough to support later flows without duplication.
- Errors are clear enough for debugging.

## Tracking Checklist

- [ ] HTTP client added
- [ ] Token auth wired
- [ ] Core resource models added
- [ ] Error mapping added

## Notes

- Keep the plugin boundary aligned with the integration API, not internal backend tables.
