using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace FlickrToOneDrive.Views
{
    public sealed partial class LoginDialog : ContentDialog
    {
        public string Url { get; set; }

        public LoginDialog()
        {
            InitializeComponent();
        }        

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void ContentDialog1_OnLoaded(object sender, RoutedEventArgs e)
        {
            WebView.Navigate(new Uri(Url));
        }
    }
}
