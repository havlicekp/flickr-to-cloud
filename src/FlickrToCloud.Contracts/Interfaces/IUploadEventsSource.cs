using System;
using FlickrToCloud.Contracts.Progress;

namespace FlickrToCloud.Contracts.Interfaces
{
    public interface IUploadEventsSource
    {
        event Action<UploadProgress> UploadProgressHandler;
        event Action<UploadProgress> UploadFinishedHandler;
        event Action<UploadProgress> UploadStartingHandler;
    }
}