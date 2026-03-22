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
    runtimeInputs = [ pkgs.nodejs_22 ];
    text = ''
      if [ ! -d "$PWD/web/node_modules" ]; then
        echo "Missing frontend dependencies in $PWD/web/node_modules"
        echo "Run: npm --prefix web install"
        exit 1
      fi

      exec npm --prefix "$PWD/web" run dev -- --host 127.0.0.1 --port 4173
    '';
  };

  initApp = pkgs.writeShellApplication {
    name = "gumo-dev-init";
    runtimeInputs = [ pkgs.coreutils ];
    text = ''
      mkdir -p ./.local/gumo/data
      mkdir -p ./.local/gumo/assets
      mkdir -p ./.local/gumo/storage
      mkdir -p ./.local/gumo/secrets
      mkdir -p ./.local/gumo/tmp

      if [ ! -f ./.local/gumo/config.toml ]; then
        cp ./config/gumo.example.toml ./.local/gumo/config.toml
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
