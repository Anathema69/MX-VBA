using Postgrest.Attributes;
using Postgrest.Models;
using System;
using Newtonsoft.Json;

namespace SistemaGestionProyectos2.Models.Database
{
    [Table("t_client")]
    public class ClientDb : BaseModel
    {
        [PrimaryKey("f_client", shouldInsert: false)]
        [Column("f_client")]
        public int Id { get; set; }

        // Este método controla si el Id debe serializarse
        public bool ShouldSerializeId() => Id > 0;

        [Column("f_name")]
        public string Name { get; set; }

        [Column("f_address1")]
        public string Address1 { get; set; }

        [Column("f_address2")]
        public string Address2 { get; set; }

        [Column("f_credit")]
        public int Credit { get; set; }

        [Column("tax_id")]
        public string TaxId { get; set; }

        [Column("phone")]
        public string Phone { get; set; }

        [Column("email")]
        public string Email { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; }

        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        [Column("created_by")]
        public int? CreatedBy { get; set; }

        [Column("updated_by")]
        public int? UpdatedBy { get; set; }
    }
}
