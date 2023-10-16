using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

using Newtonsoft.Json;

namespace Noord.Hollands.Archief.Preingest.WebApi.Entities.Status
{
    public class BodyEventMessageBody
    {
        public DateTimeOffset EventDateTime { get; set; }

        [JsonProperty(Required = Required.Always)]
        public Guid SessionId { get; set; }
        [JsonProperty(Required = Required.Always)]
        public String Name { get; set; }
        [JsonProperty(Required = Required.Always)]
        public String State { get; set; }
        [JsonProperty(Required = Required.Always)]
        public String Message { get; set; }
        [JsonProperty(Required = Required.Always)]
        public bool HasSummary { get; set; }
        public Int32 Processed { get; set; }
        public Int32 Accepted { get; set; }
        public Int32 Rejected { get; set; }
        public DateTimeOffset? Start { get; set; }
        public DateTimeOffset? End { get; set; }
    }
}
