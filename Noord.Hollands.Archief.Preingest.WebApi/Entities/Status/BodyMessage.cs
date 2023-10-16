using System;
using Newtonsoft.Json;

namespace Noord.Hollands.Archief.Preingest.WebApi.Entities.Status
{
    public class BodyMessage
    {
        [JsonProperty(Required = Required.Always)]
        public String Message { get; set; }
    }
}
