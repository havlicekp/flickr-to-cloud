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

        public async Task UploadFile(string destinationFilePath, string localFileName, CancellationToken ct)
        {
            var fileSize = await _storageService.GetFileSizeAsync(localFileName);
            if (((double)fileSize / 1024 / 1024) <= 4)
                await UploadSmallFile(destinationFilePath, localFileName, ct);
            else
                await UploadLargeFile(destinationFilePath, localFileName, ct);
        }


        public async Task<OperationStatus> CheckOperationStatus(File file)
        {
            try
            {
                _log.Information($"Checking operation status for {file.MonitorUrl}");

                using (var client = new HttpClient())
                {
                    using (var response = await client.GetAsync(file.MonitorUrl))
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        _log.Verbose($"Response {content}");

                        OperationStatus result;
                        if (response.IsSuccessStatusCode)
                        {
                            var json = JObject.Parse(content);
                            var percentageComplete = (int) double.Parse(json["percentageComplete"].ToString());
                            result = new OperationStatus(percentageComplete, true, content);
                        }
                        else
                        {
                            if (await FileExists(file.SourceUrl))
                                result = new OperationStatus(100, true, content);
                            else
                                result = new OperationStatus(0, false, content);
                        }

                        return result;
                    }
                }
            }
            catch (Exception e)
            {
                throw new CloudCopyException("Can't check OneDrive operation status", e, _log);
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

        public async Task<string> UploadFileFromUrl(string destinationPath, File file)
        {
            _log.Information("Uploading a file {@File}", file);

            var fileName = System.IO.Path.GetFileName(file.SourceUrl);
            var requestContent = $@"{{
                    ""@microsoft.graph.sourceUrl"": ""{file.SourceUrl}"",
                    ""name"": ""{fileName}"",
                    ""file"": {{}}
                }}";
            using (var response = await SendRequestAsync(
                $"https://graph.microsoft.com/v1.0/me/drive/root:{destinationPath}:/children",
                requestContent, HttpMethod.Post, $"Unable to upload {file.FileName} to {destinationPath}",
                (headers) => headers.Add("Prefer", "respond-async")))
            {
                var monitorUrl = response.Headers.Location.ToString();
                if (!response.IsSuccessStatusCode || string.IsNullOrEmpty(monitorUrl))
                    throw new CloudCopyException("Upload by URL returned empty monitor URL");
                return monitorUrl;
            }
        }        

        public async Task<bool> FolderExists(string folder)
        {
            _log.Information($"Checking if folder exists '{folder}'");

            using (var response = await SendRequestAsync(
                $"https://graph.microsoft.com/v1.0/me/drive/root:{folder}:/children",
                null,
                HttpMethod.Get,
                $"Error while checking if the folder exist"))
            {
                return response.IsSuccessStatusCode;
            }
        }

        public async Task<bool> CreateFolder(string folder)
        {
            _log.Information($"Creating folder '{folder}'");

            string endpoint;

            var slashIdx = folder.LastIndexOf('/');
            var tail = folder.Substring(slashIdx + 1);
            if (slashIdx == 0)
            {
                endpoint = "https://graph.microsoft.com/v1.0/me/drive/root/children";
            }
            else
            {
                var head = folder.Remove(slashIdx);
                endpoint = $"https://graph.microsoft.com/v1.0/me/drive/root:{head}:/children";
            }

            var requestContent = $@"{{
                    ""name"": ""{tail}"",
                    ""folder"": {{}},
                    ""@microsoft.graph.conflictBehavior"": ""replace""                    
                }}";

            using (var response = await SendRequestAsync(endpoint,
                requestContent, HttpMethod.Post, $"Unable to create folder '{folder}'"))
            {
                return response.IsSuccessStatusCode;
            }
        }

        public async Task<string[]> GetSubFolders(string folder)
        {
            _log.Information($"Getting sub folders '{folder}'");

            var endpoint = folder == "/"
                ? $"https://graph.microsoft.com/v1.0/me/drive/root/children"
                : $"https://graph.microsoft.com/v1.0/me/drive/root:/{folder}:/children";
            using (var response =
                await SendRequestAsync(endpoint, null, HttpMethod.Get, $"Error while reading subfolders"))
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

        private async Task<bool> FileExists(string url)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Head, url))
            {
                using (var httpClient = new HttpClient())
                {
                    using (var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
                    {
                        return response.StatusCode == System.Net.HttpStatusCode.OK;
                    }
                }
            }
        }

        private async Task<HttpResponseMessage> SendRequestAsync(string endpoint, string requestContent,
            HttpMethod method, string errorMsg, Action<HttpRequestHeaders> headersAction = null)
        {
            Debug.Assert(_isAuthenticated);

            try
            {
                using (var request = new HttpRequestMessage(method, endpoint))
                {
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token.AccessToken);
                    request.Content = requestContent == null
                        ? null
                        : new StringContent(requestContent, Encoding.UTF8, "application/json");
                    headersAction?.Invoke(request.Headers);

                    _log.Verbose("Request {@Request}", request);

                    using (var client = new HttpClient())
                    {
                        var response = await client.SendAsync(request);
                        _log.Verbose("Response {@Response}", response);
                        return response;
                    }
                }
            }
            catch (Exception e)
            {
                throw new CloudCopyException(errorMsg, e, _log);
            }
        }

        public Task<File[]> GetFiles(SessionFilesOrigin filesOrigin, CancellationToken ct, Action<ReadingFilesProgress> progressHandler = null)
        {
            throw new NotImplementedException();
        }
    }
}


