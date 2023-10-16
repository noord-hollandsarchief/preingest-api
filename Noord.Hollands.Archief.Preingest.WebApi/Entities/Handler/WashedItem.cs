using System;

namespace Noord.Hollands.Archief.Preingest.WebApi.Entities.Handler
{
    public class WashedItem
    {
        public bool IsWashed { get; set; }
        public String RequestUri { get; set; }
        public String MetadataFilename { get; set; }
        public String[] ErrorMessage { get; set; }
    }
}
