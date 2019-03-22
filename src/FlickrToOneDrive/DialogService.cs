using System;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;
using FlickrToOneDrive.Contracts.Interfaces;
using FlickrToOneDrive.Views;

namespace FlickrToOneDrive
{
    public class DialogService : IDialogService, IAuthenticationCallback
    {
        private readonly LoginDialog _loginDlg = new LoginDialog();

        public DialogService(IAuthenticationCallbackDispatcher callbackDispatcher)
        {
            callbackDispatcher.Register(this);
        }

        public async Task ShowUrl(string url)
        {
            _loginDlg.Url = url;
            await _loginDlg.ShowAsync();
            await Task.FromResult(0);
        }

        public async Task ShowDialog(string title, string content)
        {
            ContentDialog noWifiDialog = new ContentDialog
            {
                Title = title,
                Content = content,
                CloseButtonText = "Ok"
            };

            await noWifiDialog.ShowAsync();
        }

        public Task HandleAuthenticationCallback(Uri callbackUrl)
        {
            _loginDlg.Hide();
            return Task.FromResult(0);
        }
    }
}
