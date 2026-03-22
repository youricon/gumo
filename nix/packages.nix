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

  web = pkgs.stdenvNoCC.mkDerivation {
    pname = "gumo-web";
    version = "0.1.0";
    src = ../web;

    installPhase = ''
      runHook preInstall
      mkdir -p "$out/share/gumo/web"
      cp -r ./* "$out/share/gumo/web/"
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
