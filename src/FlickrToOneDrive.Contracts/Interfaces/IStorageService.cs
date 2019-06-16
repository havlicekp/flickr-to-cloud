using System;
using System.IO;
using System.Threading.Tasks;

namespace FlickrToOneDrive.Contracts.Interfaces
{
    public interface IStorageService
    {
        Task<Stream> OpenFileStreamForWriteAsync(string fileName);
        Task<Stream> OpenFileStreamForReadAsync(string fileName);
        Task<ulong> GetFileSizeAsync(string fileName);
        Task DeleteFileAsync(string fileName);
        Task<bool> FileExistsAsync(string fileName);
    }
}
