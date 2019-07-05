using Newtonsoft.Json;

namespace FlickrToCloud.Clouds.OneDrive.Requests
{
    internal class CopyFileRequest : BaseRequest
    {
        [JsonProperty("parentReference")]
        public ParentReference ParentReference { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }
    }
}