using System.IO;
using System.Threading.Tasks;

namespace FlickrToCloud.Contracts.Interfaces
{
    public interface IStorageService
    {
        Task<Stream> OpenFileStreamForWriteAsync(string fileName);
        Task<Stream> OpenFileStreamForReadAsync(string fileName);
        Task<ulong> GetFileSizeAsync(string fileName);
        Task DeleteFileAsync(string fileName);
        Task DeleteFilesAsync(string filter);
        Task<bool> FileExistsAsync(string fileName);
    }
}
