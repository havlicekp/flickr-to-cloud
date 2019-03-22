using System;
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
using File = FlickrToOneDrive.Contracts.File;
using System.Web;
using FlickrToOneDrive.Contracts.Models;
using Serilog;

namespace FlickrToOneDrive.OneDrive
{
    public class OneDriveFileDestination : IFileDestination, IAuthenticationCallback
    {
        private readonly ILogger _log;
        private readonly string _clientId;
        private readonly string _callbackUrl;
        private OAuth2Token _token;
        private readonly string _scope;
        private bool _isAuthorized;

        public OneDriveFileDestination(ILogger log, IConfiguration config, IAuthenticationCallbackDispatcher callbackDispatcher)
        {
            _log = log;

            _clientId = config["onedrive.clientId"];
            _callbackUrl = config["onedrive.callbackUrl"];
            _scope = config["onedrive.scope"];

            callbackDispatcher.Register(this);
        }

        public async Task<string> UploadFileFromUrl(string path, File file)
        {
            if (_token == null)
            {
                throw new CloudCopyException("Can't access OneDrive, not authorized");
            }            

            var endpoint = "https://graph.microsoft.com/v1.0/me/drive/root/children";

            using (var request = new HttpRequestMessage(HttpMethod.Post, endpoint))
            {
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token.AccessToken);
                request.Headers.Add("Prefer", "respond-async");
                request.Method = HttpMethod.Post;
                request.Content = new StringContent(@"{
                    ""@microsoft.graph.sourceUrl"": ""http://wscont2.apps.microsoft.com/winstore/1x/e33e38d9-d138-42a1-b252-27da1924ca87/Screenshot.225037.100000.jpg"",
                    ""name"": ""halo-screenshot.jpg"",
                    ""file"": {}
                }", Encoding.UTF8, "application/json");

                using (var client = new HttpClient())
                {
                    using (var response = await client.SendAsync(request))
                    {
                        if (response.IsSuccessStatusCode)
                        {
                            var json = JObject.Parse(await response.Content.ReadAsStringAsync());
                            return response.Headers.Location.ToString();
                        }
                    }
                }
            }

            return "";
        }

        public async Task<OperationStatus> CheckOperationStatus(string monitorUrl)
        {
            using (var client = new HttpClient())
            {
                using (var response = await client.GetAsync(monitorUrl))
                {
                    OperationStatus result;
                    if (response.IsSuccessStatusCode)
                    {
                        var json = JObject.Parse(await response.Content.ReadAsStringAsync());
                        var percentageComplete = (int) double.Parse(json["percentageComplete"].ToString());
                        var status = json["status"].ToString();
                        var operation = json["operation"].ToString();
                        result = new OperationStatus(percentageComplete, status, operation, response.StatusCode, monitorUrl);

                    }
                    else
                    {
                        result = new OperationStatus(0, null, null, response.StatusCode, monitorUrl);
                    }

                    return result;
                }
            }
        }

        public async Task HandleAuthenticationCallback(Uri callbackUri)
        {            
            if (callbackUri.AbsoluteUri.StartsWith(_callbackUrl))
            {
                var parts = HttpUtility.ParseQueryString(callbackUri.AbsoluteUri);
                if (parts.Count == 0)
                {
                    throw new CloudCopyException($"OneDrive callback URL not recognized: {callbackUri.AbsoluteUri}");
                }

                var code = parts[0];
                _token = await ExchangeCodeForAccessTokenAsync(code, _clientId, null, _callbackUrl);
                _isAuthorized = true;
            }            
        }

        public Task<string> GetAuthorizeUrl()
        {
            return Task.FromResult(OneDriveClient.GetRequestUrl(_clientId, _scope, _callbackUrl));
        }

        public string Name => "OneDrive";

        public bool IsAuthorized => _isAuthorized;
    }
}
