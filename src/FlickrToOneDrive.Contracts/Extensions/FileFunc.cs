using System.Threading;
using System.Threading.Tasks;
using FlickrToCloud.Contracts.Models;

namespace FlickrToCloud.Contracts.Extensions
{
    public delegate Task FileFunc<T>(File file, Setup setup, T progress, SemaphoreSlim semaphore, CancellationToken ct);
}