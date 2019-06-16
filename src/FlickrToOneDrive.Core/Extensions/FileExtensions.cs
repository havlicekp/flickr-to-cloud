using System.Linq;
using FlickrToOneDrive.Contracts;
using FlickrToOneDrive.Contracts.Models;

namespace FlickrToOneDrive.Core.Extensions
{
    public static class FileExtensions
    {
        public static void UpdateState(this File file, FileState state)
        {
            using (var db = new CloudCopyContext())
            {
                var dbFile = db.Files.First(f => f.Id == file.Id);
                dbFile.State = state;
                db.SaveChanges();
            }
        }

        public static void UpdateMonitorUrl(this File file, string monitorUrl)
        {
            using (var db = new CloudCopyContext())
            {
                var dbFile = db.Files.First(f => f.Id == file.Id);
                dbFile.MonitorUrl = monitorUrl;
                dbFile.State = FileState.InProgress;
                db.SaveChanges();
            }
        }
    }
}
