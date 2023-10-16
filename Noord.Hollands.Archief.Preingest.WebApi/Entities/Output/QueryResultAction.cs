using System;

using Noord.Hollands.Archief.Preingest.WebApi.Entities.Event;

namespace Noord.Hollands.Archief.Preingest.WebApi.Entities.Output
{
    public class QueryResultAction
    {
        public String ActionStatus { get; set; }
        public DateTimeOffset Creation { get; set; }
        public String Description { get; set; }
        public Guid FolderSessionId { get; set; }
        public String Name { get; set; }
        public Guid ProcessId { get; set; }
        public String[] ResultFiles { get; set; }
        public PreingestStatisticsSummary Summary { get; set; }
        public QueryResultState[] States { get; set; }
    }
}
