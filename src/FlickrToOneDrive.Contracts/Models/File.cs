using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace FlickrToOneDrive.Contracts.Models
{
    public class File : BaseEntity
    {
        public Session Session { get; set; }
        public int SessionId { get; set; }
        public string SourceId { get; set; }
        public string SourceUrl { get; set; }
        public string SourcePath { get; set; }
        public string FileName { get; set; }
        public string MonitorUrl { get; set; }
        public string ResponseData { get; set; }
        public FileState State { get; set; }

        [NotMapped]
        public Exception UploadException { get; set; }
    }
}
