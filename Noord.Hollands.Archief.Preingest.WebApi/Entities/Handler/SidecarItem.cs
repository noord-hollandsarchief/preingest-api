using System;

namespace Noord.Hollands.Archief.Preingest.WebApi.Entities.Handler
{
    public class SidecarItem
    {
        public String Level { get; set; }
        public bool IsCorrect { get; set; }
        public String TitlePath { get; set; }
        public String[] ErrorMessages { get; set; }
    }
}
