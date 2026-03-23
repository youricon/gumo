using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
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
        private const long FolderUploadTargetPartSizeBytes = 8L * 1024 * 1024 * 1024;
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

        private Game RunGameUploadImport(GlobalProgressActionArgs progressArgs, UploadSourceSelection source)
        {
            var cancellationToken = progressArgs.CancelToken;
            progressArgs.Text = "Preparing upload";
            progressArgs.CurrentProgressValue = 0;

            PreparedUploadArtifactSet prepared = null;
            try
            {
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
                        source.DefaultGameName);
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
                    progressArgs.Text = source.IsDirectory
                        ? $"Packaging folder '{source.DisplayName}'"
                        : $"Preparing '{source.DisplayName}'";
                    progressArgs.CurrentProgressValue = 10;
                    prepared = PrepareUploadArtifacts(source, cancellationToken);
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

                    var imported = WaitForCompletedUpload(client, pending, cancellationToken, progressArgs);
                    RemovePendingUpload(pending);
                    CleanupPreparedArtifact(pending);
                    return imported;
                }
            }
            catch
            {
                if (prepared != null)
                {
                    CleanupPreparedArtifacts(prepared);
                }
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

                Game importedGame = null;
                var result = PlayniteApi.Dialogs.ActivateGlobalProgress(
                    progressArgs =>
                    {
                        importedGame = RunGameUploadImport(progressArgs, source);
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

            var imported = WaitForCompletedUpload(client, pending, cancellationToken);
            RemovePendingUpload(pending);
            CleanupPreparedArtifact(pending);
            return imported;
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

            var imported = WaitForCompletedUpload(client, pending, cancellationToken);
            RemovePendingUpload(pending);
            CleanupPreparedArtifact(pending);
            return imported;
        }

        private Game WaitForCompletedUpload(
            GumoApiClient client,
            PendingGameUpload pending,
            CancellationToken cancellationToken)
        {
            return WaitForCompletedUpload(client, pending, cancellationToken, null);
        }

        private Game WaitForCompletedUpload(
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
                        return ImportCompletedUpload(client, job, pending);
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

        private UploadSourceSelection SelectUploadSource()
        {
            var selection = PlayniteApi.Dialogs.SelectString(
                "Enter a local path to a file, archive, or folder to upload",
                "Gumo",
                string.Empty);
            if (!selection.Result || string.IsNullOrWhiteSpace(selection.SelectedString))
            {
                return null;
            }

            var rawPath = selection.SelectedString.Trim().Trim('"');
            if (Directory.Exists(rawPath))
            {
                var info = new DirectoryInfo(rawPath);
                return new UploadSourceSelection
                {
                    Path = rawPath,
                    IsDirectory = true,
                    DefaultGameName = info.Name,
                    DisplayName = info.Name,
                };
            }

            if (File.Exists(rawPath))
            {
                return new UploadSourceSelection
                {
                    Path = rawPath,
                    IsDirectory = false,
                    DefaultGameName = Path.GetFileNameWithoutExtension(rawPath),
                    DisplayName = Path.GetFileName(rawPath),
                };
            }

            PlayniteApi.Dialogs.ShowErrorMessage(
                $"The path '{rawPath}' does not exist or is not accessible.",
                "Gumo");
            return null;
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

            var files = EnumerateDirectoryFiles(source.Path);
            if (files.Count == 0)
            {
                throw new InvalidOperationException("The selected folder does not contain any files to upload.");
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

        private static List<DirectoryUploadFile> EnumerateDirectoryFiles(string sourceDirectory)
        {
            return Directory
                .EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories)
                .Select(path => new DirectoryUploadFile
                {
                    FullPath = path,
                    RelativePath = MakeRelativePath(sourceDirectory, path),
                    SizeBytes = new FileInfo(path).Length,
                })
                .OrderBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase)
                .ToList();
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
            var basePath = baseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                           Path.DirectorySeparatorChar;
            var baseUri = new Uri(basePath);
            var fullUri = new Uri(fullPath);
            return Uri.UnescapeDataString(baseUri.MakeRelativeUri(fullUri).ToString())
                .Replace('\\', '/');
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
        }

        private sealed class DirectoryUploadFile
        {
            public string FullPath { get; set; }

            public string RelativePath { get; set; }

            public long SizeBytes { get; set; }
        }
    }
}
