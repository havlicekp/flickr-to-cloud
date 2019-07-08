using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using FlickrToCloud.Clouds.OneDrive.Requests;
using FlickrToCloud.Common;
using FlickrToCloud.Contracts.Exceptions;
using FlickrToCloud.Contracts.Interfaces;
using FlickrToCloud.Contracts.Models;
using FlickrToCloud.Contracts.Progress;
using Microsoft.Graph;
using Newtonsoft.Json.Linq;
using Open.OAuth2;
using Open.OneDrive;
using Serilog;
using BaseRequest = FlickrToCloud.Clouds.OneDrive.Requests.BaseRequest;
using File = FlickrToCloud.Contracts.Models.File;
using OperationStatus = FlickrToCloud.Contracts.Models.OperationStatus;

namespace FlickrToCloud.Clouds.OneDrive
{
    public class OneDriveFileSystem : ICloudFileSystem
    {
        private readonly ILogger _log;
        private readonly string _clientId;
        private readonly string _callbackUrl;
        private OAuth2Token _token;
        private readonly string _scope;
        private GraphServiceClient _graphClient;
        private readonly IStorageService _storageService;
        private bool _isAuthenticated;
        private const string GraphAPIEndpointPrefix = "https://graph.microsoft.com/v1.0/";

        public string Name => "OneDrive";

        public bool IsAuthenticated => _isAuthenticated;

        public OneDriveFileSystem(IConfiguration config,
            ILogger log, IStorageService storageService)
        {
            _log = log.ForContext(GetType());

            _clientId = config["onedrive.clientId"];
            _callbackUrl = config["onedrive.callbackUrl"];
            _scope = config["onedrive.scope"];
            _storageService = storageService;
        }

        public async Task<string> UploadFileFromUrlAsync(string destinationPath, string fileName, string sourceUrl, CancellationToken ct)
        {
            _log.Information($"Uploading {sourceUrl} to {destinationPath}\\{fileName}");

            var endpoint = GetEndpoint($"/v1.0/me/drive/root:{destinationPath}:/children");

            var request = new UploadFileRequest
            {
                SourceUrl = sourceUrl,
                Name = fileName,
                ConflictBehavior = "replace",
                File = new object()
            };

            using (var response = await SendRequestAsync(endpoint, request, HttpMethod.Post, ct,
                (headers) => headers.Add("Prefer", "respond-async")                
                ))
            {                
                if (!response.IsSuccessStatusCode || response.Headers.Location == null)
                {
                    var responseStr = await response.Content.ReadAsStringAsync();
                    throw new CloudCopyException($"Upload by URL failed or returned an empty monitor URL: {responseStr}", _log);
                }

                return response.Headers.Location.ToString();
            }
        }

        public async Task UploadFileAsync(string destinationFilePath, string localFileName, CancellationToken ct)
        {
            _log.Information($"Uploading local file {localFileName} to {destinationFilePath}");
            await UploadFileWithChunkedProvider(destinationFilePath, localFileName, ct);
        }

        public async Task<bool> FolderExistsAsync(string folder, CancellationToken ct)
        {
            _log.Information($"Checking if folder exists '{folder}'");

            var endpoint = GetEndpoint($"/v1.0/me/drive/root:{folder}:/children");

            using (var response = await SendRequestAsync(endpoint, null, HttpMethod.Get, ct))
            {
                return response.IsSuccessStatusCode;
            }
        }

        public async Task<bool> CreateFolderAsync(string folder, CancellationToken ct)
        {
            _log.Information($"Creating folder '{folder}'");

            // For path '/' use /drive/root/children
            // For path '/folder' use /folder
            // For path '/folder/subfolder' use /drive/root:/folder:/children
            var path = PathUtils.PathHasSubfolders(folder)
                ? $"/v1.0/me/drive/root:{PathUtils.RemoveLastFolder(folder)}:/children"
                : "/v1.0/me/drive/root/children";

            var endpoint = GetEndpoint(path);

            var request = new CreateFolderRequest
            {
                Name = PathUtils.GetLastFolder(folder),
                ConflictBehavior = "replace",
                Folder = new object(),
            };

            using (var response = await SendRequestAsync(endpoint, request, HttpMethod.Post, ct))
            {
                if (response.IsSuccessStatusCode)
                {
                    await WaitForItemToAppear(folder, ct);
                    return true;
                }

                return false;
            }
        }

