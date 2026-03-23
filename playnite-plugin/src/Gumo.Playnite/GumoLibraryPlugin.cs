using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;

namespace Gumo.Playnite
{
    public sealed class GumoLibraryPlugin : LibraryPlugin
    {
        private static readonly ILogger Logger = LogManager.GetLogger();
        private readonly GumoLibrarySettings settings;
        private readonly CancellationTokenSource startupProbeCancellation = new CancellationTokenSource();

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
            Logger.Info("Gumo GetGames requested. Library sync is not implemented yet, returning an empty result.");
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
            return new GumoLibrarySettingsView(settings);
        }

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            Logger.Info($"Gumo plugin loaded. Server URL: {settings.GumoServerUrl}");
            if (!settings.HasConnectionSettings())
            {
                Logger.Warn("Gumo plugin settings are incomplete. Configure the server URL and API token before using the plugin.");
                return;
            }

            Task.Run(() => ProbeConnectionAsync(startupProbeCancellation.Token));
        }

        public override void OnApplicationStopped(OnApplicationStoppedEventArgs args)
        {
            startupProbeCancellation.Cancel();
            startupProbeCancellation.Dispose();
            base.OnApplicationStopped(args);
        }

        internal GumoApiClient CreateApiClient()
        {
            if (!settings.HasConnectionSettings())
            {
                throw new InvalidOperationException("Gumo connection settings are incomplete.");
            }

            return new GumoApiClient(settings.NormalizedServerUrl(), settings.ApiToken);
        }

        private async Task ProbeConnectionAsync(CancellationToken cancellationToken)
        {
            try
            {
                using (var client = CreateApiClient())
                {
                    var gameCount = await client.ProbeAsync(cancellationToken);
                    Logger.Info($"Gumo API probe succeeded. Visible game count: {gameCount}.");
                }
            }
            catch (OperationCanceledException)
            {
                Logger.Info("Gumo API probe canceled during shutdown.");
            }
            catch (GumoApiException exception)
            {
                Logger.Error(
                    $"Gumo API probe failed with {(int)exception.StatusCode} {exception.StatusCode}: {exception.ApiMessage}");
            }
            catch (Exception exception)
            {
                Logger.Error($"Unexpected failure while probing the Gumo API: {exception}");
            }
        }
    }
}
