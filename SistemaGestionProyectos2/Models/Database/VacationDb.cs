using Postgrest.Attributes;
using Postgrest.Models;
using System;

namespace SistemaGestionProyectos2.Models.Database
{
    /// <summary>
    /// Modelo para la tabla t_vacation - Períodos de vacaciones
    /// </summary>
    [Table("t_vacation")]
    public class VacationTable : BaseModel
    {
        [PrimaryKey("id", shouldInsert: false)]
        public int Id { get; set; }

        public bool ShouldSerializeId() => Id > 0;

        [Column("employee_id")]
        public int EmployeeId { get; set; }

        [Column("start_date")]
        public DateTime StartDate { get; set; }

        [Column("end_date")]
        public DateTime EndDate { get; set; }

        [Column("total_days")]
        public int? TotalDays { get; set; }

        // TotalDays es columna generada en PostgreSQL, nunca serializar
        public bool ShouldSerializeTotalDays() => false;

        [Column("notes")]
        public string Notes { get; set; }

        [Column("status")]
        public string Status { get; set; }

        [Column("approved_by")]
        public int? ApprovedBy { get; set; }

        [Column("approved_at")]
        public DateTime? ApprovedAt { get; set; }

        [Column("rejection_reason")]
        public string RejectionReason { get; set; }

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
    /// Modelo extendido de vacaciones con información del empleado
    /// </summary>
    public class VacationViewModel
    {
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; }
        public string Title { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TotalDays { get; set; }
        public string Notes { get; set; }
        public string Status { get; set; }
        public string ApprovedByUser { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public string VacationStatus { get; set; }
        public int DaysRemaining { get; set; }

        // Propiedades calculadas para UI
        public string StatusDisplay => Status switch
        {
            "PENDIENTE" => "Pendiente",
            "APROBADA" => "Aprobada",
            "RECHAZADA" => "Rechazada",
            "CANCELADA" => "Cancelada",
            _ => Status
        };

        public string StatusColor => Status switch
        {
            "PENDIENTE" => "#FFC107",    // Amarillo
            "APROBADA" => "#4CAF50",     // Verde
            "RECHAZADA" => "#F44336",    // Rojo
            "CANCELADA" => "#9E9E9E",    // Gris
            _ => "#757575"
        };

        public string VacationStatusDisplay => VacationStatus switch
        {
            "EN_CURSO" => "En Curso",
            "PROXIMA" => "Próxima",
            "FINALIZADA" => "Finalizada",
            _ => VacationStatus
        };

        public string DateRangeDisplay => $"{StartDate:dd/MM/yyyy} - {EndDate:dd/MM/yyyy}";

        public string DaysDisplay => TotalDays == 1 ? "1 día" : $"{TotalDays} días";

        public bool IsActive => VacationStatus == "EN_CURSO";
        public bool IsUpcoming => VacationStatus == "PROXIMA";
    }
}
