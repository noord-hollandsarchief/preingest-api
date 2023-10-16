using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Noord.Hollands.Archief.Preingest.WebApi.Entities.Opex
{
    public enum InheritanceMethod
    {
        None,
        Combine        
    }
    public class Inheritance
    {
        public InheritanceMethod MethodResult { get; set; }

    }
}
