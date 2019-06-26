using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using FlickrToCloud.Contracts.Interfaces;
using MvvmCross.IoC;

namespace FlickrToCloud.Dialogs
{
    public sealed partial class LoginDialog
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

        private async void OnNavigationStarting(WebView sender, WebViewNavigationStartingEventArgs args)
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

        private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            WebView.ClearTemporaryWebDataAsync().GetAwaiter().GetResult();
            WebView.Navigate(new Uri(Url));
            args.Cancel = true;
        }
    }
}
