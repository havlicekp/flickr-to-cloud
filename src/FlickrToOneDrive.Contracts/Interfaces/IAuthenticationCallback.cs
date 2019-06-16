using System;
using System.Threading.Tasks;

namespace FlickrToOneDrive.Contracts.Interfaces
{
    public interface IAuthenticationCallback
    {
        Task<bool> HandleAuthenticationCallback(Uri callbackUri);
    }
}