using FlickrToOneDrive.Core.ViewModels;
using MvvmCross.Platforms.Uap.Views;
using MvvmCross.ViewModels;

namespace FlickrToOneDrive.Views
{
    [MvxViewFor(typeof(UploadViewModel))]
    public sealed partial class UploadPage : MvxWindowsPage
    {
        public UploadViewModel Vm => (UploadViewModel)ViewModel;

        public UploadPage()
        {
            this.InitializeComponent();
        }
    }
}
