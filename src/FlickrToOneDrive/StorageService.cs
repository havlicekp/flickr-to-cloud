using System;
using System.IO;
using System.Threading.Tasks;
using FlickrToOneDrive.Contracts.Interfaces;
using Windows.Storage;

namespace FlickrToOneDrive
{
    public class StorageService : IStorageService
    {
        public async Task DeleteFileAsync(string fileName)
        {
            var file = await ApplicationData.Current.LocalFolder.GetItemAsync(fileName);
            await file.DeleteAsync(StorageDeleteOption.PermanentDelete);
        }

        public async Task<bool> FileExistsAsync(string fileName)
        {
            var item = await ApplicationData.Current.LocalFolder.TryGetItemAsync(fileName);
            return item != null;
        }

        public async Task<ulong> GetFileSizeAsync(string fileName)
        {
            var file = await ApplicationData.Current.LocalFolder.GetItemAsync(fileName);
            var props = await file.GetBasicPropertiesAsync();
            return props.Size;
        }

        public async Task<Stream> OpenFileStreamForReadAsync(string fileName)
        {
            return await ApplicationData.Current.LocalFolder.OpenStreamForReadAsync(fileName);
        }

        public async Task<Stream> OpenFileStreamForWriteAsync(string fileName)
        {
            var file = await ApplicationData.Current.LocalFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
            return await file.OpenStreamForWriteAsync();
        }        
    }
}
