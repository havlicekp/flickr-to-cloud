using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using FlickrToOneDrive.Contracts.Interfaces;
using FlickrToOneDrive.Contracts.Models;
using Serilog;
using File = FlickrToOneDrive.Contracts.File;

namespace FlickrToOneDrive.Flickr
{
    public class FlickrFileSystem : ICloudFileSystem, IAuthenticationCallback
    {
        private readonly IConfiguration _config;
        private readonly ILogger _log;
        private FlickrClient _flickrClient;
        private bool _isAuthorized;
        private readonly string _callbackUrl;

        public FlickrFileSystem(IConfiguration config, IAuthenticationCallbackDispatcher callbackDispatcher, ILogger log)
        {
            _config = config;
            _log = log;
            callbackDispatcher.Register(this);
            _callbackUrl = _config["flickr.callbackUrl"];
        }

        public async Task<File[]> GetFiles()
        {
            var result = new List<File>();
            var json = await _flickrClient.PhotosSearch(1, 500, "url_o");
            var totalPages = Int32.Parse(json["photos"]["pages"].ToString());

            if (totalPages > 0)
            {
                var page = 1;

                do
                {
                    var photosCount = json["photos"]["photo"].Count();
                    for (int i = 0; i < photosCount; i++)
                    {
                        var photo = json["photos"]["photo"][i];
                        var url_o = (string) photo["url_o"];
                        var title = (string) photo["title"];
                        var file = new File
                        {                            
                            SourceUrl = url_o,
                            FileName = Path.Combine(title, Path.GetExtension(url_o))
                        };

                        result.Add(file);
                    }

                    page += 1;
                    json = await _flickrClient.PhotosSearch(page, 500, "url_o");
                } while (--totalPages > 0);
            }

            return result.ToArray();
        }

        public async Task<string> GetAuthorizeUrl()
        {
            var clientId = _config["flickr.clientId"];
            var clientSecret = _config["flickr.clientSecret"];
            var scope = _config["flickr.scope"];            

            _flickrClient = new FlickrClient(clientId, clientSecret, scope, _callbackUrl);
            return await _flickrClient.GetAuthorizeUrl();

        }

        public string Name => "Flickr";

        public bool IsAuthorized => _isAuthorized;

        public async Task HandleAuthenticationCallback(Uri callbackUrl)
        {
            if (callbackUrl.AbsoluteUri.StartsWith(_callbackUrl))
            {
                var res = HttpUtility.ParseQueryString(callbackUrl.AbsoluteUri);
                var code = res["oauth_verifier"];
                _isAuthorized = await _flickrClient.Authorize(code);
            }
        }

        public Task<string> UploadFileFromUrl(string path, File file)
        {
            throw new NotImplementedException();
        }

        public Task<OperationStatus> CheckOperationStatus(string monitorUrl)
        {
            throw new NotImplementedException();
        }

    }
}