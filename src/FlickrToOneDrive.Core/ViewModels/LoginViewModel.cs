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
    public class LoginViewModel : MvxViewModel<Setup>
    {
        private readonly IDialogService _dialogService;
        private readonly ICloudCopyService _cloudCopyService;
        private readonly ILogger _log;
        private readonly IMvxNavigationService _navigationService;
        private Setup _setup;

        public LoginViewModel(IDialogService dialogService, ICloudCopyService cloudCopyService, ILogger log, IMvxNavigationService navigationService)
        {
            _dialogService = dialogService;
            _cloudCopyService = cloudCopyService;
            _log = log.ForContext(GetType());
            _navigationService = navigationService;
            SourceLoginCommand = new MvxAsyncCommand(() => Login(cloudCopyService.Source));
            DestinationLoginCommand = new MvxAsyncCommand(() => Login(cloudCopyService.Destination));
        }

        public ICommand SourceLoginCommand { get; set; }

        public ICommand DestinationLoginCommand { get; set; }

        private async Task Login(ICloudFileSystem cloud)
        {
            try
            {
                var url = await cloud.GetAuthorizeUrl();
                await _dialogService.ShowUrl(url);
                if (_cloudCopyService.IsAuthorized)
                {
                    await _navigationService.Navigate<DestinationFolderViewModel, Setup>(_setup);
                }
            }
            catch (Exception e)
            {
                var message = $"Error logging into {cloud.Name}";
                await _dialogService.ShowDialog("Error", message);
                _log.Error(e, message);

            }
        }

        public override void Prepare(Setup setup)
        {
            _setup = setup;
        }
    }
}
