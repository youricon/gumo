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

        private string editingServerUrl;
        private string editingApiToken;
        private bool editingImportPublicGamesOnly;
        private List<PendingGameUpload> editingPendingGameUploads;

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
        }

        public void CancelEdit()
        {
            GumoServerUrl = editingServerUrl;
            ApiToken = editingApiToken;
            ImportPublicGamesOnly = editingImportPublicGamesOnly;
            PendingGameUploads = editingPendingGameUploads ?? new List<PendingGameUpload>();
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

        private List<PendingGameUpload> ClonePendingUploads()
        {
            return PendingGameUploads?.Select(upload => upload.Clone()).ToList() ?? new List<PendingGameUpload>();
        }
    }

    public class PendingGameUpload
    {
        public string UploadId { get; set; }

        public string JobId { get; set; }

        public string LibraryId { get; set; }

        public string Platform { get; set; }

        public string GameName { get; set; }

        public string VersionName { get; set; }

        public string SourcePath { get; set; }

        public string FileName { get; set; }

        public string IdempotencyKey { get; set; }

        public PendingGameUpload Clone()
        {
            return (PendingGameUpload)MemberwiseClone();
        }
    }
}
