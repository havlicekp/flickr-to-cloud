using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FlickrToCloud.Contracts;
using FlickrToCloud.Contracts.Interfaces;
using FlickrToCloud.Contracts.Models;
using FlickrToCloud.Core.Extensions;
using FlickrToCloud.Common;
using Serilog;

namespace FlickrToCloud.Core.Uploaders
{
    public class LocalUploader : BaseUploader
    {
        private readonly IDownloadService _downloadService;
        private readonly IStorageService _storageService;

        public LocalUploader(Setup setup, ILogger log, IDownloadService downloadService, IStorageService storageService) : base(setup, log.ForContext<LocalUploader>())
        {
            _downloadService = downloadService;
            _storageService = storageService;
        }

        protected override async Task<bool> UploadFiles(CancellationToken ct)
        {
            // Upload file only once and copy its occurrences on the remote server
            var groupedFiles = _files.GroupBy(f => f.SourceId);
            var tasks = groupedFiles.Select((file) => UploadFileGroup(file, ct));
            await Task.WhenAll(tasks);

            var uploadFinished = !_setup.Session.GetFiles(FileState.Failed).Any();
            if (uploadFinished)
            {
                _setup.Session.UpdateState(SessionState.Finished);
            }

            return uploadFinished;
        }

        private async Task UploadFileGroup(IGrouping<string, File> fileGroup, CancellationToken ct)
        {
            await _semaphore.WaitAsync(ct);
            try
            {
                // Upload the first file
                var uploadedFile = fileGroup.First();
                await UploadFile(uploadedFile, ct);

                if (fileGroup.Count() > 1)
                {
                    // Copy rest of the files on remote server
                    var filesToCopy = fileGroup.Where((file) => file != uploadedFile);
                    foreach (var fileToCopy in filesToCopy)
                    {
                        await CopyRemoteFile(uploadedFile.SourcePath, uploadedFile.FileName, fileToCopy, ct);
                    }
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private async Task UploadFile(File file, CancellationToken ct)
        {
            await RunFileOperationAndHandleCancellation(async () =>
                {
                    ct.ThrowIfCancellationRequested();
                    var destinationFilePath =
                        PathUtils.CombinePath(_setup.Session.DestinationFolder, file.SourcePath, file.FileName);
                    var localFileName = $"{Guid.NewGuid().ToString()}.tmp";
                    try
                    {
                        await _downloadService.DownloadFile(file.SourceUrl, localFileName, ct);
                        await _setup.Destination.UploadFileAsync(destinationFilePath, localFileName, ct);
                        file.UpdateState(FileState.Finished);
                    }
                    finally
                    {
                        if (await _storageService.FileExistsAsync(localFileName))
                            await _storageService.DeleteFileAsync(localFileName);
                    }
                },
                file, "UploadFile", ct);
        }

        private async Task CopyRemoteFile(string sourcePath, string sourceFileName, File file, CancellationToken ct)
        {
            await RunFileOperationAndHandleCancellation(async () =>
                {
                    var fromFilePath = PathUtils.CombinePath(_setup.Session.DestinationFolder, sourcePath,
                        sourceFileName);
                    var toPath = PathUtils.CombinePath(_setup.Session.DestinationFolder, file.SourcePath);
                    await _setup.Destination.CopyFileAsync(fromFilePath, toPath, file.FileName, ct);
                    file.UpdateState(FileState.Finished);
                    Interlocked.Increment(ref _progress.ProcessedWithSuccess);
                }, 
                file, 
                "CopyRemoteFile", ct);
        }

        protected override int GetUploadedFilesCount()
        {
            // For local upload, Finished means the file was successfully uploaded
            return _setup.Session.GetFiles(FileState.Finished).Count;
        }
    }
}
