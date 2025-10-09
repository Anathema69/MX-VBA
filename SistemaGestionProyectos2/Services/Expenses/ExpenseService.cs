using SistemaGestionProyectos2.Models.Database;
using SistemaGestionProyectos2.Services.Core;
using Supabase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SistemaGestionProyectos2.Services.Expenses
{
    public class ExpenseService : BaseSupabaseService
    {
        public ExpenseService(Client supabaseClient) : base(supabaseClient) { }

        /// <summary>
        /// Obtiene gastos con filtros opcionales
        /// </summary>
        public async Task<List<ExpenseDb>> GetExpenses(
            int? supplierId = null,
            string status = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            int limit = 100,
            int offset = 0)
        {
            try
            {
                // Construir la consulta base
                var query = SupabaseClient.From<ExpenseDb>().Select("*");

                // Aplicar filtros uno por uno
                if (supplierId.HasValue)
                {
                    query = query.Filter("f_supplier", Postgrest.Constants.Operator.Equals, supplierId.Value);
                }

                if (!string.IsNullOrEmpty(status))
                {
                    query = query.Filter("f_status", Postgrest.Constants.Operator.Equals, status);
                }

                if (fromDate.HasValue)
                {
                    query = query.Filter("f_expensedate", Postgrest.Constants.Operator.GreaterThanOrEqual, fromDate.Value.ToString("yyyy-MM-dd"));
                }

                if (toDate.HasValue)
                {
                    query = query.Filter("f_expensedate", Postgrest.Constants.Operator.LessThanOrEqual, toDate.Value.ToString("yyyy-MM-dd"));
                }

                // Ordenar, limitar y ejecutar
                var response = await query
                    .Order("f_expensedate", Postgrest.Constants.Ordering.Descending)
                    .Order("f_expense", Postgrest.Constants.Ordering.Descending)
                    .Range(offset, offset + limit - 1)
                    .Get();

                var expenses = response?.Models ?? new List<ExpenseDb>();
                LogSuccess($"Gastos obtenidos: {expenses.Count}" +
                    (supplierId.HasValue ? $" (Proveedor ID: {supplierId.Value})" : "") +
                    (!string.IsNullOrEmpty(status) ? $" (Estado: {status})" : ""));

                return expenses;
            }
            catch (Exception ex)
            {
                LogError("Error obteniendo gastos", ex);
                throw;
            }
        }

        /// <summary>
        /// Obtiene un gasto por ID
        /// </summary>
        public async Task<ExpenseDb> GetExpenseById(int expenseId)
        {
            try
            {
                var response = await SupabaseClient
                    .From<ExpenseDb>()
                    .Where(e => e.Id == expenseId)
                    .Single();

                return response;
            }
            catch (Exception ex)
            {
                LogError($"Error obteniendo gasto {expenseId}", ex);
                return null;
            }
        }

        /// <summary>
        /// Crea un nuevo gasto
        /// </summary>
        public async Task<ExpenseDb> CreateExpense(ExpenseDb expense)
        {
            try
            {
                // Establecer valores por defecto
                expense.Status = "PENDIENTE";
                expense.CreatedAt = DateTime.UtcNow;
                expense.UpdatedAt = DateTime.UtcNow;

                LogDebug($"Creando gasto: {expense.Description}");

                var response = await SupabaseClient
                    .From<ExpenseDb>()
                    .Insert(expense);

                if (response?.Models?.Count > 0)
                {
                    LogSuccess($"Gasto creado: {expense.Description}");
                    return response.Models.First();
                }

                throw new Exception("No se pudo crear el gasto");
            }
            catch (Exception ex)
            {
                LogError("Error creando gasto", ex);
                throw;
            }
        }

        /// <summary>
        /// Actualiza un gasto existente
        /// </summary>
        public async Task<bool> UpdateExpense(ExpenseDb expense)
        {
            try
            {
                expense.UpdatedAt = DateTime.UtcNow;

                var response = await SupabaseClient
                    .From<ExpenseDb>()
                    .Where(e => e.Id == expense.Id)
                    .Update(expense);

                bool success = response?.Models?.Any() == true;
                if (success) LogSuccess($"Gasto actualizado: {expense.Description}");
                return success;
            }
            catch (Exception ex)
            {
                LogError($"Error actualizando gasto {expense.Id}", ex);
                return false;
            }
        }

        /// <summary>
        /// Marca un gasto como pagado
        /// </summary>
        public async Task<bool> MarkExpenseAsPaid(int expenseId, DateTime paidDate, string payMethod)
        {
            try
            {
                var expense = await GetExpenseById(expenseId);
                if (expense == null) return false;

                expense.Status = "PAGADO";
                expense.PaidDate = paidDate;
                expense.PayMethod = payMethod;
                expense.UpdatedAt = DateTime.UtcNow;

                return await UpdateExpense(expense);
            }
            catch (Exception ex)
            {
                LogError($"Error marcando gasto {expenseId} como pagado", ex);
                return false;
            }
        }

        /// <summary>
        /// Elimina un gasto
        /// </summary>
        public async Task<bool> DeleteExpense(int expenseId)
        {
            try
            {
                await SupabaseClient
                    .From<ExpenseDb>()
                    .Where(e => e.Id == expenseId)
                    .Delete();

                LogSuccess($"Gasto eliminado: {expenseId}");
                return true;
            }
            catch (Exception ex)
            {
                LogError($"Error eliminando gasto {expenseId}", ex);
                return false;
            }
        }

        /// <summary>
        /// Obtiene gastos próximos a vencer en los próximos N días
        /// </summary>
        public async Task<List<ExpenseDb>> GetUpcomingExpenses(int daysAhead = 7)
        {
            try
            {
                var futureDate = DateTime.Now.Date.AddDays(daysAhead);

                var response = await SupabaseClient
                    .From<ExpenseDb>()
                    .Where(e => e.Status == "PENDIENTE")
                    .Where(e => e.ScheduledDate != null)
                    .Where(e => e.ScheduledDate <= futureDate)
                    .Order("f_scheduleddate", Postgrest.Constants.Ordering.Ascending)
                    .Get();

                var expenses = response?.Models ?? new List<ExpenseDb>();
                LogSuccess($"Gastos próximos encontrados: {expenses.Count} (próximos {daysAhead} días)");
                return expenses;
            }
            catch (Exception ex)
            {
                LogError("Error obteniendo gastos próximos", ex);
                throw;
            }
        }

        /// <summary>
        /// Obtiene gastos vencidos (fecha programada pasada y estado PENDIENTE)
        /// </summary>
        public async Task<List<ExpenseDb>> GetOverdueExpenses()
        {
            try
            {
                var today = DateTime.Now.Date;

                var response = await SupabaseClient
                    .From<ExpenseDb>()
                    .Where(e => e.Status == "PENDIENTE")
                    .Where(e => e.ScheduledDate != null)
                    .Where(e => e.ScheduledDate < today)
                    .Order("f_scheduleddate", Postgrest.Constants.Ordering.Ascending)
                    .Get();

                var expenses = response?.Models ?? new List<ExpenseDb>();
                LogSuccess($"Gastos vencidos encontrados: {expenses.Count}");
                return expenses;
            }
            catch (Exception ex)
            {
                LogError("Error obteniendo gastos vencidos", ex);
                throw;
            }
        }

        /// <summary>
        /// Obtiene estadísticas de gastos por estado
        /// </summary>
        public async Task<Dictionary<string, decimal>> GetExpensesStatsByStatus()
        {
            try
            {
                var expenses = await GetExpenses();

                var stats = expenses
                    .GroupBy(e => e.Status ?? "SIN_ESTADO")
                    .ToDictionary(
                        g => g.Key,
                        g => g.Sum(e => e.TotalExpense)
                    );

                LogSuccess($"Estadísticas de gastos calculadas: {stats.Count} estados");
                return stats;
            }
            catch (Exception ex)
            {
                LogError("Error calculando estadísticas de gastos", ex);
                throw;
            }
        }
    }
}
