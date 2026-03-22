{ self }:
{ config, lib, pkgs, ... }:
let
  cfg = config.services.gumo;
  tomlFormat = pkgs.formats.toml { };
  renderedConfig = tomlFormat.generate "gumo-config.toml" cfg.settings;
in
{
  options.services.gumo = {
    enable = lib.mkEnableOption "Gumo service";

    package = lib.mkOption {
      type = lib.types.package;
      default = self.packages.${pkgs.system}.gumo;
      defaultText = lib.literalExpression "self.packages.\${pkgs.system}.gumo";
      description = "Package providing the gumo backend binary.";
    };

    user = lib.mkOption {
      type = lib.types.str;
      default = "gumo";
      description = "System user running the gumo service.";
    };

    group = lib.mkOption {
      type = lib.types.str;
      default = "gumo";
      description = "System group running the gumo service.";
    };

    dataDir = lib.mkOption {
      type = lib.types.path;
      default = "/var/lib/gumo";
      description = "Writable state directory for the service.";
    };

    openFirewall = lib.mkOption {
      type = lib.types.bool;
      default = false;
      description = "Open the configured HTTP port in the firewall.";
    };

    settings = lib.mkOption {
      type = tomlFormat.type;
      default = { };
      description = "App-native Gumo settings rendered to TOML.";
    };
  };

  config = lib.mkIf cfg.enable {
    users.users = lib.mkIf (cfg.user == "gumo") {
      gumo = {
        isSystemUser = true;
        group = cfg.group;
        home = cfg.dataDir;
        createHome = true;
      };
    };

    users.groups = lib.mkIf (cfg.group == "gumo") {
      gumo = { };
    };

    systemd.services.gumo = {
      description = "Gumo backend service";
      wantedBy = [ "multi-user.target" ];
      after = [ "network.target" ];
      serviceConfig = {
        User = cfg.user;
        Group = cfg.group;
        StateDirectory = "gumo";
        WorkingDirectory = cfg.dataDir;
        ExecStart = "${cfg.package}/bin/gumo --config ${renderedConfig}";
        Restart = "on-failure";
      };
    };

    networking.firewall.allowedTCPPorts = lib.mkIf cfg.openFirewall [
      (cfg.settings.server.port or 8080)
    ];
  };
}
