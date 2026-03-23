using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Playnite.SDK;

namespace Gumo.Playnite
{
    public sealed class GumoApiClient : IDisposable
    {
        private static readonly ILogger Logger = LogManager.GetLogger();
        private readonly HttpClient httpClient;
        private readonly string serverUrl;

        public GumoApiClient(string serverUrl, string apiToken)
        {
            this.serverUrl = NormalizeServerUrl(serverUrl);
            httpClient = new HttpClient
            {
                BaseAddress = new Uri(this.serverUrl, UriKind.Absolute),
                Timeout = TimeSpan.FromSeconds(30),
            };
            httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiToken.Trim());
            httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task<int> ProbeAsync(CancellationToken cancellationToken)
        {
            var response = await GetAsync<GumoListResponse<GumoGame>>(
                "/api/integrations/playnite/games",
                cancellationToken);
            return response.Items?.Count ?? 0;
        }

        public async Task<List<GumoGame>> GetGamesAsync(CancellationToken cancellationToken)
        {
            var response = await GetAsync<GumoListResponse<GumoGame>>(
                "/api/integrations/playnite/games",
                cancellationToken);
            return response.Items ?? new List<GumoGame>();
        }

        public Task<GumoGame> GetGameAsync(string gameId, CancellationToken cancellationToken)
        {
            return GetAsync<GumoGame>(
                $"/api/integrations/playnite/games/{Uri.EscapeDataString(gameId)}",
                cancellationToken);
        }

        public Task<GumoGameVersion> PatchVersionAsync(
            string versionId,
            object payload,
            CancellationToken cancellationToken)
        {
            return SendJsonAsync<GumoGameVersion>(
                HttpMethod.Patch,
                $"/api/integrations/playnite/versions/{Uri.EscapeDataString(versionId)}",
                payload,
                cancellationToken);
        }

        public Task<GumoGame> PatchGameAsync(
            string gameId,
            object payload,
            CancellationToken cancellationToken)
        {
            return SendJsonAsync<GumoGame>(
                HttpMethod.Patch,
                $"/api/integrations/playnite/games/{Uri.EscapeDataString(gameId)}",
                payload,
                cancellationToken);
        }

        public async Task<List<GumoUpload>> ListUploadsAsync(CancellationToken cancellationToken)
        {
            var response = await GetAsync<GumoListResponse<GumoUpload>>(
                "/api/integrations/playnite/uploads?scope=recent",
                cancellationToken);
            return response.Items ?? new List<GumoUpload>();
        }

        public Task<GumoUpload> GetUploadAsync(string uploadId, CancellationToken cancellationToken)
        {
            return GetAsync<GumoUpload>(
                $"/api/integrations/playnite/uploads/{Uri.EscapeDataString(uploadId)}",
                cancellationToken);
        }

        public async Task<List<GumoJob>> ListJobsAsync(CancellationToken cancellationToken)
        {
            var response = await GetAsync<GumoListResponse<GumoJob>>(
                "/api/integrations/playnite/jobs?scope=recent",
                cancellationToken);
            return response.Items ?? new List<GumoJob>();
        }

        public Task<GumoJob> GetJobAsync(string jobId, CancellationToken cancellationToken)
        {
            return GetAsync<GumoJob>(
                $"/api/integrations/playnite/jobs/{Uri.EscapeDataString(jobId)}",
                cancellationToken);
        }

        public Task<GumoInstallManifest> GetInstallManifestAsync(
            string versionId,
            CancellationToken cancellationToken)
        {
            return GetAsync<GumoInstallManifest>(
                $"/api/integrations/playnite/versions/{Uri.EscapeDataString(versionId)}/install",
                cancellationToken);
        }

        public async Task<List<GumoSaveSnapshot>> GetSaveSnapshotsAsync(
            string versionId,
            CancellationToken cancellationToken)
        {
            var response = await GetAsync<GumoListResponse<GumoSaveSnapshot>>(
                $"/api/integrations/playnite/versions/{Uri.EscapeDataString(versionId)}/save-snapshots",
                cancellationToken);
            return response.Items ?? new List<GumoSaveSnapshot>();
        }

        public Task<GumoSaveRestoreManifest> GetSaveRestoreManifestAsync(
            string saveSnapshotId,
            CancellationToken cancellationToken)
        {
            return GetAsync<GumoSaveRestoreManifest>(
                $"/api/integrations/playnite/save-snapshots/{Uri.EscapeDataString(saveSnapshotId)}/restore",
                cancellationToken);
        }

        public void Dispose()
        {
            httpClient.Dispose();
        }

        private Task<T> GetAsync<T>(string path, CancellationToken cancellationToken)
        {
            return SendAsync<T>(HttpMethod.Get, path, null, cancellationToken);
        }

        private Task<T> SendJsonAsync<T>(
            HttpMethod method,
            string path,
            object payload,
            CancellationToken cancellationToken)
        {
            var body = payload == null
                ? null
                : new StringContent(
                    JsonConvert.SerializeObject(payload),
                    Encoding.UTF8,
                    "application/json");
            return SendAsync<T>(method, path, body, cancellationToken);
        }

        private async Task<T> SendAsync<T>(
            HttpMethod method,
            string path,
            HttpContent content,
            CancellationToken cancellationToken)
        {
            using (var request = new HttpRequestMessage(method, path))
            {
                request.Content = content;
                using (var response = await httpClient.SendAsync(request, cancellationToken))
                {
                    var responseBody = await response.Content.ReadAsStringAsync();
                    if (!response.IsSuccessStatusCode)
                    {
                        throw BuildApiException(response.StatusCode, responseBody);
                    }

                    if (typeof(T) == typeof(VoidResponse))
                    {
                        return (T)(object)new VoidResponse();
                    }

                    try
                    {
                        return JsonConvert.DeserializeObject<T>(responseBody);
                    }
                    catch (Exception exception)
                    {
                        Logger.Error($"Failed to deserialize Gumo API response from {path}: {exception}");
                        throw new InvalidOperationException(
                            "Gumo returned an unreadable response.",
                            exception);
                    }
                }
            }
        }

        private GumoApiException BuildApiException(HttpStatusCode statusCode, string responseBody)
        {
            try
            {
                var envelope = JsonConvert.DeserializeObject<GumoApiErrorEnvelope>(responseBody);
                if (envelope?.Error != null)
                {
                    return new GumoApiException(
                        statusCode,
                        envelope.Error.Code,
                        envelope.Error.Message,
                        $"Gumo API request failed: {envelope.Error.Message}");
                }
            }
            catch (Exception exception)
            {
                Logger.Warn($"Failed to parse Gumo API error response: {exception}");
            }

            return new GumoApiException(
                statusCode,
                "http_error",
                responseBody,
                $"Gumo API request failed with status {(int)statusCode}.");
        }

        private static string NormalizeServerUrl(string rawServerUrl)
        {
            var trimmed = (rawServerUrl ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                throw new InvalidOperationException("Gumo server URL is required.");
            }

            return trimmed.TrimEnd('/');
        }

        private sealed class VoidResponse
        {
        }
    }
}
