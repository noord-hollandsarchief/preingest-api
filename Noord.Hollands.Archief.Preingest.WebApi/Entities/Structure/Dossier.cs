using Noord.Hollands.Archief.Preingest.WebApi.Entities.ToPX.v2_3_2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Noord.Hollands.Archief.Preingest.WebApi.Entities.Structure
{
    [Serializable()]
    public class Dossier : Sidecar
    {
        public Dossier(String name, String path, Archief parent) : base(name, path, parent) { }
        public Dossier(String name, String path, Series parent) : base(name, path, parent) { }

        public override bool CompareAggregationLevel
        {
            get
            {
                bool result = false;

                if (!this.HasMetadata)
                    return result;

                var aggregatieTypeObject = (this.Metadata.Item as aggregatieType);
                if (aggregatieTypeObject == null)
                    return result;

                if (aggregatieTypeObject.aggregatieniveau == null)
                    return result;

                result = (aggregatieTypeObject.aggregatieniveau.Value == aggregatieAggregatieniveauType.Dossier);

                return result;
            }
        }

        public override bool HasIdentification
        {
            get
            {
                return base.HasIdentification;
            }
        }

        public override bool HasName
        {
            get
            {
                return base.HasName;
            }
        }
        
        public bool IsParentEmpty { get => Parent == null; }

        public override string ToString()
        {
            return String.Format("{0} (Dossier)", this.Name);
        }

        public override void Validate()
        {
            if (!this.HasMetadata)
                LoadMetadata();

            if (!CompareAggregationLevel)
                this.ObjectExceptions().Add(new SidecarException("Aggregatie niveau onjuist."));

            if (!HasIdentification)
                this.ObjectExceptions().Add(new SidecarException("Geen identificatiekenmerk gevonden."));

            if (!HasName)
                this.ObjectExceptions().Add(new SidecarException("Geen naam gevonden."));

            base.Dispose();
        }
    }
}
