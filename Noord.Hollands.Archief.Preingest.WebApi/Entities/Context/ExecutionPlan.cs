using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Noord.Hollands.Archief.Preingest.WebApi.Entities.Context
{
    [Table("Plan")]
    public class ExecutionPlan
    {
        [Key]        
        [Column("Id")]
        public Int32 RecordId { get; set; }
        [Column("SessionId")]
        public Guid SessionId { get; set; }
        [Column("ActionName")]
        public String ActionName { get; set; }
        [Column("ContinueOnError")]
        public bool ContinueOnError { get; set; }
        [Column("ContinueOnFailed")]
        public bool ContinueOnFailed { get; set; }
        [Column("StartOnError")]
        public bool StartOnError { get; set; }
        [Column("Sequence")]
        public Int32 Sequence { get; set; }        
    }    
}
