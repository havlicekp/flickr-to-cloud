using System.IO;
using Windows.Storage;
using FlickrToCloud.Contracts.Interfaces;
using FlickrToCloud.Core.PresentationHints;
using MvvmCross;
using MvvmCross.Logging;
using MvvmCross.Platforms.Uap.Core;
using MvvmCross.Platforms.Uap.Presenters;
using MvvmCross.Platforms.Uap.Views;
using Serilog;
using Serilog.Exceptions;

namespace FlickrToCloud
{
    public class ApplicationSetup : MvxWindowsSetup<Core.App>
    {
        
        public override MvxLogProviderType GetDefaultLogProviderType() => MvxLogProviderType.Serilog;

        protected override IMvxLogProvider CreateLogProvider()
        {
            const string fileOutputTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{SourceContext}] <{ThreadId}> {Message}{NewLine}{Exception}";
            var logPath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "Logs", "FlickrToCloud-{Date}.log");

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

        protected override IMvxWindowsViewPresenter CreateViewPresenter(IMvxWindowsFrame rootFrame)
        {
            var viewPresenter = base.CreateViewPresenter(rootFrame);

            var backStackHandler = new BackStackHintHandler(rootFrame);
            viewPresenter.AddPresentationHintHandler<ClearBackStackHint>(backStackHandler.HandleClearBackStackHint);
            viewPresenter.AddPresentationHintHandler<PopBackStackHint>(backStackHandler.HandlePopBackStackHint);

            return viewPresenter;
        }
    }
}
