using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using FlickrToOneDrive.Contracts.Interfaces;
using Newtonsoft.Json.Linq;
using Open.OAuth;
using Serilog;

namespace FlickrToOneDrive.Clouds.Flickr
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

        public async Task<ICollection<FlickrPhoto>> GetStreamPhotos(CancellationToken ct, Action<FlickrRequestProgress> requestProgress = null)
        {
            var parameters = new FlickrParams
            {
                ["method"] = "flickr.photos.search",
                ["user_id"] = _userId,
                ["extras"] = "url_o"
            };

            return await FlickrPagedRequest(parameters,
                (json) => int.Parse(json["photos"]["pages"].ToString()),
                (json) => ((JArray)json["photos"]["photo"]).Count,
                (json, i) => GetPhotoFromJson(json["photos"], i),
                (json) => int.Parse(json["photos"]["total"].ToString()),
                ct,
                (progress) => requestProgress?.Invoke(progress));
        }

        public async Task<ICollection<FlickrPhotoset>> GetPhotosets(CancellationToken ct)
        {
            var parameters = new FlickrParams
            {
                ["method"] = "flickr.photosets.getList",
                ["user_id"] = _userId
            };

            return await FlickrPagedRequest(parameters,
                (json) => int.Parse(json["photosets"]["pages"].ToString()),
                (json) => ((JArray)json["photosets"]["photoset"]).Count,
                (json, i) => GetPhotosetFromJson(json, i),
                (json) => int.Parse(json["photosets"]["total"].ToString()),
                ct);
        }

        public async Task<ICollection<FlickrPhoto>> GetPhotosetPhotos(string photosetId, CancellationToken ct)
        {
            var parameters = new FlickrParams
            {
                ["method"] = "flickr.photosets.getPhotos",
                ["user_id"] = _userId,
                ["photoset_id"] = photosetId,
                ["extras"] = "url_o"
            };

            return await FlickrPagedRequest(parameters,
                (json) => int.Parse(json["photoset"]["pages"].ToString()),
                (json) => ((JArray)json["photoset"]["photo"]).Count,
                (json, i) => GetPhotoFromJson(json["photoset"], i),
                (json) => int.Parse(json["photoset"]["total"].ToString()),
                ct);
        }

        public bool IsFlickrCallbackUrl(string callbackUrl)
        {
            return callbackUrl.StartsWith(_callbackUrl);
        }

        private FlickrPhotoset GetPhotosetFromJson(JObject json, int i)
        {
            var photoset = json["photosets"]["photoset"][i];
            var title = (string)photoset["title"]["_content"];
            var id = (string)photoset["id"];
            return new FlickrPhotoset
            {
                Title = title,
                Id = id
            };
        }

        private FlickrPhoto GetPhotoFromJson(JToken json, int i)
        {
            var photo = json["photo"][i];
            var id = (string)photo["id"];
            var url_o = (string)photo["url_o"];
            var title = (string)photo["title"];
            var album = (string)json["title"];
            return new FlickrPhoto
            {
                Id = id,
                Url = url_o,
                Title = title,                
                AlbumName = album
            };
        }

        private async Task<string> TestLogin()
        {
            var parameters = new FlickrParams()
            {
                {"method", "flickr.test.login"}
            };

            var obj = await FlickrGet(parameters, CancellationToken.None);
            return obj["user"]["id"].ToString();
        }

        private async Task<ICollection<T>> FlickrPagedRequest<T>(FlickrParams parameters, Func<JObject, int> pageCount, Func<JObject, int> itemCount, Func<JObject, int, T> parseItem, Func<JObject, int> totalCount, CancellationToken ct, Action<FlickrRequestProgress> progress = null)
        {
            parameters["per_page"] = "500";
            parameters["page"] = "1";

            var json = await FlickrGet(parameters, ct);
            var result = new List<T>();
            var totalPages = pageCount(json);
            var currentPage = 1;
            var totalItemCount = totalCount(json);
            var requestProgress = new FlickrRequestProgress { TotalItems = totalItemCount };

            do
            {
                var count = itemCount(json);
                for (int i = 0; i < count; i++)
                {
                    var item = parseItem(json, i);
                    result.Add(item);
                }

                requestProgress.ProcessedItems += count;
                progress?.Invoke(requestProgress);                

                if (++currentPage > totalPages)
                    break;

                parameters["page"] = currentPage.ToString();
                json = await FlickrGet(parameters, ct);

            } while (true);

            return result.ToArray();
        }

        private async Task<JObject> FlickrGet(FlickrParams parameters, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
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