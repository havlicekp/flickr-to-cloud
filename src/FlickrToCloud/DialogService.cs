using System;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;
using FlickrToCloud.Contracts;
using FlickrToCloud.Contracts.Interfaces;
using FlickrToCloud.Dialogs;
using Nito.AsyncEx;

namespace FlickrToCloud
{
    public class DialogService : IDialogService
    {
        private LoginDialog _loginDlg;

        public async Task ShowUrl(string url)
        {
            _loginDlg = new LoginDialog {Url = url};
            await _loginDlg.ShowAsync();
        }

        public async Task<string> ShowInputDialog(string title, string text, ValidationCallback<string> validationCallback)
        {
            var dlg = new InputDialog
            {
                Title = title
            };

            dlg.PrimaryButtonClick += (sender, args) =>
            {
                // when using 'await validationCallback(..)' the execution continued.
                // Probably related to events vs async. Need to investigate more.
                // Synchronous call 'validationCallback(..).Result' deadlocked. 
                // Probably related to UI thread & contexts.
                // Solved by using AsyncContext from AsyncEx library
                // https://stackoverflow.com/questions/9343594/how-to-call-asynchronous-method-from-synchronous-method-in-c

                var validation = AsyncContext.Run(() => validationCallback(dlg.Text));
                
                if (!validation.Result)
                {
                    dlg.Error = validation.Error;
                    args.Cancel = true;
                }
            };

            var result = await dlg.ShowAsync();
            if (result == ContentDialogResult.Primary)
                return dlg.Text;

            return null;
        }

        public async Task<DialogResult> ShowDialog(string title, string text, bool copyable = false)
        {
            ContentDialog dlg;
            if (copyable)
            {
                dlg = new CopyableTextDialog
                {
                    Title = title,
                    Text = text
                };
            }
            else
            {
                dlg = new ContentDialog
                {
                    Title = title,
                    Content = text,
                    CloseButtonText = "OK"
                };
            }

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

        private async Task<DialogResult> ShowDialog(ContentDialog dlg)
        {
            var result = await dlg.ShowAsync();
            return (DialogResult)(int)result;
        }
    }
}
