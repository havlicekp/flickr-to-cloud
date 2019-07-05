using Newtonsoft.Json;

namespace FlickrToCloud.Clouds.OneDrive.Requests
{
    internal class UploadFileRequest : BaseRequest
    {
        [JsonProperty("@microsoft.graph.sourceUrl")]
        public string SourceUrl { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("@name.conflictBehavior")]
        public string ConflictBehavior { get; set; }

        [JsonProperty("file")]
        public object File { get; set; }
    }
}