        public async Task<string[]> GetSubFoldersAsync(string folder, CancellationToken ct)
        {
            _log.Information($"Getting sub folders '{folder}'");

            var endpoint = folder == "/"
                ? GetEndpoint("/v1.0/me/drive/root/children")
                : GetEndpoint($"/v1.0/me/drive/root:/{folder}:/children");

            using (var response = await SendRequestAsync(endpoint, null, HttpMethod.Get, ct))
            {
                var content = await response.Content.ReadAsStringAsync();
                var json = JObject.Parse(content);
                var folders = new List<string>();
                foreach (var item in json["value"])
                {
                    if (item["folder"] != null)
                        folders.Add((string)item["name"]);
                }

                return folders.ToArray();
            }
        }

        public async Task CopyFileAsync(string fromFilePath, string destinationPath, string fileName,
            CancellationToken ct)
        {
            _log.Information($"Copying file {fileName} from {fromFilePath} to {destinationPath}");

            var endpoint = GetEndpoint($"/v1.0/me/drive/root:{fromFilePath}:/copy");

            var request = new CopyFileRequest
            {
                Name = fileName,
                ParentReference = new ParentReference
                {
                    Path = $"/drive/root:{destinationPath}"
                }
            };

            using (var response = await SendRequestAsync(endpoint, request, HttpMethod.Post, ct))
            {
                if (!response.IsSuccessStatusCode || response.Headers.Location == null)
                {
                    var responseStr = await response.Content.ReadAsStringAsync();
                    throw new CloudCopyException(
                        $"Copying a remote file failed or didn't return any monitor URL: {responseStr}", _log);
                }
            }
        }

        public Task<File[]> GetFilesAsync(SessionFilesOrigin filesOrigin, CancellationToken ct, Action<ReadingFilesProgress> progressHandler = null)
        {
            throw new NotImplementedException();
        }

        private async Task WaitForItemToAppear(string destinationItemPath, CancellationToken ct, int numRetries = 5)
        {            
            for (int i = 0; i < numRetries; i++)
            {
                ct.ThrowIfCancellationRequested();

                if (await ItemExists(destinationItemPath, ct))
                    return;

                await Task.Delay(1000, ct);
            }

            throw new CloudCopyException($"File {destinationItemPath} didn't copy in time");
        }
    
        private void InitGraphClient()
        {
            _graphClient = new GraphServiceClient(
                GraphAPIEndpointPrefix,
                new DelegateAuthenticationProvider(
                    async (requestMessage) => 
                    {
                        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("bearer", _token.AccessToken);
                    }
                )
            );
        }

        private async Task UploadFileWithChunkedProvider(string destinationFilePath, string localFileName, CancellationToken ct)
        {
            var uploadRequest = _graphClient.Me.Drive.Root.ItemWithPath(destinationFilePath).CreateUploadSession().Request().WithMaxRetry(8);
            uploadRequest.RequestBody.Item = new DriveItemUploadableProperties() { AdditionalData = new Dictionary<string, object>() };
            uploadRequest.RequestBody.Item.AdditionalData.Add("@microsoft.graph.conflictBehavior", "replace");

            var uploadSession = await uploadRequest.PostAsync(ct);
            if (uploadSession != null)
            {
                using (var inputStream = await _storageService.OpenFileStreamForReadAsync(localFileName))
                {
                    // Chunk size must be divisible by 320KiB, our chunk size will be slightly more than 1MB
                    int maxSizeChunk = 320 * 1024;
                    ChunkedUploadProvider uploadProvider = new ChunkedUploadProvider(uploadSession, _graphClient, inputStream, maxSizeChunk);
                    var chunkRequests = uploadProvider.GetUploadChunkRequests();
                    var exceptions = new List<Exception>();
                    var readBuffer = new byte[maxSizeChunk];
                    foreach (var request in chunkRequests)
                    {
                        ct.ThrowIfCancellationRequested();
                        await uploadProvider.GetChunkRequestResponseAsync(request, readBuffer, exceptions);
                    }
                }
            }
        }

