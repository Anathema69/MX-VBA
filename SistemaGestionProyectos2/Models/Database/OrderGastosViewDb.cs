using Postgrest.Attributes;
using Postgrest.Models;
using System;

namespace SistemaGestionProyectos2.Models.Database
{
    /// <summary>
    /// Modelo para la vista v_order_gastos que incluye campos calculados de gastos a proveedores
    /// </summary>
    [Table("v_order_gastos")]
    public class OrderGastosViewDb : BaseModel
    {
        [PrimaryKey("f_order", shouldInsert: false)]
        public int Id { get; set; }

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

        [Column("f_orderstat")]
        public int? OrderStatus { get; set; }

        [Column("progress_percentage")]
        public int ProgressPercentage { get; set; }

        [Column("order_percentage")]
        public int OrderPercentage { get; set; }

        [Column("f_commission_rate")]
        public decimal? CommissionRate { get; set; }

        [Column("created_by")]
        public int? CreatedBy { get; set; }

        [Column("updated_by")]
        public int? UpdatedBy { get; set; }

        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        // Campos calculados desde la vista (gastos a proveedores)
        [Column("gasto_material")]
        public decimal GastoMaterial { get; set; }

        [Column("gasto_material_pendiente")]
        public decimal GastoMaterialPendiente { get; set; }

        [Column("total_gastos_proveedor")]
        public decimal TotalGastosProveedor { get; set; }

        [Column("num_facturas_proveedor")]
        public int NumFacturasProveedor { get; set; }

        // Campo de la tabla t_order (suma de order_gastos_operativos)
        [Column("gasto_operativo")]
        public decimal GastoOperativo { get; set; }
    }
}
