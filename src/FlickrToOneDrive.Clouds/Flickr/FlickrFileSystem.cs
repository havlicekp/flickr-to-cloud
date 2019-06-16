using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FlickrToOneDrive.Contracts.Exceptions;
using FlickrToOneDrive.Contracts.Interfaces;
using FlickrToOneDrive.Contracts.Models;
using FlickrToOneDrive.Contracts.Progress;
using Serilog;
using File = FlickrToOneDrive.Contracts.Models.File;

namespace FlickrToOneDrive.Clouds.Flickr
{
    public class FlickrFileSystem : ICloudFileSystem
    {
        private readonly ILogger _log;
        private readonly IFlickrClient _flickrClient;
        private bool _isAuthenticated;

        public FlickrFileSystem(IFlickrClient flickrClient, ILogger log)
        {
            _flickrClient = flickrClient;
            _log = log.ForContext(GetType());
        }

        public string Name => "Flickr";

        public bool IsAuthenticated => _isAuthenticated;

        public async Task<File[]> GetFiles(SessionFilesOrigin filesOrigin, CancellationToken ct, Action<ReadingFilesProgress> progressHandler)
        {
            var result = new List<File>();            

            if (filesOrigin.HasFlag(SessionFilesOrigin.Structured))
            {                
                var photosets = await _flickrClient.GetPhotosets(ct);
                var progress = new ReadingFilesProgress { TotalItems = photosets.Count, Origin = SessionFilesOrigin.Structured };

                foreach (var photoset in photosets)
                {
                    var photos = await _flickrClient.GetPhotosetPhotos(photoset.Id, ct);                    
                    foreach (var photo in photos)
                    {
                        result.Add(new File
                        {
                            FileName = photo.FileName,
                            SourceUrl = photo.Url,
                            SourcePath = "/" + ReplaceInvalidPathChars(photo.AlbumName, "_")
                        });
                    }
                    progress.ProcessedItems++;
                    progressHandler?.Invoke(progress);
                }
            }

            if (filesOrigin.HasFlag(SessionFilesOrigin.Flat))
            {
                var readingProgress = new ReadingFilesProgress { Origin = SessionFilesOrigin.Flat };

                var photos = await _flickrClient.GetStreamPhotos(ct, (flickrProgress) =>
                {
                    readingProgress.TotalItems = flickrProgress.TotalItems;
                    readingProgress.ProcessedItems = flickrProgress.ProcessedItems;
                    progressHandler?.Invoke(readingProgress);
                });

                foreach (var photo in photos)
                {
                    result.Add(new File
                    {
                        FileName = photo.FileName,
                        SourceUrl = photo.Url,
                        SourcePath = "/"
                    });
                }
            }

            return result.ToArray();
        }

        private string ReplaceInvalidPathChars(string path, string replaceWith)
        {
            string invalidChars = new string(Path.GetInvalidFileNameChars());
            foreach (char c in invalidChars)
            {
                path = path.Replace(c.ToString(), replaceWith);
            }

            return path;
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

        public async Task<bool> HandleAuthenticationCallback(Uri callbackUri)
        {
            try
            {                
                if (_flickrClient.IsFlickrCallbackUrl(callbackUri.AbsoluteUri))
                {
                    _isAuthenticated = await _flickrClient.AuthenticateFromCallbackUrl(callbackUri.AbsoluteUri);
                    return true;
                }

                return false;
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

        public Task<OperationStatus> CheckOperationStatus(File file)
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

        public Task<string[]> GetSubFolders(string folder)
        {
            throw new NotImplementedException();
        }

        public bool FolderExists()
        {
            throw new NotImplementedException();
        }

        public Task UploadFile(string destinationFilePath, string localFileName, CancellationToken ct)
        {
            throw new NotImplementedException();
        }
    }
}