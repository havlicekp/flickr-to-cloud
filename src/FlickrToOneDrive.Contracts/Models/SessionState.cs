namespace FlickrToOneDrive.Contracts.Models
{
    public enum SessionState
    {
        Created,
        DestinationFolderSet,
        ReadingSource,
        CreatingFolders,
        Uploading,
        Checking,
        Finished
    }
}
