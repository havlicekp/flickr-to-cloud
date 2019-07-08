using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FlickrToCloud.Common;
using FlickrToCloud.Contracts;
using FlickrToCloud.Contracts.Interfaces;
using FlickrToCloud.Contracts.Models;
using FlickrToCloud.Contracts.Progress;
using FlickrToCloud.Core.Extensions;
using Serilog;
using File = FlickrToCloud.Contracts.Models.File;

namespace FlickrToCloud.Core.Uploaders
{

    public abstract class BaseUploader : IUploader
    {
        protected readonly Setup _setup;
        protected List<File> _files;
        protected readonly ILogger _log;
        protected UploadProgress _progress;
        protected readonly SemaphoreSlim _semaphore;

        public event Action<UploadProgress> UploadProgressHandler;
        public event Action<UploadProgress> UploadFinishedHandler;
        public event Action<UploadProgress> UploadStartingHandler;

        protected BaseUploader(Setup setup, ILogger log)
        {
            _setup = setup;
            _log = log;
            _semaphore = new SemaphoreSlim(48);
        }

        public async Task<bool> Upload(CancellationToken ct)
        {
            _files = ReadFiles();

            var uploadedFilesCount = GetUploadedFilesCount();
            _progress = new UploadProgress { TotalItems = _files.Count() + uploadedFilesCount, ProcessedItems = uploadedFilesCount };

            OnUploadStarting();
            var result = await UploadFiles(ct);
            OnUploadFinished();

            return result;
        }

        protected abstract int GetUploadedFilesCount();

        protected abstract Task<bool> UploadFiles(CancellationToken ct);

        protected List<File> ReadFiles()
        {
            var result =new List<File>();
            result.AddRange(_setup.Session.GetFiles(FileState.None));

            var failedFiles = _setup.Session.GetFiles(FileState.Failed);
            result.AddRange(failedFiles);

            return result;
        }

        protected async Task RunFileOperationAndHandleCancellation(Func<Task> action, File file, string name, CancellationToken ct)
        {
            try
            {
                _log.Information($"Running {name} for {file.LogString()}");
                await action();
                _log.Information($"{name} succeeded for {file.LogString()}");
            }
            catch (OperationCanceledException)
            {
                _log.Information($"{name} cancelled for ({file.LogString()})");
                throw;
            }
            catch (Microsoft.Graph.ServiceException e)
            {
                GraphUtils.TryHandleGraphCancellation(e, $"{name} cancelled for ({file.LogString()})", _log);
                throw;
            }
            catch (Exception e)
            {
                file.SetFailedState(e);
                Interlocked.Increment(ref _progress.ProcessedWithError);
                _log.Error(e, $"Error while {name} for {file.FileName}", e);
            }

            Interlocked.Increment(ref _progress.ProcessedItems);
            OnUploadProgress();
        }

        protected void TryHandleGraphCancellation(Microsoft.Graph.ServiceException e, string message)
        {
            _log.Error(e, "Microsoft Graph exception");
            if (e.InnerException != null && 
                e.Error.Code != "timeout" && 
                (e.InnerException is TaskCanceledException || e.InnerException is OperationCanceledException))
            {
                throw new OperationCanceledException();
            }
        }

        protected void OnUploadStarting()
        {
            UploadStartingHandler?.Invoke(_progress);
        }

        protected void OnUploadProgress()
        {
            UploadProgressHandler?.Invoke(_progress);
        }

        protected void OnUploadFinished()
        {
            UploadFinishedHandler?.Invoke(_progress);
        }
    }
}
