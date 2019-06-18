using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FlickrToOneDrive.Contracts;
using FlickrToOneDrive.Contracts.Exceptions;
using FlickrToOneDrive.Contracts.Extensions;
using FlickrToOneDrive.Contracts.Interfaces;
using FlickrToOneDrive.Contracts.Models;
using FlickrToOneDrive.Contracts.Progress;
using FlickrToOneDrive.Core.Extensions;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace FlickrToOneDrive.Core.Services
{
    public partial class CloudCopyService : ICloudCopyService
    {
        private readonly ILogger _log;
        private readonly IStorageService _storageService;

        public event Action<UploadProgress> UploadProgressHandler;
        public event Action<UploadProgress> UploadFinishedHandler;
        public event Action ReadingFilesStartingHandler;
        public event Action<UploadProgress> UploadStartingHandler;
        public event Action CreatingFoldersHandler;
        public event Action NothingToUploadHandler;
        public event Action<StatusCheckProgress> CheckingStatusHandler;
        public event Action<StatusCheckProgress> CheckingStatusFinishedHandler;
        public event Action<ReadingFilesProgress> ReadingFilesProgressHandler;

        public CloudCopyService(ILogger log, IStorageService storageService)
        {
            _log = log.ForContext(GetType());
            _storageService = storageService;
        }

        public async Task<bool> Copy(Setup setup, bool retryFailed, CancellationToken ct)
        {
            // Read source files
            if (setup.Session.State <= SessionState.ReadingSource)
            {
                await ReadSource(setup, ct);
            }

            // Create destination folders
            if (setup.Session.FilesOrigin.HasFlag(SessionFilesOrigin.Structured)
                && (setup.Session.Mode == SessionMode.Remote)
                && (setup.Session.State <= SessionState.CreatingFolders))
            {
                CreatingFoldersHandler?.Invoke();
                await CreateFoldersAsync(setup, ct);
            }

            // Copy source to destination
            return await ResumeUpload(setup, retryFailed, ct);
        }

        public async Task CheckStatus(Setup setup, CancellationToken ct, StatusCheckProgress resumeProgress)
        {
            var files = setup.Session.GetFiles();
            var progress = new StatusCheckProgress { TotalItems = files.Count() };

            if (resumeProgress != null)
            {
                // Skip already processed files
                files = files.Skip(resumeProgress.ProcessedItems).ToList();

                progress.ProcessedItems = resumeProgress.ProcessedItems;
                progress.ProcessedWithError = resumeProgress.ProcessedWithError;
                progress.ProcessedWithSuccess = resumeProgress.ProcessedWithSuccess;
            }         

            _log.Information($"Going to check status for {files.Count()} file(s)");

            await files.ForEachAsync(CheckFileStatus, setup, progress, ct);

            var sessionFinished = files.All(f => f.State != FileState.InProgress);
            if (sessionFinished)
            {
                setup.Session.UpdateState(SessionState.Finished);
            }

            CheckingStatusFinishedHandler?.Invoke(progress);

        }

        private async Task ReadSource(Setup setup, CancellationToken ct)
        {
            try
            {
                ReadingFilesStartingHandler?.Invoke();

                setup.Session.UpdateState(SessionState.ReadingSource);

                var sourceFiles = await setup.Source.GetFilesAsync(setup.Session.FilesOrigin, ct, (progress) =>
                {
                    ReadingFilesProgressHandler?.Invoke(progress);
                });

                if (sourceFiles.Length > 0)
                {
                    using (var db = new CloudCopyContext())
                    {
                        using (var transaction = await db.Database.BeginTransactionAsync())
                        {
                            foreach (var file in sourceFiles)
                            {
                                file.SessionId = setup.Session.Id;
                            }
                            await db.Files.AddRangeAsync(sourceFiles);

                            var session = db.Sessions.First(s => s.Id == setup.Session.Id);
                            await db.SaveChangesAsync();
                            transaction.Commit();
                        }
                    }
                }
                else
                {
                    NothingToUploadHandler?.Invoke();
                    _log.Warning($"No files found on {setup.Source.Name}");
                }
            }
            catch (OperationCanceledException)
            {
                _log.Information("ReadSource cancelled");
                throw;
            }
            catch (Microsoft.Graph.ServiceException e)
            {
                TryHandleGraphCancellation(e, "Error getting files from Flickr");
                throw;
            }
            catch (Exception e)
            {
                throw new ReadingSourceException($"Error getting files from {setup.Source.Name}", e, _log);
            }
        }

        private async Task<bool> ResumeUpload(Setup setup, bool retryFailed, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            setup.Session.UpdateState(SessionState.Uploading);
          
            var files = new List<File>();
            files.AddRange(setup.Session.GetFiles(FileState.None));
            if (retryFailed)
            {
                var failedFiles = setup.Session.GetFiles(FileState.Failed);
                files.AddRange(failedFiles);
            }

            var finishedState = setup.Session.Mode == SessionMode.Local ? FileState.Finished : FileState.InProgress;
            var finishedFilesCount = setup.Session.GetFiles(finishedState).Count;
            var progress = new UploadProgress { TotalItems = files.Count(), ProcessedItems = finishedFilesCount };

            UploadStartingHandler?.Invoke(progress);

            if (files.Any())
            {
                if (setup.Session.Mode == SessionMode.Local)
                {
                    // Upload file only once and copy its occurences on the remote server
                    var groupedFiles = files.GroupBy(f => f.SourceId);
                    await groupedFiles.ForEachAsync(UploadFileGroup, setup, progress, ct);
                }
                else
                {
                    // When uploading remotely (from URL) we can let destination cloud to download all files
                    // There is no need to group the files and copy them on the remote server
                    await files.ForEachAsync(UploadFile, setup, progress, ct);
                }
            }

            if (setup.Session.Mode == SessionMode.Local)
            {
                var localSessionFinished = progress.ProcessedWithError == 0 && !ct.IsCancellationRequested;
                if (localSessionFinished)
                {
                    // All files were uploaded without any errors, local session is considered finished
                    // For remote upload, session state is set to Finished during CheckStatus
                    setup.Session.UpdateState(SessionState.Finished);
                }
            }
            else
            {
                setup.Session.UpdateState(SessionState.Checking);
            }

            UploadFinishedHandler?.Invoke(progress);
            return progress.ProcessedWithError == 0;
        }

        private async Task CreateFoldersAsync(Setup setup, CancellationToken ct)
        {
            setup.Session.UpdateState(SessionState.CreatingFolders);

            var files = setup.Session.GetFiles(FileState.None);
            var folders = files.Select(f => f.SourcePath).Where(p => p != "/").Distinct();
            foreach (var folder in folders)
            {
                ct.ThrowIfCancellationRequested();
                var destinationFolder = CombinePath(setup.Session.DestinationFolder, folder);
                await setup.Destination.CreateFolderAsync(destinationFolder, ct);
            }
        }

        private async Task UploadFileGroup(IGrouping<string, File> fileGroup, Setup setup, UploadProgress progress, SemaphoreSlim semaphore, CancellationToken ct)
        {
            // Upload the first file
            var uploadedFile = fileGroup.First();
            await UploadFile(uploadedFile, setup, progress, semaphore, ct);

            if (fileGroup.Count() > 1)
            {
                // Copy rest of the files on remote server
                var filesToCopy = fileGroup.Where((file) => file != uploadedFile);
                foreach (var fileToCopy in filesToCopy)
                {                    
                    await CopyRemoteFile(setup, progress, uploadedFile, fileToCopy, ct);
                }
            }
        }

        private async Task CopyRemoteFile(Setup setup, UploadProgress progress, File existingFile, File file, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var fromFilePath = CombinePath(setup.Session.DestinationFolder, existingFile.SourcePath, existingFile.FileName);
                var toPath = CombinePath(setup.Session.DestinationFolder, file.SourcePath);
                await setup.Destination.CopyFileAsync(fromFilePath, toPath, ct);
                file.UpdateState(FileState.Finished);
                _log.Information($"Successuflly copied file from {fromFilePath} to {toPath}");
                Interlocked.Increment(ref progress.ProcessedWithSuccess);
            }
            catch (OperationCanceledException)
            {
                _log.Information($"CopyRemoteFile cancelled ({file.FileName})");
                throw;
            }
            catch (Microsoft.Graph.ServiceException e)
            {
                TryHandleGraphCancellation(e, $"CopyRemoteFile cancelled ({file.FileName})");
                throw;
            }
            catch (Exception e)
            {
                file.SetFailedState(e);
                Interlocked.Increment(ref progress.ProcessedWithError);
                _log.Error(e, "Error while copying a remote file", e);
            }

            Interlocked.Increment(ref progress.ProcessedItems);
            UploadProgressHandler?.Invoke(progress);
        }

        private void TryHandleGraphCancellation(Microsoft.Graph.ServiceException e, string message)
        {
            if (e.InnerException != null && e.InnerException is TaskCanceledException)
            {
                _log.Information(message);
                throw new OperationCanceledException();
            }
        }

        private async Task UploadFile(File file, Setup setup, UploadProgress progress, SemaphoreSlim semaphore, CancellationToken ct)
        {            
            _log.Verbose($"Uploading file ID: {file.Id}, URL: {file.SourceUrl}");

            await semaphore.WaitAsync();
            try
            {
                if (setup.Session.Mode == SessionMode.Local)
                {
                    var destinationFilePath = CombinePath(setup.Session.DestinationFolder, file.SourcePath, file.FileName);
                    await UploadFileLocaly(file, setup, destinationFilePath, ct);
                }
                else
                {
                    var destinationFilePath = CombinePath(setup.Session.DestinationFolder, file.SourcePath);
                    await UploadFileRemotely(file, setup, destinationFilePath, ct);
                }

                Interlocked.Increment(ref progress.ProcessedWithSuccess);
            }
            catch (OperationCanceledException)
            {
                _log.Information($"UploadFile cancelled ({file.FileName})");
                throw;
            }
            catch (Microsoft.Graph.ServiceException e)
            {
                TryHandleGraphCancellation(e, $"UploadFile cancelled ({file.FileName})");
                throw;
            }
            catch (Exception e)
            {
                file.SetFailedState(e);
                Interlocked.Increment(ref progress.ProcessedWithError);
                _log.Error(e, "Error while uploading a file", e);
            }
            finally
            {
                semaphore.Release();
            }

            // Increment number of processed items even when an exception occurs
            Interlocked.Increment(ref progress.ProcessedItems);
            UploadProgressHandler?.Invoke(progress);
        }

        private async Task UploadFileRemotely(File file, Setup setup, string destinationPath, CancellationToken ct)
        {
            var monitorUrl = await setup.Destination.UploadFileFromUrlAsync(destinationPath, file, ct);
            file.UpdateMonitorUrl(monitorUrl);
            _log.Information($"UploadFileRemotely succeeded (@File)", file);
        }

        private async Task UploadFileLocaly(File file, Setup setup, string destinationFilePath, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var localFileName = $"{Guid.NewGuid().ToString()}.tmp";            
            try
            {
                await DownloadFile(file.SourceUrl, localFileName, ct);
                await setup.Destination.UploadFileAsync(destinationFilePath, localFileName, ct);
                file.UpdateState(FileState.Finished);
                _log.Information($"UploadFileLocaly succeeded (@File)", file);
            }
            finally
            {
                if (await _storageService.FileExistsAsync(localFileName))
                    await _storageService.DeleteFileAsync(localFileName);
            }
        }

        private async Task CheckFileStatus(File file, Setup setup, StatusCheckProgress progress, SemaphoreSlim semaphore, CancellationToken ct)
        {            
            await semaphore.WaitAsync();            
            try
            {
                switch (file.State)
                {
                    case FileState.InProgress:
                        var status = await setup.Destination.CheckOperationStatusAsync(file, ct);
                        file.UpdateResponseData(status.RawResponse);
                        if (status.SuccessResponseCode)
                        {
                            if (status.PercentageComplete == 100)
                            {
                                file.UpdateState(FileState.Finished);
                                Interlocked.Increment(ref progress.ProcessedWithSuccess);
                            }
                            else
                            {
                                Interlocked.Increment(ref progress.InProgress);
                            }
                        }
                        else
                        {
                            file.UpdateState(FileState.Failed);
                            Interlocked.Increment(ref progress.ProcessedWithError);
                        }

                        break;
                    case FileState.Finished:
                        Interlocked.Increment(ref progress.ProcessedWithSuccess);
                        break;
                    case FileState.Failed:
                        Interlocked.Increment(ref progress.ProcessedWithError);
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                _log.Information("Checking file status cancelled");
                throw;
            }
            catch (Microsoft.Graph.ServiceException e)
            {
                TryHandleGraphCancellation(e, "Checking file status cancelled");
                throw;
            }
            catch (CloudCopyException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new ReadingSourceException($"Error checking file status {file.FileName}", e, _log);
            }
            finally
            {
                semaphore.Release();
            }

            Interlocked.Increment(ref progress.ProcessedItems);
            CheckingStatusHandler?.Invoke(progress);
        }

        private async Task DownloadFile(string sourceUrl, string localFileName, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            using (var client = new HttpClient())
            using (var inputStream = await client.GetStreamAsync(sourceUrl))
            using (var outputStream = await _storageService.OpenFileStreamForWriteAsync(localFileName))
            {
                await inputStream.CopyToAsync(outputStream, 81920, ct);
            }
        }

        private string CombinePath(params string[] parameters)
        {
            // /Test + /
            var result = new StringBuilder();
            for (int i = 0; i < parameters.Length; i++)
            {
                var p = parameters[i].TrimStart('/').TrimEnd('/');
                if (p.Length != 0)
                    result.Append($"/{p}");
            }

            if (result.Length == 0)
                return "/";
            else
                return result.ToString();
        }

    }
}