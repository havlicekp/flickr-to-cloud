namespace FlickrToCloud.Contracts.Interfaces
{
    public interface ICloudFileSystemFactory
    {
        ICloudFileSystem Create(string cloudId);
    }
}
