using FlickrToOneDrive.Contracts.Models;

namespace FlickrToOneDrive.Contracts.Interfaces
{
    public interface IUploadStatus
    {
        bool IsFinished(File file);
    }
}