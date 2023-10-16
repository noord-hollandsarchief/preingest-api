using System;
using Newtonsoft.Json;

namespace Noord.Hollands.Archief.Preingest.WebApi.Entities.Status
{
    public class BodyNewAction
    {
        [JsonProperty(Required = Required.Always)]
        public String Name { get; set; }
        [JsonProperty(Required = Required.Always)]
        public String Description { get; set; }
        [JsonProperty(Required = Required.Always)]
        public String Result { get; set; }
    }
}
