using System;

namespace Noord.Hollands.Archief.Preingest.WebApi.Entities.Handler
{
    [Serializable]
    public class GreenListItem
    {
        /* See file: voorkeursformaten.json
          {
            "type": "Audio",
            "extension": "WAVE",
            "version": "",
            "description": "Waveform Audio",
            "puid": "fmt/141",
            "categoryFormat": "Voorkeur",
            "visibility": "",
            "visibilityAfterConversion": "X",
            "onlyViewableExternTool": ""
          },
         */
        public String Type { get; set; }
        public String Extension { get; set; }
        public String Version { get; set; }
        public String Description { get; set; }
        public String Puid { get; set; }
        public String CategoryFormat { get; set; }
        public String Visibility { get; set; }
        public String VisibilityAfterConversion { get; set; }
        public String OnlyViewableExternTool { get; set; }
    }
}
