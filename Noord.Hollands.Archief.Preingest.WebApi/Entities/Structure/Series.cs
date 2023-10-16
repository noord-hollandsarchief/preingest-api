using Noord.Hollands.Archief.Preingest.WebApi.Entities.ToPX.v2_3_2;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Noord.Hollands.Archief.Preingest.WebApi.Entities.Structure
{
    [Serializable()]
    public class Series : Sidecar
    {
        public Series(String name, String path, Archief parent) : base(name, path, parent) { }
        public Series(String name, String path, Series parent) : base(name, path, parent) { }

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

                result = (aggregatieTypeObject.aggregatieniveau.Value == aggregatieAggregatieniveauType.Serie);

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
            return String.Format("{0} (Serie)", this.Name);
        }

        public static explicit operator Dossier(Series serie)
        {
            dynamic parent = null;
            if (serie.Parent is Archief)
                parent = (serie.Parent as Archief);
            if(serie.Parent is Series)
                parent = (serie.Parent as Series);

            Dossier dossier = new Dossier(serie.Name, serie.TitlePath, parent);
            dossier.MetadataFileLocation = serie.MetadataFileLocation;
            dossier.PrepareMetadata();
            
            return dossier;
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
