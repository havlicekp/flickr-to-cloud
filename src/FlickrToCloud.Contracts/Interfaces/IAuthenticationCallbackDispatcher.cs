using System;
using System.Threading.Tasks;

namespace FlickrToCloud.Contracts.Interfaces
{
    public interface IAuthenticationCallbackDispatcher
    {
        void Register(IAuthenticationCallback callback);
        void Unregister(IAuthenticationCallback callback);
        Task<bool> DispatchUriCallback(Uri eventArgsUri);
    }
}