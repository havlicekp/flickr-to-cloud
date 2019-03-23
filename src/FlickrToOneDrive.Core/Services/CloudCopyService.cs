using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using FlickrToOneDrive.Contracts;
using FlickrToOneDrive.Contracts.Exceptions;
using FlickrToOneDrive.Contracts.Interfaces;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace FlickrToOneDrive.Core.Services
{
    public class CloudCopyService : ICloudCopyService
    {
        private readonly ICloudFileSystem _source;
        private readonly ICloudFileSystem _destination;
        private int _sessionId;
        private readonly ILogger _log;
        private string _destinationPath;

        public event Action<int> UploadProgressHandler;

        public event Action ReadingSourceHandler;

        public event Action NothingToUploadHandler;

        public event Action<int> CheckingStatusHandler;

        public event Action<int, int, int> CheckingStatusFinishedHandler;

        public CloudCopyService(ICloudFileSystemFactory factory, IConfiguration config, ILogger log)
        {
            var sourceCloudId = config["config.sourceCloudId"];
            var destinationCloudId = config["config.destinationCloudId"];

            _source = factory.Create(sourceCloudId);
            _destination = factory.Create(destinationCloudId);

            _log = log.ForContext(GetType());
        }

        public ICloudFileSystem Destination => _destination;

        public ICloudFileSystem Source => _source;

        public int CreatedSessionId => _sessionId;

        public bool IsAuthorized => _source.IsAuthorized && _destination.IsAuthorized;

        public async Task Copy(string destinationPath)
        {
            _destinationPath = destinationPath;

            ReadingSourceHandler?.Invoke();

            var sourceFiles = await _source.GetFiles();
            if (sourceFiles.Length > 0)
            {
                _sessionId = CreateSession(destinationPath, sourceFiles);
                await ResumeUpload(_sessionId);
            }
            else
            {
                NothingToUploadHandler?.Invoke();
                _log.Warning("No files found on Flickr");
            }
        }

        public async Task ResumeUpload(int sessionId)
        {
            using (var db = new CloudCopyContext())
            {
                var fileCount = db.Files.Count(f => f.SessionId == sessionId);
                var files = db.Files.Where(f => string.IsNullOrEmpty(f.UploadStatusData) && f.SessionId == sessionId);
                var progress = 0;
                foreach (var file in files)
                {
                    var uploadStatusData = await _destination.UploadFileFromUrl(_destinationPath, file);
                    if (string.IsNullOrEmpty(uploadStatusData))
                    {
                        _log.Error($"{Source.Name} returned empty monitor URL");
                        throw new CloudCopyException($"Invalid response from {Source.Name}, aborting the upload");
                    }

                    file.UploadStatusData = uploadStatusData;
                    db.SaveChanges();

                    UploadProgressHandler?.Invoke((int)(progress++ * 100 / fileCount));
                }

                UploadProgressHandler?.Invoke(100);
            }
        }

        public async Task CheckStatus(int sessionId)
        {
            var finishedOk = 0;
            var inProgress = 0;
            var finishedError = 0;
            var progress = 0;            

            using (var db = new CloudCopyContext())
            {
                var files = db.Files.Where(f => f.SessionId == sessionId);
                var fileCount = files.Count();

                _log.Information($"Going to check status for {fileCount} file(s)");

                foreach (var f in files)
                {
                    switch (f.UploadStatus)
                    {
                        case UploadStatus.InProgress:
                            var status = await _destination.CheckOperationStatus(f.UploadStatusData);
                            if (status.SuccessResponseCode)
                            {
                                if (status.PercentageComplete == 100)
                                {
                                    f.UploadStatus = UploadStatus.FinishedOk;
                                    finishedOk++;
                                }
                                else
                                {
                                    inProgress++;
                                }
                            }
                            else
                            {
                                f.UploadStatus = UploadStatus.FinishedError;
                                finishedError++;                                
                            }
                            
                            break;
                        case UploadStatus.FinishedOk:
                            finishedOk++;
                            break;
                        case UploadStatus.FinishedError:
                            finishedError++;
                            break;
                    }

                    CheckingStatusHandler?.Invoke((int)(progress++ * 100 / fileCount));
                };

                var sessionFinished = files.All(f => f.UploadStatus != UploadStatus.InProgress);
                if (sessionFinished)
                {
                    var session = db.Sessions.First(s => s.SessionId == sessionId);
                    session.Finished = true;
                }

                db.SaveChanges();
            }
            
            CheckingStatusFinishedHandler?.Invoke(finishedOk, finishedError, inProgress);
        }

        private int CreateSession(string destinationPath, File[] sourceFiles)
        {
            int sessionId;
            using (var db = new CloudCopyContext())
            {
                db.Database.Migrate();

                using (var transaction = db.Database.BeginTransaction())
                {
                    var session = new Session { DestinationFolder = destinationPath, Started = DateTime.Now };
                    db.Sessions.Add(session);
                    db.SaveChanges();

                    foreach (var file in sourceFiles)
                    {
                        file.Session = session;
                        file.SessionId = session.SessionId;
                        db.Files.Add(file);
                    }

                    db.SaveChanges();
                    db.Database.CommitTransaction();

                    sessionId = session.SessionId;
                }
            }

            return sessionId;
        }
    }
}