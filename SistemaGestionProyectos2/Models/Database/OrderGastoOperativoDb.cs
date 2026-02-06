using Postgrest.Attributes;
using Postgrest.Models;
using System;

namespace SistemaGestionProyectos2.Models.Database
{
    /// <summary>
    /// Modelo para la tabla order_gastos_operativos
    /// Registra el detalle de gastos operativos por orden
    /// </summary>
    [Table("order_gastos_operativos")]
    public class OrderGastoOperativoDb : BaseModel
    {
        [PrimaryKey("id", shouldInsert: false)]
        public int Id { get; set; }

        [Column("f_order")]
        public int OrderId { get; set; }

        [Column("monto")]
        public decimal Monto { get; set; }

        [Column("f_commission_rate")]
        public decimal CommissionRate { get; set; }

        [Column("descripcion")]
        public string Descripcion { get; set; }

        [Column("categoria")]
        public string Categoria { get; set; }

        [Column("fecha_gasto")]
        public DateTime? FechaGasto { get; set; }

        [Column("created_by")]
        public int? CreatedBy { get; set; }

        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }

        [Column("updated_by")]
        public int? UpdatedBy { get; set; }

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }
    }
}
