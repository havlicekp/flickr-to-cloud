using System;
using System.IO;
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

        public OneDriveFileSystem(IConfiguration config, IAuthenticationCallbackDispatcher callbackDispatcher, ILogger log)
        {            
            _log = log.ForContext(GetType());

            _clientId = config["onedrive.clientId"];
            _callbackUrl = config["onedrive.callbackUrl"];
            _scope = config["onedrive.scope"];

            callbackDispatcher.Register(this);
        }

        public async Task<string> UploadFileFromUrl(string path, File file)
        {
            if (_token == null)
            {
                throw new CloudCopyException("Can't access OneDrive, not authenticated");
            }
            
            _log.Information("Uploading a file {@File}", file);

            var endpoint = $"https://graph.microsoft.com/v1.0/me/drive/root:{path}:/children";

            using (var request = new HttpRequestMessage(HttpMethod.Post, endpoint))
            {
                var fileName = Path.GetFileName(file.SourceUrl);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token.AccessToken);
                request.Headers.Add("Prefer", "respond-async");
                request.Method = HttpMethod.Post;
                request.Content = new StringContent($@"{{
                    ""@microsoft.graph.sourceUrl"": ""{file.SourceUrl}"",
                    ""name"": ""{fileName}"",
                    ""file"": {{}}
                }}", Encoding.UTF8, "application/json");

                _log.Verbose("Request {@Request}", request);

                using (var client = new HttpClient())
                {
                    using (var response = await client.SendAsync(request))
                    {
                        _log.Verbose("Response {@Response}", response);

                        if (response.IsSuccessStatusCode)
                        {
                            return response.Headers.Location.ToString();
                        }
                    }
                }
            }

            return null;
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
                            var percentageComplete = (int)double.Parse(json["percentageComplete"].ToString());
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

        public Task<File[]> GetFiles()
        {
            throw new NotImplementedException();
        }

    }
}
