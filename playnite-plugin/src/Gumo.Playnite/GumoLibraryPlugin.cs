using System;
using System.Collections.Generic;
using System.Windows.Controls;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;

namespace Gumo.Playnite
{
    public sealed class GumoLibraryPlugin : LibraryPlugin
    {
        private static readonly ILogger Logger = LogManager.GetLogger();
        private readonly GumoLibrarySettings settings;

        public GumoLibraryPlugin(IPlayniteAPI api) : base(api)
        {
            settings = new GumoLibrarySettings(this);
            Properties = new LibraryPluginProperties
            {
                HasSettings = true,
                HasCustomizedGameImport = true,
            };
        }

        public override Guid Id { get; } = Guid.Parse("0DBBE6A0-C352-4E98-AE54-93A1F65476D3");

        public override string Name => "Gumo";

        public override IEnumerable<GameMetadata> GetGames(LibraryGetGamesArgs args)
        {
            Logger.Info("Gumo GetGames requested. Returning an empty placeholder result.");
            return Array.Empty<GameMetadata>();
        }

        public override IEnumerable<Game> ImportGames(LibraryImportGamesArgs args)
        {
            Logger.Info("Gumo customized import requested. Returning an empty placeholder result.");
            return Array.Empty<Game>();
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return settings;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new GumoLibrarySettingsView();
        }

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            Logger.Info($"Gumo plugin loaded. Server URL: {settings.GumoServerUrl}");
        }
    }
}
