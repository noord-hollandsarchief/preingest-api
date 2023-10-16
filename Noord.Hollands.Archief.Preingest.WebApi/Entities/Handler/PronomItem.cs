using System;

namespace Noord.Hollands.Archief.Preingest.WebApi.Entities.Handler
{
    [Serializable()]
    public class PronomItem
    {
        public String Id { get; set; }
        public String ParentId { get; set; }
        public String Uri { get; set; }
        public String FilePath { get; set; }
        public String Name { get; set; }
        public String Method { get; set; }
        public String Status { get; set; }
        public String Size { get; set; }
        public String Type { get; set; }
        public String Ext { get; set; }
        public String LastModified { get; set; }
        public String ExtensionMisMatch { get; set; }
        public String Hash { get; set; }
        public String FormatCount { get; set; }
        public String Puid { get; set; }
        public String MimeType { get; set; }
        public String FormatName { get; set; }
        public String FormatVersion { get; set; }
    }

}
