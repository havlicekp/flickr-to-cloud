using System;
using System.Collections.Generic;

namespace FlickrToOneDrive.Contracts.Models
{
    public class Session : BaseEntity
    {
        public string DestinationCloud { get; set; }
        public string SourceCloud { get; set; }
        public string DestinationFolder { get; set; }
        public DateTime Started { get; set; }
        public SessionMode Mode { get; set; }
        public SessionFilesOrigin FilesOrigin { get; set; }
        public SessionState State { get; set; }
        public ICollection<File> Files { get; set; }
        public override string ToString()
        {
            return $"No: {Id}, Started {Started.ToString("g")}";
        }
    }
}
