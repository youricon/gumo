using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using Microsoft.Win32;
using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;

namespace Gumo.Playnite
{
    public sealed class GumoLibraryPlugin : LibraryPlugin
    {
        private static readonly ILogger Logger = LogManager.GetLogger();
        private const long FolderUploadTargetPartSizeBytes = 8L * 1024 * 1024 * 1024;
        private readonly GumoLibrarySettings settings;
        private readonly CancellationTokenSource startupProbeCancellation = new CancellationTokenSource();
        private bool localUploadSaveConfigurationCanceled;

        public GumoLibraryPlugin(IPlayniteAPI api) : base(api)
        {
            settings = new GumoLibrarySettings(this);
            Properties = new LibraryPluginProperties
            {
                HasSettings = true,
                HasCustomizedGameImport = false,
            };
        }

        public override Guid Id { get; } = Guid.Parse("0DBBE6A0-C352-4E98-AE54-93A1F65476D3");

        public override string Name => "Gumo";

        public override IEnumerable<GameMetadata> GetGames(LibraryGetGamesArgs args)
        {
            if (!settings.HasConnectionSettings())
            {
                Logger.Warn("Gumo GetGames skipped because plugin settings are incomplete.");
                return Array.Empty<GameMetadata>();
            }

            try
            {
                using (var client = CreateApiClient())
                {
                    var cancellationToken = CancellationToken.None;
                    var games = client.GetGamesAsync(cancellationToken).GetAwaiter().GetResult();
                    var filteredGames = games
                        .Where(game => !settings.ImportPublicGamesOnly || string.Equals(game.Visibility, "public", StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    var metadata = new List<GameMetadata>(filteredGames.Count);
                    foreach (var game in filteredGames)
                    {
                        var versions = client.GetVersionsAsync(game.Id, cancellationToken).GetAwaiter().GetResult();
                        metadata.Add(GumoMapper.ToGameMetadata(NormalizeGumoGameMediaUrls(game), versions));
                    }

                    Logger.Info($"Gumo GetGames imported {metadata.Count} game metadata records.");
                    return metadata;
                }
            }
            catch (GumoApiException exception)
            {
                Logger.Error(
                    $"Gumo GetGames failed with {(int)exception.StatusCode} {exception.StatusCode}: {exception.ApiMessage}");
            }
            catch (Exception exception)
            {
                Logger.Error($"Unexpected failure while importing Gumo games: {exception}");
            }

            return Array.Empty<GameMetadata>();
        }

        public override IEnumerable<Game> ImportGames(LibraryImportGamesArgs args)
        {
            Logger.Info("Gumo customized import hook is disabled. Use the main menu upload action instead.");
            return Array.Empty<Game>();
        }

        public override IEnumerable<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs args)
        {
            return new[]
            {
                new MainMenuItem
                {
                    Description = "Upload game archive to Gumo",
                    MenuSection = "@",
                    Action = _ => UploadGameArchiveFromMenu(),
                }
            };
        }

        public override IEnumerable<GameMenuItem> GetGameMenuItems(GetGameMenuItemsArgs args)
        {
            if (args?.Games == null || args.Games.Count == 0)
            {
                return Enumerable.Empty<GameMenuItem>();
            }

            var items = new List<GameMenuItem>();

            if (args.Games.Any(IsGumoGame))
            {
                items.Add(new GameMenuItem
                {
                    Description = "Push selected metadata to Gumo",
                    Action = _ => PushMetadataToGumo(args.Games),
                });
            }

            if (args.Games.Count == 1 && IsGumoGame(args.Games[0]))
            {
                items.Add(new GameMenuItem
                {
                    Description = "Configure save backup",
                    Action = _ => ConfigureSaveDirectory(args.Games[0]),
                });

                if (CanManageSaveSnapshots(args.Games[0]))
                {
                    items.Add(new GameMenuItem
                    {
                        Description = "Backup save snapshot to Gumo",
                        Action = _ => BackupSaveSnapshotToGumo(args.Games[0]),
                    });
                    items.Add(new GameMenuItem
                    {
                        Description = "Restore save snapshot from Gumo",
                        Action = _ => RestoreSaveSnapshotFromGumo(args.Games[0]),
                    });
                }
            }

            if (args.Games.Any(CanUploadLocalGameToGumo))
            {
                items.Add(new GameMenuItem
                {
                    Description = "Upload selected local game(s) to Gumo",
                    Action = _ => UploadSelectedLocalGamesToGumo(args.Games),
                });
            }

            return items;
        }

        public override IEnumerable<InstallController> GetInstallActions(GetInstallActionsArgs args)
        {
            if (args?.Game == null || !IsGumoGame(args.Game))
            {
                return Enumerable.Empty<InstallController>();
            }

            return new[]
            {
                new GumoInstallController(this, args.Game)
            };
        }

        public override IEnumerable<UninstallController> GetUninstallActions(GetUninstallActionsArgs args)
        {
            if (args?.Game == null || !IsGumoGame(args.Game))
            {
                return Enumerable.Empty<UninstallController>();
            }

            return new[]
            {
                new GumoUninstallController(this, args.Game)
            };
        }

        public override IEnumerable<PlayController> GetPlayActions(GetPlayActionsArgs args)
        {
            if (args?.Game == null || !IsGumoGame(args.Game))
            {
                return Enumerable.Empty<PlayController>();
            }

            var installed = settings.GetInstalledGame(args.Game.GameId);
            if (installed == null || string.IsNullOrWhiteSpace(installed.ExecutablePath) || !File.Exists(installed.ExecutablePath))
            {
                return Enumerable.Empty<PlayController>();
            }

            return new[]
            {
                new AutomaticPlayController(args.Game)
                {
                    Name = "Play",
                    Path = installed.ExecutablePath,
                    WorkingDir = Path.GetDirectoryName(installed.ExecutablePath),
                }
            };
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return settings;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new GumoLibrarySettingsView(this, settings);
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
            Task.Run(() => ResumePendingUploadsAsync(startupProbeCancellation.Token));
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

        internal void TestConnectionFromSettings()
        {
            if (!settings.HasConnectionSettings())
            {
                PlayniteApi.Dialogs.ShowErrorMessage(
                    "Configure a valid Gumo server URL and API token first.",
                    "Gumo");
                return;
            }

            try
            {
                int visibleGameCount = 0;
                var result = PlayniteApi.Dialogs.ActivateGlobalProgress(
                    progressArgs =>
                    {
                        using (var client = CreateApiClient())
                        {
                            progressArgs.Text = "Checking Gumo API connectivity";
                            visibleGameCount = client.ProbeAsync(progressArgs.CancelToken).GetAwaiter().GetResult();
                        }
                    },
                    new GlobalProgressOptions("Testing Gumo connection", true)
                    {
                        IsIndeterminate = true,
                    });

                if (result.Canceled)
                {
                    Logger.Info("Gumo connection test canceled.");
                    return;
                }

                if (result.Error != null)
                {
                    throw result.Error;
                }

                PlayniteApi.Dialogs.ShowMessage(
                    $"Connection succeeded. Gumo returned {visibleGameCount} visible game(s).",
                    "Gumo");
            }
            catch (OperationCanceledException)
            {
                Logger.Info("Gumo connection test canceled.");
            }
            catch (GumoApiException exception)
            {
                var message =
                    $"Connection failed: {(int)exception.StatusCode} {exception.StatusCode} - {exception.ApiMessage}";
                Logger.Error(message);
                PlayniteApi.Dialogs.ShowErrorMessage(message, "Gumo");
            }
            catch (Exception exception)
            {
                Logger.Error($"Unexpected failure while testing Gumo connection: {exception}");
                PlayniteApi.Dialogs.ShowErrorMessage(
                    "Unexpected failure while testing the Gumo connection. See the plugin log for details.",
                    "Gumo");
            }
        }

        internal void UploadGameArchiveFromSettings()
        {
            UploadGameArchiveFromMenu();
        }

        internal bool InstallGameFromController(Game game)
        {
            if (!settings.HasConnectionSettings())
            {
                PlayniteApi.Dialogs.ShowErrorMessage(
                    "Configure the Gumo server URL and API token before installing a game.",
                    "Gumo");
                return false;
            }

            try
            {
                var result = PlayniteApi.Dialogs.ActivateGlobalProgress(
                    progressArgs =>
                    {
                        InstallGame(progressArgs, game);
                    },
                    new GlobalProgressOptions($"Installing {game.Name} from Gumo", true)
                    {
                        IsIndeterminate = false,
                    });

                if (result.Canceled)
                {
                    Logger.Info($"Install canceled for {game.Name}.");
                    return false;
                }

                if (result.Error != null)
                {
                    throw result.Error;
                }

                return true;
            }
            catch (OperationCanceledException)
            {
                Logger.Info($"Install canceled for {game.Name}.");
                return false;
            }
            catch (GumoApiException exception)
            {
                var message =
                    $"Failed to install from Gumo: {(int)exception.StatusCode} {exception.StatusCode} - {exception.ApiMessage}";
                Logger.Error(message);
                PlayniteApi.Dialogs.ShowErrorMessage(message, "Gumo");
                return false;
            }
            catch (Exception exception)
            {
                Logger.Error($"Unexpected failure during Gumo install: {exception}");
                PlayniteApi.Dialogs.ShowErrorMessage(
                    $"Unexpected failure during Gumo install: {exception.Message}",
                    "Gumo");
                return false;
            }
        }

        internal void UninstallGameFromController(Game game)
        {
            var installed = settings.GetInstalledGame(game.GameId);
            if (installed == null || string.IsNullOrWhiteSpace(installed.InstallDirectory))
            {
                settings.RemoveInstalledGame(game.GameId);
                return;
            }

            try
            {
                if (Directory.Exists(installed.InstallDirectory))
                {
                    Directory.Delete(installed.InstallDirectory, true);
                }

                settings.RemoveInstalledGame(game.GameId);
                game.InstallDirectory = null;
                game.IsInstalled = false;
            }
            catch (Exception exception)
            {
                Logger.Error($"Unexpected failure during Gumo uninstall: {exception}");
                PlayniteApi.Dialogs.ShowErrorMessage(
                    $"Unexpected failure during Gumo uninstall: {exception.Message}",
                    "Gumo");
            }
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

        private Game RunGameUploadImport(
            GlobalProgressActionArgs progressArgs,
            UploadSourceSelection source,
            GumoLibrary library,
            string gameName,
            string versionName)
        {
            progressArgs.Text = "Preparing upload";
            progressArgs.CurrentProgressValue = 0;

            using (var client = CreateApiClient())
            {
                var completedJob = UploadGameSourceToGumo(
                    client,
                    source,
                    library,
                    gameName,
                    versionName,
                    null,
                    null,
                    progressArgs);
                var imported = ImportCompletedUpload(client, completedJob, new PendingGameUpload
                {
                    GameName = gameName,
                });
                return imported;
            }
        }

        private GumoJob UploadGameSourceToGumo(
            GumoApiClient client,
            UploadSourceSelection source,
            GumoLibrary library,
            string gameName,
            string versionName,
            Game metadataSource,
            LocalUploadSaveConfiguration saveConfiguration,
            GlobalProgressActionArgs progressArgs)
        {
            var cancellationToken = progressArgs.CancelToken;
            var gameTarget = ResolveUploadGameTarget(client, gameName, cancellationToken);
            progressArgs.Text = source.IsDirectory
                ? $"Packaging folder '{source.DisplayName}'"
                : $"Preparing '{source.DisplayName}'";
            progressArgs.CurrentProgressValue = 10;
            var prepared = PrepareUploadArtifacts(source, cancellationToken);
            var firstPartInfo = new FileInfo(prepared.Parts[0].UploadPath);
            var pending = new PendingGameUpload
            {
                LibraryId = library.Id,
                Platform = library.Platform,
                GameName = gameName,
                VersionName = versionName,
                SourcePath = source.Path,
                FileName = firstPartInfo.Name,
                PackagedPath = prepared.Parts[0].UploadPath,
                IsTemporaryPackagedArtifact = prepared.Parts[0].DeleteAfterUpload,
                IdempotencyKey = Guid.NewGuid().ToString("N"),
            };

            try
            {
                var importSession = client.CreateGamePayloadImportSessionAsync(
                        new GumoCreateGamePayloadImportSessionRequest
                        {
                            LibraryId = library.Id,
                            Platform = library.Platform,
                            Game = gameTarget,
                            Version = new GumoUploadVersionTarget
                            {
                                VersionName = versionName,
                                OriginalSourceName = source.DisplayName,
                            },
                            IdempotencyKey = pending.IdempotencyKey,
                        },
                        cancellationToken)
                    .GetAwaiter()
                    .GetResult();

                pending.ImportSessionId = importSession.Id;
                SavePendingUpload(pending);

                var orderedParts = prepared.Parts.OrderBy(part => part.PartIndex).ToList();
                for (var index = 0; index < orderedParts.Count; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var preparedPart = orderedParts[index];
                    var fileInfo = new FileInfo(preparedPart.UploadPath);
                    progressArgs.Text = $"Uploading part {index + 1}/{orderedParts.Count} for '{gameName}'";
                    progressArgs.CurrentProgressValue = 20 + (index * 50) / Math.Max(orderedParts.Count, 1);
                    var part = client.CreateImportPartAsync(
                            importSession.Id,
                            new GumoCreateImportPartRequest
                            {
                                PartIndex = preparedPart.PartIndex,
                                File = new GumoUploadFileDescriptor
                                {
                                    Filename = fileInfo.Name,
                                    SizeBytes = fileInfo.Length,
                                },
                            },
                            cancellationToken)
                        .GetAwaiter()
                        .GetResult();

                    pending.UploadPartId = part.Id;
                    SavePendingUpload(pending);

                    if (part.State == "created" || part.State == "abandoned")
                    {
                        client.PutImportPartContentAsync(part.Id, preparedPart.UploadPath, cancellationToken)
                            .GetAwaiter()
                            .GetResult();
                    }
                }

                progressArgs.Text = $"Finalizing upload for '{gameName}'";
                progressArgs.CurrentProgressValue = 75;
                var job = client.FinalizeImportSessionAsync(importSession.Id, cancellationToken)
                    .GetAwaiter()
                    .GetResult();
                pending.JobId = job.Id;
                SavePendingUpload(pending);

                var completedJob = WaitForCompletedUpload(client, pending, cancellationToken, progressArgs);
                if (metadataSource != null)
                {
                    PushSingleGameMetadataToGumo(client, completedJob, metadataSource, cancellationToken);
                }

                if (saveConfiguration != null)
                {
                    PersistSaveConfigurationAndUploadSnapshot(
                        client,
                        completedJob,
                        metadataSource,
                        saveConfiguration,
                        progressArgs);
                }

                RemovePendingUpload(pending);
                CleanupPreparedArtifact(pending);
                return completedJob;
            }
            catch
            {
                CleanupPreparedArtifacts(prepared);
                throw;
            }
        }

        private void UploadGameArchiveFromMenu()
        {
            if (!settings.HasConnectionSettings())
            {
                PlayniteApi.Dialogs.ShowErrorMessage(
                    "Configure the Gumo server URL and API token before uploading a game.",
                    "Gumo");
                return;
            }

            try
            {
                var source = SelectUploadSource();
                if (source == null || string.IsNullOrWhiteSpace(source.Path))
                {
                    return;
                }

                GumoLibrary library;
                using (var client = CreateApiClient())
                {
                    var libraries = LoadLibrariesWithProgress(client, "Loading Gumo libraries");
                    library = SelectLibraryFromList(libraries);
                }
                if (library == null)
                {
                    return;
                }

                var gameName = PromptRequiredString(
                    "Game name",
                    "Gumo",
                    source.DefaultGameName);
                if (gameName == null)
                {
                    return;
                }

                var versionName = PromptRequiredString("Version name", "Gumo", "Initial");
                if (versionName == null)
                {
                    return;
                }

                Game importedGame = null;
                var result = PlayniteApi.Dialogs.ActivateGlobalProgress(
                    progressArgs =>
                    {
                        importedGame = RunGameUploadImport(progressArgs, source, library, gameName, versionName);
                    },
                    new GlobalProgressOptions("Uploading game to Gumo", true)
                    {
                        IsIndeterminate = false,
                    });

                if (result.Canceled)
                {
                    Logger.Info("Gumo upload import canceled.");
                    return;
                }

                if (result.Error != null)
                {
                    throw result.Error;
                }

                if (importedGame != null)
                {
                    PlayniteApi.Dialogs.ShowMessage(
                        $"Uploaded and imported '{importedGame.Name}' from Gumo.",
                        "Gumo");
                }
            }
            catch (OperationCanceledException)
            {
                Logger.Info("Gumo upload import canceled.");
            }
            catch (GumoApiException exception)
            {
                var message =
                    $"Failed to upload game archive to Gumo: {(int)exception.StatusCode} {exception.StatusCode} - {exception.ApiMessage}";
                Logger.Error(message);
                PlayniteApi.Dialogs.ShowErrorMessage(message, "Gumo");
            }
            catch (Exception exception)
            {
                Logger.Error($"Unexpected failure during Gumo upload import: {exception}");
                PlayniteApi.Dialogs.ShowErrorMessage(
                    $"Unexpected failure during Gumo upload import: {exception.Message}",
                    "Gumo");
            }
        }

        private async Task ResumePendingUploadsAsync(CancellationToken cancellationToken)
        {
            var pendingUploads = settings.PendingGameUploads?.ToList();
            if (pendingUploads == null || pendingUploads.Count == 0)
            {
                return;
            }

            try
            {
                using (var client = CreateApiClient())
                {
                    foreach (var pending in pendingUploads)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        try
                        {
                            var imported = await ResumePendingUploadAsync(client, pending, cancellationToken);
                            if (imported != null)
                            {
                                Logger.Info($"Recovered pending Gumo upload for '{pending.GameName}'.");
                            }
                        }
                        catch (Exception exception)
                        {
                            Logger.Error($"Failed to recover pending upload '{pending.GameName}': {exception}");
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Logger.Info("Pending Gumo upload recovery canceled during shutdown.");
            }
            catch (Exception exception)
            {
                Logger.Error($"Unexpected failure while recovering pending Gumo uploads: {exception}");
            }
        }

        private void PushMetadataToGumo(IEnumerable<Game> games)
        {
            if (!settings.HasConnectionSettings())
            {
                Logger.Warn("Skipping metadata push because plugin settings are incomplete.");
                return;
            }

            try
            {
                var gumoGames = games.Where(IsGumoGame).ToList();
                var result = PlayniteApi.Dialogs.ActivateGlobalProgress(
                    progressArgs =>
                    {
                        using (var client = CreateApiClient())
                        {
                            for (var index = 0; index < gumoGames.Count; index++)
                            {
                                progressArgs.CancelToken.ThrowIfCancellationRequested();
                                var game = gumoGames[index];
                                progressArgs.Text = $"Pushing metadata for {game.Name}";
                                progressArgs.CurrentProgressValue = (index * 100) / Math.Max(gumoGames.Count, 1);
                                var patchRequest = BuildPatchGameRequest(client, game, progressArgs.CancelToken);
                                client.PatchGameAsync(
                                        game.GameId,
                                        patchRequest,
                                        progressArgs.CancelToken)
                                    .GetAwaiter()
                                    .GetResult();
                            }

                            progressArgs.CurrentProgressValue = 100;
                        }
                    },
                    new GlobalProgressOptions("Pushing metadata to Gumo", true)
                    {
                        IsIndeterminate = false,
                    });

                if (result.Canceled)
                {
                    Logger.Info("Metadata push to Gumo canceled.");
                    return;
                }

                if (result.Error != null)
                {
                    throw result.Error;
                }

                Logger.Info($"Pushed metadata for {gumoGames.Count} Gumo game(s).");
                PlayniteApi.Dialogs.ShowMessage(
                    $"Pushed metadata for {gumoGames.Count} Gumo game(s).",
                    "Gumo");
            }
            catch (OperationCanceledException)
            {
                Logger.Info("Metadata push to Gumo canceled.");
            }
            catch (GumoApiException exception)
            {
                var message =
                    $"Failed to push metadata to Gumo: {(int)exception.StatusCode} {exception.StatusCode} - {exception.ApiMessage}";
                Logger.Error(message);
                PlayniteApi.Dialogs.ShowErrorMessage(message, "Gumo");
            }
            catch (Exception exception)
            {
                Logger.Error($"Unexpected failure while pushing metadata to Gumo: {exception}");
                PlayniteApi.Dialogs.ShowErrorMessage(
                    $"Unexpected failure while pushing metadata to Gumo: {exception.Message}",
                    "Gumo");
            }
        }

        private void UploadSelectedLocalGamesToGumo(IEnumerable<Game> games)
        {
            if (!settings.HasConnectionSettings())
            {
                PlayniteApi.Dialogs.ShowErrorMessage(
                    "Configure the Gumo server URL and API token before uploading local games.",
                    "Gumo");
                return;
            }

            var uploadableGames = games
                .Where(CanUploadLocalGameToGumo)
                .ToList();
            if (uploadableGames.Count == 0)
            {
                PlayniteApi.Dialogs.ShowErrorMessage(
                    "No selected local games have an accessible install directory to upload.",
                    "Gumo");
                return;
            }

            localUploadSaveConfigurationCanceled = false;
            var saveConfigurations = new Dictionary<string, LocalUploadSaveConfiguration>(StringComparer.OrdinalIgnoreCase);
            foreach (var game in uploadableGames)
            {
                var saveConfiguration = PromptLocalUploadSaveConfiguration(game);
                if (localUploadSaveConfigurationCanceled)
                {
                    return;
                }

                if (saveConfiguration != null)
                {
                    saveConfigurations[game.GameId] = saveConfiguration;
                }
            }

            try
            {
                GumoLibrary library;
                using (var client = CreateApiClient())
                {
                    var libraries = LoadLibrariesWithProgress(client, "Loading Gumo libraries");
                    library = SelectLibraryFromList(libraries);
                }
                if (library == null)
                {
                    return;
                }

                using (var client = CreateApiClient())
                {
                    var result = PlayniteApi.Dialogs.ActivateGlobalProgress(
                        progressArgs =>
                        {
                            for (var index = 0; index < uploadableGames.Count; index++)
                            {
                                progressArgs.CancelToken.ThrowIfCancellationRequested();
                                var game = uploadableGames[index];
                                progressArgs.Text = $"Preparing local game {index + 1}/{uploadableGames.Count}: {game.Name}";
                                progressArgs.CurrentProgressValue = (index * 100) / Math.Max(uploadableGames.Count, 1);

                                var source = new UploadSourceSelection
                                {
                                    Path = game.InstallDirectory,
                                    IsDirectory = true,
                                    DefaultGameName = game.Name,
                                    DisplayName = new DirectoryInfo(game.InstallDirectory).Name,
                                };
                                if (saveConfigurations.TryGetValue(game.GameId, out var saveConfiguration))
                                {
                                    ApplySaveExclusionToGameUpload(source, game, saveConfiguration);
                                }

                                var versionName = !string.IsNullOrWhiteSpace(game.Version)
                                    ? game.Version.Trim()
                                    : "Imported";
                                UploadGameSourceToGumo(
                                    client,
                                    source,
                                    library,
                                    game.Name,
                                    versionName,
                                    game,
                                    saveConfiguration,
                                    progressArgs);
                            }

                            progressArgs.CurrentProgressValue = 100;
                        },
                        new GlobalProgressOptions("Uploading local Playnite games to Gumo", true)
                        {
                            IsIndeterminate = false,
                        });

                    if (result.Canceled)
                    {
                        Logger.Info("Local Playnite game upload to Gumo canceled.");
                        return;
                    }

                    if (result.Error != null)
                    {
                        throw result.Error;
                    }

                    PlayniteApi.Dialogs.ShowMessage(
                        $"Uploaded {uploadableGames.Count} local Playnite game(s) to Gumo.",
                        "Gumo");
                }
            }
            catch (OperationCanceledException)
            {
                Logger.Info("Local Playnite game upload to Gumo canceled.");
            }
            catch (GumoApiException exception)
            {
                var message =
                    $"Failed to upload local Playnite game(s) to Gumo: {(int)exception.StatusCode} {exception.StatusCode} - {exception.ApiMessage}";
                Logger.Error(message);
                PlayniteApi.Dialogs.ShowErrorMessage(message, "Gumo");
            }
            catch (Exception exception)
            {
                Logger.Error($"Unexpected failure while uploading local Playnite game(s) to Gumo: {exception}");
                PlayniteApi.Dialogs.ShowErrorMessage(
                    $"Unexpected failure while uploading local Playnite game(s) to Gumo: {exception.Message}",
                    "Gumo");
            }
        }

        private void PushSingleGameMetadataToGumo(
            GumoApiClient client,
            GumoJob completedJob,
            Game sourceGame,
            CancellationToken cancellationToken)
        {
            var gameId = completedJob.Result?.GameId;
            if (string.IsNullOrWhiteSpace(gameId))
            {
                Logger.Warn($"Completed Gumo upload for '{sourceGame.Name}' did not return a game_id for metadata sync.");
                return;
            }

            var patchRequest = BuildPatchGameRequest(client, sourceGame, cancellationToken);
            var updatedGame = client.PatchGameAsync(
                    gameId,
                    patchRequest,
                    cancellationToken)
                .GetAwaiter()
                .GetResult();
            Logger.Info(
                $"Patched Gumo game '{updatedGame.Name}' metadata after local upload. cover_image={updatedGame.CoverImage ?? "<null>"}, icon={updatedGame.Icon ?? "<null>"}");
        }

        private bool IsGumoGame(Game game)
        {
            return game != null &&
                   game.PluginId == Id &&
                   !string.IsNullOrWhiteSpace(game.GameId);
        }

        private bool CanUploadLocalGameToGumo(Game game)
        {
            return game != null &&
                   !IsGumoGame(game) &&
                   !string.IsNullOrWhiteSpace(game.InstallDirectory) &&
                   Directory.Exists(game.InstallDirectory);
        }

        private bool CanManageSaveSnapshots(Game game)
        {
            var installed = settings.GetInstalledGame(game.GameId);
            return installed != null &&
                   !string.IsNullOrWhiteSpace(installed.VersionId);
        }

        private LocalUploadSaveConfiguration PromptLocalUploadSaveConfiguration(Game game)
        {
            var selection = ShowOptionListPicker(
                "Gumo Save Upload",
                $"Choose how to handle save files for {game.Name} before uploading the game payload.",
                new[]
                {
                    new OptionListPickerItem
                    {
                        Id = LocalSaveUploadAction.Configure.ToString(),
                        Title = "Configure save folder",
                        Description = "Exclude configured saves from the game archive and upload them separately.",
                        Value = LocalSaveUploadAction.Configure,
                    },
                    new OptionListPickerItem
                    {
                        Id = LocalSaveUploadAction.Skip.ToString(),
                        Title = "Skip save upload",
                        Description = "Upload only the game payload right now.",
                        Value = LocalSaveUploadAction.Skip,
                    },
                    new OptionListPickerItem
                    {
                        Id = LocalSaveUploadAction.Cancel.ToString(),
                        Title = "Cancel upload",
                        Description = "Abort the local upload flow.",
                        Value = LocalSaveUploadAction.Cancel,
                    },
                });

            var action = selection?.Value is LocalSaveUploadAction selectedAction
                ? selectedAction
                : LocalSaveUploadAction.Skip;
            if (action == LocalSaveUploadAction.Cancel)
            {
                localUploadSaveConfigurationCanceled = true;
                return null;
            }

            if (action != LocalSaveUploadAction.Configure)
            {
                return null;
            }

            var temporaryInstalledState = new InstalledGameState
            {
                GameId = game.GameId,
                InstallDirectory = game.InstallDirectory,
                SaveDirectory = game.InstallDirectory,
            };
            var savePathType = PromptSavePathType();
            if (!savePathType.HasValue)
            {
                localUploadSaveConfigurationCanceled = true;
                return null;
            }

            var selectedPath = PromptSaveDirectory(game, temporaryInstalledState, null, savePathType.Value);
            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                localUploadSaveConfigurationCanceled = true;
                return null;
            }

            if (!Directory.Exists(selectedPath))
            {
                PlayniteApi.Dialogs.ShowErrorMessage(
                    $"The selected save directory does not exist:{Environment.NewLine}{selectedPath}",
                    "Gumo");
                localUploadSaveConfigurationCanceled = true;
                return null;
            }

            var storedPath = NormalizeConfiguredSavePath(selectedPath, savePathType.Value, game.InstallDirectory);
            if (string.IsNullOrWhiteSpace(storedPath))
            {
                localUploadSaveConfigurationCanceled = true;
                return null;
            }

            var patternInput = PlayniteApi.Dialogs.SelectString(
                "Optional match pattern. Leave blank to include every file in the selected save folder. Use patterns like '*.sav' or 'SaveData/*.dat'.",
                "Gumo",
                string.Empty);
            if (!patternInput.Result)
            {
                localUploadSaveConfigurationCanceled = true;
                return null;
            }

            var saveFilePattern = NormalizeSavePattern(patternInput.SelectedString);
            if (!ValidateSavePattern(saveFilePattern))
            {
                localUploadSaveConfigurationCanceled = true;
                return null;
            }

            var resolvedDirectory = ResolveSavePathFromConfiguration(
                game.InstallDirectory,
                storedPath,
                SavePathTypeToApiString(savePathType.Value));
            if (string.IsNullOrWhiteSpace(resolvedDirectory) || !Directory.Exists(resolvedDirectory))
            {
                PlayniteApi.Dialogs.ShowErrorMessage(
                    $"The configured save directory does not exist:{Environment.NewLine}{resolvedDirectory}",
                    "Gumo");
                localUploadSaveConfigurationCanceled = true;
                return null;
            }

            if (string.Equals(Path.GetFullPath(resolvedDirectory), Path.GetFullPath(game.InstallDirectory), StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(saveFilePattern))
            {
                PlayniteApi.Dialogs.ShowErrorMessage(
                    "When the save folder is the game root, a matching pattern is required so game files are not excluded entirely.",
                    "Gumo");
                localUploadSaveConfigurationCanceled = true;
                return null;
            }

            return new LocalUploadSaveConfiguration
            {
                SavePath = storedPath,
                SavePathType = SavePathTypeToApiString(savePathType.Value),
                SaveFilePattern = saveFilePattern,
                ResolvedDirectory = resolvedDirectory,
                SnapshotName = $"Initial save import {DateTime.Now:yyyy-MM-dd HH:mm}",
            };
        }

        private bool ValidateSavePattern(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            try
            {
                CompileSavePatternRegex(value);
                return true;
            }
            catch (ArgumentException exception)
            {
                PlayniteApi.Dialogs.ShowErrorMessage(
                    $"Invalid save matching regex:{Environment.NewLine}{exception.Message}",
                    "Gumo");
                return false;
            }
        }

        private void ApplySaveExclusionToGameUpload(
            UploadSourceSelection source,
            Game game,
            LocalUploadSaveConfiguration saveConfiguration)
        {
            if (source == null || game == null || saveConfiguration == null || string.IsNullOrWhiteSpace(source.Path))
            {
                return;
            }

            var sourceRoot = Path.GetFullPath(source.Path);
            var saveRoot = Path.GetFullPath(saveConfiguration.ResolvedDirectory);
            if (!IsPathInsideOrEqual(saveRoot, sourceRoot))
            {
                return;
            }

            var relativeSaveRoot = MakeRelativePath(sourceRoot, saveRoot);
            if (string.IsNullOrWhiteSpace(relativeSaveRoot))
            {
                source.ExcludeMatchPattern = saveConfiguration.SaveFilePattern;
                return;
            }

            if (string.IsNullOrWhiteSpace(saveConfiguration.SaveFilePattern))
            {
                source.ExcludedRelativeRoots.Add(relativeSaveRoot);
                return;
            }

            source.ExcludeRelativeRoot = relativeSaveRoot;
            source.ExcludeMatchPattern = saveConfiguration.SaveFilePattern;
        }

        private void PersistSaveConfigurationAndUploadSnapshot(
            GumoApiClient client,
            GumoJob completedJob,
            Game sourceGame,
            LocalUploadSaveConfiguration saveConfiguration,
            GlobalProgressActionArgs progressArgs)
        {
            var versionId = completedJob.Result?.GameVersionId;
            if (string.IsNullOrWhiteSpace(versionId))
            {
                Logger.Warn($"Completed upload for '{sourceGame?.Name ?? saveConfiguration.ResolvedDirectory}' did not return a game_version_id for save upload.");
                return;
            }

            progressArgs.Text = $"Saving save configuration for '{sourceGame?.Name ?? versionId}'";
            progressArgs.CurrentProgressValue = 85;
            client.PatchVersionAsync(
                    versionId,
                    new GumoPatchVersionRequest
                    {
                        SavePath = saveConfiguration.SavePath,
                        SavePathType = saveConfiguration.SavePathType,
                        SaveFilePattern = saveConfiguration.SaveFilePattern,
                    },
                    progressArgs.CancelToken)
                .GetAwaiter()
                .GetResult();

            if (!HasUploadableFiles(saveConfiguration.ResolvedDirectory, saveConfiguration.SaveFilePattern))
            {
                Logger.Info($"Skipping initial save upload for '{sourceGame?.Name ?? versionId}' because no matching save files were found.");
                return;
            }

            progressArgs.Text = $"Uploading saves for '{sourceGame?.Name ?? versionId}'";
            progressArgs.CurrentProgressValue = 90;
            UploadSaveSnapshotForVersion(
                client,
                versionId,
                saveConfiguration.ResolvedDirectory,
                saveConfiguration.SaveFilePattern,
                saveConfiguration.SnapshotName,
                progressArgs,
                sourceGame?.Name ?? versionId);
        }

        private void ConfigureSaveDirectory(Game game)
        {
            var installed = settings.GetInstalledGame(game.GameId);
            if (installed == null)
            {
                PlayniteApi.Dialogs.ShowErrorMessage(
                    "Install the Gumo game before configuring its save directory.",
                    "Gumo");
                return;
            }

            if (!settings.HasConnectionSettings())
            {
                PlayniteApi.Dialogs.ShowErrorMessage(
                    "Configure the Gumo server URL and API token before configuring save backup.",
                    "Gumo");
                return;
            }

            try
            {
                var version = LoadInstalledVersionWithProgress(game, installed, "Loading Gumo save backup settings");
                if (version == null)
                {
                    return;
                }

                var savePathType = PromptSavePathType();
                if (!savePathType.HasValue)
                {
                    return;
                }

                var selectedPath = PromptSaveDirectory(game, installed, version, savePathType.Value);
                if (string.IsNullOrWhiteSpace(selectedPath))
                {
                    return;
                }

                var storedPath = NormalizeConfiguredSavePath(selectedPath, savePathType.Value, installed.InstallDirectory);
                if (string.IsNullOrWhiteSpace(storedPath))
                {
                    return;
                }

                var patternInput = PlayniteApi.Dialogs.SelectString(
                    "Optional regex match pattern. Leave blank to include every file in the selected save folder. Use patterns like '^.*\\.sav$' or '^SaveData/.*\\.dat$'.",
                    "Gumo",
                    version.SaveFilePattern ?? string.Empty);
                if (!patternInput.Result)
                {
                    return;
                }

                var saveFilePattern = NormalizeSavePattern(patternInput.SelectedString);
                if (!ValidateSavePattern(saveFilePattern))
                {
                    return;
                }

                var updatedVersion = PatchSaveConfigurationWithProgress(
                    version.Id,
                    storedPath,
                    SavePathTypeToApiString(savePathType.Value),
                    saveFilePattern);
                if (updatedVersion == null)
                {
                    return;
                }

                installed.SaveDirectory = ResolveConfiguredSaveDirectory(installed, updatedVersion);
                settings.UpsertInstalledGame(installed);
                PlayniteApi.Dialogs.ShowMessage(
                    BuildSaveConfigurationSummary(game.Name, updatedVersion, installed.SaveDirectory),
                    "Gumo");
            }
            catch (GumoApiException exception)
            {
                var message =
                    $"Failed to configure save backup in Gumo: {(int)exception.StatusCode} {exception.StatusCode} - {exception.ApiMessage}";
                Logger.Error(message);
                PlayniteApi.Dialogs.ShowErrorMessage(message, "Gumo");
            }
            catch (Exception exception)
            {
                Logger.Error($"Unexpected failure while configuring Gumo save backup: {exception}");
                PlayniteApi.Dialogs.ShowErrorMessage(
                    $"Unexpected failure while configuring Gumo save backup: {exception.Message}",
                    "Gumo");
            }
        }

        private GumoGameVersion LoadInstalledVersion(
            GumoApiClient client,
            Game game,
            InstalledGameState installed,
            CancellationToken cancellationToken)
        {
            var versions = client.GetVersionsAsync(game.GameId, cancellationToken).GetAwaiter().GetResult();
            var version = versions.FirstOrDefault(item =>
                string.Equals(item.Id, installed.VersionId, StringComparison.OrdinalIgnoreCase));
            if (version == null)
            {
                Logger.Warn($"Installed Gumo version '{installed.VersionId}' for {game.Name} was not found.");
            }

            return version;
        }

        private GumoGameVersion LoadInstalledVersionWithProgress(
            Game game,
            InstalledGameState installed,
            string title)
        {
            GumoGameVersion version = null;
            var result = PlayniteApi.Dialogs.ActivateGlobalProgress(
                progressArgs =>
                {
                    progressArgs.Text = $"Loading save backup settings for {game.Name}";
                    using (var client = CreateApiClient())
                    {
                        version = LoadInstalledVersion(client, game, installed, progressArgs.CancelToken);
                    }
                },
                new GlobalProgressOptions(title, true)
                {
                    IsIndeterminate = true,
                });

            if (result.Canceled)
            {
                Logger.Info("Gumo save configuration loading canceled.");
                return null;
            }

            if (result.Error != null)
            {
                throw result.Error;
            }

            if (version == null)
            {
                PlayniteApi.Dialogs.ShowErrorMessage(
                    $"The installed Gumo version '{installed.VersionId}' for {game.Name} could not be found.",
                    "Gumo");
            }

            return version;
        }

        private GumoGameVersion PatchSaveConfigurationWithProgress(
            string versionId,
            string savePath,
            string savePathType,
            string saveFilePattern)
        {
            GumoGameVersion updatedVersion = null;
            var result = PlayniteApi.Dialogs.ActivateGlobalProgress(
                progressArgs =>
                {
                    progressArgs.Text = "Saving Gumo save backup settings";
                    using (var client = CreateApiClient())
                    {
                        updatedVersion = client.PatchVersionAsync(
                                versionId,
                                new GumoPatchVersionRequest
                                {
                                    SavePath = savePath,
                                    SavePathType = savePathType,
                                    SaveFilePattern = saveFilePattern,
                                },
                                progressArgs.CancelToken)
                            .GetAwaiter()
                            .GetResult();
                    }
                },
                new GlobalProgressOptions("Saving Gumo save backup settings", true)
                {
                    IsIndeterminate = true,
                });

            if (result.Canceled)
            {
                Logger.Info("Gumo save configuration update canceled.");
                return null;
            }

            if (result.Error != null)
            {
                throw result.Error;
            }

            return updatedVersion;
        }

        private SavePathType? PromptSavePathType()
        {
            var selected = ShowOptionListPicker(
                "Gumo Save Backup",
                "Choose whether the configured save path is stored relative to the install directory or as an absolute path.",
                new[]
                {
                    new OptionListPickerItem
                    {
                        Id = SavePathType.Relative.ToString(),
                        Title = "Relative to install directory",
                        Description = "Best when saves live under the game install folder on every machine.",
                        Value = SavePathType.Relative,
                    },
                    new OptionListPickerItem
                    {
                        Id = SavePathType.Absolute.ToString(),
                        Title = "Absolute path",
                        Description = "Use a fixed save location outside the install directory.",
                        Value = SavePathType.Absolute,
                    },
                });
            return selected?.Value is SavePathType savePathType ? savePathType : (SavePathType?)null;
        }

        private string PromptSaveDirectory(
            Game game,
            InstalledGameState installed,
            GumoGameVersion version,
            SavePathType savePathType)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = savePathType == SavePathType.Relative
                    ? $"Select the save folder under the install directory for {game.Name}"
                    : $"Select the absolute save folder for {game.Name}";

                var initialPath = ResolveConfiguredSaveDirectory(installed, version);
                if (string.IsNullOrWhiteSpace(initialPath) || !Directory.Exists(initialPath))
                {
                    initialPath = savePathType == SavePathType.Relative ? installed.InstallDirectory : installed.SaveDirectory;
                }

                if (!string.IsNullOrWhiteSpace(initialPath) && Directory.Exists(initialPath))
                {
                    dialog.SelectedPath = initialPath;
                }

                if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK || string.IsNullOrWhiteSpace(dialog.SelectedPath))
                {
                    return null;
                }

                return dialog.SelectedPath;
            }
        }

        private string NormalizeConfiguredSavePath(string selectedPath, SavePathType savePathType, string installDirectory)
        {
            var normalizedPath = selectedPath?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                return null;
            }

            if (savePathType != SavePathType.Relative)
            {
                return normalizedPath;
            }

            if (string.IsNullOrWhiteSpace(installDirectory) || !Directory.Exists(installDirectory))
            {
                PlayniteApi.Dialogs.ShowErrorMessage(
                    "The install directory must exist before configuring a relative save path.",
                    "Gumo");
                return null;
            }

            var installRoot = EnsureTrailingDirectorySeparator(Path.GetFullPath(installDirectory));
            var absoluteSelectedPath = Path.GetFullPath(normalizedPath);
            if (!absoluteSelectedPath.StartsWith(installRoot, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(absoluteSelectedPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    installRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase))
            {
                PlayniteApi.Dialogs.ShowErrorMessage(
                    "A relative save path must be inside the installed game directory.",
                    "Gumo");
                return null;
            }

            var relativePath = MakeRelativePath(installDirectory, absoluteSelectedPath);
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return ".";
            }

            while (relativePath.StartsWith("../", StringComparison.Ordinal) ||
                   relativePath.StartsWith("..\\", StringComparison.Ordinal))
            {
                relativePath = relativePath.Substring(3);
            }

            return string.IsNullOrWhiteSpace(relativePath) ? "." : relativePath;
        }

        private string ResolveConfiguredSaveDirectory(InstalledGameState installed, GumoGameVersion version)
        {
            if (version != null &&
                !string.IsNullOrWhiteSpace(version.SavePath) &&
                !string.IsNullOrWhiteSpace(version.SavePathType))
            {
                return ResolveSavePathFromConfiguration(installed?.InstallDirectory, version.SavePath, version.SavePathType);
            }

            return installed?.SaveDirectory;
        }

        private static string SavePathTypeToApiString(SavePathType value)
        {
            return value == SavePathType.Relative ? "relative" : "absolute";
        }

        private string ResolveSavePathFromConfiguration(string installDirectory, string savePath, string savePathType)
        {
            if (string.IsNullOrWhiteSpace(savePath) || string.IsNullOrWhiteSpace(savePathType))
            {
                return null;
            }

            if (TryParseSavePathType(savePathType, out var parsedSavePathType) &&
                parsedSavePathType == SavePathType.Relative)
            {
                if (string.IsNullOrWhiteSpace(installDirectory))
                {
                    return null;
                }

                var relativePath = savePath
                    .Replace('/', Path.DirectorySeparatorChar)
                    .Replace('\\', Path.DirectorySeparatorChar);
                return Path.GetFullPath(Path.Combine(installDirectory, relativePath));
            }

            return savePath;
        }

        private static bool TryParseSavePathType(string value, out SavePathType savePathType)
        {
            switch ((value ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "relative":
                    savePathType = SavePathType.Relative;
                    return true;
                case "absolute":
                    savePathType = SavePathType.Absolute;
                    return true;
                default:
                    savePathType = SavePathType.Relative;
                    return false;
            }
        }

        private string BuildSaveConfigurationSummary(
            string gameName,
            GumoGameVersion version,
            string resolvedPath)
        {
            var lines = new List<string>
            {
                $"Configured save backup for {gameName}.",
                $"Path mode: {version.SavePathType}",
                $"Stored path: {version.SavePath}",
            };

            if (!string.IsNullOrWhiteSpace(resolvedPath))
            {
                lines.Add($"Resolved path: {resolvedPath}");
            }

            lines.Add(string.IsNullOrWhiteSpace(version.SaveFilePattern)
                ? "Matching pattern: all files"
                : $"Matching pattern: {version.SaveFilePattern}");

            return string.Join(Environment.NewLine, lines);
        }

        private static string NormalizeSavePattern(string value)
        {
            var trimmed = value?.Trim();
            return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
        }

        private void BackupSaveSnapshotToGumo(Game game)
        {
            if (!settings.HasConnectionSettings())
            {
                PlayniteApi.Dialogs.ShowErrorMessage(
                    "Configure the Gumo server URL and API token before backing up saves.",
                    "Gumo");
                return;
            }

            var installed = settings.GetInstalledGame(game.GameId);
            if (installed == null || string.IsNullOrWhiteSpace(installed.VersionId))
            {
                PlayniteApi.Dialogs.ShowErrorMessage(
                    "Install the Gumo game before backing up saves.",
                    "Gumo");
                return;
            }

            GumoGameVersion version;
            string saveDirectory;
            string saveFilePattern;
            version = LoadInstalledVersionWithProgress(game, installed, "Loading Gumo save backup settings");
            if (version == null)
            {
                return;
            }

            saveDirectory = EnsureSaveDirectoryConfigured(game, installed, version, mustExist: true);
            if (string.IsNullOrWhiteSpace(saveDirectory))
            {
                return;
            }

            saveFilePattern = NormalizeSavePattern(version.SaveFilePattern);

            if (string.IsNullOrWhiteSpace(saveDirectory))
            {
                return;
            }

            var snapshotName = PromptRequiredString(
                "Save snapshot name",
                "Gumo",
                $"Save {DateTime.Now:yyyy-MM-dd HH:mm}");
            if (snapshotName == null)
            {
                return;
            }

            try
            {
                var result = PlayniteApi.Dialogs.ActivateGlobalProgress(
                    progressArgs =>
                    {
                        using (var client = CreateApiClient())
                        {
                            BackupSaveSnapshot(client, game, installed, saveDirectory, saveFilePattern, snapshotName, progressArgs);
                        }
                    },
                    new GlobalProgressOptions("Backing up save snapshot to Gumo", true)
                    {
                        IsIndeterminate = false,
                    });

                if (result.Canceled)
                {
                    Logger.Info("Gumo save backup canceled.");
                    return;
                }

                if (result.Error != null)
                {
                    throw result.Error;
                }

                PlayniteApi.Dialogs.ShowMessage(
                    $"Backed up '{snapshotName}' for {game.Name}.",
                    "Gumo");
            }
            catch (OperationCanceledException)
            {
                Logger.Info("Gumo save backup canceled.");
            }
            catch (GumoApiException exception)
            {
                var message =
                    $"Failed to back up save snapshot to Gumo: {(int)exception.StatusCode} {exception.StatusCode} - {exception.ApiMessage}";
                Logger.Error(message);
                PlayniteApi.Dialogs.ShowErrorMessage(message, "Gumo");
            }
            catch (Exception exception)
            {
                Logger.Error($"Unexpected failure during Gumo save backup: {exception}");
                PlayniteApi.Dialogs.ShowErrorMessage(
                    $"Unexpected failure during Gumo save backup: {exception.Message}",
                    "Gumo");
            }
        }

        private void RestoreSaveSnapshotFromGumo(Game game)
        {
            if (!settings.HasConnectionSettings())
            {
                PlayniteApi.Dialogs.ShowErrorMessage(
                    "Configure the Gumo server URL and API token before restoring saves.",
                    "Gumo");
                return;
            }

            var installed = settings.GetInstalledGame(game.GameId);
            if (installed == null || string.IsNullOrWhiteSpace(installed.VersionId))
            {
                PlayniteApi.Dialogs.ShowErrorMessage(
                    "Install the Gumo game before restoring saves.",
                    "Gumo");
                return;
            }

            GumoGameVersion version;
            string saveDirectory;
            string saveFilePattern;
            version = LoadInstalledVersionWithProgress(game, installed, "Loading Gumo save backup settings");
            if (version == null)
            {
                return;
            }

            saveDirectory = EnsureSaveDirectoryConfigured(game, installed, version, mustExist: false);
            if (string.IsNullOrWhiteSpace(saveDirectory))
            {
                return;
            }

            saveFilePattern = NormalizeSavePattern(version.SaveFilePattern);

            if (string.IsNullOrWhiteSpace(saveDirectory))
            {
                return;
            }

            try
            {
                GumoSaveSnapshot selectedSnapshot;
                using (var client = CreateApiClient())
                {
                    var snapshots = LoadSaveSnapshotsWithProgress(client, installed.VersionId, game.Name);
                    selectedSnapshot = SelectSaveSnapshotForRestore(snapshots, game.Name);
                }

                if (selectedSnapshot == null)
                {
                    return;
                }

                var replaceExisting = false;
                Directory.CreateDirectory(saveDirectory);
                if (Directory.EnumerateFileSystemEntries(saveDirectory).Any())
                {
                    var replacementLabel = string.IsNullOrWhiteSpace(saveFilePattern)
                        ? "replace the current local save contents"
                        : $"replace local files matching '{saveFilePattern}'";
                    var confirmed = PlayniteApi.Dialogs.ShowMessage(
                        $"Restore '{selectedSnapshot.Name}' into:{Environment.NewLine}{saveDirectory}{Environment.NewLine}{Environment.NewLine}This will {replacementLabel}.",
                        "Gumo",
                        System.Windows.MessageBoxButton.YesNo);
                    if (confirmed != System.Windows.MessageBoxResult.Yes)
                    {
                        return;
                    }

                    replaceExisting = true;
                }

                var result = PlayniteApi.Dialogs.ActivateGlobalProgress(
                    progressArgs =>
                    {
                        using (var client = CreateApiClient())
                        {
                            RestoreSaveSnapshot(client, game, saveDirectory, saveFilePattern, selectedSnapshot, replaceExisting, progressArgs);
                        }
                    },
                    new GlobalProgressOptions("Restoring save snapshot from Gumo", true)
                    {
                        IsIndeterminate = false,
                    });

                if (result.Canceled)
                {
                    Logger.Info("Gumo save restore canceled.");
                    return;
                }

                if (result.Error != null)
                {
                    throw result.Error;
                }

                PlayniteApi.Dialogs.ShowMessage(
                    $"Restored '{selectedSnapshot.Name}' for {game.Name}.",
                    "Gumo");
            }
            catch (OperationCanceledException)
            {
                Logger.Info("Gumo save restore canceled.");
            }
            catch (GumoApiException exception)
            {
                var message =
                    $"Failed to restore save snapshot from Gumo: {(int)exception.StatusCode} {exception.StatusCode} - {exception.ApiMessage}";
                Logger.Error(message);
                PlayniteApi.Dialogs.ShowErrorMessage(message, "Gumo");
            }
            catch (Exception exception)
            {
                Logger.Error($"Unexpected failure during Gumo save restore: {exception}");
                PlayniteApi.Dialogs.ShowErrorMessage(
                    $"Unexpected failure during Gumo save restore: {exception.Message}",
                    "Gumo");
            }
        }

        private string ResolveGameMediaReference(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim();
            if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp ||
                 uri.Scheme == Uri.UriSchemeHttps ||
                 uri.Scheme == Uri.UriSchemeFile))
            {
                return trimmed;
            }

            if (Path.IsPathRooted(trimmed))
            {
                return trimmed;
            }

            try
            {
                var resolved = PlayniteApi.Database.GetFullFilePath(trimmed);
                if (!string.IsNullOrWhiteSpace(resolved))
                {
                    return resolved;
                }
            }
            catch (Exception exception)
            {
                Logger.Warn($"Failed to resolve Playnite media reference '{trimmed}': {exception.Message}");
            }

            return trimmed;
        }

