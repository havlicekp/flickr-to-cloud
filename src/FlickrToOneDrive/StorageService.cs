using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FlickrToOneDrive.Contracts.Interfaces;
using Windows.Storage;
using Windows.Storage.Search;

namespace FlickrToOneDrive
{
    public class StorageService : IStorageService
    {
        public async Task DeleteFileAsync(string fileName)
        {
            var file = await ApplicationData.Current.LocalFolder.GetItemAsync(fileName);
            await file.DeleteAsync(StorageDeleteOption.PermanentDelete);
        }

        public async Task DeleteFilesAsync(string filter)
        {
            var fileTypeFilter = new List<string>
            {
                filter
            };

            var queryOptions = new QueryOptions(CommonFileQuery.DefaultQuery, fileTypeFilter);
            var queryResult = ApplicationData.Current.LocalFolder.CreateFileQueryWithOptions(queryOptions);
            var files = await queryResult.GetFilesAsync().AsTask().ConfigureAwait(false);
            foreach (var file in files)
            {
                await file.DeleteAsync();
            }
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
