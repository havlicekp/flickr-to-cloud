using FlickrToCloud.Core.ViewModels;
using MvvmCross.Platforms.Uap.Views;
using MvvmCross.ViewModels;

namespace FlickrToCloud.Views
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