        private GumoPatchGameRequest BuildPatchGameRequest(
            GumoApiClient client,
            Game game,
            CancellationToken cancellationToken)
        {
            return GumoMapper.ToPatchGameRequest(
                game,
                UploadGameMediaReference(client, game.CoverImage, cancellationToken),
                UploadGameMediaReference(client, game.BackgroundImage, cancellationToken),
                UploadGameMediaReference(client, game.Icon, cancellationToken));
        }

        private string UploadGameMediaReference(
            GumoApiClient client,
            string value,
            CancellationToken cancellationToken)
        {
            var resolved = ResolveGameMediaReference(value);
            if (string.IsNullOrWhiteSpace(resolved))
            {
                return null;
            }

            if (resolved.StartsWith("/media/", StringComparison.OrdinalIgnoreCase) ||
                resolved.StartsWith("/assets/", StringComparison.OrdinalIgnoreCase))
            {
                return resolved;
            }

            if (Uri.TryCreate(resolved, UriKind.Absolute, out var uri))
            {
                if (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                {
                    return resolved;
                }

                if (uri.Scheme == Uri.UriSchemeFile)
                {
                    return UploadLocalMediaFile(client, uri.LocalPath, cancellationToken) ?? resolved;
                }
            }

            if (Path.IsPathRooted(resolved))
            {
                return UploadLocalMediaFile(client, resolved, cancellationToken) ?? resolved;
            }

            return resolved;
        }

        private string UploadLocalMediaFile(
            GumoApiClient client,
            string filePath,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return null;
            }

            return client.UploadMediaAsync(filePath, cancellationToken)
                .GetAwaiter()
                .GetResult()
                .Url;
        }

