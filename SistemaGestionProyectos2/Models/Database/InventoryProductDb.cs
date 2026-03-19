using Postgrest.Attributes;
using Postgrest.Models;
using System;

namespace SistemaGestionProyectos2.Models.Database
{
    [Table("inventory_products")]
    public class InventoryProductDb : BaseModel
    {
        [PrimaryKey("id", shouldInsert: false)]
        public int Id { get; set; }

        public bool ShouldSerializeId() => Id > 0;

        [Column("category_id")]
        public int CategoryId { get; set; }

        [Column("code")]
        public string Code { get; set; }

        [Column("name")]
        public string Name { get; set; }

        [Column("description")]
        public string Description { get; set; }

        public bool ShouldSerializeDescription() => !string.IsNullOrEmpty(Description);

        [Column("stock_current")]
        public decimal StockCurrent { get; set; }

        [Column("stock_minimum")]
        public decimal StockMinimum { get; set; }

        [Column("unit")]
        public string Unit { get; set; } = "pza";

        [Column("unit_price")]
        public decimal UnitPrice { get; set; }

        [Column("location")]
        public string Location { get; set; }

        public bool ShouldSerializeLocation() => !string.IsNullOrEmpty(Location);

        [Column("supplier_id")]
        public int? SupplierId { get; set; }

        public bool ShouldSerializeSupplierId() => SupplierId.HasValue;

        [Column("notes")]
        public string Notes { get; set; }

        public bool ShouldSerializeNotes() => !string.IsNullOrEmpty(Notes);

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
