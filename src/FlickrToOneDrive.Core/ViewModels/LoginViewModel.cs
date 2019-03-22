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
        private readonly IFileSource _source;
        private readonly IFileDestination _destination;
        private readonly ILogger _log;
        private readonly IMvxNavigationService _navigationService;

        public LoginViewModel(IDialogService dialogService, IFileSource source, IFileDestination destination,
            ILogger log, IMvxNavigationService navigationService)
        {
            _dialogService = dialogService;
            _source = source;
            _destination = destination;
            _log = log;
            _navigationService = navigationService;
            SourceLoginCommand = new MvxAsyncCommand(() => Login(_source));
            DestinationLoginCommand = new MvxAsyncCommand(() => Login(_destination));
        }

        public ICommand SourceLoginCommand { get; set; }

        public ICommand DestinationLoginCommand { get; set; }

        private async Task Login(ICloudFileSystem cloud)
        {
            try
            {
                var url = await cloud.GetAuthorizeUrl();
                await _dialogService.ShowUrl(url);
                if (_source.IsAuthorized && _destination.IsAuthorized)
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
