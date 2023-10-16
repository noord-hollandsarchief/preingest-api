using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Noord.Hollands.Archief.Preingest.WebApi.Entities.Context
{
    [Table("Messages")]
    public class StateMessage
    {
        [Key]
        [Column("MessageId")]
        public Guid MessageId { get; set; }

        public Guid StatusId { get; set; }
        public ActionStates Status { get; set; }

        [Column("Creation")]
        public DateTimeOffset Creation { get; set; }

        [Column("Description")]
        public String Description { get; set; }
    }
}
