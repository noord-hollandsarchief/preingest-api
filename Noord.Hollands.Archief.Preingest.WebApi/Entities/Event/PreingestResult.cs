using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Noord.Hollands.Archief.Preingest.WebApi.Entities.Event
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum PreingestActionResults
    {
        None = 0,
        Executing = 1,
        Failed = 2,
        Error = 3,        
        Success = 4
    }
    public class PreingestResult
    {
        public PreingestActionResults ResultValue { get; set; }
    }
}
