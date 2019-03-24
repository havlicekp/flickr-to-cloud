using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace FlickrToOneDrive.Flickr
{
    public interface IFlickrClient
    {
        Task<bool> AuthenticateFromCallbackUrl(string callbackUrl);
        Task<string> GetAuthenticationUrl();
        Task<JObject> PhotosSearch(int page = 1, int perPage = 100, string extras = "", FlickrParams parameters = null);
        string CallbackUrl { get; }
    }
}