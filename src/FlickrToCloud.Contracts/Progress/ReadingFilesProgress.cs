using FlickrToCloud.Contracts.Models;

namespace FlickrToCloud.Contracts.Progress
{
    public class ReadingFilesProgress : ProgressBase
    {
        public SessionFilesOrigin Origin { get; set; }
    }
}
