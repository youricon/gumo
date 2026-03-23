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
  mkdir -p ./.local/gumo/secrets
  printf 'sha256:%s\n' "$$(printf '%s' '{{password}}' | sha256sum | cut -d' ' -f1)" > ./.local/gumo/secrets/admin-password-hash
  echo "Wrote ./.local/gumo/secrets/admin-password-hash"

admin-password-prompt:
  mkdir -p ./.local/gumo/secrets
  read -r -s -p "Admin password: " password; echo
  printf 'sha256:%s\n' "$$(printf '%s' "$$password" | sha256sum | cut -d' ' -f1)" > ./.local/gumo/secrets/admin-password-hash
  echo "Wrote ./.local/gumo/secrets/admin-password-hash"
