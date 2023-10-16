using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Noord.Hollands.Archief.Preingest.WebApi.Entities.Output
{
    public class QueryResultState
    {
        public Guid StatusId { get; set; }
        public String Name { get; set; }
        public DateTimeOffset Creation { get; set; }
    }
}
