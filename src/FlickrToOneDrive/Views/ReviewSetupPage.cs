using FlickrToCloud.Core.ViewModels;
using MvvmCross.Platforms.Uap.Views;
using MvvmCross.ViewModels;

namespace FlickrToCloud.Views
{
    [MvxViewFor(typeof(ReviewSetupViewModel))]
    public sealed partial class ReviewSetupPage : MvxWindowsPage
    {
        public ReviewSetupPage()
        {
            this.InitializeComponent();
        }
    }
}
