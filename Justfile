set shell := ["bash", "-eu", "-o", "pipefail", "-c"]

default:
  @just --list

dev-init:
  nix run .#dev-init

backend:
  nix run .#backend

frontend:
  nix run .#frontend

admin-password password:
  #!/usr/bin/env bash
  set -euo pipefail
  mkdir -p ./.local/gumo/secrets
  printf 'sha256:%s\n' "$(printf '%s' {{quote(password)}} | sha256sum | cut -d' ' -f1)" > ./.local/gumo/secrets/admin-password-hash
  echo "Wrote ./.local/gumo/secrets/admin-password-hash"

admin-password-prompt:
  #!/usr/bin/env bash
  set -euo pipefail
  mkdir -p ./.local/gumo/secrets
  printf 'Admin password: ' >&2
  IFS= read -r -s password
  printf '\n' >&2
  printf 'sha256:%s\n' "$(printf '%s' "$password" | sha256sum | cut -d' ' -f1)" > ./.local/gumo/secrets/admin-password-hash
  echo "Wrote ./.local/gumo/secrets/admin-password-hash"
