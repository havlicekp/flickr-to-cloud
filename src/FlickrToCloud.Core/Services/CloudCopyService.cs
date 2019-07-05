using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FlickrToCloud.Contracts;
using FlickrToCloud.Contracts.Exceptions;
using FlickrToCloud.Contracts.Extensions;
using FlickrToCloud.Contracts.Interfaces;
using FlickrToCloud.Contracts.Models;
using FlickrToCloud.Contracts.Progress;
using FlickrToCloud.Core.Extensions;
using FlickrToCloud.Common;
using Serilog;
using File = FlickrToCloud.Contracts.Models.File;

namespace FlickrToCloud.Core.Services
{
    public class CloudCopyService : ICloudCopyService
    {
        private readonly ILogger _log;
        private readonly IUploaderFactory _uploaderFactory;

        public event Action<UploadProgress> UploadProgressHandler;
        public event Action<UploadProgress> UploadFinishedHandler;
        public event Action<UploadProgress> UploadStartingHandler;
        public event Action ReadingFilesStartingHandler;
        public event Action CreatingFoldersHandler;
        public event Action<StatusCheckProgress> CheckingStatusHandler;
        public event Action<StatusCheckProgress> CheckingStatusFinishedHandler;
        public event Action<ReadingFilesProgress> ReadingFilesProgressHandler;

        public CloudCopyService(ILogger log, IUploaderFactory uploaderFactory)
        {
            _log = log.ForContext(GetType());
            _uploaderFactory = uploaderFactory;
        }

        public async Task<bool> Copy(Setup setup, CancellationToken ct)
        {
            // Read source files
            if (setup.Session.State <= SessionState.ReadingSource)
            {
                await ReadSource(setup, ct);
            }

            // Create destination folders for remote session
            // For local session, folders get created automatically
            if (setup.Session.FilesOrigin.HasFlag(SessionFilesOrigin.Structured)
                && (setup.Session.Mode == SessionMode.Remote)
                && (setup.Session.State <= SessionState.CreatingFolders))
            {
                CreatingFoldersHandler?.Invoke();
                await CreateFoldersAsync(setup, ct);
            }

            // Copy source to destination
            return await ResumeUpload(setup, ct);
        }

        public async Task CheckStatus(Setup setup, CancellationToken ct, StatusCheckProgress resumeProgress)
        {
            var files = setup.Session.GetFiles();
            var progress = new StatusCheckProgress { TotalItems = files.Count() };

            if (resumeProgress != null)
            {
                // Skip already processed files
                files = files.Skip(resumeProgress.ProcessedItems).ToList();
                CopyProgress(resumeProgress, progress);
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

        private void CopyProgress(StatusCheckProgress resumeProgress, StatusCheckProgress progress)
        {
            progress.ProcessedItems = resumeProgress.ProcessedItems;
            progress.ProcessedWithError = resumeProgress.ProcessedWithError;
            progress.ProcessedWithSuccess = resumeProgress.ProcessedWithSuccess;
            progress.InProgress = resumeProgress.InProgress;
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
                        using (var transaction = await db.Database.BeginTransactionAsync(ct))
                        {
                            foreach (var file in sourceFiles)
                            {
                                file.SessionId = setup.Session.Id;
                                file.FileName = GetUniqueFileName(file, sourceFiles);
                            }
                            await db.Files.AddRangeAsync(sourceFiles);

                            await db.SaveChangesAsync(ct);
                            transaction.Commit();
                        }
                    }

                    // Return here. If there are no files to process there
                    // is an exception thrown at the end of the method
                    return;
                }
            }
            catch (OperationCanceledException)
            {
                _log.Information("ReadSource cancelled");
                throw;
            }
            catch (Microsoft.Graph.ServiceException e)
            {
                TryHandleGraphCancellation(e, "ReadSource cancelled");
                throw;
            }
            catch (Exception e)
            {
                throw new ReadingSourceException($"Error getting files from {setup.Source.Name}", e, _log);
            }

            _log.Warning($"No files found on {setup.Source.Name}");
            throw new NothingToUploadException();
        }

        private async Task<bool> ResumeUpload(Setup setup, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            setup.Session.UpdateState(SessionState.Uploading);

            // Create local/remote uploader
            var uploader = _uploaderFactory.Create(setup);

            // Wire progress events
            uploader.UploadStartingHandler += (progress) => UploadStartingHandler?.Invoke(progress);
            uploader.UploadProgressHandler += (progress) => UploadProgressHandler?.Invoke(progress);
            uploader.UploadFinishedHandler += (progress) => UploadFinishedHandler?.Invoke(progress);

            // Upload the files
            return await uploader.Upload(ct);
        }

        private async Task CreateFoldersAsync(Setup setup, CancellationToken ct)
        {
            setup.Session.UpdateState(SessionState.CreatingFolders);

            var files = setup.Session.GetFiles(FileState.None);
            var folders = files.Select(f => f.SourcePath).Where(p => p != "/").Distinct();
            foreach (var folder in folders)
            {
                ct.ThrowIfCancellationRequested();
                var destinationFolder = PathUtils.CombinePath(setup.Session.DestinationFolder, folder);
                await setup.Destination.CreateFolderAsync(destinationFolder, ct);
            }
        }

        private void TryHandleGraphCancellation(Microsoft.Graph.ServiceException e, string message)
        {
            _log.Error(e, "Microsoft Graph exception");
            if (e.InnerException != null && (e.InnerException is TaskCanceledException || e.InnerException is OperationCanceledException))
            {
                throw new OperationCanceledException();
            }
        }

        private async Task CheckFileStatus(File file, Setup setup, StatusCheckProgress progress, SemaphoreSlim semaphore, CancellationToken ct)
        {            
            await semaphore.WaitAsync(ct);            
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

        protected string GetUniqueFileName(File file, File[] files)
        {
            using (var db = new CloudCopyContext())
            {
                // Find number of duplicate file names under a folder
                // => use this number to construct unique file name like 'file (2).jpg'
                var duplicateCount = files
                    .Count(dbf => dbf.SourceFileName == file.SourceFileName &&
                                  dbf.SourcePath == file.SourcePath &&
                                  !string.IsNullOrEmpty(dbf.FileName));

                // No duplicate names
                if (duplicateCount == 0)
                    return file.SourceFileName;

                // We want to start from +1 higher
                // file.txt => file (2).txt
                duplicateCount += 1;

                var fileName = Path.GetFileNameWithoutExtension(file.SourceFileName);
                var ext = Path.GetExtension(file.SourceFileName);
                return $"{fileName} ({duplicateCount}){ext}";
            }
        }
    }
}