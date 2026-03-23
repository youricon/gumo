{ pkgs, self }:
let
  backend = pkgs.rustPlatform.buildRustPackage {
    pname = "gumo-backend";
    version = "0.1.0";
    src = ../.;
    cargoRoot = "backend";
    buildAndTestSubdir = "backend";
    cargoLock.lockFile = ../backend/Cargo.lock;

    meta = {
      description = "Gumo backend placeholder package";
      mainProgram = "gumo";
    };
  };

  web = pkgs.buildNpmPackage {
    pname = "gumo-web";
    version = "0.1.0";
    src = ../web;
    npmDepsHash = "sha256-P7GxJvl9ztQYda8Q4csOlWoFMkYY/k+TzQWGjsIYZS8=";

    buildPhase = ''
      runHook preBuild
      npm run build
      runHook postBuild
    '';

    installPhase = ''
      runHook preInstall
      mkdir -p "$out/share/gumo/web"
      cp -r dist/* "$out/share/gumo/web/"
      runHook postInstall
    '';
  };

  combined = pkgs.symlinkJoin {
    name = "gumo";
    paths = [ backend web ];
  };
in
{
  default = combined;
  gumo = combined;
  gumo-backend = backend;
  gumo-web = web;
}
