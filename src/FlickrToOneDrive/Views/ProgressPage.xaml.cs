using FlickrToOneDrive.Core.ViewModels;
using MvvmCross.Platforms.Uap.Views;
using MvvmCross.ViewModels;

namespace FlickrToOneDrive.Views
{
    [MvxViewFor(typeof(ProgressViewModel))]
    public sealed partial class ProgressPage : MvxWindowsPage
    {
        public ProgressPage()
        {
            this.InitializeComponent();
        }
    }
}
