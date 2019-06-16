using System.Linq;
using FlickrToOneDrive.Contracts.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

namespace FlickrToOneDrive.Contracts.Extensions
{
    public delegate Task FileFunc<T>(File file, Setup setup, T progress, SemaphoreSlim semaphore, CancellationToken ct);

    public static class ListExtensions
    {
        public static async Task ForEachAsync<T>(this IList<File> list, FileFunc<T> fileFunc, Setup setup, T progress, CancellationToken ct, int concurrentRequestCount = 6)
        {
            var semaphore = new SemaphoreSlim(concurrentRequestCount);
            var tasks = list.Select((file) => fileFunc(file, setup, progress, semaphore, ct));
            await Task.WhenAll(tasks);
        }
    }
}
