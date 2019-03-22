using System.Threading.Tasks;

namespace FlickrToOneDrive.Contracts.Interfaces
{
    public interface IFileSource : ICloudFileSystem
    {
        Task<File[]> GetFiles();        
    }
}