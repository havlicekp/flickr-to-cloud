namespace FlickrToOneDrive.Contracts.Models
{
    public class File
    {
        public int FileId { get; set; }
        public Session Session { get; set; }
        public int SessionId { get; set; }
        public string SourceUrl { get; set; }
        public string FileName { get; set; }
        public string UploadStatusData { get; set; }
        public UploadStatus UploadStatus { get; set; }    
    }
}
