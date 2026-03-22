{
  description = "Gumo monorepo";

  inputs = {
    nixpkgs.url = "github:NixOS/nixpkgs/nixos-25.05";
  };

  outputs = { self, nixpkgs }:
    let
      lib = nixpkgs.lib;
      systems = [
        "x86_64-linux"
        "aarch64-linux"
      ];
      forAllSystems = f:
        lib.genAttrs systems (system: f (import nixpkgs { inherit system; }));
    in
    {
      packages = forAllSystems (pkgs: import ./nix/packages.nix {
        inherit pkgs self;
      });

      devShells = forAllSystems (pkgs: import ./nix/devshell.nix {
        inherit pkgs self;
      });

      checks = forAllSystems (pkgs: import ./nix/checks.nix {
        inherit pkgs self;
      });

      apps = forAllSystems (pkgs: import ./nix/apps.nix {
        inherit pkgs self;
      });

      nixosModules.gumo = import ./nix/module.nix { inherit self; };
    };
}
