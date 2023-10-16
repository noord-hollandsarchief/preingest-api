using System;

namespace Noord.Hollands.Archief.Preingest.WebApi.Entities.Handler
{
    public class TransformationItem
    {
        public bool IsTranformed { get; set; }
        public String RequestUri { get; set; }
        public String MetadataFilename { get; set; }
        public String[] ErrorMessage { get; set; }
    }
}
