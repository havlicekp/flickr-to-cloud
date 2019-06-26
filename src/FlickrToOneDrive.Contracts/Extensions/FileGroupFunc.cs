using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FlickrToCloud.Contracts.Models;

namespace FlickrToCloud.Contracts.Extensions
{
    public delegate Task FileGroupFunc<T>(IGrouping<string, File> fileGroup, Setup setup, T progress, SemaphoreSlim semaphore, CancellationToken ct);
}