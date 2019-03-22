using System;
using System.Threading.Tasks;

namespace FlickrToOneDrive.Contracts.Interfaces
{
    public interface IAuthenticationCallback
    {
        Task HandleAuthenticationCallback(Uri callbackUrl);
    }
}