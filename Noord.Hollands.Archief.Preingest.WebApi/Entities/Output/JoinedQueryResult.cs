using Noord.Hollands.Archief.Preingest.WebApi.Entities.Context;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Noord.Hollands.Archief.Preingest.WebApi.Entities.Output
{
    public class JoinedQueryResult
    {
        public PreingestAction Actions { get; set; }
        public ActionStates States { get; set; }
    }
}
