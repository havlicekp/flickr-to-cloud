using System.Threading.Tasks;

namespace FlickrToOneDrive.Contracts.Interfaces
{
    public interface IDialogService
    {
        Task ShowUrl(string url);
        Task<DialogResult> ShowDialog(string title, string text);
        Task<DialogResult> ShowDialog(string title, string text, string primaryButtonText, string closeButtonText);
    }
}
