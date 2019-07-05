using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace FlickrToCloud.Clouds.OneDrive.Requests
{
    internal class ParentReference
    {
        [JsonProperty("path")]
        public string Path { get; set; }
    }
}
