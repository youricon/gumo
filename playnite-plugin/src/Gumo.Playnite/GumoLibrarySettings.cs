using System;
using System.Collections.Generic;
using System.Linq;
using Playnite.SDK;
using Playnite.SDK.Data;

namespace Gumo.Playnite
{
    public class GumoLibrarySettings : ObservableObject, ISettings
    {
        private readonly GumoLibraryPlugin plugin;
        private string gumoServerUrl = "http://127.0.0.1:8080";
        private string apiToken = string.Empty;
        private bool importPublicGamesOnly = false;
        private List<PendingGameUpload> pendingGameUploads = new List<PendingGameUpload>();
        private List<InstalledGameState> installedGames = new List<InstalledGameState>();

        private string editingServerUrl;
        private string editingApiToken;
        private bool editingImportPublicGamesOnly;
        private List<PendingGameUpload> editingPendingGameUploads;
        private List<InstalledGameState> editingInstalledGames;

        public GumoLibrarySettings()
        {
        }

        public GumoLibrarySettings(GumoLibraryPlugin plugin)
        {
            this.plugin = plugin;
            var savedSettings = plugin.LoadPluginSettings<GumoLibrarySettings>();
            if (savedSettings != null)
            {
                GumoServerUrl = savedSettings.GumoServerUrl;
                ApiToken = savedSettings.ApiToken;
                ImportPublicGamesOnly = savedSettings.ImportPublicGamesOnly;
                PendingGameUploads = savedSettings.PendingGameUploads ?? new List<PendingGameUpload>();
                InstalledGames = savedSettings.InstalledGames ?? new List<InstalledGameState>();
            }
        }

        public string GumoServerUrl
        {
            get => gumoServerUrl;
            set => SetValue(ref gumoServerUrl, value);
        }

        public string ApiToken
        {
            get => apiToken;
            set => SetValue(ref apiToken, value);
        }

        public bool ImportPublicGamesOnly
        {
            get => importPublicGamesOnly;
            set => SetValue(ref importPublicGamesOnly, value);
        }

        public List<PendingGameUpload> PendingGameUploads
        {
            get => pendingGameUploads;
            set => SetValue(ref pendingGameUploads, value ?? new List<PendingGameUpload>());
        }

        public List<InstalledGameState> InstalledGames
        {
            get => installedGames;
            set => SetValue(ref installedGames, value ?? new List<InstalledGameState>());
        }

        public bool HasConnectionSettings()
        {
            return Uri.TryCreate(GumoServerUrl, UriKind.Absolute, out _) &&
                   !string.IsNullOrWhiteSpace(ApiToken);
        }

        public string NormalizedServerUrl()
        {
            return (GumoServerUrl ?? string.Empty).Trim().TrimEnd('/');
        }

        public void BeginEdit()
        {
            editingServerUrl = GumoServerUrl;
            editingApiToken = ApiToken;
            editingImportPublicGamesOnly = ImportPublicGamesOnly;
            editingPendingGameUploads = ClonePendingUploads();
            editingInstalledGames = CloneInstalledGames();
        }

        public void CancelEdit()
        {
            GumoServerUrl = editingServerUrl;
            ApiToken = editingApiToken;
            ImportPublicGamesOnly = editingImportPublicGamesOnly;
            PendingGameUploads = editingPendingGameUploads ?? new List<PendingGameUpload>();
            InstalledGames = editingInstalledGames ?? new List<InstalledGameState>();
        }

        public void EndEdit()
        {
            plugin?.SavePluginSettings(this);
        }

        public bool VerifySettings(out List<string> errors)
        {
            errors = new List<string>();

            if (string.IsNullOrWhiteSpace(GumoServerUrl))
            {
                errors.Add("Gumo server URL is required.");
            }
            else if (!Uri.TryCreate(GumoServerUrl, UriKind.Absolute, out _))
            {
                errors.Add("Gumo server URL must be an absolute URL.");
            }

            if (string.IsNullOrWhiteSpace(ApiToken))
            {
                errors.Add("API token is required.");
            }

            return errors.Count == 0;
        }

        public void ReplacePendingGameUploads(IEnumerable<PendingGameUpload> uploads)
        {
            PendingGameUploads = uploads?.Select(upload => upload.Clone()).ToList() ?? new List<PendingGameUpload>();
            plugin?.SavePluginSettings(this);
        }

        public InstalledGameState GetInstalledGame(string gameId)
        {
            return InstalledGames?.FirstOrDefault(game => string.Equals(game.GameId, gameId, StringComparison.OrdinalIgnoreCase));
        }

        public void UpsertInstalledGame(InstalledGameState installedGame)
        {
            var games = CloneInstalledGames();
            games.RemoveAll(game => string.Equals(game.GameId, installedGame.GameId, StringComparison.OrdinalIgnoreCase));
            games.Add(installedGame.Clone());
            InstalledGames = games;
            plugin?.SavePluginSettings(this);
        }

        public void RemoveInstalledGame(string gameId)
        {
            var games = CloneInstalledGames();
            games.RemoveAll(game => string.Equals(game.GameId, gameId, StringComparison.OrdinalIgnoreCase));
            InstalledGames = games;
            plugin?.SavePluginSettings(this);
        }

        private List<PendingGameUpload> ClonePendingUploads()
        {
            return PendingGameUploads?.Select(upload => upload.Clone()).ToList() ?? new List<PendingGameUpload>();
        }

        private List<InstalledGameState> CloneInstalledGames()
        {
            return InstalledGames?.Select(game => game.Clone()).ToList() ?? new List<InstalledGameState>();
        }
    }

    public class PendingGameUpload
    {
        public string ImportSessionId { get; set; }

        public string UploadPartId { get; set; }

        public string UploadId { get; set; }

        public string JobId { get; set; }

        public string LibraryId { get; set; }

        public string Platform { get; set; }

        public string GameName { get; set; }

        public string VersionName { get; set; }

        public string SourcePath { get; set; }

        public string FileName { get; set; }

        public string PackagedPath { get; set; }

        public bool IsTemporaryPackagedArtifact { get; set; }

        public string IdempotencyKey { get; set; }

        public PendingGameUpload Clone()
        {
            return (PendingGameUpload)MemberwiseClone();
        }
    }

    public class InstalledGameState
    {
        public string GameId { get; set; }

        public string VersionId { get; set; }

        public string InstallDirectory { get; set; }

        public string ExecutablePath { get; set; }

        public InstalledGameState Clone()
        {
            return (InstalledGameState)MemberwiseClone();
        }
    }
}
