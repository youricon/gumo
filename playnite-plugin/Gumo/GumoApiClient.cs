using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Playnite.SDK;

namespace Gumo.Playnite
{
    public sealed class GumoApiClient : IDisposable
    {
        private static readonly ILogger Logger = LogManager.GetLogger();
        private static readonly HttpMethod PatchMethod = new HttpMethod("PATCH");
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

        public async Task<List<GumoLibrary>> GetLibrariesAsync(CancellationToken cancellationToken)
        {
            var response = await GetAsync<GumoListResponse<GumoLibrary>>(
                "/api/integrations/playnite/libraries",
                cancellationToken);
            return response.Items ?? new List<GumoLibrary>();
        }

        public Task<GumoGame> GetGameAsync(string gameId, CancellationToken cancellationToken)
        {
            return GetAsync<GumoGame>(
                $"/api/integrations/playnite/games/{Uri.EscapeDataString(gameId)}",
                cancellationToken);
        }

        public async Task<List<GumoGameVersion>> GetVersionsAsync(
            string gameId,
            CancellationToken cancellationToken)
        {
            var response = await GetAsync<GumoListResponse<GumoGameVersion>>(
                $"/api/integrations/playnite/games/{Uri.EscapeDataString(gameId)}/versions",
                cancellationToken);
            return response.Items ?? new List<GumoGameVersion>();
        }

        public Task<GumoGameVersion> PatchVersionAsync(
            string versionId,
            object payload,
            CancellationToken cancellationToken)
        {
            return SendJsonAsync<GumoGameVersion>(
                PatchMethod,
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
                PatchMethod,
                $"/api/integrations/playnite/games/{Uri.EscapeDataString(gameId)}",
                payload,
                cancellationToken);
        }

