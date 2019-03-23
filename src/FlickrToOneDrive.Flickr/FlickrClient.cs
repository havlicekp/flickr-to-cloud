using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Open.OAuth;
using Serilog;

namespace FlickrToOneDrive.Flickr
{
    public class FlickrClient
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

        private const string _flickrOauthAuthorize =
            "https://secure.flickr.com/services/oauth/authorize?oauth_token={0}&perms={1}";

        private const string _flickrOauthAccessTokenUrl = "https://www.flickr.com/services/oauth/access_token";
        private const string _flickrRestUrl = "https://api.flickr.com/services/rest";

        public FlickrClient(string clientId, string clientSecret, string scope, string callbackUrl, ILogger log)
        {
            _clientId = clientId;
            _clientSecret = clientSecret;
            _scope = scope;
            _callbackUrl = callbackUrl;
            _log = log.ForContext(GetType());
        }

        public async Task<bool> Authorize(string authCode)
        {
            _accessToken = await OAuthClient.GetAccessTokenAsync(_flickrOauthAccessTokenUrl, _clientId, _clientSecret,
                _requestToken.Token, _requestToken.TokenSecret, authCode);
            _authCode = authCode;
            _userId = await TestLogin();
            return true;
        }

        public async Task<string> GetAuthorizeUrl()
        {
            _requestToken = await OAuthClient.GetRequestTokenAsync(_flickrOauthRequestTokenUrl, _clientId, _clientSecret, _callbackUrl);
            var result = string.Format(_flickrOauthAuthorize, _requestToken.Token, _scope);
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

        public async Task<string> TestLogin()
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
    }
}