        private async Task<bool> ItemExists(string path, CancellationToken ct)
        {
            var endpoint = GetEndpoint($"/v1.0/me/drive/root:{path}");

            using (var response = await SendRequestAsync(endpoint, null, HttpMethod.Get, ct))
            {
                return response.IsSuccessStatusCode;
            }
        }

        private async Task<HttpResponseMessage> SendRequestAsync(string endpoint, BaseRequest baseRequest,
            HttpMethod method, CancellationToken ct, Action<HttpRequestHeaders> headersAction = null)
        {
            Debug.Assert(_isAuthenticated);

            try
            {
                using (var request = new HttpRequestMessage(method, endpoint))
                {
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token.AccessToken);
                    request.Content = baseRequest == null
                        ? null
                        : new StringContent(baseRequest.ToString(), Encoding.UTF8, "application/json");
                    headersAction?.Invoke(request.Headers);

                    using (var client = new HttpClient())
                    {
                        _log.Verbose("{@method} request: {@endpoint}, Content: {@content}", method, endpoint,
                            request.Content);

                        var response = await client.SendAsync(request, ct);

                        _log.Verbose("{@code} {@message}, headers: {@headers}", response.StatusCode,
                            response.ReasonPhrase, response.Headers);

                        return response;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _log.Information($"SendRequestAsync cancelled ({endpoint}:{baseRequest})");
                throw;
            }
            catch (HttpRequestException e)
            {
                throw new CloudCopyException("Error talking to OneDrive. Are your connected to the internet?", e,
                    _log);
            }
            catch (Exception e)
            {
                throw new CloudCopyException("Unknown exception", e, _log);
            }
        }

        public async Task<OperationStatus> CheckOperationStatusAsync(File file, CancellationToken ct)
        {
            _log.Information($"Checking operation status for {file.MonitorUrl}");

            using (var client = new HttpClient())
            {
                try
                {
                    using (var response = await client.GetAsync(file.MonitorUrl, ct))
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        _log.Verbose($"Response {content}");

                        OperationStatus result;
                        if (response.IsSuccessStatusCode)
                        {
                            var json = JObject.Parse(content);
                            var percentageComplete = (int)double.Parse(json["percentageComplete"].ToString());
                            result = new OperationStatus(percentageComplete, true, content);
                        }
                        else
                        {
                            if (await ItemExists(file.SourceUrl, ct))
                                result = new OperationStatus(100, true, content);
                            else
                                result = new OperationStatus(0, false, content);
                        }

                        return result;
                    }
                }
                catch (HttpRequestException e)
                {
                    throw new CloudCopyException("Error talking to OneDrive. Are your connected to the internet?", e, _log);
                }
            }
        }

        public async Task<bool> HandleAuthenticationCallback(Uri callbackUri)
        {
            if (callbackUri.AbsoluteUri.StartsWith(_callbackUrl))
            {
                _log.Information($"Authentication callback for OneDrive");
                _log.Verbose(callbackUri.AbsoluteUri);

                try
                {
                    var parts = HttpUtility.ParseQueryString(callbackUri.AbsoluteUri);
                    var code = parts[0];

                    _token = await OneDriveClient.ExchangeCodeForAccessTokenAsync(code, _clientId, null, _callbackUrl);
                    _isAuthenticated = true;

                    InitGraphClient();

                    _log.Information("Successfully logged in OneDrive");
                    return true;
                }
                catch (Exception e)
                {
                    throw new CloudCopyException("Error logging into OneDrive", e, _log);
                }
            }

            return false;
        }

        public Task<string> GetAuthenticationUrl()
        {
            try
            {
                _log.Information("Getting authentication URL for OneDrive");

                var url = Task.FromResult(OneDriveClient.GetRequestUrl(_clientId, _scope, _callbackUrl));

                _log.Verbose($"Authentication URL for OneDrive: {url.Result}");

                return url;
            }
            catch (Exception e)
            {
                throw new CloudCopyException("Error during OneDrive authentication", e, _log);
            }
        }

        private string GetEndpoint(string path = null)
        {
            var result = new UriBuilder("https://graph.microsoft.com");
            if (!string.IsNullOrEmpty(path))
                result.Path = path;
            return result.Uri.AbsoluteUri;
        }

    }
}


