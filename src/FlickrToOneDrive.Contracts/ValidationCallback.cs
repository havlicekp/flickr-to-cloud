using System.Net.Http;
using System.Threading.Tasks;

namespace FlickrToOneDrive.Contracts
{
    public delegate Task<(bool Result, string Error)> ValidationCallback<in T>(T input);
}
