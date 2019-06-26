using FlickrToCloud.Core.ViewModels;
using MvvmCross.Platforms.Uap.Views;
using MvvmCross.ViewModels;

namespace FlickrToCloud.Views
{
    [MvxViewFor(typeof(FilesViewModel))]
    public sealed partial class FilesPage : MvxWindowsPage
    {
        public FilesPage()
        {
            this.InitializeComponent();
        }
    }
}
