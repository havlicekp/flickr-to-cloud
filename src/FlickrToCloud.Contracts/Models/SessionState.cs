namespace FlickrToCloud.Contracts.Models
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
