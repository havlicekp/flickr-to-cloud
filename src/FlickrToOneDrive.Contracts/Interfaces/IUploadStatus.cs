using FlickrToCloud.Contracts.Models;

namespace FlickrToCloud.Contracts.Interfaces
{
    public interface IUploadStatus
    {
        bool IsFinished(File file);
    }
}