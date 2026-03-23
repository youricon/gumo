{ pkgs, self }:
let
  initScript = pkgs.writeShellApplication {
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
  default = pkgs.mkShell {
    packages = with pkgs; [
      rustc
      cargo
      rustfmt
      clippy
      sqlite
      sqlx-cli
      nodejs_22
      pkg-config
      git
      just
      initScript
    ];

    shellHook = ''
      export GUMO_LOCAL_ROOT="$PWD/.local/gumo"
      export GUMO_CONFIG_PATH="$GUMO_LOCAL_ROOT/config.toml"
      export GUMO_DATA_DIR="$GUMO_LOCAL_ROOT/data"
      export GUMO_CACHE_DIR="$GUMO_LOCAL_ROOT/cache"
      export GUMO_LIBRARY_DIR="$GUMO_LOCAL_ROOT/library"
    '';
  };
}
