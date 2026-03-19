using Postgrest.Attributes;
using Postgrest.Models;
using System;

namespace SistemaGestionProyectos2.Models.Database
{
    [Table("inventory_categories")]
    public class InventoryCategoryDb : BaseModel
    {
        [PrimaryKey("id", shouldInsert: false)]
        public int Id { get; set; }

        public bool ShouldSerializeId() => Id > 0;

        [Column("name")]
        public string Name { get; set; }

        [Column("description")]
        public string Description { get; set; }

        [Column("color")]
        public string Color { get; set; } = "#3498DB";

        [Column("icon")]
        public string Icon { get; set; }

        public bool ShouldSerializeIcon() => !string.IsNullOrEmpty(Icon);

        [Column("display_order")]
        public int DisplayOrder { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_by")]
        public int? CreatedBy { get; set; }

        public bool ShouldSerializeCreatedBy() => CreatedBy.HasValue && CreatedBy.Value > 0;

        [Column("updated_by")]
        public int? UpdatedBy { get; set; }

        public bool ShouldSerializeUpdatedBy() => UpdatedBy.HasValue && UpdatedBy.Value > 0;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        public bool ShouldSerializeCreatedAt() => false;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }

        public bool ShouldSerializeUpdatedAt() => false;
    }
}
