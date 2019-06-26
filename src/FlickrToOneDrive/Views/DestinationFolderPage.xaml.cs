using FlickrToCloud.Core.ViewModels;
using MvvmCross.Platforms.Uap.Views;
using MvvmCross.ViewModels;

namespace FlickrToCloud.Views
{
    [MvxViewFor(typeof(DestinationFolderViewModel))]
    public sealed partial class DestinationFolderPage : MvxWindowsPage
    {
        public DestinationFolderPage()
        {
            this.InitializeComponent();
        }

        public DestinationFolderViewModel Vm => (DestinationFolderViewModel) ViewModel;
    }
}
