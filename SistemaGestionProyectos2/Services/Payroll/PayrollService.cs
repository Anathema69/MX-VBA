using SistemaGestionProyectos2.Models.Database;
using SistemaGestionProyectos2.Services.Core;
using Supabase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SistemaGestionProyectos2.Services.Payroll
{
    public class PayrollService : BaseSupabaseService
    {
        public PayrollService(Client supabaseClient) : base(supabaseClient) { }

        /// <summary>
        /// Obtiene todos los empleados activos en nómina
        /// </summary>
        public async Task<List<PayrollTable>> GetActivePayroll()
        {
            try
            {
                var response = await SupabaseClient
                    .From<PayrollTable>()
                    .Where(x => x.IsActive == true)
                    .Order(x => x.Employee, Postgrest.Constants.Ordering.Ascending)
                    .Get();

                var payrolls = response?.Models ?? new List<PayrollTable>();
                LogSuccess($"Nómina activa obtenida: {payrolls.Count} empleados");
                return payrolls;
            }
            catch (Exception ex)
            {
                LogError("Error obteniendo nómina activa", ex);
                throw;
            }
        }

        /// <summary>
        /// Obtiene todos los empleados (activos e inactivos)
        /// </summary>
        public async Task<List<PayrollTable>> GetAllPayroll()
        {
            try
            {
                var response = await SupabaseClient
                    .From<PayrollTable>()
                    .Order(x => x.Employee, Postgrest.Constants.Ordering.Ascending)
                    .Get();

                var payrolls = response?.Models ?? new List<PayrollTable>();
                LogSuccess($"Toda la nómina obtenida: {payrolls.Count} empleados");
                return payrolls;
            }
            catch (Exception ex)
            {
                LogError("Error obteniendo toda la nómina", ex);
                throw;
            }
        }

        /// <summary>
        /// Obtiene un empleado de nómina por ID
        /// </summary>
        public async Task<PayrollTable> GetPayrollById(int id)
        {
            try
            {
                var response = await SupabaseClient
                    .From<PayrollTable>()
                    .Where(x => x.Id == id)
                    .Single();

                return response;
            }
            catch (Exception ex)
            {
                LogError($"Error obteniendo empleado {id}", ex);
                return null;
            }
        }

        /// <summary>
        /// Crea un nuevo empleado en nómina
        /// </summary>
        public async Task<PayrollTable> CreatePayroll(PayrollTable payroll)
        {
            try
            {
                // Asignar timestamps correctamente
                var now = DateTime.UtcNow;
                payroll.CreatedAt = now;
                payroll.UpdatedAt = now;
                payroll.IsActive = true;

                LogDebug($"Creando empleado en nómina: {payroll.Employee}");

                var response = await SupabaseClient
                    .From<PayrollTable>()
                    .Insert(payroll);

                if (response?.Models?.Count > 0)
                {
                    LogSuccess($"Empleado creado en nómina: {payroll.Employee}");
                    return response.Models.First();
                }

                throw new Exception("No se pudo crear el empleado en nómina");
            }
            catch (Exception ex)
            {
                LogError("Error creando empleado en nómina", ex);
                throw;
            }
        }

        /// <summary>
        /// Actualiza un empleado existente en nómina
        /// </summary>
        public async Task<PayrollTable> UpdatePayroll(PayrollTable payroll)
        {
            try
            {
                payroll.UpdatedAt = DateTime.UtcNow;

                LogDebug($"Actualizando empleado en nómina: {payroll.Employee}");

                var response = await SupabaseClient
                    .From<PayrollTable>()
                    .Where(x => x.Id == payroll.Id)
                    .Set(x => x.Employee, payroll.Employee)
                    .Set(x => x.Title, payroll.Title)
                    .Set(x => x.Range, payroll.Range)
                    .Set(x => x.Condition, payroll.Condition)
                    .Set(x => x.SSPayroll, payroll.SSPayroll)
                    .Set(x => x.WeeklyPayroll, payroll.WeeklyPayroll)
                    .Set(x => x.SocialSecurity, payroll.SocialSecurity)
                    .Set(x => x.Benefits, payroll.Benefits)
                    .Set(x => x.BenefitsAmount, payroll.BenefitsAmount)
                    .Set(x => x.MonthlyPayroll, payroll.MonthlyPayroll)
                    .Set(x => x.UpdatedBy, payroll.UpdatedBy)
                    .Set(x => x.UpdatedAt, payroll.UpdatedAt)
                    .Update();

                if (response?.Models?.Count > 0)
                {
                    LogSuccess($"Empleado actualizado en nómina: {payroll.Employee}");
                    return response.Models.First();
                }

                return null;
            }
            catch (Exception ex)
            {
                LogError($"Error actualizando empleado {payroll.Id}", ex);
                throw;
            }
        }

        /// <summary>
        /// Desactiva un empleado (soft delete)
        /// </summary>
        public async Task<bool> DeactivateEmployee(int employeeId, int userId)
        {
            try
            {
                var response = await SupabaseClient
                    .From<PayrollTable>()
                    .Where(x => x.Id == employeeId)
                    .Set(x => x.IsActive, false)
                    .Set(x => x.UpdatedBy, userId)
                    .Set(x => x.UpdatedAt, DateTime.UtcNow)
                    .Update();

                bool success = response?.Models?.Any() == true;
                if (success) LogSuccess($"Empleado desactivado: {employeeId}");
                return success;
            }
            catch (Exception ex)
            {
                LogError($"Error desactivando empleado {employeeId}", ex);
                return false;
            }
        }

        /// <summary>
        /// Reactiva un empleado previamente desactivado
        /// </summary>
        public async Task<bool> ReactivateEmployee(int employeeId, int userId)
        {
            try
            {
                var response = await SupabaseClient
                    .From<PayrollTable>()
                    .Where(x => x.Id == employeeId)
                    .Set(x => x.IsActive, true)
                    .Set(x => x.UpdatedBy, userId)
                    .Set(x => x.UpdatedAt, DateTime.UtcNow)
                    .Update();

                bool success = response?.Models?.Any() == true;
                if (success) LogSuccess($"Empleado reactivado: {employeeId}");
                return success;
            }
            catch (Exception ex)
            {
                LogError($"Error reactivando empleado {employeeId}", ex);
                return false;
            }
        }

        /// <summary>
        /// Calcula el total mensual de nómina (solo empleados activos)
        /// </summary>
        public async Task<decimal> GetMonthlyPayrollTotal()
        {
            try
            {
                var payrolls = await GetActivePayroll();
                var total = payrolls.Sum(p => p.MonthlyPayroll ?? 0);
                LogSuccess($"Total mensual de nómina calculado: {total:C}");
                return total;
            }
            catch (Exception ex)
            {
                LogError("Error calculando total mensual de nómina", ex);
                throw;
            }
        }

        /// <summary>
        /// Obtiene historial de cambios de nómina
        /// </summary>
        public async Task<List<PayrollHistoryTable>> GetPayrollHistory(int? payrollId = null, int limit = 100)
        {
            try
            {
                var query = SupabaseClient
                    .From<PayrollHistoryTable>()
                    .Order(x => x.EffectiveDate, Postgrest.Constants.Ordering.Descending)
                    .Limit(limit);

                if (payrollId.HasValue)
                {
                    query = query.Filter("f_payroll", Postgrest.Constants.Operator.Equals, payrollId.Value);
                }

                var response = await query.Get();

                var history = response?.Models ?? new List<PayrollHistoryTable>();
                LogSuccess($"Historial de nómina obtenido: {history.Count} registros" +
                    (payrollId.HasValue ? $" (Empleado ID: {payrollId.Value})" : ""));
                return history;
            }
            catch (Exception ex)
            {
                LogError("Error obteniendo historial de nómina", ex);
                throw;
            }
        }

        /// <summary>
        /// Crea un registro en el historial de nómina
        /// </summary>
        public async Task<PayrollHistoryTable> CreatePayrollHistory(PayrollHistoryTable history)
        {
            try
            {
                history.CreatedAt = DateTime.UtcNow;

                LogDebug($"Creando historial de nómina para empleado: {history.Employee}");

                var response = await SupabaseClient
                    .From<PayrollHistoryTable>()
                    .Insert(history);

                if (response?.Models?.Count > 0)
                {
                    LogSuccess($"Historial de nómina creado: {history.ChangeType}");
                    return response.Models.First();
                }

                throw new Exception("No se pudo crear el historial de nómina");
            }
            catch (Exception ex)
            {
                LogError("Error creando historial de nómina", ex);
                throw;
            }
        }

        /// <summary>
        /// Obtiene estadísticas de nómina
        /// </summary>
        public async Task<Dictionary<string, object>> GetPayrollStats()
        {
            try
            {
                var allPayroll = await GetAllPayroll();
                var activePayroll = allPayroll.Where(p => p.IsActive).ToList();

                var stats = new Dictionary<string, object>
                {
                    ["TotalEmpleados"] = allPayroll.Count,
                    ["EmpleadosActivos"] = activePayroll.Count,
                    ["EmpleadosInactivos"] = allPayroll.Count - activePayroll.Count,
                    ["NominaMensualTotal"] = activePayroll.Sum(p => p.MonthlyPayroll ?? 0),
                    ["NominaSemanalTotal"] = activePayroll.Sum(p => p.WeeklyPayroll ?? 0),
                    ["PromedioSalarioMensual"] = activePayroll.Count > 0
                        ? activePayroll.Average(p => p.MonthlyPayroll ?? 0)
                        : 0
                };

                LogSuccess($"Estadísticas de nómina calculadas");
                return stats;
            }
            catch (Exception ex)
            {
                LogError("Error calculando estadísticas de nómina", ex);
                throw;
            }
        }

        /// <summary>
        /// Verifica si existe un empleado con el mismo nombre
        /// </summary>
        public async Task<bool> EmployeeExists(string employeeName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(employeeName)) return false;

                var normalizedName = employeeName.Trim().ToUpper();

                var response = await SupabaseClient
                    .From<PayrollTable>()
                    .Filter("f_employee", Postgrest.Constants.Operator.ILike, normalizedName)
                    .Get();

                return response?.Models?.Count > 0;
            }
            catch (Exception ex)
            {
                LogError("Error verificando existencia de empleado", ex);
                return false;
            }
        }

        /// <summary>
        /// Obtiene empleados por condición (Permanente, Temporal, etc.)
        /// </summary>
        public async Task<List<PayrollTable>> GetPayrollByCondition(string condition)
        {
            try
            {
                var response = await SupabaseClient
                    .From<PayrollTable>()
                    .Where(p => p.Condition == condition)
                    .Where(p => p.IsActive == true)
                    .Order(x => x.Employee, Postgrest.Constants.Ordering.Ascending)
                    .Get();

                var payrolls = response?.Models ?? new List<PayrollTable>();
                LogSuccess($"Empleados obtenidos por condición '{condition}': {payrolls.Count}");
                return payrolls;
            }
            catch (Exception ex)
            {
                LogError($"Error obteniendo empleados por condición '{condition}'", ex);
                throw;
            }
        }
    }
}
