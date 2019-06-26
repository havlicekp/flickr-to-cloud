using System.Threading.Tasks;
using System.Windows.Input;
using FlickrToCloud.Contracts;
using FlickrToCloud.Contracts.Models;
using MvvmCross.Commands;
using MvvmCross.Navigation;
using MvvmCross.ViewModels;

namespace FlickrToCloud.Core.ViewModels
{
    public class ReviewSetupViewModel : MvxViewModel<Setup>
    {
        private readonly IMvxNavigationService _navigationService;
        private Setup _setup;

        public ICommand ContinueCommand { get; set; }

        public string SourceCloudName => _setup.Source.Name;

        public string DestinationCloudName => _setup.Destination.Name;

        public string DestinationFolder => _setup.Session.DestinationFolder;

        public SessionMode SessionMode => _setup.Session.Mode;

        public SessionFilesOrigin SessionFilesOrigin => _setup.Session.FilesOrigin;

        public ReviewSetupViewModel(IMvxNavigationService navigationService)
        {
            _navigationService = navigationService;
            ContinueCommand = new MvxAsyncCommand(ContinueSetup);
        }

        public override void Prepare(Setup setup)
        {
            _setup = setup;
            base.Prepare();
        }

        private async Task ContinueSetup()
        {
            await _navigationService.Navigate<UploadViewModel, Setup>(_setup);
        }
    }
}
