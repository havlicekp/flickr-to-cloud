using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FlickrToOneDrive.Contracts;
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
        private readonly ILogger _log;

        public AppStart(IMvxApplication application, IMvxNavigationService navigationService, ILogger log) : base(application, navigationService)
        {
            _navigationService = navigationService;
            _log = log;
        }

        protected override Task NavigateToFirstViewModel(object hint = null)
        {
            try
            {
                var sessions = GetExistingSessions();
                if (sessions.Any())
                {
                    _navigationService.Navigate<SessionsViewModel, List<Session>>(sessions).GetAwaiter().GetResult();
                }
                else
                {
                    _navigationService.Navigate<LoginViewModel>().GetAwaiter().GetResult();
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
                var sessions = db.Sessions.Where(s => !s.Finished);
                return sessions.ToList();
            }
        }
    }
}
