using System;
using Noord.Hollands.Archief.Preingest.WebApi.Entities.Structure;

namespace Noord.Hollands.Archief.Preingest.WebApi.Entities.Event
{
    public class PreingestEventArgs : EventArgs
    {
        public String Description { get; set; }
        public PreingestActionStates ActionType { get; set; }
        public DateTimeOffset Initiate { get; set; }
        public PreingestActionModel PreingestAction { get; set; }
        public System.Collections.Generic.List<ISidecar> SidecarStructure { get; set; }
    }
}
