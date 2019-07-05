using System;
using System.Threading;
using System.Threading.Tasks;
using FlickrToCloud.Contracts.Models;
using FlickrToCloud.Contracts.Progress;

namespace FlickrToCloud.Contracts.Interfaces
{
    public interface ICloudFileSystem : IAuthenticationCallback
    {
        Task<string> GetAuthenticationUrl();
        string Name { get; }
        bool IsAuthenticated { get; }
        Task<string> UploadFileFromUrlAsync(string path, string fileName, string sourceUrl, CancellationToken ct);
        Task UploadFileAsync(string destinationFilePath, string localFileName, CancellationToken ct);
        Task<OperationStatus> CheckOperationStatusAsync(File file, CancellationToken ct);
        Task<File[]> GetFilesAsync(SessionFilesOrigin filesOrigin, CancellationToken ct, Action<ReadingFilesProgress> progressHandler = null);
        Task<bool> FolderExistsAsync(string folder, CancellationToken ct);
        Task<bool> CreateFolderAsync(string folder, CancellationToken ct);
        Task<string[]> GetSubFoldersAsync(string folder, CancellationToken ct);
        Task CopyFileAsync(string fromFilePath, string toPath, string fileName, CancellationToken ct);
    }
}