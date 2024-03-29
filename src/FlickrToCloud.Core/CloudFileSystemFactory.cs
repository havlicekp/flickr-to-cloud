﻿using FlickrToCloud.Clouds.Flickr;
using FlickrToCloud.Clouds.OneDrive;
using FlickrToCloud.Contracts.Exceptions;
using FlickrToCloud.Contracts.Interfaces;
using MvvmCross;
using Serilog;

namespace FlickrToCloud.Core
{
    public class CloudFileSystemFactory : ICloudFileSystemFactory
    {
        private readonly IConfiguration _config;
        private readonly ILogger _log;
        private readonly IStorageService _storageService;

        public CloudFileSystemFactory(IConfiguration config, ILogger log, IStorageService storageService)
        {
            _config = config;
            _log = log;
            _storageService = storageService;
        }

        public ICloudFileSystem Create(string cloudId)
        {
            switch (cloudId.ToLower())
            {
                case "flickr":
                    var flickrClient = Mvx.IoCProvider.Resolve<IFlickrClient>();
                    return new FlickrFileSystem(flickrClient, _log);
                case "onedrive":
                    return new OneDriveFileSystem(_config, _log, _storageService);
                default:
                    throw new CloudCopyException($"Unknown cloud identifier '{cloudId}'");
            }
        }
    }
}