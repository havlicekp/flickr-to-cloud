using FlickrToCloud.Core.ViewModels;
using MvvmCross.Platforms.Uap.Views;
using MvvmCross.ViewModels;

namespace FlickrToCloud.Views
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
