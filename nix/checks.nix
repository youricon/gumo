{ pkgs, self }:
let
  packages = self.packages.${pkgs.system};
in
{
  backend-build = packages.gumo-backend;
  web-layout = pkgs.runCommand "gumo-web-layout-check" { } ''
    test -f ${../web/package.json}
    test -f ${../web/index.html}
    test -f ${../web/src/main.tsx}
    mkdir -p "$out"
  '';
  local-dev-docs = pkgs.runCommand "gumo-local-dev-docs-check" { } ''
    test -f ${../docs/local-development.md}
    test -f ${../config/gumo.example.toml}
    mkdir -p "$out"
  '';
}
