using System;
using System.Linq;
using FlickrToCloud.Contracts;
using FlickrToCloud.Contracts.Models;
using FlickrToCloud.Common;
using File = FlickrToCloud.Contracts.Models.File;

namespace FlickrToCloud.Core.Extensions
{
    public static class FileExtensions
    {
        public static void UpdateState(this File file, FileState state)
        {
            using (var db = new CloudCopyContext())
            {
                var dbFile = db.Files.First(f => f.Id == file.Id);
                dbFile.State = file.State = state;
                db.SaveChanges();
            }
        }

        public static void UpdateResponseData(this File file, string response)
        {
            using (var db = new CloudCopyContext())
            {
                var dbFile = db.Files.First(f => f.Id == file.Id);
                dbFile.ResponseData = file.ResponseData = response;
                db.SaveChanges();
            }
        }

        public static void UpdateMonitorUrl(this File file, string monitorUrl)
        {
            using (var db = new CloudCopyContext())
            {
                var dbFile = db.Files.FirstOrDefault(f => f.Id == file.Id);
                if (dbFile != null)
                {
                    dbFile.MonitorUrl = file.MonitorUrl = monitorUrl;
                    dbFile.State = file.State = FileState.InProgress;
                    db.SaveChanges();
                }
            }
        }

        public static async void SetFailedState(this File file, Exception e)
        {
            using (var db = new CloudCopyContext())
            {
                var dbFile = db.Files.FirstOrDefault(f => f.Id == file.Id);

                // dbFile can be null when cancelling a session
                if (dbFile != null)
                {
                    dbFile.State = file.State = FileState.Failed;
                    dbFile.ResponseData = file.ResponseData = e.Message;
                    await db.SaveChangesAsync();
                }
            }
        }

        public static string LogString(this File file)
        {
            return $"{PathUtils.CombinePath(file.SourcePath, file.FileName)} ({file.Id})";
        }
    }
}
