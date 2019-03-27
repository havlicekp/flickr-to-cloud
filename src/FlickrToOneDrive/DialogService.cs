using System;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;
using FlickrToOneDrive.Contracts.Interfaces;
using FlickrToOneDrive.Views;

namespace FlickrToOneDrive
{
    public class DialogService : IDialogService, IAuthenticationCallback
    {
        private LoginDialog _loginDlg;

        public DialogService(IAuthenticationCallbackDispatcher callbackDispatcher)
        {
            callbackDispatcher.Register(this);
        }

        public async Task ShowUrl(string url)
        {
            _loginDlg = new LoginDialog {Url = url};
            await _loginDlg.ShowAsync();
        }

        public async Task<DialogResult> ShowDialog(string title, string text)
        {
            var dlg = new ContentDialog
            {
                Title = title,
                Content = text,
                CloseButtonText = "OK"
            };

            return await ShowDialog(dlg);
        }

        public async Task<DialogResult> ShowDialog(string title, string text, string primaryButtonText, string closeButtonText)
        {
            var dlg = new ContentDialog
            {
                Title = title,
                Content = text,
                PrimaryButtonText = primaryButtonText,
                CloseButtonText = closeButtonText,
            };

            return await ShowDialog(dlg);
        }

        public Task HandleAuthenticationCallback(Uri callbackUri)
        {
            _loginDlg?.Hide();
            return Task.FromResult(0);
        }

        private async Task<DialogResult> ShowDialog(ContentDialog dlg)
        {
            var result = await dlg.ShowAsync();
            return (DialogResult)(int)result;
        }
    }
}
