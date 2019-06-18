using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Open.OAuth2;
using Open.OneDrive;
using System.Web;
using Serilog;
using Microsoft.Graph;
using FlickrToOneDrive.Contracts.Exceptions;
using FlickrToOneDrive.Contracts.Interfaces;
using FlickrToOneDrive.Contracts.Models;
using File = FlickrToOneDrive.Contracts.Models.File;
using OperationStatus = FlickrToOneDrive.Contracts.Models.OperationStatus;
using System.Threading;
using FlickrToOneDrive.Contracts.Progress;
using System.IO;

namespace FlickrToOneDrive.Clouds.OneDrive
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

        public async Task UploadFileAsync(string destinationFilePath, string localFileName, CancellationToken ct)
        {
            var fileSize = await _storageService.GetFileSizeAsync(localFileName);

            // File smaller than 4MB can be uploaded directly, otherwise chunked upload is required
            if (((double)fileSize / 1024 / 1024) <= 4)
                await UploadSmallFile(destinationFilePath, localFileName, ct);
            else
                await UploadLargeFile(destinationFilePath, localFileName, ct);
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
                    new CloudCopyException("Error logging into OneDrive", e, _log);
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

        public async Task<string> UploadFileFromUrlAsync(string destinationPath, File file, CancellationToken ct)
        {
            _log.Information("Uploading a file {@File}", file);

            var requestContent = $@"{{
                    ""@microsoft.graph.sourceUrl"": ""{file.SourceUrl}"",
                    ""name"": ""{file.FileName}"",
                    ""file"": {{}}
                }}";

            var builder = GetUriBuilder($"/v1.0/me/drive/root:{destinationPath}:/children");

            using (var response = await SendRequestAsync(
                builder.Uri.AbsoluteUri,
                requestContent, 
                HttpMethod.Post, 
                $"Unable to upload {file.FileName} to {destinationPath}",
                ct,
                (headers) => headers.Add("Prefer", "respond-async")                
                ))
            {                
                if (!response.IsSuccessStatusCode || response.Headers.Location == null)
                {
                    var responseStr = await response.Content.ReadAsStringAsync();
                    var msg = "Upload by URL failed or returned an empty monitor URL";
                    _log.Error($"{msg} ({responseStr})");
                    throw new CloudCopyException(msg);
                }

                return response.Headers.Location.ToString();
            }
        }

        public async Task<bool> FolderExistsAsync(string folder, CancellationToken ct)
        {
            _log.Information($"Checking if folder exists '{folder}'");

            var builder = GetUriBuilder($"/v1.0/me/drive/root:{folder}:/children");

            using (var response = await SendRequestAsync(
                builder.Uri.AbsoluteUri,
                null,
                HttpMethod.Get,
                $"Error while checking if the folder exist",
                ct))
            {
                return response.IsSuccessStatusCode;
            }
        }

        public async Task<bool> CreateFolderAsync(string folder, CancellationToken ct)
        {
            _log.Information($"Creating folder '{folder}'");

            // For path '/' use /drive/root/children
            // For path '/folder/subfolder' use /drive/root:/folder:/children
            var path = "";
            var slashIdx = folder.LastIndexOf('/');
            var tail = folder.Substring(slashIdx + 1);
            if (slashIdx == 0)
            {
                path = "/v1.0/me/drive/root/children";
            }
            else
            {
                var head = folder.Remove(slashIdx);
                path = $"/v1.0/me/drive/root:{head}:/children";
            }

            var builder = GetUriBuilder(path);

            var requestContent = $@"{{
                    ""name"": ""{tail}"",
                    ""folder"": {{}},
                    ""@microsoft.graph.conflictBehavior"": ""replace""                    
                }}";

            using (var response = await SendRequestAsync(builder.Uri.AbsoluteUri,
                requestContent, HttpMethod.Post, $"Unable to create folder '{folder}'", ct))
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

            var builder = GetUriBuilder();
            builder.Path = folder == "/"
                ? $"/v1.0/me/drive/root/children"
                : $"/v1.0/me/drive/root:/{folder}:/children";

            using (var response =
                await SendRequestAsync(builder.Uri.AbsoluteUri, null, HttpMethod.Get, $"Error while reading subfolders", ct))
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

        public Task<File[]> GetFilesAsync(SessionFilesOrigin filesOrigin, CancellationToken ct, Action<ReadingFilesProgress> progressHandler = null)
        {
            throw new NotImplementedException();
        }

        public async Task CopyFileAsync(string fromFilePath, string destinationPath, CancellationToken ct)
        {
            _log.Information($"Copying a file from {fromFilePath} to {destinationPath}");            

            var requestContent = "{ \"parentReference\": { \"path\": \"/drive/root:" + destinationPath + "\" }}";

            var builder = GetUriBuilder($"/v1.0/me/drive/root:{fromFilePath}:/copy");

            using (var response = await SendRequestAsync(
                builder.Uri.AbsoluteUri,
                requestContent,
                HttpMethod.Post,
                $"Unable to copy {fromFilePath} to {destinationPath}",
                ct))
            {
                if (!response.IsSuccessStatusCode || response.Headers.Location == null)
                {
                    var responseStr = await response.Content.ReadAsStringAsync();
                    var msg = "Copying a remote file failed or didn't return any monitor URL";
                    _log.Error($"{msg} ({responseStr})");
                    throw new CloudCopyException(msg);
                }

                var destinationFilePath = $"{destinationPath}/{Path.GetFileName(fromFilePath)}";
                await WaitForItemToAppear(destinationFilePath, ct);
            }
        }

        private async Task WaitForItemToAppear(string destinationItemPath, CancellationToken ct)
        {            
            // Wait 5 seconds for the file to appear
            var numRetries = 5;

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

        private async Task UploadSmallFile(string destinationFilePath, string localFileName, CancellationToken ct)
        {
            using (var inputStream = await _storageService.OpenFileStreamForReadAsync(localFileName))
            {
                await _graphClient.Me.Drive.Root.ItemWithPath(destinationFilePath).Content.Request().PutAsync<DriveItem>(inputStream, ct);
            }
        }

        private async Task UploadLargeFile(string destinationFilePath, string localFileName, CancellationToken ct)
        {
            
            var uploadSession = await _graphClient.Me.Drive.Root.ItemWithPath(destinationFilePath).CreateUploadSession().Request().PostAsync(ct);
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
            var builder = GetUriBuilder($"/v1.0/me/drive/root:{path}");

            using (var response = await SendRequestAsync(
                builder.Uri.AbsoluteUri,
                null,
                HttpMethod.Get,
                $"ItemExists error for {path}",
                ct))
            {
                return response.IsSuccessStatusCode;
            }
        }

        private async Task<HttpResponseMessage> SendRequestAsync(string endpoint, string requestContent,
            HttpMethod method, string errorMsg, CancellationToken ct, Action<HttpRequestHeaders> headersAction = null)
        {
            Debug.Assert(_isAuthenticated);

            try
            {
                HttpResponseMessage response;

                using (var request = new HttpRequestMessage(method, endpoint))
                {
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token.AccessToken);
                    request.Content = requestContent == null
                        ? null
                        : new StringContent(requestContent, Encoding.UTF8, "application/json");
                    headersAction?.Invoke(request.Headers);
                    _log.Information("{@method} request: {@endpoint}, Content: {@content}", method, endpoint, request.Content);
                    using (var client = new HttpClient())
                    {
                        response = await client.SendAsync(request);
                        _log.Information("{@code} {@message}, headers: {@headers}", response.StatusCode, response.ReasonPhrase, response.Headers);
                        return response;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _log.Information($"SendRequestAsync cancelled ({endpoint}:{requestContent})");
                throw;
            }
            catch (HttpRequestException e)
            {
                throw new CloudCopyException("Error talking to OneDrive. Are your connected to the internet?", e, _log);
            }
            catch (Exception e)
            {
                throw new CloudCopyException(errorMsg, e, _log);
            }
        }

        private UriBuilder GetUriBuilder(string path = null)
        {
            var result = new UriBuilder("https://graph.microsoft.com");
            if (!string.IsNullOrEmpty(path))
                result.Path = path;
            return result;
        }

    }
}


