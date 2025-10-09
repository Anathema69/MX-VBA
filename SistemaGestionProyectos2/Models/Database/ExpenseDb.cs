using Postgrest.Attributes;
using Postgrest.Models;
using System;

namespace SistemaGestionProyectos2.Models.Database
{
    [Table("t_expense")]
    public class ExpenseDb : BaseModel
    {
        [PrimaryKey("f_expense")]
        public int Id { get; set; }

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

        [Column("f_paymethod")]
        public string PayMethod { get; set; }

        [Column("f_order")]
        public int? OrderId { get; set; }

        [Column("expense_category")]
        public string ExpenseCategory { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }

        [Column("created_by")]
        public string CreatedBy { get; set; }
    }
}
