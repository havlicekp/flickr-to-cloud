using System;
using System.Collections.Generic;
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

        public void DispatchUriCallback(Uri uri)
        {
            callbacks.ForEach(c => c.HandleAuthenticationCallback(uri));
        }
    }
}
