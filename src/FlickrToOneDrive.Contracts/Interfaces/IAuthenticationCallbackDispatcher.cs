using System;
using System.Threading.Tasks;

namespace FlickrToOneDrive.Contracts.Interfaces
{
    public interface IAuthenticationCallbackDispatcher
    {
        void Register(IAuthenticationCallback callback);
        Task DispatchUriCallback(Uri eventArgsUri);
    }
}