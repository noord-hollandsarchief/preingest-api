using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Noord.Hollands.Archief.Preingest.WebApi.Entities.Opex
{
    public enum AlgorithmTypes
    {        
        SHA1,
        MD5,
        SHA256,
        SHA512,
        SHA224,
        SHA384
    }

    public enum ExecutionMode
    {
        CalculateAndCompare,
        OnlyCalculate
    }

    public class Algorithm
    {
        public AlgorithmTypes ChecksumType { get; set; }
        public ExecutionMode ProcessingMode {get;set;}
    }
}