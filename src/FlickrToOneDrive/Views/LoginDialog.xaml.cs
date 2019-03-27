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

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            WebView.Navigate(new Uri(Url));
        }
    }
}
