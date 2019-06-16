using System;
using System.Threading;
using System.Threading.Tasks;
using FlickrToOneDrive.Contracts.Models;
using FlickrToOneDrive.Contracts.Progress;

namespace FlickrToOneDrive.Contracts.Interfaces
{
    public interface ICloudFileSystem : IAuthenticationCallback
    {
        Task<string> GetAuthenticationUrl();
        string Name { get; }
        bool IsAuthenticated { get; }
        Task<string> UploadFileFromUrl(string path, File file);
        Task UploadFile(string destinationFilePath, string localFileName, CancellationToken ct);
        Task<OperationStatus> CheckOperationStatus(File file);
        Task<File[]> GetFiles(SessionFilesOrigin filesOrigin, CancellationToken ct, Action<ReadingFilesProgress> progressHandler = null);
        Task<bool> FolderExists(string folder);
        Task<bool> CreateFolder(string folder);
        Task<string[]> GetSubFolders(string folder);
    }
}