using Postgrest.Attributes;
using Postgrest.Models;
using System;

namespace SistemaGestionProyectos2.Models.Database
{
    [Table("t_payroll")]
    public class PayrollTable : BaseModel
    {
        [PrimaryKey("f_payroll", shouldInsert: false)]
        public int Id { get; set; }
        
        // Este método controla si el Id debe serializarse
        public bool ShouldSerializeId() => Id > 0;

        [Column("f_employee")]
        public string Employee { get; set; }

        [Column("f_title")]
        public string Title { get; set; }

        [Column("f_hireddate")]
        public DateTime? HiredDate { get; set; }

        [Column("f_range")]
        public string Range { get; set; }

        [Column("f_condition")]
        public string Condition { get; set; }

        [Column("f_lastraise")]
        public DateTime? LastRaise { get; set; }

        [Column("f_sspayroll")]
        public decimal? SSPayroll { get; set; }

        [Column("f_weeklypayroll")]
        public decimal? WeeklyPayroll { get; set; }

        [Column("f_socialsecurity")]
        public decimal? SocialSecurity { get; set; }

        [Column("f_benefits")]
        public string Benefits { get; set; }

        [Column("f_benefitsamount")]
        public decimal? BenefitsAmount { get; set; }

        [Column("f_monthlypayroll")]
        public decimal? MonthlyPayroll { get; set; }

        [Column("employee_code")]
        public string EmployeeCode { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; }

        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        [Column("created_by")]
        public int? CreatedBy { get; set; }

        [Column("updated_by")]
        public int? UpdatedBy { get; set; }
    }

    [Table("t_payroll_history")]
    public class PayrollHistoryTable : BaseModel
    {
        [PrimaryKey("id", shouldInsert: false)]
        public int Id { get; set; }
        
        // Este método controla si el Id debe serializarse
        public bool ShouldSerializeId() => Id > 0;

        [Column("f_payroll")]
        public int PayrollId { get; set; }

        [Column("f_employee")]
        public string Employee { get; set; }

        [Column("f_title")]
        public string Title { get; set; }

        [Column("f_monthlypayroll")]
        public decimal? MonthlyPayroll { get; set; }

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

    [Table("t_payrollovertime")]
    public class PayrollOvertimeTable : BaseModel
    {
        [PrimaryKey("f_payrollovertime", shouldInsert: false)]
        public int Id { get; set; }
        
        // Este método controla si el Id debe serializarse
        public bool ShouldSerializeId() => Id > 0;

        [Column("f_date")]
        public DateTime Date { get; set; }

        [Column("f_payroll")]
        public decimal? Payroll { get; set; }

        [Column("f_overtime")]
        public decimal? Overtime { get; set; }

        [Column("f_fixedexpense")]
        public decimal? FixedExpense { get; set; }

        [Column("created_by")]
        public int? CreatedBy { get; set; }

        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }
    }
}
