namespace FlickrToCloud.Contracts.Interfaces
{
    public interface IConfiguration
    {
        string this[string key] { get; }
    }
}
