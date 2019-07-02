using FlickrToCloud.Contracts.Interfaces;
using FlickrToCloud.Contracts.Models;

namespace FlickrToCloud.Contracts
{
    public class Setup
    {
        public Session Session { get; set; }
        public ICloudFileSystem Source { get; set; }
        public ICloudFileSystem Destination { get; set; }
        public bool RequestStatusCheck { get; set; }
    }
}