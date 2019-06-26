using FlickrToCloud.Core.ViewModels;
using MvvmCross.Platforms.Uap.Views;
using MvvmCross.ViewModels;

namespace FlickrToCloud.Views
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
