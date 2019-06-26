using System;
using System.Threading;
using System.Threading.Tasks;
using FlickrToCloud.Clouds.Flickr;
using FlickrToCloud.Contracts.Interfaces;
using FlickrToCloud.Contracts.Models;
using FlickrToCloud.Contracts.Progress;
using FlickrToCloud.Core.Services;
using Moq;
using MvvmCross;
using MvvmCross.IoC;
using MvvmCross.ViewModels;

namespace FlickrToCloud.Core
{
    public class App : MvxApplication
    {
        public override void Initialize()
        {
            //InitializeMockedClasses();
            InitializeRealClasses();
        }

        private void InitializeRealClasses()
        {
            Mvx.IoCProvider.ConstructAndRegisterSingleton<IFlickrClient, FlickrClient>();
            Mvx.IoCProvider.RegisterType<ICloudFileSystemFactory, CloudFileSystemFactory>();
            Mvx.IoCProvider.LazyConstructAndRegisterSingleton<ICloudCopyService, CloudCopyService>();
            Mvx.IoCProvider.RegisterSingleton<IAuthenticationCallbackDispatcher>(new AuthenticationCallbackDispatcher());
            RegisterCustomAppStart<AppStart>();
        }

        private void InitializeMockedClasses()
        {
            var mockedFlickr = new Mock<ICloudFileSystem>();
            mockedFlickr.Setup(x => x.GetFilesAsync(It.IsAny<SessionFilesOrigin>(), It.IsAny<CancellationToken>(), It.IsAny<Action<ReadingFilesProgress>>())).Returns(async () =>
            {
                var files = new File[]
                {
                    new File
                    {
                        SourceUrl = @"https://farm2.staticflickr.com/1673/26638474801_081d111a4b_o.png",
                        FileName = "Screenshot_2016-04-29-00-57-12"
                    },
                    new File
                    {
                        SourceUrl = @"https://farm2.staticflickr.com/1597/26098709134_baa6a392e9_o.png",
                        FileName = "20160428_153952"
                    },
                    new File
                    {
                        SourceUrl = @"https://farm2.staticflickr.com/1597/26098709134_baa6a392e9_o.png",
                        FileName = "20160428_153952"
                    },
                    new File
                    {
                        SourceUrl = @"https://farm2.staticflickr.com/1597/26098709134_baa6a392e9_o.png",
                        FileName = "20160428_153952"
                    }
                };

                await Task.Delay(1000);
                return files;
            });
            mockedFlickr.Setup(x => x.IsAuthenticated).Returns(true);
            mockedFlickr.Setup(x => x.GetAuthenticationUrl()).Returns(Task.FromResult("about:blank"));
            mockedFlickr.Setup(x => x.Name).Returns("Flickr");


            var mockedOneDrive = new Mock<ICloudFileSystem>();
            mockedOneDrive.Setup(x => x.UploadFileFromUrlAsync(It.IsAny<string>(), It.IsAny<File>(), It.IsAny<CancellationToken>())).Returns(async () =>
            {
                await Task.Delay(2000);
                return
                    "https://api.onedrive.com/v1.0/monitor/4sDWoX9ohvmpgkRnnZHfEgM1o0K0QFAkI2bBFZgrxAhqvhL7496sD55QNhFwcNktnLauQBwB5DhVCncGDclrMGCvA2BzkhWMpEVZaKBaNsfxGMFeaa4_ta6hang-Ob_kK8K98Xa-qfsNcix031Vp7p9K1h7MoOfvm_jatr9jG96x4MiH_WPkPQ4WZvMeUhJPlh";
            });
            mockedOneDrive.Setup(x => x.CheckOperationStatusAsync(It.IsAny<File>(), It.IsAny<CancellationToken>())).Returns<string>(async (monitorUrl) =>
            {
                await Task.Delay(2000);
                return new OperationStatus(20, true, "");
            });
            mockedOneDrive.Setup(x => x.IsAuthenticated).Returns(true);
            mockedOneDrive.Setup(x => x.GetAuthenticationUrl()).Returns(Task.FromResult("about:blank"));
            mockedOneDrive.Setup(x => x.Name).Returns("OneDrive");

            var mockedCloudFactory = new Mock<ICloudFileSystemFactory>();
            mockedCloudFactory.Setup(x => x.Create(It.Is<string>((s) => s == "onedrive"))).Returns(mockedOneDrive.Object);
            mockedCloudFactory.Setup(x => x.Create(It.Is<string>((s) => s == "flickr"))).Returns(mockedFlickr.Object);

            Mvx.IoCProvider.RegisterSingleton(mockedCloudFactory.Object);
            Mvx.IoCProvider.ConstructAndRegisterSingleton<IFlickrClient, FlickrClient>();
            Mvx.IoCProvider.RegisterSingleton<IAuthenticationCallbackDispatcher>(new AuthenticationCallbackDispatcher());
            Mvx.IoCProvider.LazyConstructAndRegisterSingleton<ICloudCopyService, CloudCopyService>();

            RegisterCustomAppStart<AppStart>();
        }

    }
}
