using Postgrest.Attributes;
using Postgrest.Models;
using System;

namespace SistemaGestionProyectos2.Models.Database
{
    [Table("inventory_movements")]
    public class InventoryMovementDb : BaseModel
    {
        [PrimaryKey("id", shouldInsert: false)]
        public int Id { get; set; }

        public bool ShouldSerializeId() => Id > 0;

        [Column("product_id")]
        public int ProductId { get; set; }

        [Column("movement_type")]
        public string MovementType { get; set; }

        [Column("quantity")]
        public decimal Quantity { get; set; }

        [Column("previous_stock")]
        public decimal? PreviousStock { get; set; }

        public bool ShouldSerializePreviousStock() => PreviousStock.HasValue;

        [Column("new_stock")]
        public decimal? NewStock { get; set; }

        public bool ShouldSerializeNewStock() => NewStock.HasValue;

        [Column("reference_type")]
        public string ReferenceType { get; set; }

        public bool ShouldSerializeReferenceType() => !string.IsNullOrEmpty(ReferenceType);

        [Column("reference_id")]
        public int? ReferenceId { get; set; }

        public bool ShouldSerializeReferenceId() => ReferenceId.HasValue;

        [Column("notes")]
        public string Notes { get; set; }

        public bool ShouldSerializeNotes() => !string.IsNullOrEmpty(Notes);

        [Column("created_by")]
        public int? CreatedBy { get; set; }

        public bool ShouldSerializeCreatedBy() => CreatedBy.HasValue && CreatedBy.Value > 0;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        public bool ShouldSerializeCreatedAt() => false;
    }
}
