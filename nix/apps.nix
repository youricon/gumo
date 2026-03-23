{ pkgs, self }:
let
  backendApp = pkgs.writeShellApplication {
    name = "gumo-backend-dev";
    runtimeInputs = [ pkgs.cargo pkgs.rustc ];
    text = ''
      if [ ! -f "$PWD/.local/gumo/config.toml" ]; then
        echo "Missing $PWD/.local/gumo/config.toml"
        echo "Run: nix run .#dev-init"
        exit 1
      fi

      export GUMO_CONFIG_PATH="$PWD/.local/gumo/config.toml"
      exec cargo run --manifest-path "$PWD/backend/Cargo.toml" -- --config "$GUMO_CONFIG_PATH"
    '';
  };

  frontendApp = pkgs.writeShellApplication {
    name = "gumo-frontend-dev";
    runtimeInputs = [ pkgs.gawk pkgs.nodejs_22 ];
    text = ''
      config_file="$PWD/.local/gumo/config.toml"

      if [ ! -f "$config_file" ]; then
        echo "Missing $config_file"
        echo "Run: nix run .#dev-init"
        exit 1
      fi

      if [ ! -d "$PWD/web/node_modules" ]; then
        echo "Missing frontend dependencies in $PWD/web/node_modules"
        echo "Run: npm --prefix web install"
        exit 1
      fi

      listen_address="$(
        awk '
          /^\[server\]/ { in_server = 1; next }
          /^\[/ && $0 != "[server]" { in_server = 0 }
          in_server && $1 ~ /^listen_address$/ {
            value = $0
            sub(/^[^=]*=[[:space:]]*/, "", value)
            gsub(/"/, "", value)
            print value
            exit
          }
        ' "$config_file"
      )"

      server_port="$(
        awk '
          /^\[server\]/ { in_server = 1; next }
          /^\[/ && $0 != "[server]" { in_server = 0 }
          in_server && $1 ~ /^port$/ {
            value = $0
            sub(/^[^=]*=[[:space:]]*/, "", value)
            gsub(/"/, "", value)
            print value
            exit
          }
        ' "$config_file"
      )"

      frontend_listen_address="$(
        awk '
          /^\[frontend\]/ { in_frontend = 1; next }
          /^\[/ && $0 != "[frontend]" { in_frontend = 0 }
          in_frontend && $1 ~ /^dev_listen_address$/ {
            value = $0
            sub(/^[^=]*=[[:space:]]*/, "", value)
            gsub(/"/, "", value)
            print value
            exit
          }
        ' "$config_file"
      )"

      frontend_port="$(
        awk '
          /^\[frontend\]/ { in_frontend = 1; next }
          /^\[/ && $0 != "[frontend]" { in_frontend = 0 }
          in_frontend && $1 ~ /^dev_port$/ {
            value = $0
            sub(/^[^=]*=[[:space:]]*/, "", value)
            gsub(/"/, "", value)
            print value
            exit
          }
        ' "$config_file"
      )"

      listen_address="''${listen_address:-127.0.0.1}"
      server_port="''${server_port:-8080}"
      frontend_listen_address="''${frontend_listen_address:-$listen_address}"
      frontend_port="''${frontend_port:-4173}"

      export GUMO_API_ORIGIN="http://$listen_address:$server_port"
      exec npm --prefix "$PWD/web" run dev -- --host "$frontend_listen_address" --port "$frontend_port"
    '';
  };

  initApp = pkgs.writeShellApplication {
    name = "gumo-dev-init";
    runtimeInputs = [ pkgs.coreutils ];
    text = ''
      default_password="admin"
      password_file="./.local/gumo/secrets/admin-password-hash"

      mkdir -p ./.local/gumo/data
      mkdir -p ./.local/gumo/cache
      mkdir -p ./.local/gumo/library
      mkdir -p ./.local/gumo/secrets
      mkdir -p ./.local/gumo/tmp

      if [ ! -f ./.local/gumo/config.toml ]; then
        cp ./config/gumo.example.toml ./.local/gumo/config.toml
      fi

      if [ ! -f "$password_file" ]; then
        printf 'sha256:%s\n' "$(printf '%s' "$default_password" | sha256sum | cut -d' ' -f1)" > "$password_file"
        echo "Created $password_file with default password: $default_password"
      fi

      echo "Initialized ./.local/gumo/"
    '';
  };
in
{
  backend = {
    type = "app";
    program = "${backendApp}/bin/gumo-backend-dev";
  };

  frontend = {
    type = "app";
    program = "${frontendApp}/bin/gumo-frontend-dev";
  };

  dev-init = {
    type = "app";
    program = "${initApp}/bin/gumo-dev-init";
  };

  default = {
    type = "app";
    program = "${backendApp}/bin/gumo-backend-dev";
  };
}
