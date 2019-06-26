using System.Threading.Tasks;

namespace FlickrToCloud.Contracts.Interfaces
{
    public interface IDialogService
    {
        Task ShowUrl(string url);
        Task<string> ShowInputDialog(string title, string text, ValidationCallback<string> validationCallback);
        Task<DialogResult> ShowDialog(string title, string text, bool copyable = false);
        Task<DialogResult> ShowDialog(string title, string text, string primaryButtonText, string closeButtonText);
    }
}
