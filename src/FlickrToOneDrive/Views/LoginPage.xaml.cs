using MvvmCross.Platforms.Uap.Views;
using MvvmCross.ViewModels;
using FlickrToOneDrive.Core.ViewModels;

namespace FlickrToOneDrive.Views
{
    [MvxViewFor(typeof(LoginViewModel))]
    public sealed partial class LoginPage : MvxWindowsPage
    {
        public LoginPage()
        {
            this.InitializeComponent();
        }
    }
}
