using System;

namespace Noord.Hollands.Archief.Preingest.WebApi.Entities.Event
{
    public class PreingestProperties
    {
        public Guid SessionId { get; set; }
        public String ActionName { get; set; }
        public String CollectionItem { get; set; }
        public String[] Messages { get; set; }
        public DateTimeOffset CreationTimestamp { get; set; }
    }
}
