using System.Windows.Input;
using MvvmCross.Commands;
using MvvmCross.Navigation;
using MvvmCross.ViewModels;

namespace FlickrToOneDrive.Core.ViewModels
{
    public class DestinationFolderViewModel : MvxViewModel<Setup>
    {
        private Setup _setup;

        public DestinationFolderViewModel(IMvxNavigationService navigationService)
        {
            SetDestinationFolderCommand = new MvxAsyncCommand(async () =>
            {
                if (string.IsNullOrEmpty(_setup.DestinationFolder))
                {
                    _setup.DestinationFolder = "/";
                }
                
                await navigationService.Navigate<ProgressViewModel, Setup>(_setup);
                });
        }

        public override void Prepare(Setup setup)
        {
            _setup = setup;
        }

        public ICommand SetDestinationFolderCommand { get; set; }

        public string DestinationFolder
        {
            get => _setup.DestinationFolder;
            set
            {
                _setup.DestinationFolder = value;
                RaisePropertyChanged(() => DestinationFolder);
            }
        }
    }
}
