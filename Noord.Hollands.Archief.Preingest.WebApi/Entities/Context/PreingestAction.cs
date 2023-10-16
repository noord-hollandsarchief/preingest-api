using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Noord.Hollands.Archief.Preingest.WebApi.Entities.Context
{
    [Table("Actions")]
    public class PreingestAction
    {
        [Key]
        [Column("ProcessId")]
        public Guid ProcessId { get; set; }

        [Column("FolderSessionId")]
        public Guid FolderSessionId { get; set; }

        [Column("Name")]
        public String Name { get; set; }

        [Column("Description")]
        public String Description { get; set; }

        [Column("Creation")]
        public DateTimeOffset Creation { get; set; }

        [Column("ResultFiles")]
        public String ResultFiles { get; set; }

        [Column("ActionStatus")]
        public String ActionStatus { get; set; }

        [Column("StatisticsSummary")]
        public String StatisticsSummary { get; set; }

        [ForeignKey("ProcessId")]
        public ICollection<ActionStates> Status { get; set; }
    }
}
