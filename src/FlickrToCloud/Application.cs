using System;
using Windows.ApplicationModel.Activation;
using FlickrToCloud.Contracts.Interfaces;
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

        protected override async void OnActivated(IActivatedEventArgs args)
        {
            if (args.Kind == ActivationKind.Protocol)
            {
                try
                {
                    var authCallbackDispatcher = MvxIoCProvider.Instance.Resolve<IAuthenticationCallbackDispatcher>();
                    var eventArgs = args as ProtocolActivatedEventArgs;
                    await authCallbackDispatcher.DispatchUriCallback(eventArgs.Uri);
                }
                catch (Exception e)
                {
                    var dialogService = MvxIoCProvider.Instance.Resolve<IDialogService>();
                    await dialogService.ShowDialog("Error", e.Message);
                }
            }
        }        
    }
}
