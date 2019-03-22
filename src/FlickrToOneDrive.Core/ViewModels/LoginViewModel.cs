using System;
using System.Threading.Tasks;
using System.Windows.Input;
using FlickrToOneDrive.Contracts;
using FlickrToOneDrive.Contracts.Interfaces;
using MvvmCross.Commands;
using MvvmCross.Navigation;
using MvvmCross.ViewModels;
using Serilog;

namespace FlickrToOneDrive.Core.ViewModels
{
    public class LoginViewModel : MvxViewModel
    {
        private readonly IDialogService _dialogService;
        private readonly ICloudCopyService _cloudCopy;
        private readonly ILogger _log;
        private readonly IMvxNavigationService _navigationService;

        public LoginViewModel(IDialogService dialogService, ICloudCopyService cloudCopy, ILogger log, IMvxNavigationService navigationService)
        {
            _dialogService = dialogService;
            _cloudCopy = cloudCopy;
            _log = log;
            _navigationService = navigationService;
            SourceLoginCommand = new MvxAsyncCommand(() => Login(cloudCopy.Source));
            DestinationLoginCommand = new MvxAsyncCommand(() => Login(cloudCopy.Destination));
        }

        public ICommand SourceLoginCommand { get; set; }

        public ICommand DestinationLoginCommand { get; set; }

        private async Task Login(ICloudFileSystem cloud)
        {
            try
            {
                var url = await cloud.GetAuthorizeUrl();
                await _dialogService.ShowUrl(url);
                if (_cloudCopy.IsAuthorized)
                {
                    await _navigationService.Navigate<ProgressViewModel, Session>((Session)null);
                }
            }
            catch (Exception e)
            {
                var message = $"Error logging into {cloud.Name}";
                await _dialogService.ShowDialog("Error", message);
                _log.Error(e, message);

            }
        }
    }
}
