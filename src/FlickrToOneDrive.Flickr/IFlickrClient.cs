using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace FlickrToOneDrive.Flickr
{
    public interface IFlickrClient
    {
        Task<bool> Authenticate(string authCode);
        Task<string> GetAuthorizeUrl();
        Task<JObject> PhotosSearch(int page = 1, int perPage = 100, string extras = "", FlickrParams parameters = null);
    }
}