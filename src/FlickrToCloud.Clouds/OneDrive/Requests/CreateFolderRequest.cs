using Newtonsoft.Json;

namespace FlickrToCloud.Clouds.OneDrive.Requests
{
    internal class CreateFolderRequest : BaseRequest
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("folder")]
        public object Folder { get; set; }

        [JsonProperty("@microsoft.graph.conflictBehavior")]
        public string ConflictBehavior { get; set; }
    }
}
