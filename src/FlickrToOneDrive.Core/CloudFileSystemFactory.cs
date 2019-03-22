using FlickrToOneDrive.Contracts.Exceptions;
using FlickrToOneDrive.Contracts.Interfaces;
using FlickrToOneDrive.Flickr;
using FlickrToOneDrive.OneDrive;
using Serilog;

namespace FlickrToOneDrive.Core
{
    public class CloudFileSystemFactory : ICloudFileSystemFactory
    {
        private readonly IConfiguration _config;
        private readonly ILogger _log;
        private readonly IAuthenticationCallbackDispatcher _authCallbackDispatcher;

        public CloudFileSystemFactory(IConfiguration config, ILogger log, IAuthenticationCallbackDispatcher authCallbackDispatcher)
        {
            _config = config;
            _log = log;
            _authCallbackDispatcher = authCallbackDispatcher;
        }

        public ICloudFileSystem Create(string cloudId)
        {
            switch (cloudId.ToLower())
            {
                case "flickr":
                    return new FlickrFileSystem(_config, _authCallbackDispatcher, _log);                    
                case "onedrive":
                    return new OneDriveFileSystem(_config, _authCallbackDispatcher, _log);
                default:
                    throw new CloudCopyException($"Unknown cloud identifier '{cloudId}'");
            }
        }
    }
}