using System.Threading.Tasks;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;
using FlickrToCloud.Core.PresentationHints;
using MvvmCross.Platforms.Uap.Views;

namespace FlickrToCloud
{
    public class BackStackHintHandler
    {
        private readonly Frame _frame;

        public BackStackHintHandler(IMvxWindowsFrame rootFrame)
        {
            _frame = (Frame)rootFrame.UnderlyingControl;
        }

        public Task<bool> HandleClearBackStackHint(ClearBackStackHint hint)
        {
            _frame.BackStack.Clear();
            UpdateBackButtonVisibility();
            return Task.FromResult(true);
        }

        public Task<bool> HandlePopBackStackHint(PopBackStackHint hint)
        {
            if (_frame.CanGoBack)
            {
                _frame.BackStack.RemoveAt(_frame.BackStackDepth - 1);
                UpdateBackButtonVisibility();
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        private void UpdateBackButtonVisibility()
        {
            SystemNavigationManager.
                    GetForCurrentView().AppViewBackButtonVisibility =
                _frame.CanGoBack ?
                    AppViewBackButtonVisibility.Visible :
                    AppViewBackButtonVisibility.Collapsed;
        }

    }
}
