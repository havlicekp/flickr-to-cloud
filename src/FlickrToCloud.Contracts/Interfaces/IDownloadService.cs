using System.Threading;
using System.Threading.Tasks;

namespace FlickrToCloud.Contracts.Interfaces
{
    public interface IDownloadService
    {
        Task DownloadFile(string sourceUrl, string localFileName, CancellationToken ct);
    }
}
