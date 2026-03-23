{ pkgs, self }:
(import "${pkgs.path}/nixos/tests/make-test-python.nix" {
  name = "gumo-nixos-module";

  nodes.machine = { pkgs, ... }: {
    imports = [ self.nixosModules.gumo ];

    environment.systemPackages = [ pkgs.curl ];

    services.gumo = {
      enable = true;
      openFirewall = true;
      settings = {
        server = {
          listen_address = "127.0.0.1";
          port = 8080;
        };
        auth = {
          admin_mode = "proxy";
          proxy_user_header = "X-Remote-User";
        };
        libraries = [
          {
            name = "main";
            root_path = "/var/lib/gumo/library";
            platform = "pc";
            visibility = "public";
            enabled = true;
          }
        ];
        platforms = [
          {
            id = "pc";
            enabled = true;
            match_priority = 100;
          }
        ];
      };
    };
  };

  testScript = ''
    start_all()

    machine.wait_for_unit("gumo.service")
    machine.wait_for_open_port(8080)
    machine.succeed("systemctl is-active gumo.service")

    machine.succeed("test -f /etc/gumo/gumo.toml")
    machine.succeed("grep -F '/var/lib/gumo/data/gumo.db' /etc/gumo/gumo.toml")
    machine.succeed("grep -F '/var/lib/gumo/cache' /etc/gumo/gumo.toml")
    machine.succeed("grep -F '/var/lib/gumo/library' /etc/gumo/gumo.toml")
    machine.succeed("grep -F 'admin_mode = \"proxy\"' /etc/gumo/gumo.toml")

    machine.succeed("test -d /var/lib/gumo/data")
    machine.succeed("test -d /var/lib/gumo/cache")
    machine.succeed("test -d /var/lib/gumo/library")
    machine.succeed("test -d /var/lib/gumo/tmp")
    machine.succeed("test -f /var/lib/gumo/data/gumo.db")

    machine.succeed("curl --fail http://127.0.0.1:8080/api/health")
  '';
}) {
  inherit pkgs;
  system = pkgs.stdenv.hostPlatform.system;
}
