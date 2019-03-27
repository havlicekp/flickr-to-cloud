using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using FlickrToOneDrive.Contracts.Exceptions;
using FlickrToOneDrive.Contracts.Interfaces;
using Microsoft.EntityFrameworkCore.Internal;
using MvvmCross.Commands;
using MvvmCross.Navigation;
using MvvmCross.ViewModels;
using Serilog;

namespace FlickrToOneDrive.Core.ViewModels
{
    public class DestinationFolderViewModel : MvxViewModel<Setup>
    {
        private readonly ICloudCopyService _copyService;
        private readonly IDialogService _dialogService;
        private readonly ILogger _log;
        private Setup _setup;
        private bool _checkingFolder;
        private string _folder;

        public DestinationFolderViewModel(IMvxNavigationService navigationService, ICloudCopyService copyService, IDialogService dialogService, ILogger log)
        {
            _copyService = copyService;
            _dialogService = dialogService;
            _log = log.ForContext(GetType());

            SetDestinationFolderCommand = new MvxAsyncCommand(async () =>
            {
                if (string.IsNullOrEmpty(_folder) || _folder == "/" || await EnsureFolderExists())
                {
                    if (!_folder.StartsWith("/"))
                    {
                        _folder = "/" + _folder;
                    }

                    _setup.DestinationFolder = _folder;
                    await navigationService.Navigate<ProgressViewModel, Setup>(_setup);
                }
            });
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

        public string DestinationFolder
        {
            get => _folder;
            set
            {
                _folder = value;
                RaisePropertyChanged(() => DestinationFolder);
            }
        }

        public override void Prepare(Setup setup)
        {
            _setup = setup;
        }

        private async Task<bool> EnsureFolderExists()
        {
            char invalidChar = InvalidCharacterInPath();
            if (invalidChar != default(char))
            {
                await _dialogService.ShowDialog("Error", $"Folder name contains invalid character: {invalidChar}");
                return false;
            }

            CheckingFolder = true;
            try
            {
                var folderExist = await _copyService.Destination.FolderExists(_folder);
                if (!folderExist)
                {
                    var dlgResult = await _dialogService.ShowDialog("Error",
                        $"The folder '{_folder}' does not exist. Create?", "Yes", "Cancel");
                    if (dlgResult == DialogResult.Primary)
                    {
                        var folderCreated = await _copyService.Destination.CreateFolder(_folder);
                        if (!folderCreated)
                        {
                            await _dialogService.ShowDialog("Error", "Unable to create the folder");
                            return false;
                        }
                    }                            
                }

                return true;
            }
            catch (CloudCopyException ce)
            {
                await _dialogService.ShowDialog("Error", ce.Message);
            }
            catch (Exception e)
            {
                await _dialogService.ShowDialog("Error", "Unknown error");
                _log.Error(e, e.Message);
            }
            finally
            {
                CheckingFolder = false;
            }

            return false;
        }

        private char InvalidCharacterInPath()
        {
            var pathInvalidCharacters = Path.GetInvalidFileNameChars().Except(new[] { '/' });
            return pathInvalidCharacters.FirstOrDefault(c => _folder.Contains(c));
        }
    }
}
