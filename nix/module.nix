{ self }:
{ config, lib, pkgs, ... }:
let
  cfg = config.services.gumo;
  tomlFormat = pkgs.formats.toml { };
  defaultSettings = {
    server = {
      listen_address = "127.0.0.1";
      port = 8080;
      trusted_proxies = [ ];
    };
    storage = {
      database_path = "${cfg.dataDir}/data/gumo.db";
      cache_dir = "${cfg.dataDir}/cache";
      temp_dir = "${cfg.dataDir}/tmp";
      split_part_size_bytes = 2147483648;
      deduplicate_by_checksum = true;
    };
    auth = {
      admin_mode = "proxy";
      proxy_user_header = "X-Remote-User";
      proxy_email_header = "X-Remote-Email";
      trusted_proxy_headers = [ "X-Remote-User" "X-Remote-Email" ];
    };
    integrations = {
      playnite = {
        enabled = false;
        allow_uploads = false;
      };
    };
  };
  finalSettings = lib.recursiveUpdate defaultSettings cfg.settings;
  renderedConfig = tomlFormat.generate "gumo.toml" finalSettings;
  libraryDirs = map (library: library.root_path) (finalSettings.libraries or [ ]);
in
{
  options.services.gumo = {
    enable = lib.mkEnableOption "Gumo service";

    package = lib.mkOption {
      type = lib.types.package;
      default = self.packages.${pkgs.stdenv.hostPlatform.system}.gumo;
      defaultText = lib.literalExpression "self.packages.\${pkgs.stdenv.hostPlatform.system}.gumo";
      description = "Package providing the Gumo backend binary and packaged frontend assets.";
    };

    user = lib.mkOption {
      type = lib.types.str;
      default = "gumo";
      description = "System user running the Gumo service.";
    };

    group = lib.mkOption {
      type = lib.types.str;
      default = "gumo";
      description = "System group running the Gumo service.";
    };

    dataDir = lib.mkOption {
      type = lib.types.str;
      default = "/var/lib/gumo";
      description = "Writable state directory for database, archives, assets, temp files, and optional secrets.";
    };

    openFirewall = lib.mkOption {
      type = lib.types.bool;
      default = false;
      description = "Open the configured HTTP port in the firewall.";
    };

    settings = lib.mkOption {
      type = tomlFormat.type;
      default = { };
      example = lib.literalExpression ''
        {
          auth = {
            admin_mode = "proxy";
            proxy_user_header = "X-Remote-User";
          };
          libraries = [
            {
              name = "main";
              root_path = "/srv/gumo/library";
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
        }
      '';
      description = "App-native Gumo settings rendered directly to TOML. Storage paths default to subdirectories of dataDir.";
    };
  };

  config = lib.mkIf cfg.enable {
    assertions = [
      {
        assertion = (finalSettings ? libraries) && finalSettings.libraries != [ ];
        message = "services.gumo.settings.libraries must define at least one managed library.";
      }
      {
        assertion = (finalSettings ? platforms) && finalSettings.platforms != [ ];
        message = "services.gumo.settings.platforms must define at least one platform.";
      }
      {
        assertion = (finalSettings ? auth) && (finalSettings.auth ? admin_mode);
        message = "services.gumo.settings.auth.admin_mode must be set, or rely on the module default proxy auth.";
      }
    ];

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

    environment.etc."gumo/gumo.toml".source = renderedConfig;

    systemd.tmpfiles.rules = [
      "d ${cfg.dataDir} 0750 ${cfg.user} ${cfg.group} -"
      "d ${cfg.dataDir}/data 0750 ${cfg.user} ${cfg.group} -"
      "d ${cfg.dataDir}/cache 0750 ${cfg.user} ${cfg.group} -"
      "d ${cfg.dataDir}/tmp 0750 ${cfg.user} ${cfg.group} -"
      "d ${cfg.dataDir}/secrets 0750 ${cfg.user} ${cfg.group} -"
    ] ++ map (path: "d ${path} 0750 ${cfg.user} ${cfg.group} -") libraryDirs;

    systemd.services.gumo = {
      description = "Gumo backend service";
      wantedBy = [ "multi-user.target" ];
      after = [ "network.target" ];
      wants = [ "network.target" ];
      serviceConfig = {
        User = cfg.user;
        Group = cfg.group;
        WorkingDirectory = cfg.dataDir;
        ExecStart = "${cfg.package}/bin/gumo --config /etc/gumo/gumo.toml";
        Restart = "on-failure";
        RestartSec = 2;
        UMask = "0027";
      };
    };

    networking.firewall.allowedTCPPorts = lib.mkIf cfg.openFirewall [
      (finalSettings.server.port or 8080)
    ];
  };
}
