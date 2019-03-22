using System.Threading.Tasks;

namespace FlickrToOneDrive.Contracts.Interfaces
{
    public interface ICloudFileSystem
    {
        Task<string> GetAuthorizeUrl();
        string Name { get; }
        bool IsAuthorized { get; }
    }
}