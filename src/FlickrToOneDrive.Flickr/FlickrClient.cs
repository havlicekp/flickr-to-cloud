using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using FlickrToOneDrive.Contracts.Interfaces;
using Newtonsoft.Json.Linq;
using Open.OAuth;
using Serilog;

namespace FlickrToOneDrive.Flickr
{
    public class FlickrClient : IFlickrClient
    {
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _scope;
        private readonly string _callbackUrl;
        private readonly ILogger _log;
        private string _authCode;
        private OAuthToken _requestToken;
        private OAuthToken _accessToken;
        private string _userId;

        private const string _flickrOauthRequestTokenUrl = "https://www.flickr.com/services/oauth/request_token";

        private const string _flickrOauthAuthenticate =
            "https://secure.flickr.com/services/oauth/authorize?oauth_token={0}&perms={1}";

        private const string _flickrOauthAccessTokenUrl = "https://www.flickr.com/services/oauth/access_token";
        private const string _flickrRestUrl = "https://api.flickr.com/services/rest";

        public FlickrClient(ILogger log, IConfiguration config)
        {
            _log = log.ForContext(GetType());

            _clientId = config["flickr.clientId"];
            _clientSecret = config["flickr.clientSecret"];
            _scope = config["flickr.scope"];
            _callbackUrl = config["flickr.callbackUrl"];
        }

        public async Task<bool> AuthenticateFromCallbackUrl(string callbackUrl)
        {
            if (callbackUrl.StartsWith(_callbackUrl))
            {
                _log.Information($"Flickr authentication from callback URL");
                _log.Verbose(callbackUrl);

                var res = HttpUtility.ParseQueryString(callbackUrl);
                _authCode = res["oauth_verifier"];

                _accessToken = await OAuthClient.GetAccessTokenAsync(_flickrOauthAccessTokenUrl, _clientId,
                    _clientSecret,
                    _requestToken.Token, _requestToken.TokenSecret, _authCode);

                _userId = await TestLogin();

                _log.Information($"Successfully logged in Flickr with user ID {_userId}");
                return true;
            }

            return false;
        }

        public async Task<string> GetAuthenticationUrl()
        {
            _log.Information("Getting authentication URL for Flickr");

            _requestToken = await OAuthClient.GetRequestTokenAsync(_flickrOauthRequestTokenUrl, _clientId, _clientSecret, _callbackUrl);
            var result = string.Format(_flickrOauthAuthenticate, _requestToken.Token, _scope);

            _log.Verbose($"Authentication URL for Flickr: {result}");

            return result;
        }

        public async Task<JObject> PhotosSearch(int page = 1, int perPage = 100, string extras = "", FlickrParams parameters = null)
        {
            if (parameters == null)
            {
                parameters = new FlickrParams();
            }

            parameters["method"] = "flickr.photos.search";
            parameters["per_page"] = perPage.ToString();
            parameters["page"] = page.ToString();
            parameters["user_id"] = _userId;
            parameters["extras"] = extras;

            return await FlickrGet(parameters);
        }

        private async Task<string> TestLogin()
        {
            var parameters = new FlickrParams()
            {
                {"method", "flickr.test.login"}
            };

            var obj = await FlickrGet(parameters);
            return obj["user"]["id"].ToString();
        }

        private async Task<JObject> FlickrGet(FlickrParams parameters)
        {
            return await FlickrHttpRequest("GET", parameters);
        }

        private async Task<JObject> FlickrHttpRequest(string mode, FlickrParams parameters)
        {
            _log.Information($"Flickr {mode} HTTTP request {{method}}", parameters["method"]);

            parameters["nojsoncallback"] = "1";
            parameters["format"] = "json";

            using (var http = new HttpClient())
            {
                var uri = new Uri(OAuthClient.CreateOAuthUrl(_flickrRestUrl, _clientId, _clientSecret, _accessToken.Token,
                    _accessToken.TokenSecret, mode: mode, oauthVerifier: _authCode, parameters: parameters));
                var response = await http.GetAsync(uri);
                var responseString = await response.Content.ReadAsStringAsync();
                var jsonObj = JObject.Parse(responseString);
                return jsonObj;
            }
        }

        public bool IsFlickrCallbackUrl(string callbackUrl)
        {
            return callbackUrl.StartsWith(_callbackUrl);
        }

    }
}