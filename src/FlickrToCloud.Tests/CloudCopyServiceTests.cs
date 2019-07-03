using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FlickrToCloud.Contracts;
using FlickrToCloud.Contracts.Interfaces;
using FlickrToCloud.Contracts.Models;
using FlickrToCloud.Contracts.Progress;
using FlickrToCloud.Core.Services;
using Moq;
using MvvmCross.IoC;
using MvvmCross.Tests;
using Serilog;
using Xunit;

namespace FlickrToCloud.Tests
{
    public class CloudCopyServiceTests : MvxIoCSupportingTest, IClassFixture<DatabaseFixture>
    {
        private Mock<ICloudFileSystem> _mockedOneDrive;

        /// <summary>
        /// Verify that folders specified for the source files are created on the destination cloud
        /// </summary>
        [Fact]
        public async void RemoteUploadFoldersGetCreated()
        {
            await Setup((session, setup) => session.Mode = SessionMode.Remote);
            
            _mockedOneDrive.Verify(x => x.CreateFolderAsync("/Test", CancellationToken.None), Times.Once());
            _mockedOneDrive.Verify(x => x.CreateFolderAsync("/Auto Upload/Test", CancellationToken.None), Times.Once());
        }

        /// <summary>
        /// Verify that duplicate files are just copied on the destination cloud (instead of uploading them again)
        /// </summary>
        [Fact]
        public async void LocalUploadDuplicateItemsGetCopied()
        {
            await Setup((session, setup) => session.Mode = SessionMode.Local);

            _mockedOneDrive.Verify(x => x.UploadFileAsync("/Test/DSC05801.jpg", It.IsAny<string>(), CancellationToken.None), Times.Once());
            _mockedOneDrive.Verify(x => x.CopyFileAsync("/Test/DSC05801.jpg", "/Auto Upload/Test", CancellationToken.None), Times.Once());
        }

        /// <summary>
        /// Verify that files returned from the source cloud are correctly persisted
        /// </summary>
        [Fact]
        public async void ReadSourceItemsAreSavedTest()
        {
            await Setup();

            using (var db = new CloudCopyContext())
            {
                Assert.True(db.Files.Count() == 2);
            }
        }

        private new async Task Setup(Action<Session, Setup> beforeCopy = null)
        {
            // Brings MvvmCross's Ioc test container 
            base.Setup();

            var mockedFlickr = new Mock<ICloudFileSystem>();
            mockedFlickr.Setup(x => x.GetFilesAsync(It.IsAny<SessionFilesOrigin>(), It.IsAny<CancellationToken>(),
                It.IsAny<Action<ReadingFilesProgress>>())).Returns(async () =>
            {
                var files = new[]
                {
                    new File
                    {
                        SourceUrl = @"https://farm2.staticflickr.com/1673/26638474801_081d111a4b_o.png",
                        SourceId = "23195831080",
                        FileName = "DSC05801.jpg",
                        SourcePath = "/Test"
                    },
                    new File
                    {
                        SourceUrl = @"https://farm2.staticflickr.com/1597/26098709134_baa6a392e9_o.png",
                        SourceId = "23195831080",
                        FileName = "DSC05791.jpg",
                        SourcePath = "/Auto Upload/Test"

                    }
                };
                return files;
            });
            mockedFlickr.Setup(x => x.IsAuthenticated).Returns(true);
            mockedFlickr.Setup(x => x.GetAuthenticationUrl()).Returns(Task.FromResult("about:blank"));
            mockedFlickr.Setup(x => x.Name).Returns("Flickr");


            _mockedOneDrive = new Mock<ICloudFileSystem>();
            _mockedOneDrive.Setup(x =>
                    x.UploadFileFromUrlAsync(It.IsAny<string>(), It.IsAny<File>(), It.IsAny<CancellationToken>()))
                .Returns(async () =>
                {
                    return
                        "https://api.onedrive.com/v1.0/monitor/4sDWoX9ohvmpgkRnnZHfEgM1o0K0QFAkI2bBFZgrxAhqvhL7496sD55QNhFwcNktnLauQBwB5DhVCncGDclrMGCvA2BzkhWMpEVZaKBaNsfxGMFeaa4_ta6hang-Ob_kK8K98Xa-qfsNcix031Vp7p9K1h7MoOfvm_jatr9jG96x4MiH_WPkPQ4WZvMeUhJPlh";
                });

            var mockedLog = new Mock<ILogger>();
            mockedLog.Setup(x => x.ForContext(typeof(CloudCopyService))).Returns(mockedLog.Object);

            var mockedStorageService = new Mock<IStorageService>();
            var mockedDownloadService = new Mock<IDownloadService>();

            /*mockedOneDrive.Setup(x => x.CheckOperationStatusAsync(It.IsAny<File>(), It.IsAny<CancellationToken>())).Returns<string>(async (monitorUrl) =>
            {
                await Task.Delay(2000);
                return new OperationStatus(20, true, "");
            });*/
            //mockedOneDrive.Setup(x => x.IsAuthenticated).Returns(true);
            //mockedOneDrive.Setup(x => x.GetAuthenticationUrl()).Returns(Task.FromResult("about:blank"));
            _mockedOneDrive.Setup(x => x.Name).Returns("OneDrive");

            /*var mockedCloudFactory = new Mock<ICloudFileSystemFactory>();
            mockedCloudFactory.Setup(x => x.Create(It.Is<string>((s) => s == "onedrive"))).Returns(mockedOneDrive.Object);
            mockedCloudFactory.Setup(x => x.Create(It.Is<string>((s) => s == "flickr"))).Returns(mockedFlickr.Object);
            */
            //Mvx.IoCProvider.RegisterSingleton(mockedCloudFactory.Object);
            //Mvx.IoCProvider.RegisterSingleton<IAuthenticationCallbackDispatcher>(new AuthenticationCallbackDispatcher());

            //Mvx.IoCProvider.ConstructAndRegisterSingleton<IFlickrClient, FlickrClient>();

            Ioc.RegisterSingleton(mockedStorageService.Object);
            Ioc.RegisterSingleton(mockedLog.Object);
            Ioc.RegisterSingleton(mockedDownloadService.Object);
            Ioc.LazyConstructAndRegisterSingleton<ICloudCopyService, CloudCopyService>();

            var session = new Session()
            {
                SourceCloud = mockedFlickr.Object.Name,
                DestinationCloud = _mockedOneDrive.Object.Name,
                DestinationFolder = "/",
                Started = DateTime.Now,
                Mode = SessionMode.Remote, // Default value for Mode
                FilesOrigin = SessionFilesOrigin.Structured // Default value for FilesOrigin
            };

            using (var db = new CloudCopyContext())
            {
                db.Sessions.Add(session);
                db.SaveChanges();
            }

            var setup = new Setup()
            {
                Destination = _mockedOneDrive.Object,
                Source = mockedFlickr.Object,
                Session = session
            };

            beforeCopy?.Invoke(session, setup);

            var cloudCopyService = Ioc.Resolve<ICloudCopyService>();
            await cloudCopyService.Copy(setup, false, CancellationToken.None);
        }
    }
}