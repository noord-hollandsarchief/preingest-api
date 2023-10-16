using Noord.Hollands.Archief.Preingest.WebApi.Entities.ToPX;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Noord.Hollands.Archief.Preingest.WebApi.Entities.Structure
{
    [Serializable()]
    public class NotDefined : Sidecar
    {
        public NotDefined(String name, String path, ISidecar parent) : base(name, path, parent){ }

        public override bool CompareAggregationLevel => throw new NotImplementedException();

        public override string ToString()
        {
            return String.Format("Zwevend object: {0} (Onbekende aggregatie niveau: {1})", this.Name, this.TitlePath);
        }

        public override void Validate()
        {
            this.ObjectExceptions().Add(new SidecarException(this.ToString()));
        }
    }
}
