using Windows.UI.Xaml.Controls;
using FlickrToOneDrive.Core.ViewModels;
using MvvmCross.Platforms.Uap.Views;
using MvvmCross.ViewModels;

namespace FlickrToOneDrive.Views
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
