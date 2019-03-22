using System;

namespace FlickrToOneDrive.Contracts.Interfaces
{
    public interface IAuthenticationCallbackDispatcher
    {
        void Register(IAuthenticationCallback callback);
        void DispatchUriCallback(Uri eventArgsUri);
    }
}