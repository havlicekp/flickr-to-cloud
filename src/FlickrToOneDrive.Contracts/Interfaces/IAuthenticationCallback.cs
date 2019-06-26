using System;
using System.Threading.Tasks;

namespace FlickrToCloud.Contracts.Interfaces
{
    public interface IAuthenticationCallback
    {
        Task<bool> HandleAuthenticationCallback(Uri callbackUri);
    }
}