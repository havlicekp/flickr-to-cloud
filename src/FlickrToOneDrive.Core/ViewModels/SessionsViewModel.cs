using System.Collections.Generic;
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
    public class SessionsViewModel : MvxViewModel<List<Session>>
    {
        private readonly ILogger _log;
        private readonly IMvxNavigationService _navigationService;
        private readonly IDialogService _dialogService;
        private MvxObservableCollection<Session> _sessions;

        public SessionsViewModel(ILogger log, IMvxNavigationService navigationService, IDialogService dialogService)
        {
            _log = log.ForContext(GetType());
            _navigationService = navigationService;
            _dialogService = dialogService;
            CheckStatusCommand = new MvxAsyncCommand(CheckStatus);
            NewSessionCommand = new MvxAsyncCommand(NewSession);
            DeleteSessionCommand = new MvxAsyncCommand(DeleteSession);
        }

        public ICommand DeleteSessionCommand { get; }

        public ICommand NewSessionCommand { get; }

        public ICommand CheckStatusCommand { get;  }

        public Session SelectedSession { get; set; }

        public MvxObservableCollection<Session> Sessions
        {
            get => _sessions;
            set
            {
                _sessions = value;
                RaisePropertyChanged(() => Sessions);
            }
        }

        public override void Prepare(List<Session> sessions)
        {
            Sessions = new MvxObservableCollection<Session>(sessions);
        }

        private async Task CheckStatus()
        {
            if (SelectedSession == null)
            {
                await _dialogService.ShowDialog("Error", "Please select a session from the list");
                return;
            }

            var setup = new Setup {Session = SelectedSession};
            _log.Information("Going to continue with existing session {SelectedSession}", SelectedSession);
            await _navigationService.Navigate<ProgressViewModel, Setup>(setup);
        }

        private async Task NewSession()
        {
            var setup = new Setup();
            _log.Information("Going to create a new session");
            await _navigationService.Navigate<LoginViewModel, Setup>(setup);
        }

        private Task DeleteSession()
        {
            if (SelectedSession != null)
            {
                using (var db = new CloudCopyContext())
                {
                    db.Sessions.Remove(SelectedSession);
                    db.SaveChanges();
                    Sessions.Remove(SelectedSession);
                }
            }

            return Task.FromResult(0);
        }    
    }
}
