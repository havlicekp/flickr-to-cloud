using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FlickrToCloud.Clouds.Flickr
{
    public interface IFlickrClient
    {
        Task<bool> AuthenticateFromCallbackUrl(string callbackUrl);
        Task<string> GetAuthenticationUrl();
        bool IsFlickrCallbackUrl(string callbackUrl);
        Task<ICollection<FlickrPhoto>> GetStreamPhotos(CancellationToken ct, Action<FlickrRequestProgress> requestProgress = null);
        Task<ICollection<FlickrPhotoset>> GetPhotosets(CancellationToken ct);
        Task<ICollection<FlickrPhoto>> GetPhotosetPhotos(string photosetId, CancellationToken ct);
    }
}