        public async Task<GumoMediaAsset> UploadMediaAsync(
            string filePath,
            CancellationToken cancellationToken)
        {
            var bytes = File.ReadAllBytes(filePath);
            using (var content = new ByteArrayContent(bytes))
            {
                content.Headers.ContentType =
                    new MediaTypeHeaderValue(DetectMediaContentType(filePath));
                return await SendAsync<GumoMediaAsset>(
                    HttpMethod.Post,
                    $"/api/integrations/playnite/media?filename={Uri.EscapeDataString(Path.GetFileName(filePath))}",
                    content,
                    cancellationToken);
            }
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

        public async Task DownloadToFileAsync(
            string path,
            string destinationPath,
            CancellationToken cancellationToken)
        {
            using (var response = await httpClient.GetAsync(path, cancellationToken))
            {
                if (!response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync();
                    throw BuildApiException(response.StatusCode, responseBody);
                }

                var bytes = await response.Content.ReadAsByteArrayAsync();
                File.WriteAllBytes(destinationPath, bytes);
            }
        }

        public Task<GumoUpload> CreateGamePayloadUploadAsync(
            GumoCreateGamePayloadUploadRequest request,
            CancellationToken cancellationToken)
        {
            return SendJsonAsync<GumoUpload>(
                HttpMethod.Post,
                "/api/integrations/playnite/uploads/game-payloads",
                request,
                cancellationToken);
        }

        public Task<GumoImportSession> CreateGamePayloadImportSessionAsync(
            GumoCreateGamePayloadImportSessionRequest request,
            CancellationToken cancellationToken)
        {
            return SendJsonAsync<GumoImportSession>(
                HttpMethod.Post,
                "/api/integrations/playnite/import-sessions/game-payloads",
                request,
                cancellationToken);
        }

        public Task<GumoImportSession> CreateSaveSnapshotImportSessionAsync(
            GumoCreateSaveSnapshotImportSessionRequest request,
            CancellationToken cancellationToken)
        {
            return SendJsonAsync<GumoImportSession>(
                HttpMethod.Post,
                "/api/integrations/playnite/import-sessions/save-snapshots",
                request,
                cancellationToken);
        }

        public Task<GumoImportSession> GetImportSessionAsync(
            string importSessionId,
            CancellationToken cancellationToken)
        {
            return GetAsync<GumoImportSession>(
                $"/api/integrations/playnite/import-sessions/{Uri.EscapeDataString(importSessionId)}",
                cancellationToken);
        }

        public async Task<List<GumoImportSession>> ListImportSessionsAsync(
            CancellationToken cancellationToken)
        {
            var response = await GetAsync<GumoListResponse<GumoImportSession>>(
                "/api/integrations/playnite/import-sessions?scope=recent",
                cancellationToken);
            return response.Items ?? new List<GumoImportSession>();
        }

        public Task<GumoUploadPart> CreateImportPartAsync(
            string importSessionId,
            GumoCreateImportPartRequest request,
            CancellationToken cancellationToken)
        {
            return SendJsonAsync<GumoUploadPart>(
                HttpMethod.Post,
                $"/api/integrations/playnite/import-sessions/{Uri.EscapeDataString(importSessionId)}/parts",
                request,
                cancellationToken);
        }

        public async Task<List<GumoUploadPart>> ListImportPartsAsync(
            string importSessionId,
            CancellationToken cancellationToken)
        {
            var response = await GetAsync<GumoListResponse<GumoUploadPart>>(
                $"/api/integrations/playnite/import-sessions/{Uri.EscapeDataString(importSessionId)}/parts",
                cancellationToken);
            return response.Items ?? new List<GumoUploadPart>();
        }

        public async Task<GumoUploadPart> PutImportPartContentAsync(
            string uploadPartId,
            string filePath,
            CancellationToken cancellationToken)
        {
            // TODO: Replace this buffered upload path with true streaming from disk.
            // The current ByteArrayContent approach reads the entire archive part into memory,
            // which is acceptable only as a temporary bring-up path for smaller payloads.
            var bytes = File.ReadAllBytes(filePath);
            using (var content = new ByteArrayContent(bytes))
            {
                return await SendAsync<GumoUploadPart>(
                    HttpMethod.Put,
                    $"/api/integrations/playnite/upload-parts/{Uri.EscapeDataString(uploadPartId)}/content",
                    content,
                    cancellationToken);
            }
        }

        public Task<GumoJob> FinalizeImportSessionAsync(
            string importSessionId,
            CancellationToken cancellationToken)
        {
            return SendAsync<GumoJob>(
                HttpMethod.Post,
                $"/api/integrations/playnite/import-sessions/{Uri.EscapeDataString(importSessionId)}/finalize",
                new StringContent(string.Empty, Encoding.UTF8, "application/json"),
                cancellationToken);
        }

        public async Task<GumoUpload> PutUploadContentAsync(
            string uploadId,
            string filePath,
            CancellationToken cancellationToken)
        {
            // TODO: Replace this buffered upload path with true streaming from disk.
            // The current ByteArrayContent approach reads the entire archive into memory,
            // which is acceptable only as a temporary bring-up path for smaller files.
            var bytes = File.ReadAllBytes(filePath);
            using (var content = new ByteArrayContent(bytes))
            {
                return await SendAsync<GumoUpload>(
                    HttpMethod.Put,
                    $"/api/integrations/playnite/uploads/{Uri.EscapeDataString(uploadId)}/content",
                    content,
                    cancellationToken);
            }
        }

        public Task<GumoJob> FinalizeUploadAsync(string uploadId, CancellationToken cancellationToken)
        {
            return SendAsync<GumoJob>(
                HttpMethod.Post,
                $"/api/integrations/playnite/uploads/{Uri.EscapeDataString(uploadId)}/finalize",
                new StringContent(string.Empty, Encoding.UTF8, "application/json"),
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
                    SerializeJson(payload),
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
                        return DeserializeJson<T>(responseBody);
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
                var envelope = DeserializeJson<GumoApiErrorEnvelope>(responseBody);
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

        private static string SerializeJson(object value)
        {
            using (var stream = new MemoryStream())
            {
                var serializer = new DataContractJsonSerializer(value.GetType());
                serializer.WriteObject(stream, value);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        private static T DeserializeJson<T>(string json)
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json ?? string.Empty)))
            {
                var serializer = new DataContractJsonSerializer(typeof(T));
                return (T)serializer.ReadObject(stream);
            }
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

        private static string DetectMediaContentType(string filePath)
        {
            switch (Path.GetExtension(filePath)?.ToLowerInvariant())
            {
                case ".png":
                    return "image/png";
                case ".jpg":
                case ".jpeg":
                    return "image/jpeg";
                case ".webp":
                    return "image/webp";
                case ".bmp":
                    return "image/bmp";
                case ".gif":
                    return "image/gif";
                case ".ico":
                    return "image/x-icon";
                default:
                    return "application/octet-stream";
            }
        }

        private sealed class VoidResponse
        {
        }
    }
}
