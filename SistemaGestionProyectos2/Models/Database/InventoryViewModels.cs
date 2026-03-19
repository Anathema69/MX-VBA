using Postgrest.Attributes;
using Postgrest.Models;

namespace SistemaGestionProyectos2.Models.Database
{
    /// <summary>
    /// Modelo read-only para la vista v_inventory_category_summary.
    /// Postgrest expone vistas PostgreSQL como tablas.
    /// </summary>
    [Table("v_inventory_category_summary")]
    public class CategorySummaryView : BaseModel
    {
        [PrimaryKey("id", shouldInsert: false)]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; }

        [Column("description")]
        public string Description { get; set; }

        [Column("color")]
        public string Color { get; set; }

        [Column("icon")]
        public string Icon { get; set; }

        [Column("display_order")]
        public int DisplayOrder { get; set; }

        [Column("total_products")]
        public int TotalProducts { get; set; }

        [Column("total_stock")]
        public decimal TotalStock { get; set; }

        [Column("low_stock_count")]
        public int LowStockCount { get; set; }

        [Column("total_value")]
        public decimal TotalValue { get; set; }

        [Column("health_percent")]
        public int HealthPercent { get; set; }
    }
}
