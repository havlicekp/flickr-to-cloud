using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage;
using FlickrToOneDrive.Contracts.Interfaces;
using Newtonsoft.Json.Linq;
using Serilog;

namespace FlickrToOneDrive
{
    public class Configuration : IConfiguration
    {
        private static readonly Uri ConfigFileUri = new Uri("ms-appx:///Configuration.json");

        private readonly ILogger _log;        
        private Dictionary<string, string> _config;

        public Configuration(ILogger log)
        {
            _log = log;
            _config = new Dictionary<string, string>();
            Init();
        }

        private void Init()
        {
            var configFile = StorageFile.GetFileFromApplicationUriAsync(ConfigFileUri).GetAwaiter().GetResult();
            var contents = FileIO.ReadTextAsync(configFile).GetAwaiter().GetResult();
            var json = JObject.Parse(contents);
            foreach (var child in json)
            {
                foreach (var subChild in json[child.Key])
                {
                    _config.Add(subChild.Path, subChild.First.ToString());
                }
            }
        }

        public string this[string key] => _config[key];
    }    
}
