using FlickrToOneDrive.Contracts.Progress;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FlickrToOneDrive.Contracts.Interfaces
{
    public interface ICloudCopyService
    {
        event Action<UploadProgress> UploadStartingHandler;
        event Action<UploadProgress> UploadProgressHandler;
        event Action<UploadProgress> UploadFinishedHandler;        
        event Action ReadingFilesStartingHandler;
        event Action<ReadingFilesProgress> ReadingFilesProgressHandler;
        event Action CreatingFoldersHandler;
        event Action NothingToUploadHandler;
        event Action<StatusCheckProgress> CheckingStatusHandler;
        event Action<StatusCheckProgress> CheckingStatusFinishedHandler;
        Task<bool> Copy(Setup setup, bool retryFailed, CancellationToken ct);
        Task CheckStatus(Setup setup, CancellationToken ct, StatusCheckProgress resumeProgress);
    }
}