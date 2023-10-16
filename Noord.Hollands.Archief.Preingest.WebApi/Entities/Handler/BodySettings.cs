using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Noord.Hollands.Archief.Preingest.WebApi.Entities.Handler
{
    public class BodySettings
    {
        public string ChecksumType { get; set; }
        public string ChecksumValue { get; set; }
        /**
         * (TOPX/MDTO) PrewashHandler: prewash xslt filename
         * */
        public string Prewash { get; set; }
        /**
         * (OPEX) PolishHandler: polish xslt filename
         * */
        public string Polish { get; set; }
        /**          
         * BuildOpexHandler: build option
         * */        
        public string MergeRecordAndFile { get; set; }
        /**
         * IndexMetadataHandler: validation schema, extra xml 
         */
        public string SchemaToValidate { get; set; }
        public string RootNamesExtraXml { get; set; }
        public string IgnoreValidation { get; set; }
    }
}
