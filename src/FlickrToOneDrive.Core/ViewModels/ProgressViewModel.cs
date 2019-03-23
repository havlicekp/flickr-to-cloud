using System;
using System.Threading.Tasks;
using System.Windows.Input;
using FlickrToOneDrive.Contracts.Exceptions;
using FlickrToOneDrive.Contracts.Interfaces;
using MvvmCross.Commands;
using MvvmCross.ViewModels;
using Serilog;

namespace FlickrToOneDrive.Core.ViewModels
{
    public class ProgressViewModel : MvxViewModel<Setup>
    {
        private readonly ICloudCopyService _copyService;
        private readonly IDialogService _dialogService;
        private readonly ILogger _log;
        private string _statusMessage;
        private string _title;
        private bool _inProgress;
        private Setup _setup;

        public ProgressViewModel(ICloudCopyService copyService, IDialogService dialogService, ILogger log)
        {
            CheckStatusCommand = new MvxAsyncCommand(CheckStatus);
            _copyService = copyService;
            _dialogService = dialogService;
            _log = log.ForContext(GetType());
        }

        public override void Prepare(Setup setup)
        {
            _setup = setup;
            _copyService.NothingToUploadHandler += () => StatusMessage = $"No files found on {_copyService.Source.Name}";
            _copyService.ReadingSourceHandler += () => StatusMessage = $"Reading files from {_copyService.Source.Name} ...";
            _copyService.UploadProgressHandler += (progress) =>
            {
                StatusMessage = progress == 100
                    ? $"{_copyService.Destination.Name} was instructed to upload files from {_copyService.Source.Name}. You can click the Check Status button below or close the app and re-open it later to check the progress"
                    : $"Uploading files to {_copyService.Destination.Name} ... {progress}%";
            };
            _copyService.CheckingStatusHandler += (progress) => StatusMessage = $"Checking upload status ... {progress}%";
            _copyService.CheckingStatusFinishedHandler += (finishedOk, finishedFailed, inProgress) =>
                StatusMessage = $"Checking finished. {finishedOk} files successfully uploaded, {finishedFailed} failed to upload, {inProgress} are still in progress";
            Title =
                $"{_copyService.Source.Name} will be queried for the number of photos and {_copyService.Destination.Name} will be instructed to upload them to the specified folder";
        }

        public override async void ViewAppeared()
        {
            base.ViewAppeared();            
            if (_setup.Session == null)
            {
                await ExecuteWithProgress(() => _copyService.Copy(_setup.DestinationFolder));
            }
            else
            {
                await CheckStatus();
            }                
        }

        public async Task CheckStatus()
        {
            StatusMessage = "";
            if (_setup.Session == null)
            {
                await ExecuteWithProgress(() => _copyService.CheckStatus(_copyService.CreatedSessionId)); 
            }
            else
            {
                await ExecuteWithProgress(() => _copyService.CheckStatus(_setup.Session.SessionId));
            }            
        }

        public ICommand CheckStatusCommand
        {
            get; set;
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                RaisePropertyChanged(() => StatusMessage);
            }
        }

        public string Title
        {
            get => _title;
            set
            {
                _title = value;
                RaisePropertyChanged(() => Title);
            }
        }

        public bool InProgress
        {
            get => _inProgress;
            set
            {
                _inProgress = value;
                RaisePropertyChanged(() => InProgress);
            }
        }

        private async Task ExecuteWithProgress(Func<Task> a)
        {
            InProgress = true;
            try
            {
                await a();
            }
            catch (CloudCopyException e)
            {
                await _dialogService.ShowDialog("Error", e.Message);
            }
            catch (Exception e)
            {
                _log.Error(e, "Unhandled exception");
                await _dialogService.ShowDialog("Error", "Unknown Error occured");
            }
            InProgress = false;
        }
    }
}
