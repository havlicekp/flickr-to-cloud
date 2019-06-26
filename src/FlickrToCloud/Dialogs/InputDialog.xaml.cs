using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace FlickrToCloud.Dialogs
{
    public sealed partial class InputDialog : ContentDialog
    {
        public InputDialog()
        {
            this.InitializeComponent();
        }

        public string Text => tbText.Text;

        public string Error
        {
            get => tbError.Text;
            set => tbError.Text = value;
        }

        private void OnOpenedHandler(ContentDialog sender, ContentDialogOpenedEventArgs args)
        {
            tbText.Focus(FocusState.Keyboard);
        }
    }
}
