using FlickrToOneDrive.Contracts.Interfaces;
using FlickrToOneDrive.Contracts.Models;

namespace FlickrToOneDrive.Contracts
{
    public class Setup
    {
        public Session Session { get; set; }
        public ICloudFileSystem Source { get; set; }
        public ICloudFileSystem Destination { get; set; }
    }
}