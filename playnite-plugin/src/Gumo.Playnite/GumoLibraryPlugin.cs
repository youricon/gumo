using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
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
        private readonly GumoLibrarySettings settings;
        private readonly CancellationTokenSource startupProbeCancellation = new CancellationTokenSource();

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
                        metadata.Add(GumoMapper.ToGameMetadata(game, versions));
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

            if (!args.Games.Any(IsGumoGame))
            {
                return Enumerable.Empty<GameMenuItem>();
            }

            return new[]
            {
                new GameMenuItem
                {
                    Description = "Push selected metadata to Gumo",
                    Action = _ => PushMetadataToGumo(args.Games),
                }
            };
        }

        public override IEnumerable<InstallAction> GetInstallActions(GetInstallActionsArgs args)
        {
            if (args?.Game == null || !IsGumoGame(args.Game))
            {
                return Enumerable.Empty<InstallAction>();
            }

            return new[]
            {
                new InstallAction(new GumoInstallController(this, args.Game))
                {
                    Name = "Install from Gumo",
                }
            };
        }

        public override IEnumerable<PlayAction> GetPlayActions(GetPlayActionsArgs args)
        {
            if (args?.Game == null || !IsGumoGame(args.Game))
            {
                return Enumerable.Empty<PlayAction>();
            }

            var installed = settings.GetInstalledGame(args.Game.GameId);
            if (installed == null || string.IsNullOrWhiteSpace(installed.ExecutablePath) || !File.Exists(installed.ExecutablePath))
            {
                return Enumerable.Empty<PlayAction>();
            }

            return new[]
            {
                new PlayAction
                {
                    Name = "Play",
                    Type = PlayActionType.File,
                    Path = installed.ExecutablePath,
                    IsPlayAction = true,
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

        internal void InstallGameFromController(Game game)
        {
            if (!settings.HasConnectionSettings())
            {
                PlayniteApi.Dialogs.ShowErrorMessage(
                    "Configure the Gumo server URL and API token before installing a game.",
                    "Gumo");
                return;
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
                    return;
                }

                if (result.Error != null)
                {
                    throw result.Error;
                }
            }
            catch (OperationCanceledException)
            {
                Logger.Info($"Install canceled for {game.Name}.");
            }
            catch (GumoApiException exception)
            {
                var message =
                    $"Failed to install from Gumo: {(int)exception.StatusCode} {exception.StatusCode} - {exception.ApiMessage}";
                Logger.Error(message);
                PlayniteApi.Dialogs.ShowErrorMessage(message, "Gumo");
            }
            catch (Exception exception)
            {
                Logger.Error($"Unexpected failure during Gumo install: {exception}");
                PlayniteApi.Dialogs.ShowErrorMessage(
                    $"Unexpected failure during Gumo install: {exception.Message}",
                    "Gumo");
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

        private Game RunGameUploadImport(CancellationToken cancellationToken)
        {
            var sourceFile = SelectUploadFile();
            if (string.IsNullOrWhiteSpace(sourceFile))
            {
                return null;
            }

            using (var client = CreateApiClient())
            {
                var library = SelectLibrary(client, cancellationToken);
                if (library == null)
                {
                    return null;
                }

                var gameName = PromptRequiredString(
                    "Game name",
                    "Gumo",
                    Path.GetFileNameWithoutExtension(sourceFile));
                if (gameName == null)
                {
                    return null;
                }

                var versionName = PromptRequiredString("Version name", "Gumo", "Initial");
                if (versionName == null)
                {
                    return null;
                }

                var gameTarget = ResolveUploadGameTarget(client, gameName, cancellationToken);

                var fileInfo = new FileInfo(sourceFile);
                var pending = new PendingGameUpload
                {
                    LibraryId = library.Id,
                    Platform = library.Platform,
                    GameName = gameName,
                    VersionName = versionName,
                    SourcePath = sourceFile,
                    FileName = fileInfo.Name,
                    IdempotencyKey = Guid.NewGuid().ToString("N"),
                };

                var upload = client.CreateGamePayloadUploadAsync(
                        new GumoCreateGamePayloadUploadRequest
                        {
                            LibraryId = library.Id,
                            Platform = library.Platform,
                            Game = gameTarget,
                            Version = new GumoUploadVersionTarget
                            {
                                VersionName = versionName,
                            },
                            File = new GumoUploadFileDescriptor
                            {
                                Filename = fileInfo.Name,
                                SizeBytes = fileInfo.Length,
                            },
                            IdempotencyKey = pending.IdempotencyKey,
                        },
                        cancellationToken)
                    .GetAwaiter()
                    .GetResult();

                pending.UploadId = upload.Id;
                SavePendingUpload(pending);

                if (upload.State == "created" || upload.State == "abandoned")
                {
                    client.PutUploadContentAsync(upload.Id, sourceFile, cancellationToken)
                        .GetAwaiter()
                        .GetResult();
                }

                var job = client.FinalizeUploadAsync(upload.Id, cancellationToken)
                    .GetAwaiter()
                    .GetResult();
                pending.JobId = job.Id;
                SavePendingUpload(pending);

                var imported = WaitForCompletedUpload(client, pending, cancellationToken);
                RemovePendingUpload(pending.UploadId);
                return imported;
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
                Game importedGame = null;
                var result = PlayniteApi.Dialogs.ActivateGlobalProgress(
                    progressArgs =>
                    {
                        importedGame = RunGameUploadImport(progressArgs.CancelToken);
                    },
                    new GlobalProgressOptions("Uploading game archive to Gumo", true)
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
                                client.PatchGameAsync(
                                        game.GameId,
                                        GumoMapper.ToPatchGameRequest(game),
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

        private bool IsGumoGame(Game game)
        {
            return game != null &&
                   game.PluginId == Id &&
                   !string.IsNullOrWhiteSpace(game.GameId);
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

                var tempArchivePath = Path.Combine(
                    Path.GetTempPath(),
                    $"gumo-install-{installManifest.Artifact.Id}.zip");
                try
                {
                    progressArgs.Text = $"Downloading {installManifest.Game.Name}";
                    progressArgs.CurrentProgressValue = 10;
                    var part = installManifest.Artifact.Parts.FirstOrDefault();
                    if (part == null)
                    {
                        throw new InvalidOperationException("Install manifest did not contain any downloadable parts.");
                    }

                    client.DownloadToFileAsync(part.DownloadUrl, tempArchivePath, progressArgs.CancelToken)
                        .GetAwaiter()
                        .GetResult();

                    progressArgs.Text = $"Verifying {installManifest.Game.Name}";
                    progressArgs.CurrentProgressValue = 45;
                    VerifyFileChecksum(tempArchivePath, installManifest.Artifact.Checksum);

                    progressArgs.Text = $"Extracting {installManifest.Game.Name}";
                    progressArgs.CurrentProgressValue = 70;
                    ZipFile.ExtractToDirectory(tempArchivePath, installDirectory);

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
                    InvokeOnInstalled(new GameInstalledEventArgs(
                        new GameInstallationData
                        {
                            GameID = game.Id,
                            InstallDirectory = installDirectory,
                        }));
                    progressArgs.CurrentProgressValue = 100;
                }
                finally
                {
                    if (File.Exists(tempArchivePath))
                    {
                        File.Delete(tempArchivePath);
                    }
                }
            }
        }

        private async Task<Game> ResumePendingUploadAsync(
            GumoApiClient client,
            PendingGameUpload pending,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(pending.UploadId))
            {
                RemovePendingUpload(pending.UploadId);
                return null;
            }

            var upload = await client.GetUploadAsync(pending.UploadId, cancellationToken);
            if (upload.State == "completed")
            {
                var completedJob = !string.IsNullOrWhiteSpace(pending.JobId)
                    ? await client.GetJobAsync(pending.JobId, cancellationToken)
                    : null;
                RemovePendingUpload(pending.UploadId);
                return completedJob != null ? ImportCompletedUpload(client, completedJob, pending) : null;
            }

            if (upload.State == "failed" || upload.State == "expired")
            {
                RemovePendingUpload(pending.UploadId);
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

            var imported = WaitForCompletedUpload(client, pending, cancellationToken);
            RemovePendingUpload(pending.UploadId);
            return imported;
        }

        private Game WaitForCompletedUpload(
            GumoApiClient client,
            PendingGameUpload pending,
            CancellationToken cancellationToken)
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var job = client.GetJobAsync(pending.JobId, cancellationToken).GetAwaiter().GetResult();
                switch (job.State)
                {
                    case "completed":
                        return ImportCompletedUpload(client, job, pending);
                    case "failed":
                        RemovePendingUpload(pending.UploadId);
                        throw new InvalidOperationException(
                            $"Gumo upload job failed: {job.Error?.Message ?? "unknown error"}");
                    case "pending":
                    case "processing":
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
            var gameIdToken = job.Result != null ? job.Result["game_id"] : null;
            var gameId = gameIdToken != null ? gameIdToken.ToString() : null;
            if (string.IsNullOrWhiteSpace(gameId))
            {
                Logger.Warn($"Completed Gumo upload for '{pending.GameName}' without a game_id result.");
                return null;
            }

            var game = client.GetGameAsync(gameId, CancellationToken.None).GetAwaiter().GetResult();
            var versions = client.GetVersionsAsync(gameId, CancellationToken.None).GetAwaiter().GetResult();
            var metadata = GumoMapper.ToGameMetadata(game, versions);
            var imported = PlayniteApi.Database.ImportGame(metadata, this);

            Logger.Info($"Imported uploaded Gumo game '{game.Name}' into Playnite.");
            return imported ?? GumoMapper.ToDatabaseGame(game, versions, Id);
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

        private GumoLibrary SelectLibrary(GumoApiClient client, CancellationToken cancellationToken)
        {
            var libraries = client.GetLibrariesAsync(cancellationToken)
                .GetAwaiter()
                .GetResult()
                .Where(library => library.Enabled)
                .ToList();

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

        private string SelectUploadFile()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select game archive payload to upload to Gumo",
                CheckFileExists = true,
                Multiselect = false,
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
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
                if (!string.Equals(actualChecksum, expectedChecksum, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Downloaded archive checksum verification failed.");
                }
            }
        }

        private string ResolveExecutablePath(string installDirectory)
        {
            var executables = Directory
                .EnumerateFiles(installDirectory, "*.exe", SearchOption.AllDirectories)
                .OrderBy(path => path)
                .ToList();

            if (executables.Count == 0)
            {
                throw new InvalidOperationException("No executable was found in the extracted install directory.");
            }

            if (executables.Count == 1)
            {
                return executables[0];
            }

            var defaultInput = executables[0];
            var prompt = string.Join(Environment.NewLine, executables);
            var selection = PlayniteApi.Dialogs.SelectString(
                $"Multiple executables were found. Enter the executable path to use:{Environment.NewLine}{prompt}",
                "Gumo",
                defaultInput);

            if (!selection.Result || string.IsNullOrWhiteSpace(selection.SelectedString))
            {
                throw new InvalidOperationException("An executable selection is required to finish installation.");
            }

            if (!executables.Contains(selection.SelectedString, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Selected executable '{selection.SelectedString}' was not found in the install directory.");
            }

            return executables.First(path => string.Equals(path, selection.SelectedString, StringComparison.OrdinalIgnoreCase));
        }

        private static string SanitizePathComponent(string value)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return new string((value ?? "gumo-game").Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
        }

        private void SavePendingUpload(PendingGameUpload pending)
        {
            var uploads = settings.PendingGameUploads?.ToList() ?? new List<PendingGameUpload>();
            uploads.RemoveAll(upload => upload.UploadId == pending.UploadId);
            uploads.Add(pending.Clone());
            settings.ReplacePendingGameUploads(uploads);
        }

        private void RemovePendingUpload(string uploadId)
        {
            var uploads = settings.PendingGameUploads?.ToList() ?? new List<PendingGameUpload>();
            uploads.RemoveAll(upload => upload.UploadId == uploadId);
            settings.ReplacePendingGameUploads(uploads);
        }
    }
}
