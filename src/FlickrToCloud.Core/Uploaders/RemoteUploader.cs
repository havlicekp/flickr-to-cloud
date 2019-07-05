using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FlickrToCloud.Contracts;
using FlickrToCloud.Contracts.Models;
using FlickrToCloud.Core.Extensions;
using FlickrToCloud.Common;
using Serilog;

namespace FlickrToCloud.Core.Uploaders
{
    public class RemoteUploader : BaseUploader
    {
        public RemoteUploader(Setup setup, ILogger log) : base(setup, log.ForContext<RemoteUploader>())
        {
        }

        protected override async Task<bool> UploadFiles(CancellationToken ct)
        {
            // When uploading remotely (from URL) we let the destination cloud download all the files in background
            // There is no need to group the files and copy them on the remote server
            var tasks = _files.Select((file) => UploadFile(file, ct));
            await Task.WhenAll(tasks);

            var uploadFinished = !_setup.Session.GetFiles(FileState.Failed).Any();
            if (uploadFinished)
            {
                _setup.Session.UpdateState(SessionState.Checking);
            }

            return uploadFinished;
        }

        protected override int GetUploadedFilesCount()
        {
            // For remote upload, InProgress state means that destination
            // cloud was instructed to download the file
            return _setup.Session.GetFiles(FileState.InProgress).Count;
        }

        protected async Task UploadFile(File file, CancellationToken ct)
        {
            await _semaphore.WaitAsync(ct);
            try
            {
                await RunFileOperationAndHandleCancellation(async () =>
                    {
                        var destinationFilePath = PathUtils.CombinePath(_setup.Session.DestinationFolder, file.SourcePath);
                        var monitorUrl = await _setup.Destination.UploadFileFromUrlAsync(destinationFilePath, file.FileName, file.SourceUrl, ct);
                        file.UpdateMonitorUrl(monitorUrl);
                    }, 
                    file, "UploadFileRemotely", ct);
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}
