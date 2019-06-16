using System;
using System.Threading.Tasks;
using System.Windows.Input;
using FlickrToOneDrive.Contracts;
using FlickrToOneDrive.Contracts.Interfaces;
using FlickrToOneDrive.Contracts.Models;
using MvvmCross.Commands;
using MvvmCross.Navigation;
using MvvmCross.ViewModels;
using Serilog;

namespace FlickrToOneDrive.Core.ViewModels
{
    public class LoginViewModel : MvxViewModel<Setup>
    {
        private readonly IDialogService _dialogService;
        private readonly ILogger _log;
        private readonly IMvxNavigationService _navigationService;
        private readonly ICloudFileSystemFactory _cloudFactory;
        private readonly IAuthenticationCallbackDispatcher _callbackDispatcher;
        private Setup _setup;

        public LoginViewModel(IDialogService dialogService, ILogger log, IMvxNavigationService navigationService, ICloudFileSystemFactory cloudFactory, IAuthenticationCallbackDispatcher callbackDispatcher)
        {
            _dialogService = dialogService;
            _log = log.ForContext(GetType());
            _navigationService = navigationService;
            _cloudFactory = cloudFactory;
            _callbackDispatcher = callbackDispatcher;
            SourceLoginCommand = new MvxAsyncCommand(() => Login(_setup.Source));
            DestinationLoginCommand = new MvxAsyncCommand(() => Login(_setup.Destination));
        }

        public ICommand SourceLoginCommand { get; set; }

        public ICommand DestinationLoginCommand { get; set; }

        private async Task Login(ICloudFileSystem cloud)
        {
            try
            {
                var url = await cloud.GetAuthenticationUrl();
                await _dialogService.ShowUrl(url);

                var authenticated = SourceLoginNeeded ? _setup.Source.IsAuthenticated && _setup.Destination.IsAuthenticated : _setup.Destination.IsAuthenticated;
                if (authenticated)
                {
                    if (_setup.Session == null)
                    {                        
                        // New session => create it
                        using (var db = new CloudCopyContext())
                        {
                            _setup.Session = new Session {
                                SourceCloud = _setup.Source.Name,
                                DestinationCloud = _setup.Destination.Name,
                                Started = DateTime.Now
                            }; 
                            db.Sessions.Add(_setup.Session);
                            db.SaveChanges();
                        }
                    }

                    switch (_setup.Session.State)
                    {
                        case SessionState.Created:
                            await _navigationService.Navigate<SettingsViewModel, Setup>(_setup);
                            break;
                        case SessionState.DestinationFolderSet:
                        case SessionState.CreatingFolders:
                        case SessionState.ReadingSource:
                        case SessionState.Uploading:
                            await _navigationService.Navigate<UploadViewModel, Setup>(_setup);
                            break;
                        case SessionState.Checking:
                            await _navigationService.Navigate<StatusViewModel, Setup>(_setup);
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                var message = $"Error logging into {cloud.Name}";
                await _dialogService.ShowDialog("Error", message);
                _log.Error(e, message);

            }
        }

        public bool SourceLoginNeeded
        {
            get => _setup.Session == null || _setup.Session.State <= SessionState.ReadingSource;
        }

        public bool NewSession
        {
            get => _setup.Session == null;
        }

        public override void Prepare(Setup setup)
        {
            _setup = setup;
            if (_setup.Source == null)
                _setup.Source = _cloudFactory.Create("flickr");
            if (_setup.Destination == null)
                _setup.Destination = _cloudFactory.Create("onedrive");

            _callbackDispatcher.Register(_setup.Source);
            _callbackDispatcher.Register(_setup.Destination);
        }

        public override void ViewDisappeared()
        {
            base.ViewDisappeared();
            _callbackDispatcher.Unregister(_setup.Source);
            _callbackDispatcher.Unregister(_setup.Destination);
        }
    }
}
