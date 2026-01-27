using Postgrest.Attributes;
using Postgrest.Models;
using System;

namespace SistemaGestionProyectos2.Models.Database
{
    [Table("t_order")]
    public class OrderDb : BaseModel
    {
        [PrimaryKey("f_order", shouldInsert: false)]
        public int Id { get; set; }
        
        // Este método controla si el Id debe serializarse
        public bool ShouldSerializeId() => Id > 0;

        [Column("f_po")]
        public string Po { get; set; }

        [Column("f_quote")]
        public string Quote { get; set; }

        [Column("f_podate")]
        public DateTime? PoDate { get; set; }

        [Column("f_client")]
        public int? ClientId { get; set; }

        [Column("f_contact")]
        public int? ContactId { get; set; }

        [Column("f_description")]
        public string Description { get; set; }

        [Column("f_salesman")]
        public int? SalesmanId { get; set; }

        [Column("f_estdelivery")]
        public DateTime? EstDelivery { get; set; }

        [Column("f_salesubtotal")]
        public decimal? SaleSubtotal { get; set; }

        [Column("f_saletotal")]
        public decimal? SaleTotal { get; set; }

        [Column("f_expense")]
        public decimal? Expense { get; set; }

        [Column("f_orderstat")]
        public int? OrderStatus { get; set; }

        /// <summary>
        /// Porcentaje de avance del TRABAJO (0-100%). Editable manualmente por el usuario.
        /// NO confundir con OrderPercentage que es el porcentaje de facturación.
        /// </summary>
        [Column("progress_percentage")]
        public int ProgressPercentage { get; set; }

        /// <summary>
        /// Porcentaje de FACTURACIÓN (0-100%). Calculado automáticamente por el trigger
        /// de BD (update_order_status_from_invoices) basado en las facturas.
        /// NO confundir con ProgressPercentage que es el avance del trabajo.
        /// </summary>
        [Column("order_percentage")]
        public int OrderPercentage { get; set; }

        [Column("created_by")]
        public int? CreatedBy { get; set; }

        [Column("updated_by")]
        public int? UpdatedBy { get; set; }

        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        [Column("f_commission_rate")]
        public decimal? CommissionRate { get; set; }

        // Columna v2.0 - Gasto operativo (suma de order_gastos_operativos)
        [Column("gasto_operativo")]
        public decimal? GastoOperativo { get; set; }

        // Columna v2.1 - Gasto indirecto (suma de order_gastos_indirectos)
        [Column("gasto_indirecto")]
        public decimal? GastoIndirecto { get; set; }
    }
}
