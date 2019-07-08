using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FlickrToCloud.Contracts;
using FlickrToCloud.Contracts.Interfaces;
using FlickrToCloud.Contracts.Models;
using FlickrToCloud.Contracts.Progress;
using FlickrToCloud.Core;
using FlickrToCloud.Core.Services;
using FlickrToCloud.Core.Uploaders;
using Microsoft.EntityFrameworkCore;
using Moq;
using MvvmCross.IoC;
using MvvmCross.Tests;
using Serilog;
using Xunit;

namespace FlickrToCloud.Tests
{
    public class CloudCopyServiceTests : MvxIoCSupportingTest, IDisposable
    {
        public CloudCopyServiceTests()
        {
            using (var db = new CloudCopyContext())
            {
                db.Database.Migrate();
            }
        }

        public void Dispose()
        {
            using (var db = new CloudCopyContext())
            {
                db.Database.EnsureDeleted();
            }
        }

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

            _mockedOneDrive.Verify(x => x.UploadFileAsync("/DSC05801.jpg", It.IsAny<string>(), CancellationToken.None), Times.Once());
            _mockedOneDrive.Verify(x => x.CopyFileAsync("/DSC05801.jpg", "/Test", "DSC05801.jpg", CancellationToken.None), Times.Once());
        }

        /// <summary>
        /// Verify that files returned from the source cloud are correctly persisted
        /// </summary>
        [Fact]
        public async void DuplicateFilesGetUniqueFileNamesTest()
        {
            await Setup();

            using (var db = new CloudCopyContext())
            {
                var files = db.Files
                    .Where(f => f.SourcePath == "/" && f.SourceFileName.StartsWith("DSC05801", StringComparison.CurrentCultureIgnoreCase))
                    .OrderByDescending(f => f.FileName)
                    .ToArray();

                
                Assert.True(files[0].FileName == "dsc05801 (3).jpg");
                Assert.True(files[1].FileName == "dsc05801 (2) (2).jpg");
                Assert.True(files[2].FileName == "DSC05801.jpg");
                Assert.True(files[3].FileName == "DSC05801 (2).jpg");
                
            }
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
                Assert.True(db.Files.Count() == 7);
            }
        }

        private async Task Setup(Action<Session, Setup> beforeCopy = null)
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
                        SourceUrl = @"https://farm2.staticflickr.com/1673/a.png",
                        SourceId = "23195831080",
                        SourceFileName = "DSC05801.jpg",
                        SourcePath = "/"
                    },
                    new File
                    {
                        SourceUrl = @"https://farm2.staticflickr.com/1673/b.png",
                        SourceId = "23195831081", // <= different source ID under '/' but the same file name
                                                  // => should be uploaded as 'DSC05801 (2).jpg'
                        SourceFileName = "DSC05801.jpg",
                        SourcePath = "/"
                    },
                    new File
                    {
                        SourceUrl = @"https://farm2.staticflickr.com/1673/b.png",
                        SourceId = "23195831082", 
                        SourceFileName = "dsc05801.jpg", // <= same as previous file but the file name is in lowercase
                                                         // => test for grouping by file name
                        SourcePath = "/"
                    },
                    new File
                    {
                        SourceUrl = @"https://farm2.staticflickr.com/1673/b.png",
                        SourceId = "23195831083",
                        SourceFileName = "dsc05801 (2).jpg", // <= file with (2) already exists,
                                                             // => unique file names should continue with 3
                        SourcePath = "/"
                    },

                    new File
                    {
                        SourceUrl = @"https://farm2.staticflickr.com/1673/b.png",
                        SourceId = "23195831080", // <= same as under '/'
                                                  // but this time the file should be COPIED as 'DSC05801 (2).jpg'
                                                
                        SourceFileName = "DSC05801.jpg",
                        SourcePath = "/Test"
                    },
                    new File
                    {
                        SourceUrl = @"https://farm2.staticflickr.com/1673/b.png",
                        SourceId = "23195831081",
                        SourceFileName = "DSC05801.jpg",
                        SourcePath = "/Test"
                    },
                    new File
                    {
                        SourceUrl = @"https://farm2.staticflickr.com/1597/26098709134_baa6a392e9_o.png",
                        SourceId = "23195831080",
                        SourceFileName = "DSC05791.jpg",
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
                    x.UploadFileFromUrlAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(async () =>
                {
                    return
                        "https://api.onedrive.com/v1.0/monitor/4sDWoX9ohvmpgkRnnZHfEgM1o0K0QFAkI2bBFZgrxAhqvhL7496sD55QNhFwcNktnLauQBwB5DhVCncGDclrMGCvA2BzkhWMpEVZaKBaNsfxGMFeaa4_ta6hang-Ob_kK8K98Xa-qfsNcix031Vp7p9K1h7MoOfvm_jatr9jG96x4MiH_WPkPQ4WZvMeUhJPlh";
                });

            var mockedLog = new Mock<ILogger>();
            mockedLog.Setup(x => x.ForContext(It.IsAny<Type>())).Returns(mockedLog.Object);
            mockedLog.Setup(x => x.ForContext<RemoteUploader>()).Returns(mockedLog.Object);
            mockedLog.Setup(x => x.ForContext<LocalUploader>()).Returns(mockedLog.Object);

            var mockedStorageService = new Mock<IStorageService>();
            var mockedDownloadService = new Mock<IDownloadService>();

            _mockedOneDrive.Setup(x => x.Name).Returns("OneDrive");

            Ioc.RegisterSingleton(mockedStorageService.Object);
            Ioc.RegisterSingleton(mockedLog.Object);
            Ioc.RegisterSingleton(mockedDownloadService.Object);
            Ioc.LazyConstructAndRegisterSingleton<ICloudCopyService, CloudCopyService>();
            Ioc.LazyConstructAndRegisterSingleton<IUploaderFactory, UploaderFactory>();

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
            await cloudCopyService.Copy(setup, CancellationToken.None);
        }
    }
}