using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Graphics.Display;
using Windows.UI.ViewManagement;
using MvvmCross.IoC;
using MvvmCross.Platforms.Uap.Views;
using Serilog;

namespace FlickrToCloud
{
    public abstract class Application : MvxApplication<ApplicationSetup, Core.App>                                                
    {
        protected override void RunAppStart(IActivatedEventArgs activationArgs)
        {
            base.RunAppStart(activationArgs);
            UnhandledException += HandleUnhandledException;
        }

        private void HandleUnhandledException(object sender, Windows.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            var log = MvxIoCProvider.Instance.Resolve<ILogger>();
            log.Error(e.Exception, "Unhandled Exception");
        }

        protected override void OnLaunched(LaunchActivatedEventArgs activationArgs)
        {
            base.OnLaunched(activationArgs);
            var dpi = DisplayInformation.GetForCurrentView().LogicalDpi;
            ApplicationView.PreferredLaunchViewSize = new Size((502 * 96.0f / dpi), (739 * 96.0f / dpi));
            ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.PreferredLaunchViewSize;
        }
    }
}
