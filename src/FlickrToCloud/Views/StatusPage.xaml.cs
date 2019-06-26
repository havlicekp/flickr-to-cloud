using FlickrToCloud.Core.ViewModels;
using MvvmCross.Platforms.Uap.Views;
using MvvmCross.ViewModels;

namespace FlickrToCloud.Views
{
    [MvxViewFor(typeof(StatusViewModel))]
    public sealed partial class StatusPage : MvxWindowsPage
    {
        public StatusViewModel Vm => (StatusViewModel)ViewModel;

        public StatusPage()
        {
            this.InitializeComponent();
        }
    }
}
