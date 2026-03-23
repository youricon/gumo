{ pkgs, self }:
let
  packages = self.packages.${pkgs.stdenv.hostPlatform.system};
in
{
  backend-package = packages.gumo-backend;
  web-package = packages.gumo-web;
  combined-package = packages.gumo;
  local-dev-docs = pkgs.runCommand "gumo-local-dev-docs-check" { } ''
    test -f ${../docs/local-development.md}
    test -f ${../config/gumo.example.toml}
    mkdir -p "$out"
  '';
  vm-module = import ./vm-test.nix {
    inherit pkgs self;
  };
}
