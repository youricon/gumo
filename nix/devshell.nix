{ pkgs, self }:
let
  initScript = pkgs.writeShellApplication {
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
      export GUMO_ASSET_DIR="$GUMO_LOCAL_ROOT/assets"
      export GUMO_STORAGE_DIR="$GUMO_LOCAL_ROOT/storage"
    '';
  };
}
