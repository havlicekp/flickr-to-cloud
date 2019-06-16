using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using FlickrToOneDrive.Contracts;
using FlickrToOneDrive.Contracts.Exceptions;
using FlickrToOneDrive.Contracts.Interfaces;
using FlickrToOneDrive.Contracts.Models;
using FlickrToOneDrive.Core.Extensions;
using MvvmCross.Commands;
using MvvmCross.Navigation;
using MvvmCross.ViewModels;
using Serilog;

namespace FlickrToOneDrive.Core.ViewModels
{
    public class StatusViewModel : MvxViewModel<Setup>
    {
        private readonly IMvxNavigationService _navigationService;
        private readonly ICloudCopyService _copyService;
        private readonly IDialogService _dialogService;
        private readonly ILogger _log;
        private Setup _setup;
        private bool _checkingStatus;
        private int _finishedFilesCount;
        private int _failedFilesCount;
        private int _inProgressFilesCount;
        private int _filesProcessed;
        private string _statusMessage;
        private int _progressValue;
        private string _progressMessage;
        private string _progressHeading;
        private string _headingMessage;

        public StatusViewModel(IMvxNavigationService navigationService, ICloudCopyService copyService, IDialogService dialogService, ILogger log)
        {
            CheckStatusCommand = new MvxAsyncCommand(CheckStatus);
            ViewFilesCommand = new MvxAsyncCommand(ViewFiles);
            _navigationService = navigationService;
            _copyService = copyService;
            _dialogService = dialogService;
            _log = log.ForContext(GetType());
        }

        public override void Prepare(Setup setup)
        {
            _setup = setup;
            
            _copyService.CheckingStatusHandler += (progress, filesProcessed) =>
            {
                ProgressHeading = filesProcessed.ToString();
                ProgressValue = progress;
                FilesProcessed = filesProcessed;
            };
            _copyService.CheckingStatusFinishedHandler += (finishedFiles, failedFiles, inProgressFiles, progress) =>
            {
                UpdateStatus(finishedFiles, failedFiles, inProgressFiles, progress);
            };
        }

        private void UpdateStatus(int finishedFiles, int failedFiles, int inProgressFiles, int progress)
        {
            CheckingStatus = false;
            ProgressHeading = $"{progress}%";
            ProgressMessage = "total progress";
            ProgressValue = progress;
            FinishedFilesCount = finishedFiles;
            FailedFilesCount = failedFiles;
            InProgressFilesCount = inProgressFiles;
            if (_setup.Session.State == SessionState.Finished)
            {
                HeadingMessage = "Finished!";
                StatusMessage = "The session is finished now. Thank you for using Flickr To Cloud";
                IsSessionFinished = true;
            }
        }

        public override async void ViewAppeared()
        {
            base.ViewAppeared();
            await CheckStatus();
        }

        public async Task CheckStatus()
        {
            if (_setup.Session.Mode == SessionMode.Remote)
            {
                HeadingMessage = "Checking status";
                StatusMessage = $"Querying {_setup.Destination.Name} to check which files were already uploaded.";
                ProgressHeading = "0";
                ProgressMessage = "file(s) checked";
                _cancellationTokeSource = new CancellationTokenSource();
                await ExecuteWithProgress(() => _copyService.CheckStatus(_setup, _cancellationTokeSource.Token), () => CheckingStatus);
            }
            else
            {
                var finishedFilesCount = _setup.Session.GetFiles(FileState.Finished).Count;
                var failedFilesCount = _setup.Session.GetFiles(FileState.Failed).Count;
                UpdateStatus(finishedFilesCount, failedFilesCount, 0, 100);
            }
        }

        public async Task ViewFiles()
        {
            await _navigationService.Navigate<FilesViewModel, Setup>(_setup);
        }

        public ICommand CheckStatusCommand
        {
            get; set;
        }

        public ICommand ViewFilesCommand
        {
            get; set;
        }

        public int ProgressValue
        {
            get => _progressValue;
            set
            {
                _progressValue = value;
                RaisePropertyChanged(() => ProgressValue);
            }
        }

        public string ProgressMessage
        {
            get => _progressMessage;
            set
            {
                _progressMessage = value;
                RaisePropertyChanged(() => ProgressMessage);
            }
        }

        private CancellationTokenSource _cancellationTokeSource;

        public string ProgressHeading
        {
            get => _progressHeading;
            set
            {
                _progressHeading = value;
                RaisePropertyChanged(() => ProgressHeading);
            }
        }


        public int FilesProcessed
        {
            get => _filesProcessed;
            set
            {
                _filesProcessed = value;
                RaisePropertyChanged(() => FilesProcessed);
            }
        }

        public int FinishedFilesCount
        {
            get => _finishedFilesCount;
            set
            {
                _finishedFilesCount = value;
                RaisePropertyChanged(() => FinishedFilesCount);
            }
        }

        public int FailedFilesCount
        {
            get => _failedFilesCount;
            set
            {
                _failedFilesCount = value;
                RaisePropertyChanged(() => FailedFilesCount);
            }
        }

        public int InProgressFilesCount
        {
            get => _inProgressFilesCount;
            set
            {
                _inProgressFilesCount = value;
                RaisePropertyChanged(() => InProgressFilesCount);
            }
        }

        public bool CheckingStatus
        {
            get => _checkingStatus;
            set
            {
                _checkingStatus = value;
                RaisePropertyChanged(() => CheckingStatus);
            }
        }

        public string HeadingMessage
        {
            get => _headingMessage;
            set
            {
                _headingMessage = value;
                RaisePropertyChanged(() => HeadingMessage);
            }
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

        public bool IsSessionFinished
        {
            get => _setup.Session.State == SessionState.Finished;
            set => RaisePropertyChanged(() => IsSessionFinished);
        }

        private async Task ExecuteWithProgress<T>(Func<Task> a, Expression<Func<T>> progressExpr)
        {
            var expr = (MemberExpression)progressExpr.Body;
            var progressProp = (PropertyInfo)expr.Member;
            progressProp.SetValue(this, true);
            try
            {
                await a();
            }
            catch (CloudCopyException e)
            {
                await _dialogService.ShowDialog("Error", e.Message);
                //StatusMessage = e.Message;
            }
            catch (Exception e)
            {
                _log.Error(e, "Unhandled exception");
                await _dialogService.ShowDialog("Error", "Unknown Error occured");
                //StatusMessage = "Unknown Error occured";
            }
            progressProp.SetValue(this, false);
        }
    }
}
