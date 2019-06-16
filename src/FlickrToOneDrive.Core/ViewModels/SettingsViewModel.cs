using FlickrToOneDrive.Contracts;
using FlickrToOneDrive.Contracts.Interfaces;
using FlickrToOneDrive.Contracts.Models;
using MvvmCross.Commands;
using MvvmCross.Navigation;
using MvvmCross.ViewModels;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace FlickrToOneDrive.Core.ViewModels
{
    public class SettingsViewModel : MvxViewModel<Setup>
    {
        private Setup _setup;
        private bool _sessionModeRemote;
        private bool _sessionModeLocal;
        private bool _filesOriginFolders;
        private bool _filesOriginFlat;
        private readonly IMvxNavigationService _navigationService;
        private readonly IDialogService _dialogService;

        public ICommand ContinueSetupCommand { get; }

        public SettingsViewModel(IMvxNavigationService navigationService, IDialogService dialogService)
        {
            ContinueSetupCommand = new MvxAsyncCommand(ContinueSetup);
            _navigationService = navigationService;
            _dialogService = dialogService;
        }

        public override void Prepare(Setup setup)
        {
            _setup = setup;
        }

        public bool SessionModeRemote
        {
            get { return _sessionModeRemote; }
            set
            {
                _sessionModeRemote = value;
                RaisePropertyChanged(() => SessionModeRemote);
            }
        }

        public bool SessionModeLocal
        {
            get { return _sessionModeLocal; }
            set
            {
                _sessionModeLocal = value;
                RaisePropertyChanged(() => SessionModeLocal);
            }
        }

        public bool FilesOriginFolders
        {
            get { return _filesOriginFolders; }
            set
            {
                _filesOriginFolders = value;
                RaisePropertyChanged(() => FilesOriginFolders);
            }
        }

        public bool FilesOriginFlat
        {
            get { return _filesOriginFlat; }
            set
            {
                _filesOriginFlat = value;
                RaisePropertyChanged(() => FilesOriginFlat);
            }
        }

        private async Task ContinueSetup()
        {
            if (!_filesOriginFlat && !_filesOriginFolders)
            {
                await _dialogService.ShowDialog("Error", "Please select what to copy");
                return;
            }

            using (var db = new CloudCopyContext())
            {
                var session = db.Sessions.First(s => s.Id == _setup.Session.Id);

                session.Mode = _sessionModeLocal ? SessionMode.Local : SessionMode.Remote;

                if (_filesOriginFlat)
                    session.FilesOrigin |= SessionFilesOrigin.Flat;
                else
                    session.FilesOrigin &= ~SessionFilesOrigin.Flat;

                if (_filesOriginFolders)
                    session.FilesOrigin |= SessionFilesOrigin.Structured;
                else
                    session.FilesOrigin &= ~SessionFilesOrigin.Structured;

                db.SaveChanges();
                _setup.Session = session;
            }

            await _navigationService.Navigate<DestinationFolderViewModel, Setup>(_setup);
        }

    }
}
