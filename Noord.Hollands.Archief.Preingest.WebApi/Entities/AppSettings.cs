using System;

namespace Noord.Hollands.Archief.Preingest.WebApi.Entities
{
    /// <summary>
    /// Preingest settings
    /// </summary>
    public class AppSettings
    {
        public String WithOrigins { get; set; }
        public String DataFolderName { get; set; }
        public String ClamServerNameOrIp { get; set; }
        public String ClamServerPort { get; set; }
        public String XslWebServerName { get; set; }
        public String XslWebServerPort { get; set; }
        public String DroidServerName { get; set; }
        public String DroidServerPort { get; set; }
        public String PreWashFolder { set; get; }
        public String UtilitiesServerName { get; set; }
        public String UtilitiesServerPort { get; set; }
    }                 
}
