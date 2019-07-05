using System;
using System.Threading;
using System.Threading.Tasks;
using FlickrToCloud.Contracts.Progress;

namespace FlickrToCloud.Contracts.Interfaces
{
    public interface ICloudCopyService : IUploadEventsSource
    {
        event Action ReadingFilesStartingHandler;
        event Action<ReadingFilesProgress> ReadingFilesProgressHandler;
        event Action CreatingFoldersHandler;
        event Action<StatusCheckProgress> CheckingStatusHandler;
        event Action<StatusCheckProgress> CheckingStatusFinishedHandler;
        Task<bool> Copy(Setup setup, CancellationToken ct);
        Task CheckStatus(Setup setup, CancellationToken ct, StatusCheckProgress resumeProgress);
    }
}