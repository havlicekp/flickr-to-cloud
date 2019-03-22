using System;
using System.Threading.Tasks;

namespace FlickrToOneDrive.Contracts.Interfaces
{
    public interface ICloudCopyService
    {
        event Action<int> UploadProgressHandler;
        event Action ReadingSourceHandler;
        event Action NothingToUploadHandler;
        event Action<int> CheckingStatusHandler;
        event Action<int, int, int> CheckingStatusFinishedHandler;
        ICloudFileSystem Destination { get; }
        ICloudFileSystem Source { get; }
        int CreatedSessionId { get; }
        Task Copy(string destinationPath);
        Task ResumeUpload(int sessionId);
        Task CheckStatus(int sessionId);
        bool IsAuthorized { get; }
    }
}