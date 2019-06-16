using System;
using System.Linq;
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
    public class UploadViewModel : MvxViewModel<Setup>
    {
        private readonly IMvxNavigationService _navigationService;
        private readonly ICloudCopyService _copyService;
        private readonly IDialogService _dialogService;
        private readonly ILogger _log;
        private string _statusMessage;
        private Setup _setup;
        private int _processedItems;
        private string _headingMessage;
        private int _filesProcessed;
        private bool _remoteUploadFinishedWithSuccess;
        private bool _remoteUploadFinishedWithErrors;
        private Exception _exception;
        private bool _hasError;
        private bool _localUploadFinishedWithErrors;
        private bool _localUploadFinishedWithSuccess;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _inProgress;
        private bool _cancelled;
        private int _totalItems;
        private string _itemName;
        private bool _progressIndicatorNeeded;
        private bool _paused;
        private bool _pausing;
        private bool _cancelling;
        private bool _stopping;

        public UploadViewModel(IMvxNavigationService navigationService, ICloudCopyService copyService, IDialogService dialogService, ILogger log)
        {
            CheckStatusCommand = new MvxAsyncCommand(CheckStatus);
            ShowErrorDetailsCommand = new MvxAsyncCommand(ShowErrorDetails);
            RetryCommand = new MvxAsyncCommand(RetryUpload);
            ViewFilesCommand = new MvxAsyncCommand(ViewFiles);
            CancelCommand = new MvxAsyncCommand(CancelUpload);            
            NewSessionCommand = new MvxAsyncCommand(NewSession);
            PauseCommand = new MvxCommand(PauseUpload);
            ResumeCommand = new MvxAsyncCommand(ResumeUpload);
            _navigationService = navigationService;
            _copyService = copyService;
            _dialogService = dialogService;
            _log = log.ForContext(GetType());
        }

        public ICommand CheckStatusCommand { get; set; }
        public ICommand ShowErrorDetailsCommand { get; set; }
        public ICommand RetryCommand { get; set; }
        public ICommand ViewFilesCommand { get; set; }
        public ICommand CancelCommand { get; set; }
        public ICommand NewSessionCommand { get; set; }
        public ICommand PauseCommand { get; set; }
        public ICommand ResumeCommand { get; set; }

        public override void Prepare(Setup setup)
        {
            _setup = setup;

            _copyService.NothingToUploadHandler += () => StatusMessage = $"No files found on {_setup.Source.Name}";
            
            _copyService.ReadingFilesStartingHandler += () =>
            {
                HeadingMessage = "Reading Source";
                StatusMessage = $"Connecting to {_setup.Source.Name}. Please wait";
            };

            _copyService.ReadingFilesProgressHandler += (progress) =>
            {
                ProgressIndicatorNeeded = true;

                switch (progress.Origin)
                {
                    case SessionFilesOrigin.Structured:
                        StatusMessage = "Obtaining photos inside Flickr albums";
                        ItemName = "album(s) read";
                        break;
                    case SessionFilesOrigin.Flat:
                        StatusMessage = "Obtaining list of photos in your photo stream";
                        ItemName = "photo(s) found";
                        break;
                }

                UpdateProgressCounts(progress);
            };

            _copyService.CreatingFoldersHandler += () =>
            {
                // Folders get created beforehand during remote upload
                HeadingMessage = "Creating folders";
                StatusMessage = $"Creating folder structure on {_setup.Source.Name}. Please wait";
            };

            _copyService.UploadStartingHandler += (progress) =>
            {
                LocalUploadFinishedWithErrors = false;
                RemoteUploadFinishedWithErrors = false;
                SetInitialMessages();
                UpdateProgressCounts(progress);
                ProgressIndicatorNeeded = true;
                ItemName = "photo(s) uploaded";
                if (_setup.Session.Mode == SessionMode.Remote)
                {
                    StatusMessage = $"{_setup.Source.Name} is being instructed to download files from {_setup.Destination.Name}. Plese wait until the process finishes";
                }
                else
                {
                    StatusMessage = $"Photos from Flickr are being downloaded and uploaded to {_setup.Destination.Name}. Plese wait until the process finishes";
                }
            };

            _copyService.UploadProgressHandler += (progress) =>
            {                
                ProcessedItems = progress.ProcessedItems;
                TotalItems = progress.TotalItems;
            };

            _copyService.UploadFinishedHandler += (progress) =>
            {
                if (progress.ProcessedWithError > 0)
                {
                    HeadingMessage = "Finished with errors";
                    StatusMessage = $"Some files were not uploaded to {_setup.Destination.Name}. Click Retry to try again or Show Files to see error details for the failed files";
                    LocalUploadFinishedWithErrors = true;
                }

                if (_setup.Session.Mode == SessionMode.Remote)
                {
                    StatusMessage = $"The upload runs in background now. Use Check Status button or re-open the app to see how many files were already uploaded";
                }

                /*else
                {
                    await CheckStatus();
                    /*_navigationService.Navigate<>
                    HeadingMessage = "Finished!";
                    StatusMessage = $"{_setup.Source.Name} now contains files copied from {_setup.Destination.Name}. Thank you for using Flickr To Cloud";
                    LocalUploadFinishedWithSuccess = true;
                }*/
            };

            SetInitialMessages();

        }

        public override async void ViewAppeared()
        {
            base.ViewAppeared();
            var failedFiles = _setup.Session.GetFiles(FileState.Failed);
            if (failedFiles.Any())
            {
                var result = await _dialogService.ShowDialog("Question", "There are files which failed to upload last time. Do you want to retry them?", "Yes", "No");
                if (result == DialogResult.Primary)
                {
                    await StartUpload(true);
                    return;
                }
            }

            await StartUpload();
        }

        #region Properties

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                RaisePropertyChanged(() => StatusMessage);
            }
        }

        public string ItemName
        {
            get { return _itemName; }
            set
            {
                _itemName = value;
                RaisePropertyChanged(() => ItemName);
            }
        }

        public bool LocalUploadFinishedWithSuccess
        {
            get => _localUploadFinishedWithSuccess;
            set
            {
                _localUploadFinishedWithSuccess = value;
                RaisePropertyChanged(() => LocalUploadFinishedWithSuccess);
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

        public int FilesProcessed
        {
            get => _filesProcessed;
            set
            {
                _filesProcessed = value;
                RaisePropertyChanged(() => FilesProcessed);
            }
        }

        public int ProcessedItems
        {
            get => _processedItems;
            set
            {
                _processedItems = value;
                RaisePropertyChanged(() => ProcessedItems);
            }
        }

        public int TotalItems
        {
            get => _totalItems;
            set
            {
                _totalItems = value;
                RaisePropertyChanged(() => TotalItems);
            }
        }

        public bool RemoteUploadFinishedWithSuccess
        {
            get => _remoteUploadFinishedWithSuccess;
            set
            {
                _remoteUploadFinishedWithSuccess = value;
                RaisePropertyChanged(() => RemoteUploadFinishedWithSuccess);
            }
        }

        public bool RemoteUploadFinishedWithErrors
        {
            get => _remoteUploadFinishedWithErrors;
            set
            {
                _remoteUploadFinishedWithErrors = value;
                RaisePropertyChanged(() => RemoteUploadFinishedWithErrors);
            }
        }

        public bool LocalUploadFinishedWithErrors
        {
            get => _localUploadFinishedWithErrors;
            set
            {
                _localUploadFinishedWithErrors = value;
                RaisePropertyChanged(() => LocalUploadFinishedWithErrors);
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

        public bool HasError
        {
            get => _hasError;
            set
            {
                _hasError = value;
                RaisePropertyChanged(() => HasError);
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

        public bool Paused
        {
            get => _paused;
            set
            {
                _paused = value;
                RaisePropertyChanged(() => Paused);
            }
        }

        public bool ProgressIndicatorNeeded
        {
            get => _progressIndicatorNeeded;
            set
            {
                _progressIndicatorNeeded = value;
                RaisePropertyChanged(() => ProgressIndicatorNeeded);
            }
        }

        public bool Cancelled
        {
            get => _cancelled;
            set
            {
                _cancelled = value;
                RaisePropertyChanged(() => Cancelled);
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

        public bool Cancelling
        {
            get => _cancelling;
            set
            {
                _cancelling = value;
                RaisePropertyChanged(() => Cancelling);

            }
        }

        /// <summary>
        /// True when Pausing or Cancelling
        /// </summary>
        public bool Stopping
        {
            get => _stopping;
            set
            {
                _stopping = value;
                RaisePropertyChanged(() => Stopping);

            }
        }

        #endregion

        #region Command methods

        private async Task CheckStatus()
        {
            await _navigationService.Navigate<StatusViewModel, Setup>(_setup);
        }

        private async Task<DialogResult> ShowErrorDetails()
        {
            return await _dialogService.ShowDialog("Error", _exception.ToString(), true);
        }

        private async Task ViewFiles()
        {
            await _navigationService.Navigate<FilesViewModel, Setup>(_setup);
        }

        private async Task RetryUpload()
        {
            await StartUpload(true);
        }

        private async Task NewSession()
        {
            await _navigationService.Navigate<LoginViewModel, Setup>(new Setup());
        }

        private async Task CancelUpload()
        {
            var result = await _dialogService.ShowDialog("Cancel", "Do you really want to cancel the current session?", "Yes", "No");
            if (result == DialogResult.Primary)
            {
                Cancelling = Stopping = true;
                _log.Information("Cancel pressed");
                _cancellationTokenSource.Cancel();
                _setup.Session.Delete();
            }
        }

        private void PauseUpload()
        {
            Pausing = Stopping = true;
            _log.Information("Pause pressed");
            _paused = true;
            _cancellationTokenSource.Cancel();
        }

        private async Task ResumeUpload()
        {
            _log.Information("Resume pressed");
            await StartUpload();
        }

        #endregion

        private async Task StartUpload(bool retryFailed = false)
        {
            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                InProgress = true;
                Paused = HasError = false;

                var result = await _copyService.Copy(_setup, retryFailed, _cancellationTokenSource.Token);
                if (result)
                {
                    await _navigationService.Navigate<StatusViewModel, Setup>(_setup);
                }
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
                InProgress = false;
                ProgressIndicatorNeeded = false;
                if (!_cancellationTokenSource.IsCancellationRequested)
                {
                    _cancellationTokenSource.Dispose();
                }
            }            
        }

        private void HandleError(Exception e)
        {
            var retryMsg = "Click Retry to retry the upload or Error Details to see error description";
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

        private void UpdateProgressCounts(ProgressBase progress)
        {
            ProcessedItems = progress.ProcessedItems;
            TotalItems = progress.TotalItems;
        }

        private void SetInitialMessages()
        {
            if (_setup.Session.Mode == SessionMode.Local)
            {
                HeadingMessage = "Uploading files";
                StatusMessage = "Please wait until all files get uploaded. Closing the app will pause the upload. It will be resumed after the app is reopened. " +
                    "Click Cancel to cancel the upload";
            }
        }

        private void HandleCancellation()
        {
            Stopping = false;
            if (_paused)
            {
                Pausing = false;
                Paused = true;                
                HeadingMessage = "Paused";
                StatusMessage = "The session is paused. Press Resume to resume the upload";
            }
            else
            {
                Cancelling = false;
                Cancelled = true;
                HeadingMessage = "Cancelled!";
                StatusMessage = $"The current session was cancelled. Any files already uploaded to {_setup.Destination.Name} need to be cleared manually. Click New Session to start a new session";
            }
        }
    }
}
