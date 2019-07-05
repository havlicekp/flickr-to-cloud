using FlickrToCloud.Contracts;
using FlickrToCloud.Contracts.Exceptions;
using FlickrToCloud.Contracts.Interfaces;
using FlickrToCloud.Contracts.Models;
using FlickrToCloud.Core.Uploaders;
using Serilog;

namespace FlickrToCloud.Core
{
    public class UploaderFactory : IUploaderFactory
    {
        private readonly ILogger _log;
        private readonly IDownloadService _downloadService;
        private readonly IStorageService _storageService;

        public UploaderFactory(ILogger log, IDownloadService downloadService, IStorageService storageService)
        {
            _log = log;
            _downloadService = downloadService;
            _storageService = storageService;
        }

        public IUploader Create(Setup setup)
        {
            switch (setup.Session.Mode)
            {
                case SessionMode.Local:
                    return new LocalUploader(setup, _log, _downloadService, _storageService);
                case SessionMode.Remote:
                    return new RemoteUploader(setup, _log);
                default:
                    throw new CloudCopyException("No uploader exists for the session mode");
            }
        }
    }
}