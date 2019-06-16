using System;
using System.IO;
using Windows.ApplicationModel.Activation;
using Windows.Storage;
using FlickrToOneDrive.Contracts.Interfaces;
using MvvmCross;
using MvvmCross.IoC;
using MvvmCross.Logging;
using MvvmCross.Platforms.Uap.Core;
using MvvmCross.Platforms.Uap.Views;
using Serilog;
using Serilog.Exceptions;

namespace FlickrToOneDrive
{
    public sealed partial class App
    {
        public App()
        {
            this.InitializeComponent();
        }
    }

    public class Setup : MvxWindowsSetup<Core.App>
    {
        
        public override MvxLogProviderType GetDefaultLogProviderType() => MvxLogProviderType.Serilog;

        protected override IMvxLogProvider CreateLogProvider()
        {
            const string fileOutputTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{SourceContext}] <{ThreadId}> {Message}{NewLine}{Exception}";
            var logPath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "Logs", "FlickrToOneDrive-{Date}.log");

            var logConfiguration = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .Enrich.WithThreadId()
                .Enrich.WithExceptionDetails()
                .WriteTo.RollingFile(logPath, outputTemplate: fileOutputTemplate);

            Log.Logger = logConfiguration.CreateLogger();            

            return base.CreateLogProvider();
        }

        protected override void InitializeFirstChance()
        {            
            base.InitializeFirstChance();
            Mvx.IoCProvider.RegisterSingleton<ILogger>(() => Log.Logger);
            Mvx.IoCProvider.RegisterType<IConfiguration, Configuration>();
            Mvx.IoCProvider.RegisterType<IDialogService, DialogService>();  
            Mvx.IoCProvider.RegisterType<IStorageService, StorageService>();
        }
    }


    public abstract class FlickrToOneDriveApp : MvxApplication<Setup, Core.App>                                                
    {

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
