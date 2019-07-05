using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using FlickrToCloud.Contracts;
using FlickrToCloud.Contracts.Exceptions;
using FlickrToCloud.Contracts.Interfaces;
using FlickrToCloud.Contracts.Models;
using FlickrToCloud.Contracts.Progress;
using FlickrToCloud.Core.Extensions;
using FlickrToCloud.Core.PresentationHints;
using MvvmCross.Commands;
using MvvmCross.Navigation;
using MvvmCross.ViewModels;
using Serilog;

namespace FlickrToCloud.Core.ViewModels
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
        private bool _sourceIsEmpty;

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
                StatusMessage = $"Creating folder structure on {_setup.Destination.Name}. Please wait";
            };

            _copyService.UploadStartingHandler += (progress) =>
            {
                LocalUploadFinishedWithErrors = false;
                RemoteUploadFinishedWithErrors = false;
                SetInitialMessages();
                UpdateProgressCounts(progress);
                ProgressIndicatorNeeded = true;
                if (_setup.Session.Mode == SessionMode.Remote)
                {
                    HeadingMessage = "Queuing files for download";
                    StatusMessage = $"{_setup.Destination.Name} is being instructed to download files from {_setup.Source.Name}. Please wait until the process finishes";
                    ItemName = "photo(s) queued";
                }
                else
                {
                    HeadingMessage = "Processing files";
                    StatusMessage = $"Photos from {_setup.Source.Name} are being downloaded and uploaded to {_setup.Destination.Name}. Please wait until the process finishes";
                    ItemName = "photo(s) uploaded";
                }
            };

            _copyService.UploadProgressHandler += (progress) =>
            {                
                ProcessedItems = progress.ProcessedItems;
                TotalItems = progress.TotalItems;
            };

            _copyService.UploadFinishedHandler += (progress) =>
            {
                var errorsOccured = _setup.Session.GetFiles(FileState.Failed).Any();
                if (errorsOccured)
                {
                    HeadingMessage = "Finished with errors";
                    StatusMessage = $"Some files were not uploaded to {_setup.Destination.Name}. Click Retry to try again or Show Files to see error details for the failed files";
                    LocalUploadFinishedWithErrors = true;
                }
                /*else
                {
                    // Actually, this branch won't be used, since after an successful upload, the app will switch to status check
                    if (_setup.Session.Mode == SessionMode.Remote)
                    {
                        HeadingMessage = "Done, files queued";
                        StatusMessage = $"The upload runs in background now. Use Check Status button or re-open the app to see how many files were already uploaded";
                    }
                    else
                    {
                        HeadingMessage = "Finished!";
                        StatusMessage = $"{_setup.Destination.Name} now contains files copied from {_setup.Source.Name}. Thank you for using Flickr To Cloud";
                        LocalUploadFinishedWithSuccess = true;
                    }
                }*/
            };

            SetInitialMessages();

        }

        public override async void ViewAppearing()
        {
            base.ViewAppearing();

            // Discard back stack - it shouldn't be possible to go back after
            // upload settings are confirmed
            await _navigationService.ChangePresentation(new ClearBackStackHint());
        }

        public override async Task Initialize()
        {
            await base.Initialize();

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
            get => _itemName;
            set
            {
                _itemName = value;
                RaisePropertyChanged(() => ItemName);
            }
        }

        public bool SourceIsEmpty
        {
            get => _sourceIsEmpty;
            set
            {
                _sourceIsEmpty = value;
                RaisePropertyChanged(() => SourceIsEmpty);
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
            // Remove upload from back stack. If status check follows, it shouldn't
            // be possible to go back to upload from there.
            // For SessionMode.Local there is no status check => leave back track
            // to allow coming back from "View Files"
            if (_setup.Session.Mode == SessionMode.Remote)
            {
                await _navigationService.ChangePresentation(new PopBackStackHint());
            }

            _setup.RequestStatusCheck = true;
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
            await StartUpload();
        }

        private async Task NewSession()
        {
            // Remove upload from back stack. It shouldn't
            // be possible to go back to upload from here.
            if (_setup.Session.Mode == SessionMode.Remote)
            {
                await _navigationService.ChangePresentation(new PopBackStackHint());
            }

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

        private async Task StartUpload()
        {
            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                InProgress = true;
                Paused = Pausing = HasError = SourceIsEmpty = false;

                var result = await _copyService.Copy(_setup, _cancellationTokenSource.Token);
                if (result)
                {
                    await CheckStatus();
                }
            }
            catch (OperationCanceledException e)
            {
                if (_cancellationTokenSource.IsCancellationRequested)
                    HandleCancellation();
                else
                    HandleError(e);
            }
            catch (NothingToUploadException)
            {
                HandleNothingToUpload();
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

        private void HandleNothingToUpload()
        {
            HeadingMessage = "Nothing to upload";
            StatusMessage = $"There are no albums/files on {_setup.Source.Name}. Click Retry to give it another chance or New Session to start over";
            SourceIsEmpty = true;
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
                StatusMessage =
                    $"The current session was cancelled. Any files already uploaded to {_setup.Destination.Name} need to be cleared manually. Click New Session to start a new session";
            }
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
    }
}
