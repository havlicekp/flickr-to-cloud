using System.Threading.Tasks;
using FlickrToOneDrive.Contracts.Models;

namespace FlickrToOneDrive.Contracts.Interfaces
{
    public interface ICloudFileSystem
    {
        Task<string> GetAuthenticationUrl();
        string Name { get; }
        bool IsAuthenticated { get; }
        Task<string> UploadFileFromUrl(string path, File file);
        Task<OperationStatus> CheckOperationStatus(string monitorUrl);
        Task<File[]> GetFiles();
    }
}