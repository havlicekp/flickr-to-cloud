namespace FlickrToCloud.Contracts.Interfaces
{
    public interface IUploaderFactory
    {
        IUploader Create(Setup setup);
    }
}