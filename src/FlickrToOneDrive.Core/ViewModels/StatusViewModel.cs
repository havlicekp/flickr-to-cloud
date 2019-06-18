using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using FlickrToOneDrive.Contracts;
using FlickrToOneDrive.Contracts.Exceptions;
using FlickrToOneDrive.Contracts.Interfaces;
using FlickrToOneDrive.Contracts.Models;
using FlickrToOneDrive.Contracts.Progress;
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
        private string _statusMessage;
        private int _progressValue;
        private string _progressMessage;
        private string _progressHeading;
        private string _headingMessage;
        private CancellationTokenSource _cancellationTokeSource;
        private bool _pausing;
        private bool _paused;
        private bool _hasError;
        private Exception _exception;
        private bool _checkingFinishedWithSuccess;
        private StatusCheckProgress _lastProgress;

        public StatusViewModel(IMvxNavigationService navigationService, ICloudCopyService copyService, IDialogService dialogService, ILogger log)
        {
            CheckStatusCommand = new MvxAsyncCommand(CheckStatus);
            ViewFilesCommand = new MvxAsyncCommand(ViewFiles);
            PauseCommand = new MvxCommand(PauseChecking);
            ResumeCommand = new MvxAsyncCommand(ResumeChecking);
            ShowErrorDetailsCommand = new MvxAsyncCommand(ShowErrorDetails);
            _navigationService = navigationService;
            _copyService = copyService;
            _dialogService = dialogService;
            _log = log.ForContext(GetType());
        }

        public ICommand CheckStatusCommand { get; set; }
        public ICommand ViewFilesCommand { get; set; }
        public ICommand PauseCommand { get; set; }
        public ICommand ResumeCommand { get; set; }
        public ICommand ShowErrorDetailsCommand { get; set; }
        
        public override void Prepare(Setup setup)
        {
            _setup = setup;
            
            _copyService.CheckingStatusHandler += (progress) =>
            {
                var percentage = progress.ProcessedItems * 100 / progress.TotalItems;
                ProgressValue = percentage;
                ProgressHeading = progress.ProcessedItems.ToString();
                _lastProgress = progress;
            };

            _copyService.CheckingStatusFinishedHandler += (progress) =>
            {
                var percentage = (progress.ProcessedWithSuccess + progress.ProcessedWithError) * 100 / progress.TotalItems;
                UpdateStatus(progress.ProcessedWithSuccess, progress.ProcessedWithError, progress.InProgress, percentage);
                CheckingFinishedWithSuccess = true;
            };
        }

        public override async void ViewAppeared()
        {
            base.ViewAppeared();
            await CheckStatus();
        }

        #region Properties

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

        public string ProgressHeading
        {
            get => _progressHeading;
            set
            {
                _progressHeading = value;
                RaisePropertyChanged(() => ProgressHeading);
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

        public bool Pausing
        {
            get => _pausing;
            set
            {
                _pausing = value;
                RaisePropertyChanged(() => Pausing);
            }
        }

        public bool Paused
        {
            get => _paused;
            set
            {
                _paused = value;
                RaisePropertyChanged(() => Paused);
            }
        }

        public bool HasError
        {
            get => _hasError;
            set
            {
                _hasError = value;
                RaisePropertyChanged(() => HasError);
            }
        }

        public bool CheckingFinishedWithSuccess
        {
            get => _checkingFinishedWithSuccess;
            set
            {
                _checkingFinishedWithSuccess = value;
                RaisePropertyChanged(() => CheckingFinishedWithSuccess);
            }
        }

        public Exception Exception
        {
            get => _exception;
            set
            {
                _exception = value;
                RaisePropertyChanged(() => Exception);
            }
        }

        public bool IsSessionFinished
        {
            get => _setup.Session.State == SessionState.Finished;
            set => RaisePropertyChanged(() => IsSessionFinished);
        }

        #endregion

        #region Command methods

        private async Task CheckStatus()
        {
            if (_setup.Session.Mode == SessionMode.Remote)
            {
                await CheckStatusForRemoteUpload();
            }
            else
            {
                var finishedFilesCount = _setup.Session.GetFiles(FileState.Finished).Count;
                var failedFilesCount = _setup.Session.GetFiles(FileState.Failed).Count;
                UpdateStatus(finishedFilesCount, failedFilesCount, 0, 100);
            }
        }

        private async Task ViewFiles()
        {
            await _navigationService.Navigate<FilesViewModel, Setup>(_setup);
        }

        private void PauseChecking()
        {
            Pausing = true;
            _cancellationTokeSource.Cancel();
        }

        private async Task ResumeChecking()
        {
            await CheckStatusForRemoteUpload(_lastProgress);
        }

        private async Task<DialogResult> ShowErrorDetails()
        {
            return await _dialogService.ShowDialog("Error", _exception.ToString(), true);
        }

        #endregion

        private async Task CheckStatusForRemoteUpload(StatusCheckProgress resumeProgress = null)
        {
            HeadingMessage = "Checking status";
            StatusMessage = $"Querying {_setup.Destination.Name} to check which files were already uploaded";
            ProgressMessage = "file(s) checked";
            if (resumeProgress == null)
            {
                ProgressHeading = "0";
                ProgressValue = 0;
            }
            CheckingStatus = true;
            Paused = HasError = CheckingFinishedWithSuccess = false;
            _cancellationTokeSource = new CancellationTokenSource();

            try
            {
                await _copyService.CheckStatus(_setup, _cancellationTokeSource.Token, resumeProgress);
            }
            catch (OperationCanceledException)
            {
                HandleCancellation();
            }
            catch (Exception e)
            {
                HandleError(e);
            }
            finally
            {
                CheckingStatus = false;
            }
        }        

        private void HandleCancellation()
        {
            Pausing = false;
            Paused = true;
            HeadingMessage = "Paused";
            StatusMessage = "Checking is paused. Press Resume to continue";
        }

        private void HandleError(Exception e)
        {
            var retryMsg = "Click Retry to check for the upload status again. Click Error Details to see the error description";
            var unhandledMsg = "Unhandled exception";

            HeadingMessage = "Error";
            if (e is CloudCopyException)
            {
                StatusMessage = $"{e.Message}. {retryMsg}";
            }
            else
            {
                StatusMessage = $"{unhandledMsg}. {retryMsg}";
                _log.Error(e, unhandledMsg);
            }

            Exception = e;
            HasError = true;
        }

        private void UpdateStatus(int finishedFiles, int failedFiles, int inProgressFiles, int percentage)
        {
            CheckingStatus = false;

            // Update progress bar
            ProgressHeading = $"{percentage}%";
            ProgressMessage = "total progress";
            ProgressValue = percentage;

            // Update file numbers
            FinishedFilesCount = finishedFiles;
            FailedFilesCount = failedFiles;
            InProgressFilesCount = inProgressFiles;

            // Update heading
            if (_setup.Session.State == SessionState.Finished)
            {
                HeadingMessage = "Finished!";
                StatusMessage = "The session is finished now. Thank you for using Flickr To Cloud";
                IsSessionFinished = true;
            }
        }
    }
}
