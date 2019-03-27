using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using FlickrToOneDrive.Contracts.Exceptions;
using FlickrToOneDrive.Contracts.Interfaces;
using Newtonsoft.Json.Linq;
using Open.OAuth2;
using Open.OneDrive;
using static Open.OneDrive.OneDriveClient;
using File = FlickrToOneDrive.Contracts.Models.File;
using System.Web;
using FlickrToOneDrive.Contracts.Models;
using Serilog;

namespace FlickrToOneDrive.OneDrive
{
    public class OneDriveFileSystem : ICloudFileSystem, IAuthenticationCallback
    {
        private readonly ILogger _log;
        private readonly string _clientId;
        private readonly string _callbackUrl;
        private OAuth2Token _token;
        private readonly string _scope;
        private bool _isAuthenticated;

        public string Name => "OneDrive";

        public bool IsAuthenticated => _isAuthenticated;

        public OneDriveFileSystem(IConfiguration config, IAuthenticationCallbackDispatcher callbackDispatcher,
            ILogger log)
        {
            _log = log.ForContext(GetType());

            _clientId = config["onedrive.clientId"];
            _callbackUrl = config["onedrive.callbackUrl"];
            _scope = config["onedrive.scope"];

            callbackDispatcher.Register(this);
        }

        public async Task<OperationStatus> CheckOperationStatus(string monitorUrl)
        {
            try
            {
                _log.Information($"Checking operation status for {monitorUrl}");

                using (var client = new HttpClient())
                {
                    using (var response = await client.GetAsync(monitorUrl))
                    {
                        OperationStatus result;
                        if (response.IsSuccessStatusCode)
                        {
                            var content = await response.Content.ReadAsStringAsync();

                            _log.Verbose($"Response {content}");

                            var json = JObject.Parse(content);
                            var percentageComplete = (int) double.Parse(json["percentageComplete"].ToString());
                            var status = json["status"].ToString();
                            var operation = json["operation"].ToString();
                            result = new OperationStatus(percentageComplete, status, operation, true, monitorUrl);
                        }
                        else
                        {
                            result = new OperationStatus(0, null, null, false, monitorUrl);
                        }

                        return result;
                    }
                }
            }
            catch (Exception e)
            {
                var msg = "Can't check OneDrive operation status";
                _log.Error(e, msg);
                throw new CloudCopyException(msg);
            }
        }

        public async Task HandleAuthenticationCallback(Uri callbackUri)
        {
            if (callbackUri.AbsoluteUri.StartsWith(_callbackUrl))
            {
                _log.Information($"Authentication callback for OneDrive");
                _log.Verbose(callbackUri.AbsoluteUri);

                try
                {
                    var parts = HttpUtility.ParseQueryString(callbackUri.AbsoluteUri);
                    var code = parts[0];

                    _token = await ExchangeCodeForAccessTokenAsync(code, _clientId, null, _callbackUrl);
                    _isAuthenticated = true;

                    _log.Information("Successfully logged in OneDrive");
                }
                catch (Exception e)
                {
                    _log.Error(e, $"Error logging into OneDrive with callback URI {callbackUri.AbsoluteUri}");
                    throw new CloudCopyException("Error logging into OneDrive");
                }
            }
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
                var msg = "Error during OneDrive authentication";
                _log.Error(e, msg);
                throw new CloudCopyException(msg);
            }
        }

        public async Task<string> UploadFileFromUrl(string path, File file)
        {            
            _log.Information("Uploading a file {@File}", file);

            var fileName = Path.GetFileName(file.SourceUrl);
            var requestContent = $@"{{
                    ""@microsoft.graph.sourceUrl"": ""{file.SourceUrl}"",
                    ""name"": ""{fileName}"",
                    ""file"": {{}}
                }}";
            var response = await SendRequestAsync($"https://graph.microsoft.com/v1.0/me/drive/root:{path}:/children",
                requestContent, HttpMethod.Post, $"Unable to upload {file.FileName} to {path}",
                (headers) => headers.Add("Prefer", "respond-async"));

            if (response.IsSuccessStatusCode)
            {
                return response.Headers.Location.ToString();
            }

            return null;
        }

        public async Task<bool> FolderExists(string folder)
        {
            _log.Information($"Checking if folder exists '{folder}'");

            folder = AddSlashIfMissing(folder);
            var response = await SendRequestAsync(
                $"https://graph.microsoft.com/v1.0/me/drive/root:{folder}:/children",
                null,
                HttpMethod.Get,
                $"Error while checking if the folder exist");
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> CreateFolder(string folder)
        {
            _log.Information($"Creating folder '{folder}'");

            var endpoint = "";

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
                    ""@microsoft.graph.conflictBehavior"": ""rename""                    
                }}";

            var response = await SendRequestAsync(endpoint,
                requestContent, HttpMethod.Post, $"Unable to create folder '{folder}'");
            return response.IsSuccessStatusCode;

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
                        using (var response = await client.SendAsync(request))
                        {
                            _log.Verbose("Response {@Response}", response);
                            return response;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                _log.Error(e, errorMsg);
                throw new CloudCopyException(errorMsg);
            }
        }

        private string AddSlashIfMissing(string path)
        {
            if (!path.StartsWith("/"))
            {
                path = "/" + path;
            }

            return path;
        }

        public Task<File[]> GetFiles()
        {
            throw new NotImplementedException();
        }
    }
}


