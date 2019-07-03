using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FlickrToCloud.Contracts.Interfaces;

namespace FlickrToCloud.Core.Services
{
    public class DownloadService : IDownloadService
    {
        private readonly IStorageService _storageService;

        public DownloadService(IStorageService storageService)
        {
            _storageService = storageService;
        }

        public async Task DownloadFile(string sourceUrl, string localFileName, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            using (var client = new HttpClient())
            using (var inputStream = await client.GetStreamAsync(sourceUrl))
            using (var outputStream = await _storageService.OpenFileStreamForWriteAsync(localFileName))
            {
                await inputStream.CopyToAsync(outputStream, 81920, ct);
            }
        }
    }
}