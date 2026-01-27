using Postgrest.Attributes;
using Postgrest.Models;
using System;

namespace SistemaGestionProyectos2.Models.Database
{
    [Table("t_expense")]
    public class ExpenseDb : BaseModel
    {
        [PrimaryKey("f_expense", shouldInsert: false)]
        public int Id { get; set; }

        // Controla si el Id debe serializarse
        public bool ShouldSerializeId() => Id > 0;

        [Column("f_supplier")]
        public int SupplierId { get; set; }

        [Column("f_description")]
        public string Description { get; set; }

        [Column("f_expensedate")]
        public DateTime ExpenseDate { get; set; }

        [Column("f_totalexpense")]
        public decimal TotalExpense { get; set; }

        [Column("f_scheduleddate")]
        public DateTime? ScheduledDate { get; set; }

        [Column("f_status")]
        public string Status { get; set; }

        [Column("f_paiddate")]
        public DateTime? PaidDate { get; set; }

        // Solo serializar si tiene valor
        public bool ShouldSerializePaidDate() => PaidDate.HasValue;

        [Column("f_paymethod")]
        public string PayMethod { get; set; }

        [Column("f_order")]
        public int? OrderId { get; set; }

        // Solo serializar si tiene valor
        public bool ShouldSerializeOrderId() => OrderId.HasValue;

        [Column("expense_category")]
        public string ExpenseCategory { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        // No enviar en INSERT/UPDATE - la BD lo maneja
        public bool ShouldSerializeCreatedAt() => false;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }

        // No enviar en INSERT - la BD lo maneja con trigger
        public bool ShouldSerializeUpdatedAt() => false;

        [Column("created_by")]
        public int? CreatedBy { get; set; }

        // Solo serializar si tiene valor
        public bool ShouldSerializeCreatedBy() => CreatedBy.HasValue && CreatedBy.Value > 0;

        [Column("updated_by")]
        public string UpdatedBy { get; set; }

        // Solo serializar si tiene valor (para UPDATE, no INSERT)
        public bool ShouldSerializeUpdatedBy() => !string.IsNullOrEmpty(UpdatedBy);
    }
}
