using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using FlickrToOneDrive.Contracts.Interfaces;
using MvvmCross.IoC;

namespace FlickrToOneDrive.Dialogs
{
    public sealed partial class LoginDialog : ContentDialog
    {
        public string Url { get; set; }

        public LoginDialog()
        {
            InitializeComponent();
        }        

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            WebView.Navigate(new Uri(Url));
        }

        private async void WebView_OnNavigationStarting(WebView sender, WebViewNavigationStartingEventArgs args)
        {
            try
            {
                var authCallbackDispatcher = MvxIoCProvider.Instance.Resolve<IAuthenticationCallbackDispatcher>();
                if (await authCallbackDispatcher.DispatchUriCallback(args.Uri))
                    Hide();
            }
            catch (Exception e)
            {
                var dialogService = MvxIoCProvider.Instance.Resolve<IDialogService>();
                await dialogService.ShowDialog("Error", e.Message);
            }
        }
    }
}
