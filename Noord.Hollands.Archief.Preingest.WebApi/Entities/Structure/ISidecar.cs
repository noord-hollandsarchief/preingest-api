using System;
using System.Collections.Generic;
using Noord.Hollands.Archief.Preingest.WebApi.Entities.Handler;

namespace Noord.Hollands.Archief.Preingest.WebApi.Entities.Structure
{
    public interface ISidecar
    {
        Guid InternalId { get; }
        String Name { get; }
        String TitlePath { get; }
        ToPX.v2_3_2.topxType Metadata { get; set; }
        PronomItem PronomMetadataInfo { get; set; }
        String MetadataEncoding { get; set; }
        bool HasMetadata { get; }
        void PrepareMetadata(bool validateMetadata = false);
        void Validate();
        String MetadataFileLocation { get; set; }
        ISidecar Parent { get; set; }
        Boolean CompareAggregationLevel { get; }
        Boolean HasIdentification { get; }
        Boolean HasName { get; }
        List<SidecarException> ObjectExceptions();
    }
}
