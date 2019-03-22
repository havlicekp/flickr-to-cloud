namespace FlickrToOneDrive.Contracts.Interfaces
{
    public interface ICloudFileSystemFactory
    {
        ICloudFileSystem Create(string cloudId);
    }
}
