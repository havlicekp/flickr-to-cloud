using FlickrToOneDrive.Core.ViewModels;
using MvvmCross.Platforms.Uap.Views;
using MvvmCross.ViewModels;

namespace FlickrToOneDrive.Views
{
    [MvxViewFor(typeof(SettingsViewModel))]
    public sealed partial class SettingsPage : MvxWindowsPage
    {
        public SettingsPage()
        {
            this.InitializeComponent();
        }
    }
}
