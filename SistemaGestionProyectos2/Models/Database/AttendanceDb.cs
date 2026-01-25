using Postgrest.Attributes;
using Postgrest.Models;
using System;

namespace SistemaGestionProyectos2.Models.Database
{
    /// <summary>
    /// Modelo para la tabla t_attendance - Registro diario de asistencia
    /// </summary>
    [Table("t_attendance")]
    public class AttendanceTable : BaseModel
    {
        [PrimaryKey("id", shouldInsert: false)]
        public int Id { get; set; }

        public bool ShouldSerializeId() => Id > 0;

        [Column("employee_id")]
        public int EmployeeId { get; set; }

        [Column("attendance_date")]
        public DateTime AttendanceDate { get; set; }

        [Column("status")]
        public string Status { get; set; }

        [Column("check_in_time")]
        public TimeSpan? CheckInTime { get; set; }

        [Column("check_out_time")]
        public TimeSpan? CheckOutTime { get; set; }

        [Column("late_minutes")]
        public int? LateMinutes { get; set; }

        [Column("notes")]
        public string Notes { get; set; }

        [Column("is_justified")]
        public bool IsJustified { get; set; }

        [Column("justification")]
        public string Justification { get; set; }

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
    /// Modelo extendido para mostrar asistencia con nombre del empleado
    /// </summary>
    public class AttendanceViewModel
    {
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; }
        public string EmployeeCode { get; set; }
        public string Title { get; set; }
        public string Initials { get; set; }
        public DateTime AttendanceDate { get; set; }
        public string Status { get; set; }
        public TimeSpan? CheckInTime { get; set; }
        public TimeSpan? CheckOutTime { get; set; }
        public int? LateMinutes { get; set; }
        public string Notes { get; set; }
        public bool IsJustified { get; set; }
        public bool OnVacation { get; set; }
        public bool IsHoliday { get; set; }
        public string HolidayName { get; set; }
        public bool IsWorkday { get; set; }

        // Propiedades calculadas para UI
        public string StatusDisplay => Status switch
        {
            "ASISTENCIA" => "Asistencia",
            "RETARDO" => $"Retardo ({LateMinutes} min)",
            "FALTA" => IsJustified ? "Falta Justificada" : "Falta",
            "VACACIONES" => "Vacaciones",
            "FERIADO" => "Feriado",
            "DESCANSO" => "Descanso",
            "SIN_REGISTRO" => "Sin Registro",
            _ => Status
        };

        public string StatusColor => Status switch
        {
            "ASISTENCIA" => "#4CAF50",   // Verde
            "RETARDO" => "#FFC107",       // Amarillo
            "FALTA" => "#F44336",         // Rojo
            "VACACIONES" => "#9C27B0",    // Morado
            "FERIADO" => "#2196F3",       // Azul
            "DESCANSO" => "#9E9E9E",      // Gris
            _ => "#757575"                 // Gris oscuro
        };

        public string CheckInDisplay => CheckInTime?.ToString(@"hh\:mm") ?? "--:--";
        public string CheckOutDisplay => CheckOutTime?.ToString(@"hh\:mm") ?? "--:--";
    }

    /// <summary>
    /// Modelo para estadísticas mensuales de asistencia
    /// </summary>
    public class AttendanceMonthlyStats
    {
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; }
        public string Title { get; set; }
        public string EmployeeCode { get; set; }
        public DateTime Month { get; set; }
        public int Asistencias { get; set; }
        public int Retardos { get; set; }
        public int Faltas { get; set; }
        public int Vacaciones { get; set; }
        public int Feriados { get; set; }
        public int Descansos { get; set; }
        public int TotalLateMinutes { get; set; }
        public int JustifiedCount { get; set; }

        public string LateTimeDisplay
        {
            get
            {
                if (TotalLateMinutes == 0) return "0 min";
                var hours = TotalLateMinutes / 60;
                var mins = TotalLateMinutes % 60;
                return hours > 0 ? $"{hours}h {mins}m" : $"{mins} min";
            }
        }
    }

    /// <summary>
    /// Modelo para día del calendario con estadísticas
    /// </summary>
    public class CalendarDayInfo
    {
        public DateTime Date { get; set; }
        public int DayOfWeek { get; set; }
        public string DayName { get; set; }
        public bool IsWorkday { get; set; }
        public bool IsHoliday { get; set; }
        public string HolidayName { get; set; }
        public int TotalEmployees { get; set; }
        public int Asistencias { get; set; }
        public int Retardos { get; set; }
        public int Faltas { get; set; }
        public int Vacaciones { get; set; }
        public int SinRegistro { get; set; }

        public bool IsToday => Date.Date == DateTime.Today;
        public bool IsWeekend => DayOfWeek == 0 || DayOfWeek == 6;
        public bool HasIssues => Faltas > 0 || Retardos > 0;
    }
}
