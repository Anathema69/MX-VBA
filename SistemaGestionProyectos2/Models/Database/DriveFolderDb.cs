using Postgrest.Attributes;
using Postgrest.Models;
using System;

namespace SistemaGestionProyectos2.Models.Database
{
    [Table("drive_folders")]
    public class DriveFolderDb : BaseModel
    {
        [PrimaryKey("id", shouldInsert: false)]
        public int Id { get; set; }

        public bool ShouldSerializeId() => Id > 0;

        [Column("parent_id")]
        public int? ParentId { get; set; }

        [Column("name")]
        public string Name { get; set; }

        [Column("linked_order_id")]
        public int? LinkedOrderId { get; set; }

        [Column("created_by")]
        public int? CreatedBy { get; set; }

        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }
    }
}
