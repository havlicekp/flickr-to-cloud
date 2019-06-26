using System.Collections.Generic;
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
            ResumeSessionCommand = new MvxAsyncCommand(ResumeSession);
            NewSessionCommand = new MvxAsyncCommand(NewSession);
            DeleteSessionCommand = new MvxAsyncCommand(DeleteSession);
        }

        public ICommand DeleteSessionCommand { get; }

        public ICommand NewSessionCommand { get; }

        public ICommand ResumeSessionCommand { get;  }

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

        private async Task ResumeSession()
        {
            if (SelectedSession == null)
            {
                await _dialogService.ShowDialog("Error", "Please select a session from the list");
                return;
            }

            _log.Information("Going to continue with existing session {SelectedSession}", SelectedSession);

            await _navigationService.Navigate<LoginViewModel, Setup>(new Setup { Session = SelectedSession });
        }

        private async Task NewSession()
        {
            _log.Information("Going to create a new session");
            await _navigationService.Navigate<LoginViewModel, Setup>(new Setup());
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
