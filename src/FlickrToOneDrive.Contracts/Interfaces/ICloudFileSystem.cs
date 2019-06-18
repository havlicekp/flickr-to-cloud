﻿using System;
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
        Task<string> UploadFileFromUrlAsync(string path, File file, CancellationToken ct);
        Task UploadFileAsync(string destinationFilePath, string localFileName, CancellationToken ct);
        Task<OperationStatus> CheckOperationStatusAsync(File file, CancellationToken ct);
        Task<File[]> GetFilesAsync(SessionFilesOrigin filesOrigin, CancellationToken ct, Action<ReadingFilesProgress> progressHandler = null);
        Task<bool> FolderExistsAsync(string folder, CancellationToken ct);
        Task<bool> CreateFolderAsync(string folder, CancellationToken ct);
        Task<string[]> GetSubFoldersAsync(string folder, CancellationToken ct);
        Task CopyFileAsync(string fromFilePath, string toPath, CancellationToken ct);
    }
}