        private GumoGame NormalizeGumoGameMediaUrls(GumoGame game)
        {
            if (game == null)
            {
                return null;
            }

            game.CoverImage = NormalizeGumoMediaUrl(game.CoverImage);
            game.BackgroundImage = NormalizeGumoMediaUrl(game.BackgroundImage);
            game.Icon = NormalizeGumoMediaUrl(game.Icon);
            return game;
        }

        private string NormalizeGumoMediaUrl(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim();
            if (Uri.TryCreate(trimmed, UriKind.Absolute, out _))
            {
                return trimmed;
            }

            if (!trimmed.StartsWith("/", StringComparison.Ordinal))
            {
                trimmed = "/" + trimmed;
            }

            return settings.NormalizedServerUrl() + trimmed;
        }

        private void InstallGame(GlobalProgressActionArgs progressArgs, Game game)
        {
            using (var client = CreateApiClient())
            {
                var versions = client.GetVersionsAsync(game.GameId, progressArgs.CancelToken).GetAwaiter().GetResult();
                var selectedVersion = SelectVersionForInstall(game, versions);
                if (selectedVersion == null)
                {
                    return;
                }

                var installManifest = client
                    .GetInstallManifestAsync(selectedVersion.Id, progressArgs.CancelToken)
                    .GetAwaiter()
                    .GetResult();
                var installDirectory = ResolveInstallDirectory(game, installManifest);
                if (string.IsNullOrWhiteSpace(installDirectory))
                {
                    return;
                }

                EnsureInstallDirectoryIsUsable(installDirectory);

                var tempDownloadDirectory = Path.Combine(
                    Path.GetTempPath(),
                    $"gumo-install-{installManifest.Artifact.Id}-{Guid.NewGuid():N}");
                try
                {
                    Directory.CreateDirectory(tempDownloadDirectory);
                    var parts = installManifest.Artifact.Parts
                        .OrderBy(part => part.PartIndex)
                        .ToList();
                    if (parts.Count == 0)
                    {
                        throw new InvalidOperationException("Install manifest did not contain any downloadable parts.");
                    }

                    for (var index = 0; index < parts.Count; index++)
                    {
                        progressArgs.CancelToken.ThrowIfCancellationRequested();
                        var part = parts[index];
                        var tempArchivePath = Path.Combine(
                            tempDownloadDirectory,
                            $"part-{part.PartIndex:D4}.zip");
                        var progressBase = (index * 60) / Math.Max(parts.Count, 1);

                        progressArgs.Text = $"Downloading part {index + 1}/{parts.Count} for {installManifest.Game.Name}";
                        progressArgs.CurrentProgressValue = 10 + progressBase;
                        client.DownloadToFileAsync(part.DownloadUrl, tempArchivePath, progressArgs.CancelToken)
                            .GetAwaiter()
                            .GetResult();

                        progressArgs.Text = $"Verifying part {index + 1}/{parts.Count} for {installManifest.Game.Name}";
                        progressArgs.CurrentProgressValue = 20 + progressBase;
                        VerifyFileChecksum(tempArchivePath, part.Checksum);

                        progressArgs.Text = $"Extracting part {index + 1}/{parts.Count} for {installManifest.Game.Name}";
                        progressArgs.CurrentProgressValue = 30 + progressBase;
                        ZipFile.ExtractToDirectory(tempArchivePath, installDirectory);
                    }

                    progressArgs.Text = $"Normalizing install layout for {installManifest.Game.Name}";
                    progressArgs.CurrentProgressValue = 85;
                    FlattenRedundantTopLevelDirectory(installDirectory);

                    progressArgs.Text = $"Selecting executable for {installManifest.Game.Name}";
                    progressArgs.CurrentProgressValue = 90;
                    var executablePath = ResolveExecutablePath(installDirectory);

                    settings.UpsertInstalledGame(new InstalledGameState
                    {
                        GameId = game.GameId,
                        VersionId = selectedVersion.Id,
                        InstallDirectory = installDirectory,
                        ExecutablePath = executablePath,
                    });

                    game.InstallDirectory = installDirectory;
                    game.IsInstalled = true;
                    progressArgs.CurrentProgressValue = 100;
                }
                finally
                {
                    if (Directory.Exists(tempDownloadDirectory))
                    {
                        Directory.Delete(tempDownloadDirectory, true);
                    }
                }
            }
        }

