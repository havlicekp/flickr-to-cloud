using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
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
        public event Action<int, int> CheckingStatusHandler;
        public event Action<int, int, int, int> CheckingStatusFinishedHandler;
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
                CreateFolders(setup, ct);
            }

            // Copy source to destination
            return await ResumeUpload(setup, retryFailed, ct);
        }

        public async Task CheckStatus(Setup setup, CancellationToken ct)
        {
            using (var db = new CloudCopyContext())
            {
                var files = db.Files.Where(f => f.SessionId == setup.Session.Id).ToList();
                var fileCount = files.Count();
                var progress = new StatusCheckProgress { TotalItems = fileCount };

                _log.Information($"Going to check status for {fileCount} file(s)");

                await files.ForEachAsync(CheckFileStatus, setup, progress, ct);

                var sessionFinished = files.All(f => f.State != FileState.InProgress);
                if (sessionFinished)
                {
                    var session = db.Sessions.First(s => s.Id == setup.Session.Id);
                    session.State = SessionState.Finished;
                }

                db.SaveChanges();

                CheckingStatusFinishedHandler?.Invoke(progress.ProcessedWithSuccess, progress.ProcessedWithError, progress.InProgress, (int)((progress.ProcessedWithSuccess + progress.ProcessedWithError) * 100 / fileCount));
            }
        }

        private async Task ReadSource(Setup setup, CancellationToken ct)
        {
            try
            {
                ReadingFilesStartingHandler?.Invoke();

                setup.Session.UpdateState(SessionState.ReadingSource);

                var sourceFiles = await setup.Source.GetFiles(setup.Session.FilesOrigin, ct, (progress) =>
                {
                    ReadingFilesProgressHandler?.Invoke(progress);
                });

                if (sourceFiles.Length > 0)
                {
                    using (var db = new CloudCopyContext())
                    {
                        using (var transaction = await db.Database.BeginTransactionAsync())
                        {
                            //db.ChangeTracker.AutoDetectChangesEnabled = false;
                            foreach (var file in sourceFiles)
                            {
                                file.SessionId = setup.Session.Id;
                            }
                            await db.Files.AddRangeAsync(sourceFiles);

                            var session = db.Sessions.First(s => s.Id == setup.Session.Id);
                            session.State = setup.Session.State = SessionState.Uploading;
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
                if (e.InnerException != null && e.InnerException is TaskCanceledException)
                {
                    _log.Information($"ReadingSource cancelled");
                    throw new OperationCanceledException();
                }
                else
                    throw new ReadingSourceException("Error getting files from Flickr", e, _log);
            }
            catch (Exception e)
            {
                throw new ReadingSourceException("Error getting files from Flickr", e, _log);
            }
        }

        private async Task<bool> ResumeUpload(Setup setup, bool retryFailed, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();            

            //setup.Session.UpdateState(SessionState.Uploading);
            
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
                await files.ForEachAsync(UploadFile, setup, progress, ct);
            }

            if (setup.Session.Mode == SessionMode.Local && progress.ProcessedWithError == 0 && !ct.IsCancellationRequested)
            {
                // All files were uploaded without any errors, local session is considered finished
                // For remote upload, session state is set to Finished during CheckStatus
                setup.Session.UpdateState(SessionState.Finished);
            }

            UploadFinishedHandler?.Invoke(progress);
            return progress.ProcessedWithError == 0;
        }

        private void CreateFolders(Setup setup, CancellationToken ct)
        {
            setup.Session.UpdateState(SessionState.CreatingFolders);

            var files = setup.Session.GetFiles(FileState.None);
            var folders = files.Select(f => f.SourcePath).Where(p => p != "/").Distinct();
            foreach (var folder in folders)
            {
                ct.ThrowIfCancellationRequested();
                var destinationFolder = setup.Session.DestinationFolder + folder;
                setup.Destination.CreateFolder(destinationFolder);
            }
        }

        private async Task UploadFile(File file, Setup setup, UploadProgress progress, SemaphoreSlim semaphore, CancellationToken ct)
        {
            await semaphore.WaitAsync();

            _log.Verbose($"Uploading file ID: {file.Id}, URL: {file.SourceUrl}");
            var destinationFilePath = $"{setup.Session.DestinationFolder}{file.SourcePath}/{file.FileName}";

            try
            {
                if (setup.Session.Mode == SessionMode.Local)
                {
                    await UploadFileLocaly(file, setup, destinationFilePath, ct);
                }
                else
                {
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
                if (e.InnerException != null && e.InnerException is TaskCanceledException)
                {
                    _log.Information($"UploadFile cancelled ({file.FileName})");
                    throw new OperationCanceledException();
                }
                else
                    throw;
            }
            catch (Exception e)
            {
                using (var db = new CloudCopyContext())
                {
                    var dbFile = db.Files.FirstOrDefault(f => f.Id == file.Id);

                    // dbFile can be null when cancelling a session
                    if (dbFile != null)
                    {
                        dbFile.State = FileState.Failed;
                        dbFile.ResponseData = e.Message;
                        await db.SaveChangesAsync();
                    }
                }

                Interlocked.Increment(ref progress.ProcessedWithError);
                _log.Error(e, "Error while uploading a file", e);
            }
            finally
            {
                semaphore.Release();
            }

            Interlocked.Increment(ref progress.ProcessedItems);
            //UploadProgressHandler?.Invoke(progress.ProcessedItems * 100 / progress.TotalItems, progress);
            UploadProgressHandler?.Invoke(progress);
        }

        private async Task UploadFileRemotely(File file, Setup setup, string destinationFilePath, CancellationToken ct)
        {
            var monitorUrl = await setup.Destination.UploadFileFromUrl(destinationFilePath, file);
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
                await setup.Destination.UploadFile(destinationFilePath, localFileName, ct);
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

            switch (file.State)
            {
                case FileState.InProgress:
                    var status = await setup.Destination.CheckOperationStatus(file);
                    file.ResponseData = status.RawResponse;
                    if (status.SuccessResponseCode)
                    {
                        if (status.PercentageComplete == 100)
                        {
                            file.State = FileState.Finished;
                            Interlocked.Increment(ref progress.ProcessedWithSuccess);
                        }
                        else
                        {
                            Interlocked.Increment(ref progress.InProgress);
                        }
                    }
                    else
                    {
                        file.State = FileState.Failed;
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

            Interlocked.Increment(ref progress.ProcessedItems);
            CheckingStatusHandler?.Invoke(progress.ProcessedItems * 100 / progress.TotalItems, progress.ProcessedItems);
        }

        public async Task DownloadFile(string sourceUrl, string localFileName, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            using (var client = new HttpClient())
            using (var inputStream = await client.GetStreamAsync(sourceUrl))
            using (var outputStream = await _storageService.OpenFileStreamForWriteAsync(localFileName))
            {
                await inputStream.CopyToAsync(outputStream, 81920, ct);
            }
        }

    }
}