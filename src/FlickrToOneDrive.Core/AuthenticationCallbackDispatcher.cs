using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FlickrToOneDrive.Contracts.Interfaces;

namespace FlickrToOneDrive.Core
{
    public class AuthenticationCallbackDispatcher : IAuthenticationCallbackDispatcher
    {
        private List<IAuthenticationCallback> callbacks = new List<IAuthenticationCallback>();

        public void Register(IAuthenticationCallback callback)
        {
            callbacks.Add(callback);
        }

        public async Task DispatchUriCallback(Uri uri)
        {
            foreach (var callback in callbacks)
            {
                await callback.HandleAuthenticationCallback(uri);
            }
        }
    }
}
