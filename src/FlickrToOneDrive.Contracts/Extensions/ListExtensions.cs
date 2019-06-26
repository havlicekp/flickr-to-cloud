using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FlickrToCloud.Contracts.Models;

namespace FlickrToCloud.Contracts.Extensions
{
    public static class ListExtensions
    {
        public static async Task ForEachAsync<T>(this IList<File> list, FileFunc<T> fileFunc, Setup setup, T progress, CancellationToken ct, int concurrentRequestCount = 48)
        {
            var semaphore = new SemaphoreSlim(concurrentRequestCount);
            var tasks = list.Select((file) => fileFunc(file, setup, progress, semaphore, ct));
            await Task.WhenAll(tasks);
        }

        public static async Task ForEachAsync<T>(this IEnumerable<IGrouping<string, File>> list, FileGroupFunc<T> fileFunc, Setup setup, T progress, CancellationToken ct, int concurrentRequestCount = 48)
        {
            var semaphore = new SemaphoreSlim(concurrentRequestCount);
            var tasks = list.Select((file) => fileFunc(file, setup, progress, semaphore, ct));
            await Task.WhenAll(tasks);
        }
    }
}