        private async Task<Game> ResumePendingUploadAsync(
            GumoApiClient client,
            PendingGameUpload pending,
            CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(pending.ImportSessionId))
            {
                return await ResumePendingImportSessionAsync(client, pending, cancellationToken);
            }

            if (string.IsNullOrWhiteSpace(pending.UploadId))
            {
                RemovePendingUpload(pending);
                return null;
            }

            var upload = await client.GetUploadAsync(pending.UploadId, cancellationToken);
            if (upload.State == "completed")
            {
                var completedJob = !string.IsNullOrWhiteSpace(pending.JobId)
                    ? await client.GetJobAsync(pending.JobId, cancellationToken)
                    : null;
                RemovePendingUpload(pending);
                CleanupPreparedArtifact(pending);
                return completedJob != null ? ImportCompletedUpload(client, completedJob, pending) : null;
            }

            if (upload.State == "failed" || upload.State == "expired")
            {
                RemovePendingUpload(pending);
                CleanupPreparedArtifact(pending);
                return null;
            }

            if (upload.State == "created" || upload.State == "abandoned" || upload.State == "uploading")
            {
                Logger.Warn(
                    $"Pending Gumo upload '{pending.GameName}' requires manual resume from the upload action; automatic re-upload is disabled.");
                return null;
            }

