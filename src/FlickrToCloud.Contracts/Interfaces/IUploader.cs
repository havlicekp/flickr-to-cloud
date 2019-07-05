using System.Threading;
using System.Threading.Tasks;

namespace FlickrToCloud.Contracts.Interfaces
{
    public interface IUploader : IUploadEventsSource
    {
        Task<bool> Upload(CancellationToken ct);
    }
}
