using SistemaGestionProyectos2.Models.Database;
using SistemaGestionProyectos2.Services.Core;
using Supabase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SistemaGestionProyectos2.Services.Attendance
{
    /// <summary>
    /// Servicio para gestión de asistencia, vacaciones y feriados
    /// </summary>
    public class AttendanceService : BaseSupabaseService
    {
        // Cache para mejorar rendimiento
        private List<PayrollTable> _employeesCache;
        private DateTime _employeesCacheTime;
        private List<WorkdayConfigTable> _workdayConfigCache;
        private List<HolidayTable> _holidaysCache;
        private int _holidaysCacheYear;

        public AttendanceService(Client supabaseClient) : base(supabaseClient) { }

        #region Asistencia

        /// <summary>
        /// Obtiene la asistencia de todos los empleados activos para una fecha específica (OPTIMIZADO)
        /// </summary>
        public async Task<List<AttendanceViewModel>> GetAttendanceForDate(DateTime date)
        {
            try
            {
                // Cargar datos en PARALELO para mejor rendimiento
                var employeesTask = GetEmployeesCached();
                var attendanceTask = GetAttendanceRecords(date);
                var vacationsTask = GetActiveVacationsForDate(date);
                var holidayTask = GetHolidayForDate(date);
                var workdayTask = GetWorkdayConfig();

                await Task.WhenAll(employeesTask, attendanceTask, vacationsTask, holidayTask, workdayTask);

                var employees = await employeesTask;
                var attendanceRecords = await attendanceTask;
                var activeVacations = await vacationsTask;
                var holiday = await holidayTask;
                var workdayConfigs = await workdayTask;

                // Determinar si es día laboral
                var dayConfig = workdayConfigs.FirstOrDefault(w => w.DayOfWeek == (int)date.DayOfWeek);
                bool isWorkday = (dayConfig?.IsWorkday ?? true) && holiday == null;

                // Combinar datos eficientemente
                var result = employees.Select(emp =>
                {
                    var attendance = attendanceRecords.FirstOrDefault(a => a.EmployeeId == emp.Id);
                    var onVacation = activeVacations.Any(v => v.EmployeeId == emp.Id);

                    return new AttendanceViewModel
                    {
                        Id = attendance?.Id ?? 0,
                        EmployeeId = emp.Id,
                        EmployeeName = emp.Employee ?? "Sin nombre",
                        EmployeeCode = emp.EmployeeCode,
                        Title = emp.Title,
                        Initials = GetInitials(emp.Employee),
                        AttendanceDate = date,
                        Status = attendance?.Status ?? "SIN_REGISTRO",
                        CheckInTime = attendance?.CheckInTime,
                        CheckOutTime = attendance?.CheckOutTime,
                        LateMinutes = attendance?.LateMinutes ?? 0,
                        Notes = attendance?.Notes,
                        IsJustified = attendance?.IsJustified ?? false,
                        OnVacation = onVacation,
                        IsHoliday = holiday != null,
                        HolidayName = holiday?.Name,
                        IsWorkday = isWorkday && !onVacation
                    };
                }).ToList();

                return result;
            }
            catch (Exception ex)
            {
                LogError($"Error obteniendo asistencia para {date:dd/MM/yyyy}", ex);
                throw;
            }
        }

        /// <summary>
        /// Obtiene empleados desde cache o BD
        /// </summary>
        private async Task<List<PayrollTable>> GetEmployeesCached()
        {
            // Cache válido por 5 minutos
            if (_employeesCache != null && (DateTime.Now - _employeesCacheTime).TotalMinutes < 5)
            {
                return _employeesCache;
            }

            var response = await SupabaseClient
                .From<PayrollTable>()
                .Where(x => x.IsActive == true)
                .Order(x => x.Employee, Postgrest.Constants.Ordering.Ascending)
                .Get();

            _employeesCache = response?.Models ?? new List<PayrollTable>();
            _employeesCacheTime = DateTime.Now;
            return _employeesCache;
        }

        /// <summary>
        /// Obtiene registros de asistencia para una fecha
        /// </summary>
        private async Task<List<AttendanceTable>> GetAttendanceRecords(DateTime date)
        {
            try
            {
                var response = await SupabaseClient
                    .From<AttendanceTable>()
                    .Filter("attendance_date", Postgrest.Constants.Operator.Equals, date.ToString("yyyy-MM-dd"))
                    .Get();
                return response?.Models ?? new List<AttendanceTable>();
            }
            catch
            {
                return new List<AttendanceTable>();
            }
        }

        /// <summary>
        /// Obtiene vacaciones activas para una fecha
        /// </summary>
        private async Task<List<VacationTable>> GetActiveVacationsForDate(DateTime date)
        {
            try
            {
                var response = await SupabaseClient
                    .From<VacationTable>()
                    .Filter("status", Postgrest.Constants.Operator.Equals, "APROBADA")
                    .Get();

                var vacations = response?.Models ?? new List<VacationTable>();
                return vacations.Where(v => date >= v.StartDate && date <= v.EndDate).ToList();
            }
            catch
            {
                return new List<VacationTable>();
            }
        }

        /// <summary>
        /// Obtiene feriado para una fecha
        /// </summary>
        private async Task<HolidayTable> GetHolidayForDate(DateTime date)
        {
            try
            {
                // Usar cache si es del mismo año
                if (_holidaysCache == null || _holidaysCacheYear != date.Year)
                {
                    var response = await SupabaseClient
                        .From<HolidayTable>()
                        .Get();
                    _holidaysCache = response?.Models ?? new List<HolidayTable>();
                    _holidaysCacheYear = date.Year;
                }

                return _holidaysCache.FirstOrDefault(h => h.HolidayDate.Date == date.Date && h.IsMandatory);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Obtiene configuración de días laborales
        /// </summary>
        private async Task<List<WorkdayConfigTable>> GetWorkdayConfig()
        {
            if (_workdayConfigCache != null)
            {
                return _workdayConfigCache;
            }

            try
            {
                var response = await SupabaseClient
                    .From<WorkdayConfigTable>()
                    .Get();
                _workdayConfigCache = response?.Models ?? new List<WorkdayConfigTable>();
                return _workdayConfigCache;
            }
            catch
            {
                // Config por defecto
                return new List<WorkdayConfigTable>
                {
                    new WorkdayConfigTable { DayOfWeek = 0, IsWorkday = false }, // Domingo
                    new WorkdayConfigTable { DayOfWeek = 6, IsWorkday = false }  // Sábado
                };
            }
        }

        /// <summary>
        /// Guarda o actualiza un registro de asistencia
        /// </summary>
        public async Task<AttendanceTable> SaveAttendance(AttendanceTable attendance)
        {
            try
            {
                var now = DateTime.UtcNow;

                if (attendance.Id > 0)
                {
                    // Actualizar existente
                    attendance.UpdatedAt = now;

                    var response = await SupabaseClient
                        .From<AttendanceTable>()
                        .Where(x => x.Id == attendance.Id)
                        .Set(x => x.Status, attendance.Status)
                        .Set(x => x.CheckInTime, attendance.CheckInTime)
                        .Set(x => x.CheckOutTime, attendance.CheckOutTime)
                        .Set(x => x.LateMinutes, attendance.LateMinutes)
                        .Set(x => x.Notes, attendance.Notes)
                        .Set(x => x.IsJustified, attendance.IsJustified)
                        .Set(x => x.Justification, attendance.Justification)
                        .Set(x => x.UpdatedBy, attendance.UpdatedBy)
                        .Set(x => x.UpdatedAt, attendance.UpdatedAt)
                        .Update();

                    if (response?.Models?.Count > 0)
                    {
                        return response.Models.First();
                    }
                }
                else
                {
                    // Crear nuevo
                    attendance.CreatedAt = now;
                    attendance.UpdatedAt = now;

                    var response = await SupabaseClient
                        .From<AttendanceTable>()
                        .Insert(attendance);

                    if (response?.Models?.Count > 0)
                    {
                        return response.Models.First();
                    }
                }

                throw new Exception("No se pudo guardar la asistencia");
            }
            catch (Exception ex)
            {
                LogError("Error guardando asistencia", ex);
                throw;
            }
        }

        /// <summary>
        /// Registra asistencia masiva para todos los empleados sin registro
        /// </summary>
        public async Task<int> MarkAllPresent(DateTime date, int userId, TimeSpan defaultCheckIn)
        {
            try
            {
                var attendance = await GetAttendanceForDate(date);
                var withoutRecord = attendance.Where(a => a.Status == "SIN_REGISTRO" && a.IsWorkday).ToList();

                int count = 0;
                foreach (var emp in withoutRecord)
                {
                    var record = new AttendanceTable
                    {
                        EmployeeId = emp.EmployeeId,
                        AttendanceDate = date,
                        Status = "ASISTENCIA",
                        CheckInTime = defaultCheckIn,
                        LateMinutes = 0,
                        IsJustified = false,
                        CreatedBy = userId
                    };

                    await SaveAttendance(record);
                    count++;
                }

                return count;
            }
            catch (Exception ex)
            {
                LogError("Error en asistencia masiva", ex);
                throw;
            }
        }

        /// <summary>
        /// Obtiene estadísticas de asistencia del mes
        /// </summary>
        public async Task<Dictionary<string, int>> GetMonthlyStats(int year, int month)
        {
            try
            {
                var startDate = new DateTime(year, month, 1);
                var endDate = startDate.AddMonths(1).AddDays(-1);

                List<AttendanceTable> records = new List<AttendanceTable>();
                try
                {
                    var response = await SupabaseClient
                        .From<AttendanceTable>()
                        .Filter("attendance_date", Postgrest.Constants.Operator.GreaterThanOrEqual, startDate.ToString("yyyy-MM-dd"))
                        .Filter("attendance_date", Postgrest.Constants.Operator.LessThanOrEqual, endDate.ToString("yyyy-MM-dd"))
                        .Get();

                    records = response?.Models ?? new List<AttendanceTable>();
                }
                catch { }

                return new Dictionary<string, int>
                {
                    ["Asistencias"] = records.Count(r => r.Status == "ASISTENCIA"),
                    ["Retardos"] = records.Count(r => r.Status == "RETARDO"),
                    ["Faltas"] = records.Count(r => r.Status == "FALTA"),
                    ["Vacaciones"] = records.Count(r => r.Status == "VACACIONES"),
                    ["TotalMinutosRetardo"] = records.Sum(r => r.LateMinutes ?? 0)
                };
            }
            catch (Exception ex)
            {
                LogError($"Error obteniendo estadísticas de {month}/{year}", ex);
                return new Dictionary<string, int>
                {
                    ["Asistencias"] = 0, ["Retardos"] = 0, ["Faltas"] = 0,
                    ["Vacaciones"] = 0, ["TotalMinutosRetardo"] = 0
                };
            }
        }

        /// <summary>
        /// Calcula minutos de retardo basado en hora de entrada
        /// </summary>
        public int CalculateLateMinutes(TimeSpan checkInTime, TimeSpan expectedStartTime)
        {
            if (checkInTime <= expectedStartTime)
                return 0;

            return (int)(checkInTime - expectedStartTime).TotalMinutes;
        }

        /// <summary>
        /// Invalida el cache de empleados
        /// </summary>
        public void InvalidateCache()
        {
            _employeesCache = null;
            _workdayConfigCache = null;
            _holidaysCache = null;
        }

        #endregion

        #region Vacaciones

        /// <summary>
        /// Obtiene la lista de empleados activos para selección
        /// </summary>
        public async Task<List<PayrollTable>> GetActiveEmployees()
        {
            return await GetEmployeesCached();
        }

        /// <summary>
        /// Crea una nueva solicitud de vacaciones
        /// </summary>
        public async Task<VacationTable> CreateVacation(VacationTable vacation)
        {
            try
            {
                // NOTA: total_days es columna generada en PostgreSQL, no se debe asignar
                vacation.Status = "APROBADA"; // Auto-aprobar desde el calendario
                vacation.CreatedAt = DateTime.UtcNow;
                vacation.UpdatedAt = DateTime.UtcNow;
                vacation.ApprovedAt = DateTime.UtcNow;
                vacation.ApprovedBy = vacation.CreatedBy;

                var response = await SupabaseClient
                    .From<VacationTable>()
                    .Insert(vacation);

                if (response?.Models?.Count > 0)
                {
                    return response.Models.First();
                }

                throw new Exception("No se pudo crear la vacación");
            }
            catch (Exception ex)
            {
                LogError("Error creando vacación", ex);
                throw;
            }
        }

        /// <summary>
        /// Obtiene las vacaciones activas y próximas
        /// </summary>
        public async Task<List<VacationViewModel>> GetActiveVacations()
        {
            try
            {
                var employees = await GetEmployeesCached();
                var response = await SupabaseClient
                    .From<VacationTable>()
                    .Filter("status", Postgrest.Constants.Operator.Equals, "APROBADA")
                    .Order("start_date", Postgrest.Constants.Ordering.Ascending)
                    .Get();

                var vacations = response?.Models ?? new List<VacationTable>();
                var today = DateTime.Today;

                return vacations.Select(v =>
                {
                    var emp = employees.FirstOrDefault(e => e.Id == v.EmployeeId);
                    string vacationStatus = v.EndDate.Date < today ? "FINALIZADA" :
                                           v.StartDate.Date <= today && v.EndDate.Date >= today ? "EN_CURSO" : "PROXIMA";

                    return new VacationViewModel
                    {
                        Id = v.Id,
                        EmployeeId = v.EmployeeId,
                        EmployeeName = emp?.Employee ?? "Desconocido",
                        Title = emp?.Title,
                        StartDate = v.StartDate,
                        EndDate = v.EndDate,
                        TotalDays = v.TotalDays ?? 0,
                        Notes = v.Notes,
                        Status = v.Status,
                        ApprovedAt = v.ApprovedAt,
                        VacationStatus = vacationStatus
                    };
                })
                .Where(v => v.VacationStatus != "FINALIZADA") // Solo activas y próximas
                .ToList();
            }
            catch (Exception ex)
            {
                LogError("Error obteniendo vacaciones", ex);
                return new List<VacationViewModel>();
            }
        }

        /// <summary>
        /// Verifica si un empleado tiene vacaciones en un rango de fechas
        /// </summary>
        public async Task<bool> HasVacationConflict(int employeeId, DateTime startDate, DateTime endDate, int? excludeVacationId = null)
        {
            try
            {
                var response = await SupabaseClient
                    .From<VacationTable>()
                    .Filter("employee_id", Postgrest.Constants.Operator.Equals, employeeId.ToString())
                    .Filter("status", Postgrest.Constants.Operator.Equals, "APROBADA")
                    .Get();

                var vacations = response?.Models ?? new List<VacationTable>();

                return vacations.Any(v =>
                    (excludeVacationId == null || v.Id != excludeVacationId) &&
                    startDate <= v.EndDate && endDate >= v.StartDate);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Cancela una vacación existente
        /// </summary>
        public async Task<bool> CancelVacation(int vacationId, int userId)
        {
            try
            {
                var response = await SupabaseClient
                    .From<VacationTable>()
                    .Where(x => x.Id == vacationId)
                    .Set(x => x.Status, "CANCELADA")
                    .Set(x => x.UpdatedBy, userId)
                    .Set(x => x.UpdatedAt, DateTime.UtcNow)
                    .Update();

                return response?.Models?.Count > 0;
            }
            catch (Exception ex)
            {
                LogError("Error cancelando vacación", ex);
                return false;
            }
        }

        /// <summary>
        /// Calcula días laborales entre dos fechas (excluyendo fines de semana)
        /// </summary>
        public int CalculateWorkingDays(DateTime startDate, DateTime endDate)
        {
            int workingDays = 0;
            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                if (date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday)
                    workingDays++;
            }
            return workingDays;
        }

        #endregion

        #region Helpers

        private string GetInitials(string name)
        {
            if (string.IsNullOrEmpty(name)) return "??";

            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
                return $"{parts[0][0]}{parts[1][0]}".ToUpper();
            else if (parts.Length == 1)
                return parts[0].Substring(0, Math.Min(2, parts[0].Length)).ToUpper();
            return "??";
        }

        #endregion
    }
}
