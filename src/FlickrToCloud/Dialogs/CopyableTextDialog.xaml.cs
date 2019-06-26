using Windows.ApplicationModel.DataTransfer;
using Windows.UI.Xaml.Controls;

namespace FlickrToCloud.Dialogs
{
    public sealed partial class CopyableTextDialog : ContentDialog
    {
        public CopyableTextDialog()
        {
            this.InitializeComponent();
        }

        public string Text
        {
            get => tbContent.Text;
            set => tbContent.Text = value;
        }

        private void CopyButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText(tbContent.Text);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
            args.Cancel = true;
        }
    }
}
