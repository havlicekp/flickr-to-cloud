using Newtonsoft.Json;

namespace FlickrToCloud.Clouds.OneDrive.Requests
{
    internal class BaseRequest
    {
        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}