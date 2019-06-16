using System;
using System.IO;
using System.Linq;
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
    public class DestinationFolderViewModel : MvxViewModel<Setup>
    {
        private readonly IDialogService _dialogService;
        private readonly ILogger _log;
        private Setup _setup;
        private bool _checkingFolder;
        private MvxObservableCollection<string> _folders = new MvxObservableCollection<string>();
        private string _currentPath;

        public DestinationFolderViewModel(IMvxNavigationService navigationService, IDialogService dialogService, ILogger log)
        {
            _dialogService = dialogService;
            _log = log.ForContext(GetType());

            SetDestinationFolderCommand = new MvxAsyncCommand(async () =>
            {
                if (String.IsNullOrEmpty(SelectedFolder) || (SelectedFolder == ".."))
                {
                    await _dialogService.ShowDialog("Error", "Please select a folder");
                    return;
                }

                using (var db = new CloudCopyContext())
                {
                    var session = db.Sessions.First(s => s.Id == _setup.Session.Id);
                    session.DestinationFolder = GetFolderPath(SelectedFolder);
                    session.State = SessionState.DestinationFolderSet;
                    db.SaveChanges();
                    _setup.Session = session;
                }

                await navigationService.Navigate<UploadViewModel, Setup>(_setup);
            });

            CreateFolderCommand = new MvxAsyncCommand(async () =>
            {
                var folder = await _dialogService.ShowInputDialog("New Folder", "Please enter a folder name:", ValidateAndCreateFolder);
                if (!string.IsNullOrEmpty(folder))
                {
                    var insertIndex = 0;
                    if (_folders.Count > 0 && _folders[0] == "..")
                        insertIndex = 1;
                    _folders.Insert(insertIndex, folder);
                }
            });

            OpenFolderCommand = new MvxAsyncCommand(async () =>
            {
                await OpenFolder(SelectedFolder);
            });
        }

        private async Task OpenFolder(string folder)
        {
            if (folder == "..")
            {
                var parts = _currentPath.Split('/');
                if (parts.Length <= 2)
                    _currentPath = "/";
                else
                    _currentPath = string.Join("/", parts, 0, parts.Length - 1);
            }
            else
            {
                _currentPath = GetFolderPath(folder);
            }
            var folders = await _setup.Destination.GetSubFolders(_currentPath);
            _folders.Clear();
            if (_currentPath != "/")
                _folders.Add("..");
            if (folders.Length > 0)
                _folders.AddRange(folders);
        }

        public override async void ViewAppeared()
        {
            base.ViewAppeared();
            await OpenFolder("/");
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

        public ICommand SetDestinationFolderCommand
        {
            get; set; 

        }

        public ICommand OpenFolderCommand
        {
            get; set;
        }

        public ICommand CreateFolderCommand
        {
            get; set;
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

        public override void Prepare(Setup setup)
        {
            _setup = setup;
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
                var folderExist = await _setup.Destination.FolderExists(GetFolderPath(folder));
                if (folderExist)
                    return (false, $"The folder '{folder}' already exists");

                var folderCreated = await _setup.Destination.CreateFolder(GetFolderPath(folder));
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

        private string GetFolderPath(string subFolder)
        {
            // '/'
            // '/Folder'
            // '/Folder/SubFolder'
            if (subFolder == "/")
                return subFolder;
            if (_currentPath == "/")
                return $"/{subFolder}";
            return $"{_currentPath}/{subFolder}";
        }
    }
}
