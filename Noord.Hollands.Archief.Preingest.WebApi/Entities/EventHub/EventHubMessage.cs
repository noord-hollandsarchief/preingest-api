using System;
using Noord.Hollands.Archief.Preingest.WebApi.Entities.Event;

namespace Noord.Hollands.Archief.Preingest.WebApi.Entities.EventHub
{
    public class EventHubMessage
    {
        public DateTimeOffset EventDateTime { get; set; }
        public Guid SessionId { get; set; }
        public String Name { get; set; }
        public PreingestActionStates State { get; set; }
        public String Message { get; set; }
        public PreingestStatisticsSummary Summary { get; set; }
    }
}
