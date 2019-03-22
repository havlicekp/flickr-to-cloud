using System.Threading.Tasks;
using FlickrToOneDrive.Contracts.Models;

namespace FlickrToOneDrive.Contracts.Interfaces
{
    public interface ICloudFileSystem
    {
        Task<string> GetAuthorizeUrl();
        string Name { get; }
        bool IsAuthorized { get; }
        Task<string> UploadFileFromUrl(string path, File file);
        Task<OperationStatus> CheckOperationStatus(string monitorUrl);
        Task<File[]> GetFiles();
    }
}