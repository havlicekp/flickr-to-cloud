using Windows.System;
using Windows.UI.Xaml.Input;
using FlickrToCloud.Core.ViewModels;
using MvvmCross.Platforms.Uap.Views;
using MvvmCross.ViewModels;

namespace FlickrToCloud.Views
{
    [MvxViewFor(typeof(SessionsViewModel))]
    public sealed partial class SessionsPage : MvxWindowsPage
    {
        public SessionsPage()
        {
            this.InitializeComponent();
        }

        public SessionsViewModel Vm => (SessionsViewModel)ViewModel;

        private void OnKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Delete)
            {
                Vm.DeleteSessionCommand.Execute(null);
            }
        }
    }
}
