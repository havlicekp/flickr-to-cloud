using System;
using System.Collections.Generic;

namespace FlickrToOneDrive.Contracts.Models
{
    public class Session
    {
        public int SessionId { get; set; }
        public string DestinationFolder { get; set; }
        public DateTime Started { get; set; }
        public bool Finished { get; set; }
        public ICollection<File> Files { get; set; }
        public override string ToString()
        {
            return $"No: {SessionId}, Started {Started.ToString("g")}";
        }
    }
}
