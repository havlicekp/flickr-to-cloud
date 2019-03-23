using FlickrToOneDrive.Contracts;
using FlickrToOneDrive.Contracts.Models;

namespace FlickrToOneDrive.Core
{
    public class Setup
    {
        public Session Session { get; set; }
        public string DestinationFolder { get; set; }
    }
}