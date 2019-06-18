using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FlickrToOneDrive.Contracts;
using FlickrToOneDrive.Contracts.Interfaces;
using FlickrToOneDrive.Contracts.Models;
using FlickrToOneDrive.Core.ViewModels;
using Microsoft.EntityFrameworkCore;
using MvvmCross.Exceptions;
using MvvmCross.Navigation;
using MvvmCross.ViewModels;
using Serilog;

namespace FlickrToOneDrive.Core
{
    public class AppStart : MvxAppStart
    {
        private readonly IMvxNavigationService _navigationService;
        private readonly IStorageService _storageService;
        private readonly ILogger _log;

        public AppStart(IMvxApplication application, IMvxNavigationService navigationService, ILogger log, IStorageService storageService) : base(application, navigationService)
        {
            _navigationService = navigationService;
            _storageService = storageService;
            _log = log.ForContext(GetType());
        }

        protected override Task NavigateToFirstViewModel(object hint = null)
        {
            try
            {
                // Cleanup any temporary files
                _storageService.DeleteFilesAsync(".tmp").GetAwaiter().GetResult();

                var sessions = GetExistingSessions();
                if (sessions.Any())
                {
                    _navigationService.Navigate<SessionsViewModel, List<Session>>(sessions).GetAwaiter().GetResult();
                }
                else
                {
                    _navigationService.Navigate<LoginViewModel, Setup>(new Setup()).GetAwaiter().GetResult();
                }

                return Task.FromResult(0);
            }
            catch (System.Exception ex)
            {                
                throw ex.MvxWrap("Problem navigating to the initial screen");                
            }
        }

        private List<Session> GetExistingSessions()
        {
            using (var db = new CloudCopyContext())
            {
                db.Database.Migrate();
                var sessions = db.Sessions.Where(s => s.State != SessionState.Finished);
                return sessions.ToList();
            }
        }
    }
}
