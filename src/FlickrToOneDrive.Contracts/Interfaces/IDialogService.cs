using System.Threading.Tasks;

namespace FlickrToOneDrive.Contracts.Interfaces
{
    public interface IDialogService
    {
        Task ShowUrl(string url);
        Task ShowDialog(string title, string content);
    }
}
