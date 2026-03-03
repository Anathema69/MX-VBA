using Postgrest.Attributes;
using Postgrest.Models;
using System;

namespace SistemaGestionProyectos2.Models.Database
{
    [Table("order_ejecutores")]
    public class OrderEjecutorDb : BaseModel
    {
        [PrimaryKey("id", shouldInsert: false)]
        public int Id { get; set; }

        [Column("f_order")]
        public int OrderId { get; set; }

        [Column("payroll_id")]
        public int PayrollId { get; set; }

        [Column("assigned_at")]
        public DateTime AssignedAt { get; set; }

        [Column("assigned_by")]
        public int? AssignedBy { get; set; }
    }
}
