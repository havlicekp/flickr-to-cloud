using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using FlickrToCloud.Contracts;
using FlickrToCloud.Contracts.Interfaces;
using FlickrToCloud.Contracts.Models;
using FlickrToCloud.Core.Extensions;
using MvvmCross.Commands;
using MvvmCross.Navigation;
using MvvmCross.ViewModels;
using Serilog;

namespace FlickrToCloud.Core.ViewModels
{
    public class DestinationFolderViewModel : MvxViewModel<Setup>
    {
        private readonly IDialogService _dialogService;
        private readonly ILogger _log;
        private Setup _setup;
        private bool _checkingFolder;
        private MvxObservableCollection<string> _folders = new MvxObservableCollection<string>();
        private string _currentPath;
        private bool _loadingFolders;
        private bool _hasError;

        public ICommand ContinueCommand { get; set; }
        public ICommand OpenFolderCommand { get; set; }
        public ICommand CreateFolderCommand { get; set; }
        public ICommand RetryCommand { get; set; }

        public DestinationFolderViewModel(IMvxNavigationService navigationService, IDialogService dialogService, ILogger log)
        {
            _dialogService = dialogService;
            _log = log.ForContext(GetType());

            ContinueCommand = new MvxAsyncCommand(async () =>
            {
                _setup.Session.UpdateDestinationFolder(CurrentPath);
                _setup.Session.UpdateState(SessionState.DestinationFolderSet);
                await navigationService.Navigate<ReviewSetupViewModel, Setup>(_setup);
            });

            CreateFolderCommand = new MvxAsyncCommand(async () =>
            {
                var folder = await _dialogService.ShowInputDialog("New Folder", "Please enter a folder name:", ValidateAndCreateFolder);
                if (!string.IsNullOrEmpty(folder))
                {
                    await OpenFolder(GetFolderPath(folder));
                    /*var insertIndex = 0;
                    if (_folders.Count > 0 && _folders[0] == "..")
                        insertIndex = 1;
                    _folders.Insert(insertIndex, folder);*/
                }
            });

            OpenFolderCommand = new MvxAsyncCommand(async () =>
            {
                await OpenFolder(GetFolderPath(SelectedFolder));
            });

            RetryCommand = new MvxAsyncCommand(async () =>
            {
                HasError = false;
                await OpenFolder(CurrentPath);
            });
        }

        private async Task OpenFolder(string path)
        {
            _folders.Clear();
            LoadingFolders = true;
            try
            {                
                var folders = await _setup.Destination.GetSubFoldersAsync(path, CancellationToken.None);
                if (path != "/")
                {
                    _folders.Add("..");
                }

                if (folders.Length > 0)
                {
                    _folders.AddRange(folders);
                }
                _setup.Session.UpdateDestinationFolder(path);
                CurrentPath = path;
            }
            catch (Exception e)
            {
                _log.Error(e, "Error reading folders");
                HasError = true;
            }
            finally
            {
                LoadingFolders = false;
            }
        }

        public bool LoadingFolders
        {
            get => _loadingFolders;
            set
            {
                _loadingFolders = value;
                RaisePropertyChanged(() => LoadingFolders);
            }
        }

        public bool CheckingFolder
        {
            get => _checkingFolder;
            set
            {
                _checkingFolder = value;
                RaisePropertyChanged(() => CheckingFolder);
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

        public string SelectedFolder
        {
            get; set; 

        }

        public MvxObservableCollection<string> Folders
        {
            get => _folders;
            set
            {
                _folders = value;
                RaisePropertyChanged(() => Folders);
            }
        }

        public string CurrentPath
        {
            get => _currentPath;
            set
            {
                _currentPath = value;
                RaisePropertyChanged(() => CurrentPath);
            }
        }

        public override async void Prepare(Setup setup)
        {
            _setup = setup;
            if (string.IsNullOrEmpty(_setup.Session.DestinationFolder))
                await OpenFolder("/");
            else
                await OpenFolder(_setup.Session.DestinationFolder);
        }

        private async Task<(bool result, string error)> ValidateAndCreateFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder))
                return (false, $"Folder name can't be empty");

            if (InvalidCharacterInPath(folder, out var invalidChar))
                return (false, $"Folder name contains invalid character: {invalidChar}");

            CheckingFolder = true;
            try
            {
                var folderExist = await _setup.Destination.FolderExistsAsync(GetFolderPath(folder), CancellationToken.None);
                if (folderExist)
                    return (false, $"The folder '{folder}' already exists");

                var folderCreated = await _setup.Destination.CreateFolderAsync(GetFolderPath(folder), CancellationToken.None);
                if (!folderCreated)
                    return (false, "Unable to create the folder");

                return (true, "");
            }
            catch (Exception e)
            {
                _log.Error(e, e.Message);
                return (false, e.Message);
            }
            finally
            {
                CheckingFolder = false;
            }
        }

        private bool InvalidCharacterInPath(string path, out char invalidChar)
        {
            var pathInvalidCharacters = Path.GetInvalidFileNameChars().Except(new[] { '/', '\\' });
            invalidChar = pathInvalidCharacters.FirstOrDefault(c => path.Contains(c));
            return invalidChar != default(char);
        }

        private string GetFolderPath(string folder)
        {
            if (folder == "..")
            {
                var parts = _currentPath.Split('/');
                if (parts.Length <= 2)
                    return "/";
                else
                    return string.Join("/", parts, 0, parts.Length - 1);
            }
            else
            {
                // '/'
                // '/Folder'
                // '/Folder/SubFolder'
                if (folder == "/")
                    return folder;
                if (_currentPath == "/")
                    return $"/{folder}";
                return $"{_currentPath}/{folder}";
            }
        }
    }
}
