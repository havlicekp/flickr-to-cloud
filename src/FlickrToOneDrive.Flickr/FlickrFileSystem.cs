using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FlickrToOneDrive.Contracts.Exceptions;
using FlickrToOneDrive.Contracts.Interfaces;
using FlickrToOneDrive.Contracts.Models;
using Serilog;
using File = FlickrToOneDrive.Contracts.Models.File;

namespace FlickrToOneDrive.Flickr
{
    public class FlickrFileSystem : ICloudFileSystem, IAuthenticationCallback
    {
        private readonly IConfiguration _config;
        private readonly ILogger _log;
        private readonly IFlickrClient _flickrClient;
        private bool _isAuthenticated;

        public FlickrFileSystem(IFlickrClient flickrClient, IConfiguration config, IAuthenticationCallbackDispatcher callbackDispatcher, ILogger log)
        {
            _flickrClient = flickrClient;
            _config = config;
            _log = log.ForContext(GetType());
            callbackDispatcher.Register(this);
        }

        public string Name => "Flickr";

        public bool IsAuthenticated => _isAuthenticated;

        public async Task<File[]> GetFiles()
        {
            try
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
                            var url_o = (string)photo["url_o"];
                            var title = (string)photo["title"];
                            var file = new File
                            {
                                SourceUrl = url_o,
                                FileName = title + Path.GetExtension(url_o)
                            };

                            result.Add(file);
                        }

                        page += 1;
                        json = await _flickrClient.PhotosSearch(page, 500, "url_o");
                    } while (--totalPages > 0);
                }

                return result.ToArray();
            }
            catch (Exception e)
            {
                var msg = "Error getting files from Flickr";
                _log.Error(e, msg);
                throw new CloudCopyException(msg);
            }
        }

        public async Task<string> GetAuthenticationUrl()
        {
            try
            {                
                return await _flickrClient.GetAuthenticationUrl();                
            }
            catch (Exception e)
            {
                var msg = "Error during Flickr authentication";
                _log.Error(e, msg);
                throw new CloudCopyException(msg);
            }
        }

        public async Task HandleAuthenticationCallback(Uri callbackUri)
        {
            try
            {                
                if (_flickrClient.IsFlickrCallbackUrl(callbackUri.AbsoluteUri))
                {
                    _isAuthenticated = await _flickrClient.AuthenticateFromCallbackUrl(callbackUri.AbsoluteUri);
                }
            }
            catch (Exception e)
            {
                var msg = "Error logging into Flickr";
                _log.Error(e, msg);
                throw new CloudCopyException(msg);
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

        public Task<bool> FolderExists(string folder)
        {
            throw new NotImplementedException();
        }

        public Task<bool> CreateFolder(string folder)
        {
            throw new NotImplementedException();
        }

        public bool FolderExists()
        {
            throw new NotImplementedException();
        }
    }
}