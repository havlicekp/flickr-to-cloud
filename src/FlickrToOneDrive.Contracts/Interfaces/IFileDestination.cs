using System.Threading.Tasks;
using FlickrToOneDrive.Contracts.Models;

namespace FlickrToOneDrive.Contracts.Interfaces
{
    public interface IFileDestination : ICloudFileSystem
    {
        Task<string> UploadFileFromUrl(string path, File file);
        Task<OperationStatus> CheckOperationStatus(string monitorUrl);
    }
}