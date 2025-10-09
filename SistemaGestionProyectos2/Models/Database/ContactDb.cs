using Postgrest.Attributes;
using Postgrest.Models;

namespace SistemaGestionProyectos2.Models.Database
{
    [Table("t_contact")]
    public class ContactDb : BaseModel
    {
        [PrimaryKey("f_contact", shouldInsert: false)]
        public int Id { get; set; }

        // Este método controla si el Id debe serializarse
        public bool ShouldSerializeId() => Id > 0;

        [Column("f_client")]
        public int ClientId { get; set; }

        [Column("f_contactname")]
        public string ContactName { get; set; }

        [Column("f_email")]
        public string Email { get; set; }

        [Column("f_phone")]
        public string Phone { get; set; }

        [Column("position")]
        public string Position { get; set; }

        [Column("is_primary")]
        public bool IsPrimary { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; }
    }
}
