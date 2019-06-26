using System.Threading.Tasks;

namespace FlickrToCloud.Contracts
{
    public delegate Task<(bool Result, string Error)> ValidationCallback<in T>(T input);
}