            if (string.IsNullOrWhiteSpace(pending.JobId) &&
                (upload.State == "uploaded" || upload.State == "queued" || upload.State == "processing"))
            {
                var job = await client.FinalizeUploadAsync(pending.UploadId, cancellationToken);
                pending.JobId = job.Id;
                SavePendingUpload(pending);
            }

            if (string.IsNullOrWhiteSpace(pending.JobId))
            {
                return null;
            }

            var finishedJob = WaitForCompletedUpload(client, pending, cancellationToken);
            RemovePendingUpload(pending);
            CleanupPreparedArtifact(pending);
            return ImportCompletedUpload(client, finishedJob, pending);
        }

        private async Task<Game> ResumePendingImportSessionAsync(
            GumoApiClient client,
            PendingGameUpload pending,
            CancellationToken cancellationToken)
        {
            var session = await client.GetImportSessionAsync(pending.ImportSessionId, cancellationToken);
            if (session.State == "completed")
            {
                var completedJob = !string.IsNullOrWhiteSpace(pending.JobId)
                    ? await client.GetJobAsync(pending.JobId, cancellationToken)
                    : (!string.IsNullOrWhiteSpace(session.JobId)
                        ? await client.GetJobAsync(session.JobId, cancellationToken)
                        : null);
                RemovePendingUpload(pending);
                CleanupPreparedArtifact(pending);
                return completedJob != null ? ImportCompletedUpload(client, completedJob, pending) : null;
            }

            if (session.State == "failed" || session.State == "expired")
            {
                RemovePendingUpload(pending);
                CleanupPreparedArtifact(pending);
                return null;
            }

            if (session.State == "created" || session.State == "abandoned" || session.State == "uploading")
            {
                Logger.Warn(
                    $"Pending Gumo import session '{pending.GameName}' requires manual resume from the upload action; automatic re-upload is disabled.");
                return null;
            }

            if (string.IsNullOrWhiteSpace(pending.JobId) &&
                (session.State == "uploaded" || session.State == "queued" || session.State == "processing"))
            {
                var job = await client.FinalizeImportSessionAsync(session.Id, cancellationToken);
                pending.JobId = job.Id;
                SavePendingUpload(pending);
            }

            if (string.IsNullOrWhiteSpace(pending.JobId))
            {
                pending.JobId = session.JobId;
                if (!string.IsNullOrWhiteSpace(pending.JobId))
                {
                    SavePendingUpload(pending);
                }
            }

            if (string.IsNullOrWhiteSpace(pending.JobId))
            {
                return null;
            }

            var finishedJob = WaitForCompletedUpload(client, pending, cancellationToken);
            RemovePendingUpload(pending);
            CleanupPreparedArtifact(pending);
            return ImportCompletedUpload(client, finishedJob, pending);
        }

        private GumoJob WaitForCompletedUpload(
            GumoApiClient client,
            PendingGameUpload pending,
            CancellationToken cancellationToken)
        {
            return WaitForCompletedUpload(client, pending, cancellationToken, null);
        }

