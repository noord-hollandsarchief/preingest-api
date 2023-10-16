using System;

namespace Noord.Hollands.Archief.Preingest.WebApi.Entities.Handler
{
    public class NamingItem
    {
        public bool IsSuccess
        {
            get
            {
                return (ContainsDosNames == false && ContainsInvalidCharacters == false && Length < 255);
            }
        }
        public bool ContainsInvalidCharacters { get; set; }
        public bool ContainsDosNames { get; set; }
        public int Length { get; set; }
        public String Name { get; set; }
        public String[] ErrorMessages { get; set; }
    }
}
