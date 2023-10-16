using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Noord.Hollands.Archief.Preingest.WebApi.Entities.Event
{ 

    [JsonConverter(typeof(StringEnumConverter))]
    public enum PreingestActionStates
    {
        None,        
        Started,
        Executing,
        Completed,
        Failed
    }
    public class PreingestActionModel
    {
        public PreingestStatisticsSummary Summary { get; set; }
        public PreingestResult ActionResult { get; set; }
        public PreingestProperties Properties { get; set; }
        public object ActionData { get; set; }
    }


}
