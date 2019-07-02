using System;
using System.Threading.Tasks;
using System.Windows.Input;
using FlickrToCloud.Contracts;
using FlickrToCloud.Contracts.Interfaces;
using FlickrToCloud.Contracts.Models;
using MvvmCross.Commands;
using MvvmCross.Navigation;
using MvvmCross.ViewModels;
using Serilog;

namespace FlickrToCloud.Core.ViewModels
{
    public class LoginViewModel : MvxViewModel<Setup>
    {
        private readonly IDialogService _dialogService;
        private readonly ILogger _log;
        private readonly IMvxNavigationService _navigationService;
        private readonly ICloudFileSystemFactory _cloudFactory;
        private readonly IAuthenticationCallbackDispatcher _callbackDispatcher;
        private Setup _setup;
        private bool _canContinue;

        public LoginViewModel(IDialogService dialogService, ILogger log, IMvxNavigationService navigationService, ICloudFileSystemFactory cloudFactory, IAuthenticationCallbackDispatcher callbackDispatcher)
        {
            _dialogService = dialogService;
            _log = log.ForContext(GetType());
            _navigationService = navigationService;
            _cloudFactory = cloudFactory;
            _callbackDispatcher = callbackDispatcher;
            SourceLoginCommand = new MvxAsyncCommand(() => Login(_setup.Source));
            DestinationLoginCommand = new MvxAsyncCommand(() => Login(_setup.Destination));
            ContinueCommand = new MvxAsyncCommand(ContinueSetup);
        }

        public ICommand SourceLoginCommand { get; set; }
        public ICommand DestinationLoginCommand { get; set; }
        public ICommand ContinueCommand { get; set; }

        private async Task Login(ICloudFileSystem cloud)
        {
            try
            {
                var url = await cloud.GetAuthenticationUrl();
                await _dialogService.ShowUrl(url);

                SourceIsAuthenticated = _setup.Source.IsAuthenticated;
                DestinationIsAuthenticated = _setup.Destination.IsAuthenticated;

                var authenticated = SourceLoginNeeded ? SourceIsAuthenticated && DestinationIsAuthenticated : DestinationIsAuthenticated;
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
                                Started = DateTime.Now,
                                Mode = SessionMode.Remote, // Default value for Mode
                                FilesOrigin = SessionFilesOrigin.Structured // Default value for FilesOrigin
                                     
                            }; 
                            db.Sessions.Add(_setup.Session);
                            db.SaveChanges();
                        }                        
                    }

                    CanContinue = true;
                }
            }
            catch (Exception e)
            {
                var message = $"Error logging into {cloud.Name}";
                await _dialogService.ShowDialog("Error", message);
                _log.Error(e, message);

            }
        }

        public async Task ContinueSetup()
        {
            switch (_setup.Session.State)
            {
                // For session already started, skip settings and jump directly to upload/status check
                case SessionState.CreatingFolders:
                case SessionState.ReadingSource:
                case SessionState.Uploading:
                    await _navigationService.Navigate<UploadViewModel, Setup>(_setup);
                    break;
                case SessionState.Checking:
                    _setup.RequestStatusCheck = true;
                    await _navigationService.Navigate<StatusViewModel, Setup>(_setup);
                    break;
                default:
                    // For session not started yet, move to settings
                    await _navigationService.Navigate<SettingsViewModel, Setup>(_setup);
                    break;
            }
        }


        public bool CanContinue
        {
            get => _canContinue;
            set
            {
                _canContinue = value;
                RaisePropertyChanged(() => CanContinue);
            }
        }

        public bool SourceIsAuthenticated
        {
            get => _setup.Source.IsAuthenticated;
            set => RaisePropertyChanged(() => SourceIsAuthenticated);
        }

        public bool DestinationIsAuthenticated
        {
            get => _setup.Destination.IsAuthenticated;
            set => RaisePropertyChanged(() => DestinationIsAuthenticated);
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
