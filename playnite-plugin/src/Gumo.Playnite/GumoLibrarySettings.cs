using System;
using System.Collections.Generic;
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

        private string editingServerUrl;
        private string editingApiToken;
        private bool editingImportPublicGamesOnly;

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

        public bool HasConnectionSettings()
        {
            return Uri.TryCreate(GumoServerUrl, UriKind.Absolute, out _) &&
                   !string.IsNullOrWhiteSpace(ApiToken);
        }

        public void BeginEdit()
        {
            editingServerUrl = GumoServerUrl;
            editingApiToken = ApiToken;
            editingImportPublicGamesOnly = ImportPublicGamesOnly;
        }

        public void CancelEdit()
        {
            GumoServerUrl = editingServerUrl;
            ApiToken = editingApiToken;
            ImportPublicGamesOnly = editingImportPublicGamesOnly;
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
    }
}
