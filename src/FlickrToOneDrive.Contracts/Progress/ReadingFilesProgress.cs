using FlickrToOneDrive.Contracts.Models;

namespace FlickrToOneDrive.Contracts.Progress
{
    public class ReadingFilesProgress : ProgressBase
    {
        public SessionFilesOrigin Origin { get; set; }
    }
}
