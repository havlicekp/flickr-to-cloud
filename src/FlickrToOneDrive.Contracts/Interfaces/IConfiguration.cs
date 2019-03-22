using System.Collections.Generic;

namespace FlickrToOneDrive.Contracts.Interfaces
{
    public interface IConfiguration
    {
        string this[string key] { get; }
    }
}
