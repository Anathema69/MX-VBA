using Postgrest.Attributes;
using Postgrest.Models;
using System;

namespace SistemaGestionProyectos2.Models.Database
{
    /// <summary>
    /// Modelo para la tabla t_holiday - Días feriados
    /// </summary>
    [Table("t_holiday")]
    public class HolidayTable : BaseModel
    {
        [PrimaryKey("id", shouldInsert: false)]
        public int Id { get; set; }

        public bool ShouldSerializeId() => Id > 0;

        [Column("holiday_date")]
        public DateTime HolidayDate { get; set; }

        [Column("name")]
        public string Name { get; set; }

        [Column("description")]
        public string Description { get; set; }

        [Column("is_mandatory")]
        public bool IsMandatory { get; set; }

        [Column("is_recurring")]
        public bool IsRecurring { get; set; }

        [Column("recurring_month")]
        public int? RecurringMonth { get; set; }

        [Column("recurring_day")]
        public int? RecurringDay { get; set; }

        [Column("recurring_rule")]
        public string RecurringRule { get; set; }

        [Column("year")]
        public int? Year { get; set; }

        [Column("created_by")]
        public int? CreatedBy { get; set; }

        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }

        [Column("updated_by")]
        public int? UpdatedBy { get; set; }

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }
    }

    /// <summary>
    /// Modelo para la tabla t_workday_config - Configuración de días laborales
    /// </summary>
    [Table("t_workday_config")]
    public class WorkdayConfigTable : BaseModel
    {
        [PrimaryKey("id", shouldInsert: false)]
        public int Id { get; set; }

        public bool ShouldSerializeId() => Id > 0;

        [Column("day_of_week")]
        public int DayOfWeek { get; set; }

        [Column("is_workday")]
        public bool IsWorkday { get; set; }

        [Column("description")]
        public string Description { get; set; }

        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }
    }

    /// <summary>
    /// Modelo de vista para mostrar feriados
    /// </summary>
    public class HolidayViewModel
    {
        public int Id { get; set; }
        public DateTime HolidayDate { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsMandatory { get; set; }
        public bool IsRecurring { get; set; }

        public string TypeDisplay => IsMandatory ? "Obligatorio" : "Opcional";
        public string TypeColor => IsMandatory ? "#2196F3" : "#9E9E9E";
        public string DateDisplay => HolidayDate.ToString("dd/MM/yyyy");
        public string DayOfWeekDisplay => HolidayDate.ToString("dddd", new System.Globalization.CultureInfo("es-MX"));

        public bool IsPast => HolidayDate.Date < DateTime.Today;
        public bool IsToday => HolidayDate.Date == DateTime.Today;
        public bool IsUpcoming => HolidayDate.Date > DateTime.Today;
    }
}
