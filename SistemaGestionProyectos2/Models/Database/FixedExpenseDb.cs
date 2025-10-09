using Postgrest.Attributes;
using Postgrest.Models;
using System;

namespace SistemaGestionProyectos2.Models.Database
{
    [Table("t_fixed_expenses")]
    public class FixedExpenseTable : BaseModel
    {
        [PrimaryKey("id", shouldInsert: false)]
        public int Id { get; set; }
        
        // Este método controla si el Id debe serializarse
        public bool ShouldSerializeId() => Id > 0;

        [Column("expense_type")]
        public string ExpenseType { get; set; }

        [Column("description")]
        public string Description { get; set; }

        [Column("monthly_amount")]
        public decimal? MonthlyAmount { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; }

        [Column("created_by")]
        public int? CreatedBy { get; set; }

        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        [Column("effective_date")]
        public DateTime? EffectiveDate { get; set; }
    }

    [Table("t_fixed_expenses_history")]
    public class FixedExpenseHistoryTable : BaseModel
    {
        [PrimaryKey("id", shouldInsert: false)]
        public int Id { get; set; }
        
        // Este método controla si el Id debe serializarse
        public bool ShouldSerializeId() => Id > 0;

        [Column("expense_id")]
        public int ExpenseId { get; set; }

        [Column("description")]
        public string Description { get; set; }

        [Column("monthly_amount")]
        public decimal? MonthlyAmount { get; set; }

        [Column("effective_date")]
        public DateTime EffectiveDate { get; set; }

        [Column("change_type")]
        public string ChangeType { get; set; }

        [Column("change_summary")]
        public string ChangeSummary { get; set; }

        [Column("created_by")]
        public int? CreatedBy { get; set; }

        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }
    }
}