        private GumoJob WaitForCompletedUpload(
            GumoApiClient client,
            PendingGameUpload pending,
            CancellationToken cancellationToken,
            GlobalProgressActionArgs progressArgs)
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var job = client.GetJobAsync(pending.JobId, cancellationToken).GetAwaiter().GetResult();
                switch (job.State)
                {
                    case "completed":
                        if (progressArgs != null)
                        {
                            progressArgs.Text = $"Import completed for '{pending.GameName}'";
                            progressArgs.CurrentProgressValue = 100;
                        }
                        return job;
                    case "failed":
                        RemovePendingUpload(pending);
                        CleanupPreparedArtifact(pending);
                        throw new InvalidOperationException(
                            $"Gumo upload job failed: {job.Error?.Message ?? "unknown error"}");
                    case "pending":
                    case "processing":
                        if (progressArgs != null)
                        {
                            progressArgs.Text = $"Processing upload for '{pending.GameName}'";
                            progressArgs.CurrentProgressValue = 80 + (job.Progress?.Percent ?? 0) / 5;
                        }
                        Thread.Sleep(1000);
                        continue;
                    default:
                        Thread.Sleep(1000);
                        continue;
                }
            }
        }

        private Game ImportCompletedUpload(GumoApiClient client, GumoJob job, PendingGameUpload pending)
        {
            var gameId = job.Result?.GameId;
            if (string.IsNullOrWhiteSpace(gameId))
            {
                Logger.Warn($"Completed Gumo upload for '{pending.GameName}' without a game_id result.");
                return null;
            }

            var game = NormalizeGumoGameMediaUrls(
                client.GetGameAsync(gameId, CancellationToken.None).GetAwaiter().GetResult());
            var versions = client.GetVersionsAsync(gameId, CancellationToken.None).GetAwaiter().GetResult();
            var metadata = GumoMapper.ToGameMetadata(game, versions);
            var imported = PlayniteApi.Database.ImportGame(metadata, this);
            var localGame = imported ?? PlayniteApi.Database.Games.FirstOrDefault(item =>
                item.PluginId == Id &&
                string.Equals(item.GameId, game.Id, StringComparison.OrdinalIgnoreCase));
            if (localGame != null)
            {
                SyncImportedGameMedia(client, localGame, game, CancellationToken.None);
            }

            Logger.Info($"Imported uploaded Gumo game '{game.Name}' into Playnite.");
            return localGame ?? GumoMapper.ToDatabaseGame(game, versions, Id);
        }

        private void SyncImportedGameMedia(
            GumoApiClient client,
            Game importedGame,
            GumoGame gumoGame,
            CancellationToken cancellationToken)
        {
            var changed = false;

            var coverImageId = DownloadMediaToPlayniteFile(client, gumoGame.CoverImage, importedGame.Id, cancellationToken);
            if (!string.IsNullOrWhiteSpace(coverImageId) && !string.Equals(importedGame.CoverImage, coverImageId, StringComparison.Ordinal))
            {
                RemoveDatabaseFileIfManaged(importedGame.CoverImage);
                importedGame.CoverImage = coverImageId;
                changed = true;
            }

            var iconId = DownloadMediaToPlayniteFile(client, gumoGame.Icon, importedGame.Id, cancellationToken);
            if (!string.IsNullOrWhiteSpace(iconId) && !string.Equals(importedGame.Icon, iconId, StringComparison.Ordinal))
            {
                RemoveDatabaseFileIfManaged(importedGame.Icon);
                importedGame.Icon = iconId;
                changed = true;
            }

            var backgroundImageId = DownloadMediaToPlayniteFile(client, gumoGame.BackgroundImage, importedGame.Id, cancellationToken);
            if (!string.IsNullOrWhiteSpace(backgroundImageId) && !string.Equals(importedGame.BackgroundImage, backgroundImageId, StringComparison.Ordinal))
            {
                RemoveDatabaseFileIfManaged(importedGame.BackgroundImage);
                importedGame.BackgroundImage = backgroundImageId;
                changed = true;
            }

            if (changed)
            {
                PlayniteApi.Database.Games.Update(importedGame);
            }
        }

        private string DownloadMediaToPlayniteFile(
            GumoApiClient client,
            string mediaUrl,
            Guid gameId,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(mediaUrl))
            {
                return null;
            }

            var extension = Path.GetExtension(mediaUrl);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".bin";
            }

            var tempPath = Path.Combine(
                Path.GetTempPath(),
                $"gumo-media-{Guid.NewGuid():N}{extension}");

            try
            {
                client.DownloadToFileAsync(mediaUrl, tempPath, cancellationToken)
                    .GetAwaiter()
                    .GetResult();
                return PlayniteApi.Database.AddFile(tempPath, gameId);
            }
            finally
            {
                try
                {
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }
                }
                catch (Exception exception)
                {
                    Logger.Warn($"Failed to delete temporary Gumo media file '{tempPath}': {exception.Message}");
                }
            }
        }

        private void RemoveDatabaseFileIfManaged(string fileId)
        {
            if (string.IsNullOrWhiteSpace(fileId))
            {
                return;
            }

            if (Uri.TryCreate(fileId, UriKind.Absolute, out _))
            {
                return;
            }

            try
            {
                PlayniteApi.Database.RemoveFile(fileId);
            }
            catch (Exception exception)
            {
                Logger.Warn($"Failed to remove Playnite-managed media file '{fileId}': {exception.Message}");
            }
        }

        private string EnsureSaveDirectoryConfigured(
            Game game,
            InstalledGameState installed,
            GumoGameVersion version,
            bool mustExist)
        {
            if (installed == null)
            {
                return null;
            }

            var saveDirectory = ResolveConfiguredSaveDirectory(installed, version);
            if (string.IsNullOrWhiteSpace(saveDirectory))
            {
                ConfigureSaveDirectory(game);
                installed = settings.GetInstalledGame(game.GameId);
                if (installed == null)
                {
                    return null;
                }
                saveDirectory = installed.SaveDirectory;
            }

            if (string.IsNullOrWhiteSpace(saveDirectory))
            {
                return null;
            }

            if (mustExist && !Directory.Exists(saveDirectory))
            {
                PlayniteApi.Dialogs.ShowErrorMessage(
                    $"Configured save directory does not exist:{Environment.NewLine}{saveDirectory}",
                    "Gumo");
                return null;
            }

            installed.SaveDirectory = saveDirectory;
            settings.UpsertInstalledGame(installed);
            return saveDirectory;
        }

        private void BackupSaveSnapshot(
            GumoApiClient client,
            Game game,
            InstalledGameState installed,
            string saveDirectory,
            string saveFilePattern,
            string snapshotName,
            GlobalProgressActionArgs progressArgs)
        {
            progressArgs.Text = $"Packaging save snapshot for {game.Name}";
            progressArgs.CurrentProgressValue = 5;

            var source = new UploadSourceSelection
            {
                Path = saveDirectory,
                IsDirectory = true,
                DefaultGameName = snapshotName,
                DisplayName = new DirectoryInfo(saveDirectory).Name,
                MatchPattern = saveFilePattern,
            };

            var prepared = PrepareUploadArtifacts(source, progressArgs.CancelToken);
            try
            {
                progressArgs.Text = $"Creating save snapshot session for {game.Name}";
                progressArgs.CurrentProgressValue = 15;
                var session = client.CreateSaveSnapshotImportSessionAsync(
                        new GumoCreateSaveSnapshotImportSessionRequest
                        {
                            GameVersionId = installed.VersionId,
                            Name = snapshotName,
                            IdempotencyKey = Guid.NewGuid().ToString("N"),
                        },
                        progressArgs.CancelToken)
                    .GetAwaiter()
                    .GetResult();

                var orderedParts = prepared.Parts.OrderBy(part => part.PartIndex).ToList();
                for (var index = 0; index < orderedParts.Count; index++)
                {
                    progressArgs.CancelToken.ThrowIfCancellationRequested();
                    var preparedPart = orderedParts[index];
                    var fileInfo = new FileInfo(preparedPart.UploadPath);
                    progressArgs.Text = $"Uploading save part {index + 1}/{orderedParts.Count} for {game.Name}";
                    progressArgs.CurrentProgressValue = 20 + (index * 50) / Math.Max(orderedParts.Count, 1);

                    var part = client.CreateImportPartAsync(
                            session.Id,
                            new GumoCreateImportPartRequest
                            {
                                PartIndex = preparedPart.PartIndex,
                                File = new GumoUploadFileDescriptor
                                {
                                    Filename = fileInfo.Name,
                                    SizeBytes = fileInfo.Length,
                                },
                            },
                            progressArgs.CancelToken)
                        .GetAwaiter()
                        .GetResult();

                    if (part.State == "created" || part.State == "abandoned")
                    {
                        client.PutImportPartContentAsync(part.Id, preparedPart.UploadPath, progressArgs.CancelToken)
                            .GetAwaiter()
                            .GetResult();
                    }
                }

                progressArgs.Text = $"Finalizing save snapshot for {game.Name}";
                progressArgs.CurrentProgressValue = 75;
                var job = client.FinalizeImportSessionAsync(session.Id, progressArgs.CancelToken)
                    .GetAwaiter()
                    .GetResult();

                WaitForGenericJobCompletion(client, job.Id, $"save snapshot for '{game.Name}'", progressArgs.CancelToken, progressArgs);
                progressArgs.CurrentProgressValue = 100;
            }
            finally
            {
                CleanupPreparedArtifacts(prepared);
            }
        }

        private void UploadSaveSnapshotForVersion(
            GumoApiClient client,
            string versionId,
            string saveDirectory,
            string saveFilePattern,
            string snapshotName,
            GlobalProgressActionArgs progressArgs,
            string gameName)
        {
            var source = new UploadSourceSelection
            {
                Path = saveDirectory,
                IsDirectory = true,
                DefaultGameName = snapshotName,
                DisplayName = new DirectoryInfo(saveDirectory).Name,
                MatchPattern = saveFilePattern,
            };

            var prepared = PrepareUploadArtifacts(source, progressArgs.CancelToken);
            try
            {
                var session = client.CreateSaveSnapshotImportSessionAsync(
                        new GumoCreateSaveSnapshotImportSessionRequest
                        {
                            GameVersionId = versionId,
                            Name = snapshotName,
                            IdempotencyKey = Guid.NewGuid().ToString("N"),
                        },
                        progressArgs.CancelToken)
                    .GetAwaiter()
                    .GetResult();

                var orderedParts = prepared.Parts.OrderBy(part => part.PartIndex).ToList();
                for (var index = 0; index < orderedParts.Count; index++)
                {
                    progressArgs.CancelToken.ThrowIfCancellationRequested();
                    var preparedPart = orderedParts[index];
                    var fileInfo = new FileInfo(preparedPart.UploadPath);
                    progressArgs.Text = $"Uploading save part {index + 1}/{orderedParts.Count} for {gameName}";
                    progressArgs.CurrentProgressValue = 92 + (index * 4) / Math.Max(orderedParts.Count, 1);

                    var part = client.CreateImportPartAsync(
                            session.Id,
                            new GumoCreateImportPartRequest
                            {
                                PartIndex = preparedPart.PartIndex,
                                File = new GumoUploadFileDescriptor
                                {
                                    Filename = fileInfo.Name,
                                    SizeBytes = fileInfo.Length,
                                },
                            },
                            progressArgs.CancelToken)
                        .GetAwaiter()
                        .GetResult();

                    if (part.State == "created" || part.State == "abandoned")
                    {
                        client.PutImportPartContentAsync(part.Id, preparedPart.UploadPath, progressArgs.CancelToken)
                            .GetAwaiter()
                            .GetResult();
                    }
                }

                progressArgs.Text = $"Finalizing save upload for {gameName}";
                progressArgs.CurrentProgressValue = 97;
                var job = client.FinalizeImportSessionAsync(session.Id, progressArgs.CancelToken)
                    .GetAwaiter()
                    .GetResult();

                WaitForGenericJobCompletion(client, job.Id, $"save snapshot for '{gameName}'", progressArgs.CancelToken, progressArgs);
            }
            finally
            {
                CleanupPreparedArtifacts(prepared);
            }
        }

        private List<GumoSaveSnapshot> LoadSaveSnapshotsWithProgress(
            GumoApiClient client,
            string versionId,
            string gameName)
        {
            List<GumoSaveSnapshot> snapshots = null;
            var result = PlayniteApi.Dialogs.ActivateGlobalProgress(
                progressArgs =>
                {
                    progressArgs.Text = $"Loading save snapshots for {gameName}";
                    snapshots = client.GetSaveSnapshotsAsync(versionId, progressArgs.CancelToken)
                        .GetAwaiter()
                        .GetResult()
                        .OrderByDescending(snapshot => snapshot.CapturedAt)
                        .ToList();
                },
                new GlobalProgressOptions("Loading Gumo save snapshots", true)
                {
                    IsIndeterminate = true,
                });

            if (result.Canceled)
            {
                Logger.Info("Gumo save snapshot loading canceled.");
                return null;
            }

            if (result.Error != null)
            {
                throw result.Error;
            }

            return snapshots ?? new List<GumoSaveSnapshot>();
        }

        private GumoSaveSnapshot SelectSaveSnapshotForRestore(
            IReadOnlyList<GumoSaveSnapshot> snapshots,
            string gameName)
        {
            if (snapshots == null || snapshots.Count == 0)
            {
                PlayniteApi.Dialogs.ShowErrorMessage(
                    $"No save snapshots are available for {gameName}.",
                    "Gumo");
                return null;
            }

            if (snapshots.Count == 1)
            {
                return snapshots[0];
            }

            var items = snapshots
                .Select(snapshot => new OptionListPickerItem
                {
                    Id = snapshot.Id,
                    Title = snapshot.Name,
                    Description = $"{snapshot.Id} | {snapshot.CapturedAt}",
                    Value = snapshot,
                })
                .ToList();

            var selected = ShowOptionListPicker(
                "Restore Gumo Save Snapshot",
                $"Choose a save snapshot to restore for {gameName}.",
                items);

            return selected != null ? (GumoSaveSnapshot)selected.Value : null;
        }

        private void RestoreSaveSnapshot(
            GumoApiClient client,
            Game game,
            string saveDirectory,
            string saveFilePattern,
            GumoSaveSnapshot snapshot,
            bool replaceExisting,
            GlobalProgressActionArgs progressArgs)
        {
            var restoreManifest = client.GetSaveRestoreManifestAsync(snapshot.Id, progressArgs.CancelToken)
                .GetAwaiter()
                .GetResult();

            Directory.CreateDirectory(saveDirectory);
            if (replaceExisting)
            {
                if (string.IsNullOrWhiteSpace(saveFilePattern))
                {
                    ClearDirectoryContents(saveDirectory);
                }
                else
                {
                    ClearMatchingDirectoryContents(saveDirectory, saveFilePattern);
                }
            }

            var tempDownloadDirectory = Path.Combine(
                Path.GetTempPath(),
                $"gumo-save-restore-{snapshot.Id}-{Guid.NewGuid():N}");
            try
            {
                Directory.CreateDirectory(tempDownloadDirectory);
                var parts = restoreManifest.Parts.OrderBy(part => part.PartIndex).ToList();
                for (var index = 0; index < parts.Count; index++)
                {
                    progressArgs.CancelToken.ThrowIfCancellationRequested();
                    var part = parts[index];
                    var tempArchivePath = Path.Combine(tempDownloadDirectory, $"part-{part.PartIndex:D4}.zip");
                    var progressBase = (index * 70) / Math.Max(parts.Count, 1);

                    progressArgs.Text = $"Downloading save part {index + 1}/{parts.Count} for {game.Name}";
                    progressArgs.CurrentProgressValue = 10 + progressBase;
                    client.DownloadToFileAsync(part.DownloadUrl, tempArchivePath, progressArgs.CancelToken)
                        .GetAwaiter()
                        .GetResult();

                    progressArgs.Text = $"Verifying save part {index + 1}/{parts.Count} for {game.Name}";
                    progressArgs.CurrentProgressValue = 20 + progressBase;
                    VerifyFileChecksum(tempArchivePath, part.Checksum);

                    progressArgs.Text = $"Extracting save part {index + 1}/{parts.Count} for {game.Name}";
                    progressArgs.CurrentProgressValue = 30 + progressBase;
                    ZipFile.ExtractToDirectory(tempArchivePath, saveDirectory);
                }

                progressArgs.CurrentProgressValue = 100;
            }
            finally
            {
                if (Directory.Exists(tempDownloadDirectory))
                {
                    Directory.Delete(tempDownloadDirectory, true);
                }
            }
        }

        private void WaitForGenericJobCompletion(
            GumoApiClient client,
            string jobId,
            string label,
            CancellationToken cancellationToken,
            GlobalProgressActionArgs progressArgs)
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var job = client.GetJobAsync(jobId, cancellationToken).GetAwaiter().GetResult();
                switch (job.State)
                {
                    case "completed":
                        progressArgs.Text = $"Completed {label}";
                        return;
                    case "failed":
                        throw new InvalidOperationException(
                            $"Gumo job failed while processing {label}: {job.Error?.Message ?? "unknown error"}");
                    case "pending":
                    case "processing":
                        progressArgs.Text = $"Processing {label}";
                        progressArgs.CurrentProgressValue = 80 + (job.Progress?.Percent ?? 0) / 5;
                        Thread.Sleep(1000);
                        break;
                    default:
                        Thread.Sleep(1000);
                        break;
                }
            }
        }

        private void ClearDirectoryContents(string path)
        {
            foreach (var directory in Directory.GetDirectories(path))
            {
                Directory.Delete(directory, true);
            }

            foreach (var file in Directory.GetFiles(path))
            {
                File.Delete(file);
            }
        }

        private void ClearMatchingDirectoryContents(string path, string matchPattern)
        {
            var directories = Directory.GetDirectories(path, "*", SearchOption.AllDirectories)
                .OrderByDescending(directory => directory.Length)
                .ToList();

            foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
            {
                var relativePath = MakeRelativePath(path, file);
                if (FileMatchesPattern(relativePath, matchPattern))
                {
                    File.Delete(file);
                }
            }

            foreach (var directory in directories)
            {
                if (!Directory.EnumerateFileSystemEntries(directory).Any())
                {
                    Directory.Delete(directory, false);
                }
            }
        }

        private GumoGameVersion SelectVersionForInstall(Game game, List<GumoGameVersion> versions)
        {
            if (versions == null || versions.Count == 0)
            {
                throw new InvalidOperationException($"No Gumo versions are available for {game.Name}.");
            }

            if (versions.Count == 1)
            {
                return versions[0];
            }

            var defaultInput = versions.FirstOrDefault(version => version.IsLatest)?.Id ?? versions[0].Id;
            var prompt = string.Join(
                Environment.NewLine,
                versions.Select(version =>
                    $"{version.Id} ({version.VersionName}{(string.IsNullOrWhiteSpace(version.VersionCode) ? string.Empty : $" / {version.VersionCode}")})"));
            var selection = PlayniteApi.Dialogs.SelectString(
                $"Enter the Gumo version ID to install for {game.Name}:{Environment.NewLine}{prompt}",
                "Gumo",
                defaultInput);

            if (!selection.Result)
            {
                return null;
            }

            var selected = versions.FirstOrDefault(version =>
                string.Equals(version.Id, selection.SelectedString, StringComparison.OrdinalIgnoreCase));
            if (selected == null)
            {
                throw new InvalidOperationException($"Unknown Gumo version ID '{selection.SelectedString}'.");
            }

            return selected;
        }

        private List<GumoLibrary> LoadLibrariesWithProgress(GumoApiClient client, string title)
        {
            List<GumoLibrary> libraries = null;
            var result = PlayniteApi.Dialogs.ActivateGlobalProgress(
                progressArgs =>
                {
                    progressArgs.Text = "Loading Gumo libraries";
                    libraries = client.GetLibrariesAsync(progressArgs.CancelToken)
                        .GetAwaiter()
                        .GetResult()
                        .Where(library => library.Enabled)
                        .ToList();
                },
                new GlobalProgressOptions(title, true)
                {
                    IsIndeterminate = true,
                });

            if (result.Canceled)
            {
                Logger.Info("Gumo library loading canceled.");
                return null;
            }

            if (result.Error != null)
            {
                throw result.Error;
            }

            return libraries ?? new List<GumoLibrary>();
        }

        private GumoLibrary SelectLibraryFromList(IReadOnlyList<GumoLibrary> libraries)
        {
            if (libraries.Count == 0)
            {
                PlayniteApi.Dialogs.ShowErrorMessage("No enabled Gumo libraries are available for uploads.", "Gumo");
                return null;
            }

            if (libraries.Count == 1)
            {
                return libraries[0];
            }

            var defaultInput = libraries[0].Id;
            var prompt = string.Join(
                Environment.NewLine,
                libraries.Select(library => $"{library.Id} ({library.Name}, {library.Platform})"));
            var selection = PlayniteApi.Dialogs.SelectString(
                $"Enter the target Gumo library ID:{Environment.NewLine}{prompt}",
                "Gumo",
                defaultInput);

            if (!selection.Result)
            {
                return null;
            }

            var selected = libraries.FirstOrDefault(library =>
                string.Equals(library.Id, selection.SelectedString, StringComparison.OrdinalIgnoreCase));
            if (selected == null)
            {
                PlayniteApi.Dialogs.ShowErrorMessage(
                    $"Unknown Gumo library ID '{selection.SelectedString}'.",
                    "Gumo");
            }

            return selected;
        }

        private string PromptRequiredString(string message, string caption, string defaultValue)
        {
            var selection = PlayniteApi.Dialogs.SelectString(message, caption, defaultValue ?? string.Empty);
            if (!selection.Result)
            {
                return null;
            }

            var value = selection.SelectedString?.Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                PlayniteApi.Dialogs.ShowErrorMessage($"{message} is required.", caption);
                return null;
            }

            return value;
        }

        private OptionListPickerItem ShowOptionListPicker(
            string title,
            string prompt,
            IReadOnlyList<OptionListPickerItem> items)
        {
            if (items == null || items.Count == 0)
            {
                return null;
            }

            var pickerWindow = PlayniteApi.Dialogs.CreateWindow(new WindowCreationOptions
            {
                ShowCloseButton = true,
                ShowMaximizeButton = false,
                ShowMinimizeButton = false,
            });
            pickerWindow.Title = title;
            pickerWindow.Width = 560;
            pickerWindow.Height = 420;
            pickerWindow.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner;
            pickerWindow.Owner = PlayniteApi.Dialogs.GetCurrentAppWindow();

            var picker = new OptionListPickerWindow(pickerWindow, prompt, items, items[0]);
            pickerWindow.Content = picker;
            var result = pickerWindow.ShowDialog();
            return result == true ? picker.SelectedItem : null;
        }

        private UploadSourceSelection SelectUploadSource()
        {
            var pickerWindow = PlayniteApi.Dialogs.CreateWindow(new WindowCreationOptions
            {
                ShowCloseButton = true,
                ShowMaximizeButton = false,
                ShowMinimizeButton = false,
            });
            pickerWindow.Title = "Gumo Upload Source";
            pickerWindow.Width = 360;
            pickerWindow.Height = 170;
            pickerWindow.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner;
            pickerWindow.Owner = PlayniteApi.Dialogs.GetCurrentAppWindow();

            var picker = new UploadSourcePickerWindow(pickerWindow);
            pickerWindow.Content = picker;
            var result = pickerWindow.ShowDialog();
            if (result != true || picker.Selection == UploadSourcePickerSelection.None)
            {
                return null;
            }

            if (picker.Selection == UploadSourcePickerSelection.Folder)
            {
                using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
                {
                    dialog.Description = "Select game folder to upload to Gumo";
                    if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK || string.IsNullOrWhiteSpace(dialog.SelectedPath))
                    {
                        return null;
                    }

                    var info = new DirectoryInfo(dialog.SelectedPath);
                    return new UploadSourceSelection
                    {
                        Path = dialog.SelectedPath,
                        IsDirectory = true,
                        DefaultGameName = info.Name,
                        DisplayName = info.Name,
                    };
                }
            }

            var fileDialog = new OpenFileDialog
            {
                Title = "Select game file or archive to upload to Gumo",
                CheckFileExists = true,
                Multiselect = false,
            };

            if (fileDialog.ShowDialog() != true || string.IsNullOrWhiteSpace(fileDialog.FileName))
            {
                return null;
            }

            return new UploadSourceSelection
            {
                Path = fileDialog.FileName,
                IsDirectory = false,
                DefaultGameName = Path.GetFileNameWithoutExtension(fileDialog.FileName),
                DisplayName = Path.GetFileName(fileDialog.FileName),
            };
        }

        private GumoUploadGameTarget ResolveUploadGameTarget(
            GumoApiClient client,
            string gameName,
            CancellationToken cancellationToken)
        {
            var existingGames = client.GetGamesAsync(cancellationToken)
                .GetAwaiter()
                .GetResult();
            var existingMatch = existingGames.FirstOrDefault(game =>
                string.Equals(game.Name, gameName, StringComparison.OrdinalIgnoreCase));

            if (existingMatch != null)
            {
                Logger.Info($"Matched existing Gumo game '{gameName}' to {existingMatch.Id} for version upload.");
                return new GumoUploadGameTarget
                {
                    Id = existingMatch.Id,
                };
            }

            return new GumoUploadGameTarget
            {
                Create = new GumoUploadNewGameTarget
                {
                    Name = gameName,
                },
            };
        }

        private string ResolveInstallDirectory(Game game, GumoInstallManifest installManifest)
        {
            var existing = settings.GetInstalledGame(game.GameId);
            if (existing != null && !string.IsNullOrWhiteSpace(existing.InstallDirectory))
            {
                return existing.InstallDirectory;
            }

            if (!string.IsNullOrWhiteSpace(game.InstallDirectory))
            {
                return game.InstallDirectory;
            }

            var selected = PlayniteApi.Dialogs.SelectFolder();
            if (string.IsNullOrWhiteSpace(selected))
            {
                return null;
            }

            return Path.Combine(selected, SanitizePathComponent(installManifest.Game.Name));
        }

        private void EnsureInstallDirectoryIsUsable(string installDirectory)
        {
            if (Directory.Exists(installDirectory) && Directory.EnumerateFileSystemEntries(installDirectory).Any())
            {
                throw new InvalidOperationException(
                    $"Install directory '{installDirectory}' already exists and is not empty.");
            }

            Directory.CreateDirectory(installDirectory);
        }

        private static void VerifyFileChecksum(string filePath, string expectedChecksum)
        {
            if (string.IsNullOrWhiteSpace(expectedChecksum))
            {
                return;
            }

            using (var stream = File.OpenRead(filePath))
            using (var sha256 = SHA256.Create())
            {
                var actualChecksum = BitConverter
                    .ToString(sha256.ComputeHash(stream))
                    .Replace("-", string.Empty)
                    .ToLowerInvariant();
                var normalizedExpected = expectedChecksum.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)
                    ? expectedChecksum.Substring("sha256:".Length)
                    : expectedChecksum;
                if (!string.Equals(actualChecksum, normalizedExpected, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Downloaded archive checksum verification failed.");
                }
            }
        }

        private string ResolveExecutablePath(string installDirectory)
        {
            FlattenRedundantTopLevelDirectory(installDirectory);
            var executables = FindExecutables(installDirectory);

            if (executables.Count == 0)
            {
                TryExpandSingleNestedZip(installDirectory);
                FlattenRedundantTopLevelDirectory(installDirectory);
                executables = FindExecutables(installDirectory);
            }

            if (executables.Count == 0)
            {
                throw new InvalidOperationException(
                    "No executable was found in the extracted install directory. The uploaded payload may need different extraction handling.");
            }

            if (executables.Count == 1)
            {
                return executables[0];
            }

            var items = executables
                .Select(path => new OptionListPickerItem
                {
                    Id = path,
                    Title = Path.GetFileName(path),
                    Description = MakeRelativePath(installDirectory, path),
                    Value = path,
                })
                .ToList();
            var selected = ShowOptionListPicker(
                "Select Executable",
                "Multiple executables were found. Choose the executable to use for this installation.",
                items);

            if (selected == null || !(selected.Value is string selectedPath) || string.IsNullOrWhiteSpace(selectedPath))
            {
                throw new InvalidOperationException("An executable selection is required to finish installation.");
            }

            if (!executables.Contains(selectedPath, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Selected executable '{selectedPath}' was not found in the install directory.");
            }

            return executables.First(path => string.Equals(path, selectedPath, StringComparison.OrdinalIgnoreCase));
        }

        private static List<string> FindExecutables(string installDirectory)
        {
            return Directory
                .EnumerateFiles(installDirectory, "*.exe", SearchOption.AllDirectories)
                .OrderBy(path => path)
                .ToList();
        }

        private static void TryExpandSingleNestedZip(string installDirectory)
        {
            var zipFiles = Directory
                .EnumerateFiles(installDirectory, "*.zip", SearchOption.AllDirectories)
                .OrderBy(path => path)
                .ToList();

            if (zipFiles.Count != 1)
            {
                return;
            }

            var nestedZip = zipFiles[0];
            var extractionRoot = Path.Combine(
                Path.GetDirectoryName(nestedZip) ?? installDirectory,
                $"{Path.GetFileNameWithoutExtension(nestedZip)}__expanded");

            if (Directory.Exists(extractionRoot))
            {
                Directory.Delete(extractionRoot, true);
            }

            Directory.CreateDirectory(extractionRoot);
            ZipFile.ExtractToDirectory(nestedZip, extractionRoot);

            var normalizedRoot = NormalizeSingleTopLevelDirectory(extractionRoot);
            MoveDirectoryContents(normalizedRoot, installDirectory);

            File.Delete(nestedZip);
            Directory.Delete(extractionRoot, true);
        }

        private static string NormalizeSingleTopLevelDirectory(string directory)
        {
            var directories = Directory.GetDirectories(directory);
            var files = Directory.GetFiles(directory);

            if (files.Length == 0 && directories.Length == 1)
            {
                return directories[0];
            }

            return directory;
        }

        private static void MoveDirectoryContents(string sourceDirectory, string destinationDirectory)
        {
            foreach (var directory in Directory.GetDirectories(sourceDirectory))
            {
                var targetDirectory = Path.Combine(destinationDirectory, Path.GetFileName(directory));
                if (Directory.Exists(targetDirectory))
                {
                    MoveDirectoryContents(directory, targetDirectory);
                    Directory.Delete(directory, true);
                }
                else
                {
                    Directory.Move(directory, targetDirectory);
                }
            }

            foreach (var file in Directory.GetFiles(sourceDirectory))
            {
                var targetFile = Path.Combine(destinationDirectory, Path.GetFileName(file));
                if (File.Exists(targetFile))
                {
                    File.Delete(targetFile);
                }
                File.Move(file, targetFile);
            }
        }

        private static void FlattenRedundantTopLevelDirectory(string installDirectory)
        {
            var normalizedRoot = NormalizeSingleTopLevelDirectory(installDirectory);
            if (string.Equals(normalizedRoot, installDirectory, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            MoveDirectoryContents(normalizedRoot, installDirectory);
            if (Directory.Exists(normalizedRoot) && !Directory.EnumerateFileSystemEntries(normalizedRoot).Any())
            {
                Directory.Delete(normalizedRoot, true);
            }
        }

        private static string SanitizePathComponent(string value)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return new string((value ?? "gumo-game").Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
        }

        private void SavePendingUpload(PendingGameUpload pending)
        {
            var uploads = settings.PendingGameUploads?.ToList() ?? new List<PendingGameUpload>();
            uploads.RemoveAll(upload => PendingUploadKey(upload) == PendingUploadKey(pending));
            uploads.Add(pending.Clone());
            settings.ReplacePendingGameUploads(uploads);
        }

        private void RemovePendingUpload(PendingGameUpload pending)
        {
            var uploads = settings.PendingGameUploads?.ToList() ?? new List<PendingGameUpload>();
            uploads.RemoveAll(upload => PendingUploadKey(upload) == PendingUploadKey(pending));
            settings.ReplacePendingGameUploads(uploads);
        }

        private PreparedUploadArtifactSet PrepareUploadArtifacts(UploadSourceSelection source, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!source.IsDirectory && IsZipArchivePath(source.Path))
            {
                return new PreparedUploadArtifactSet
                {
                    Parts = new List<PreparedUploadArtifact>
                    {
                        new PreparedUploadArtifact
                        {
                            UploadPath = source.Path,
                            DeleteAfterUpload = false,
                            PartIndex = 0,
                        },
                    },
                };
            }

            var tempDirectory = Path.Combine(Path.GetTempPath(), "gumo-upload");
            Directory.CreateDirectory(tempDirectory);

            if (!source.IsDirectory)
            {
                var archivePath = Path.Combine(tempDirectory, $"gumo-upload-{Guid.NewGuid():N}.zip");
                using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
                {
                    archive.CreateEntryFromFile(source.Path, Path.GetFileName(source.Path), CompressionLevel.Optimal);
                }

                return new PreparedUploadArtifactSet
                {
                    Parts = new List<PreparedUploadArtifact>
                    {
                        new PreparedUploadArtifact
                        {
                            UploadPath = archivePath,
                            DeleteAfterUpload = true,
                            PartIndex = 0,
                        },
                    },
                };
            }

            var files = EnumerateDirectoryFiles(
                source.Path,
                source.MatchPattern,
                source.ExcludedRelativeRoots,
                source.ExcludeRelativeRoot,
                source.ExcludeMatchPattern);
            if (files.Count == 0)
            {
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(source.MatchPattern)
                        ? "The selected folder does not contain any files to upload."
                        : $"The selected folder does not contain any files matching '{source.MatchPattern}'.");
            }
            var groups = PartitionDirectoryFiles(files, FolderUploadTargetPartSizeBytes);
            var parts = new List<PreparedUploadArtifact>();
            for (var index = 0; index < groups.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var archivePath = Path.Combine(tempDirectory, $"gumo-upload-{Guid.NewGuid():N}.part{index:D4}.zip");
                using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
                {
                    foreach (var file in groups[index])
                    {
                        archive.CreateEntryFromFile(file.FullPath, file.RelativePath, CompressionLevel.Optimal);
                    }
                }

                parts.Add(new PreparedUploadArtifact
                {
                    UploadPath = archivePath,
                    DeleteAfterUpload = true,
                    PartIndex = index,
                });
            }

            return new PreparedUploadArtifactSet
            {
                Parts = parts,
            };
        }

        private static List<DirectoryUploadFile> EnumerateDirectoryFiles(
            string sourceDirectory,
            string matchPattern,
            IEnumerable<string> excludedRelativeRoots,
            string excludeRelativeRoot,
            string excludeMatchPattern)
        {
            var excludedRoots = (excludedRelativeRoots ?? Enumerable.Empty<string>())
                .Where(root => !string.IsNullOrWhiteSpace(root))
                .Select(root => root.Replace('\\', '/').Trim('/'))
                .ToList();

            return Directory
                .EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories)
                .Select(path => new DirectoryUploadFile
                {
                    FullPath = path,
                    RelativePath = MakeRelativePath(sourceDirectory, path),
                    SizeBytes = new FileInfo(path).Length,
                })
                .Where(file => FileMatchesPattern(file.RelativePath, matchPattern))
                .Where(file => !IsExcludedFromUpload(file.RelativePath, excludedRoots, excludeRelativeRoot, excludeMatchPattern))
                .OrderBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static bool HasUploadableFiles(string sourceDirectory, string matchPattern)
        {
            return EnumerateDirectoryFiles(sourceDirectory, matchPattern, null, null, null).Count > 0;
        }

        private static List<List<DirectoryUploadFile>> PartitionDirectoryFiles(
            List<DirectoryUploadFile> files,
            long targetPartSizeBytes)
        {
            var groups = new List<List<DirectoryUploadFile>>();
            var currentGroup = new List<DirectoryUploadFile>();
            long currentGroupSize = 0;

            foreach (var file in files)
            {
                var shouldStartNewGroup =
                    currentGroup.Count > 0 &&
                    currentGroupSize + file.SizeBytes > targetPartSizeBytes;

                if (shouldStartNewGroup)
                {
                    groups.Add(currentGroup);
                    currentGroup = new List<DirectoryUploadFile>();
                    currentGroupSize = 0;
                }

                currentGroup.Add(file);
                currentGroupSize += file.SizeBytes;
            }

            if (currentGroup.Count > 0)
            {
                groups.Add(currentGroup);
            }

            return groups;
        }

        private static string MakeRelativePath(string baseDirectory, string fullPath)
        {
            if (string.IsNullOrWhiteSpace(baseDirectory))
            {
                throw new ArgumentException("Base directory is required.", nameof(baseDirectory));
            }

            if (string.IsNullOrWhiteSpace(fullPath))
            {
                throw new ArgumentException("Full path is required.", nameof(fullPath));
            }

            var relativePath = GetRelativePathCompat(
                Path.GetFullPath(baseDirectory),
                Path.GetFullPath(fullPath));

            if (string.Equals(relativePath, ".", StringComparison.Ordinal))
            {
                return string.Empty;
            }

            return relativePath.Replace('\\', '/');
        }

        private static string GetRelativePathCompat(string baseDirectory, string fullPath)
        {
            var normalizedBase = Path.GetFullPath(baseDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var normalizedFull = Path.GetFullPath(fullPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            var baseRoot = Path.GetPathRoot(normalizedBase);
            var fullRoot = Path.GetPathRoot(normalizedFull);
            if (!string.Equals(baseRoot, fullRoot, StringComparison.OrdinalIgnoreCase))
            {
                return normalizedFull;
            }

            var baseSegments = normalizedBase
                .Substring(baseRoot.Length)
                .Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
            var fullSegments = normalizedFull
                .Substring(fullRoot.Length)
                .Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);

            var commonLength = 0;
            while (commonLength < baseSegments.Length &&
                   commonLength < fullSegments.Length &&
                   string.Equals(baseSegments[commonLength], fullSegments[commonLength], StringComparison.OrdinalIgnoreCase))
            {
                commonLength++;
            }

            if (commonLength == baseSegments.Length && commonLength == fullSegments.Length)
            {
                return ".";
            }

            var parts = new List<string>();
            for (var index = commonLength; index < baseSegments.Length; index++)
            {
                parts.Add("..");
            }

            for (var index = commonLength; index < fullSegments.Length; index++)
            {
                parts.Add(fullSegments[index]);
            }

            return parts.Count == 0
                ? "."
                : string.Join(Path.DirectorySeparatorChar.ToString(), parts);
        }

        private static string EnsureTrailingDirectorySeparator(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            return value.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ||
                   value.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? value
                : value + Path.DirectorySeparatorChar;
        }

        private static bool FileMatchesPattern(string relativePath, string matchPattern)
        {
            if (string.IsNullOrWhiteSpace(matchPattern))
            {
                return true;
            }

            var normalizedPattern = matchPattern.Trim().Replace('\\', '/');
            var normalizedPath = relativePath.Replace('\\', '/');
            var target = normalizedPattern.Contains("/")
                ? normalizedPath
                : Path.GetFileName(normalizedPath);

            return CompileSavePatternRegex(normalizedPattern).IsMatch(target);
        }

        private static bool IsExcludedFromUpload(
            string relativePath,
            IReadOnlyList<string> excludedRelativeRoots,
            string excludeRelativeRoot,
            string excludeMatchPattern)
        {
            var normalizedPath = relativePath.Replace('\\', '/');
            if (excludedRelativeRoots != null)
            {
                foreach (var root in excludedRelativeRoots)
                {
                    if (string.IsNullOrWhiteSpace(root))
                    {
                        continue;
                    }

                    if (string.Equals(normalizedPath, root, StringComparison.OrdinalIgnoreCase) ||
                        normalizedPath.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(excludeMatchPattern))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(excludeRelativeRoot))
            {
                return FileMatchesPattern(relativePath, excludeMatchPattern);
            }

            var normalizedRoot = excludeRelativeRoot.Replace('\\', '/').Trim('/');
            if (string.IsNullOrWhiteSpace(normalizedRoot))
            {
                return FileMatchesPattern(relativePath, excludeMatchPattern);
            }

            if (string.Equals(normalizedPath, normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!normalizedPath.StartsWith(normalizedRoot + "/", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var scopedRelativePath = normalizedPath.Substring(normalizedRoot.Length + 1);
            return FileMatchesPattern(scopedRelativePath, excludeMatchPattern);
        }

        private static bool IsPathInsideOrEqual(string candidatePath, string rootPath)
        {
            var candidateFullPath = EnsureTrailingDirectorySeparator(Path.GetFullPath(candidatePath));
            var rootFullPath = EnsureTrailingDirectorySeparator(Path.GetFullPath(rootPath));
            return candidateFullPath.StartsWith(rootFullPath, StringComparison.OrdinalIgnoreCase);
        }

        private static Regex CompileSavePatternRegex(string pattern)
        {
            return new Regex(
                pattern,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        private static bool IsZipArchivePath(string path)
        {
            return string.Equals(Path.GetExtension(path), ".zip", StringComparison.OrdinalIgnoreCase);
        }

        private void CleanupPreparedArtifact(PendingGameUpload pending)
        {
            if (pending != null && pending.IsTemporaryPackagedArtifact)
            {
                TryDeleteFile(pending.PackagedPath);
            }
        }

        private void CleanupPreparedArtifacts(PreparedUploadArtifactSet prepared)
        {
            if (prepared?.Parts == null)
            {
                return;
            }

            foreach (var part in prepared.Parts)
            {
                if (part.DeleteAfterUpload)
                {
                    TryDeleteFile(part.UploadPath);
                }
            }
        }

        private void TryDeleteFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception exception)
            {
                Logger.Warn($"Failed to delete temporary Gumo artifact '{path}': {exception.Message}");
            }
        }

        private string PendingUploadKey(PendingGameUpload pending)
        {
            return !string.IsNullOrWhiteSpace(pending?.ImportSessionId)
                ? $"ims:{pending.ImportSessionId}"
                : $"upl:{pending?.UploadId}";
        }

        private sealed class PreparedUploadArtifact
        {
            public string UploadPath { get; set; }

            public bool DeleteAfterUpload { get; set; }

            public int PartIndex { get; set; }
        }

        private sealed class PreparedUploadArtifactSet
        {
            public List<PreparedUploadArtifact> Parts { get; set; } = new List<PreparedUploadArtifact>();
        }

        private sealed class UploadSourceSelection
        {
            public string Path { get; set; }

            public bool IsDirectory { get; set; }

            public string DefaultGameName { get; set; }

            public string DisplayName { get; set; }

            public string MatchPattern { get; set; }

            public string ExcludeMatchPattern { get; set; }

            public string ExcludeRelativeRoot { get; set; }

            public List<string> ExcludedRelativeRoots { get; set; } = new List<string>();
        }

        private sealed class LocalUploadSaveConfiguration
        {
            public string SavePath { get; set; }

            public string SavePathType { get; set; }

            public string SaveFilePattern { get; set; }

            public string ResolvedDirectory { get; set; }

            public string SnapshotName { get; set; }
        }

        private enum SavePathType
        {
            Relative = 0,
            Absolute = 1,
        }

        private enum LocalSaveUploadAction
        {
            Configure = 0,
            Skip = 1,
            Cancel = 2,
        }

        private sealed class DirectoryUploadFile
        {
            public string FullPath { get; set; }

            public string RelativePath { get; set; }

            public long SizeBytes { get; set; }
        }
    }
}
