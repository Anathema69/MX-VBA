using SistemaGestionProyectos2.Models.Database;
using SistemaGestionProyectos2.Services.Core;
using Supabase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SistemaGestionProyectos2.Services.FixedExpenses
{
    public class FixedExpenseService : BaseSupabaseService
    {
        public FixedExpenseService(Client supabaseClient) : base(supabaseClient) { }

        /// <summary>
        /// Obtiene todos los gastos fijos activos
        /// </summary>
        public async Task<List<FixedExpenseTable>> GetActiveFixedExpenses()
        {
            try
            {
                var response = await SupabaseClient
                    .From<FixedExpenseTable>()
                    .Where(x => x.IsActive == true)
                    .Order(x => x.ExpenseType, Postgrest.Constants.Ordering.Ascending)
                    .Get();

                var expenses = response?.Models ?? new List<FixedExpenseTable>();
                LogSuccess($"Gastos fijos activos obtenidos: {expenses.Count}");
                return expenses;
            }
            catch (Exception ex)
            {
                LogError("Error obteniendo gastos fijos activos", ex);
                throw;
            }
        }

        /// <summary>
        /// Obtiene todos los gastos fijos (activos e inactivos)
        /// </summary>
        public async Task<List<FixedExpenseTable>> GetAllFixedExpenses()
        {
            try
            {
                var response = await SupabaseClient
                    .From<FixedExpenseTable>()
                    .Order(x => x.ExpenseType, Postgrest.Constants.Ordering.Ascending)
                    .Get();

                var expenses = response?.Models ?? new List<FixedExpenseTable>();
                LogSuccess($"Todos los gastos fijos obtenidos: {expenses.Count}");
                return expenses;
            }
            catch (Exception ex)
            {
                LogError("Error obteniendo todos los gastos fijos", ex);
                throw;
            }
        }

        /// <summary>
        /// Obtiene gastos fijos efectivos para una fecha específica
        /// </summary>
        public async Task<List<FixedExpenseTable>> GetEffectiveFixedExpenses(DateTime effectiveDate)
        {
            try
            {
                var response = await SupabaseClient
                    .From<FixedExpenseTable>()
                    .Where(e => e.IsActive == true)
                    .Get();

                var expenses = response?.Models ?? new List<FixedExpenseTable>();
                LogSuccess($"Gastos fijos efectivos obtenidos para {effectiveDate:yyyy-MM-dd}: {expenses.Count}");
                return expenses;
            }
            catch (Exception ex)
            {
                LogError($"Error obteniendo gastos efectivos para {effectiveDate:yyyy-MM-dd}", ex);
                return new List<FixedExpenseTable>();
            }
        }

        /// <summary>
        /// Obtiene un gasto fijo por ID
        /// </summary>
        public async Task<FixedExpenseTable> GetFixedExpenseById(int id)
        {
            try
            {
                var response = await SupabaseClient
                    .From<FixedExpenseTable>()
                    .Where(e => e.Id == id)
                    .Single();

                return response;
            }
            catch (Exception ex)
            {
                LogError($"Error obteniendo gasto fijo {id}", ex);
                return null;
            }
        }

        /// <summary>
        /// Crea un nuevo gasto fijo
        /// </summary>
        public async Task<FixedExpenseTable> CreateFixedExpense(FixedExpenseTable expense)
        {
            try
            {
                // Asignar timestamps correctamente
                var now = DateTime.UtcNow;
                expense.CreatedAt = now;
                expense.UpdatedAt = now;
                expense.IsActive = true;

                LogDebug($"Creando gasto fijo: {expense.ExpenseType}");

                var response = await SupabaseClient
                    .From<FixedExpenseTable>()
                    .Insert(expense);

                if (response?.Models?.Count > 0)
                {
                    LogSuccess($"Gasto fijo creado: {expense.ExpenseType}");
                    return response.Models.First();
                }

                throw new Exception("No se pudo crear el gasto fijo");
            }
            catch (Exception ex)
            {
                LogError("Error creando gasto fijo", ex);
                throw;
            }
        }

        /// <summary>
        /// Actualiza un gasto fijo existente
        /// </summary>
        public async Task<FixedExpenseTable> UpdateFixedExpense(FixedExpenseTable expense)
        {
            try
            {
                expense.UpdatedAt = DateTime.UtcNow;

                LogDebug($"Actualizando gasto fijo: {expense.ExpenseType}");

                var response = await SupabaseClient
                    .From<FixedExpenseTable>()
                    .Where(x => x.Id == expense.Id)
                    .Set(x => x.ExpenseType, expense.ExpenseType)
                    .Set(x => x.Description, expense.Description)
                    .Set(x => x.MonthlyAmount, expense.MonthlyAmount)
                    .Set(x => x.UpdatedAt, expense.UpdatedAt)
                    .Update();

                if (response?.Models?.Count > 0)
                {
                    LogSuccess($"Gasto fijo actualizado: {expense.ExpenseType}");
                    return response.Models.First();
                }

                return null;
            }
            catch (Exception ex)
            {
                LogError($"Error actualizando gasto fijo {expense.Id}", ex);
                throw;
            }
        }

        /// <summary>
        /// Elimina un gasto fijo (soft delete)
        /// </summary>
        public async Task<bool> DeleteFixedExpense(int expenseId)
        {
            try
            {
                var response = await SupabaseClient
                    .From<FixedExpenseTable>()
                    .Where(x => x.Id == expenseId)
                    .Set(x => x.IsActive, false)
                    .Set(x => x.UpdatedAt, DateTime.UtcNow)
                    .Update();

                bool success = response?.Models?.Any() == true;
                if (success) LogSuccess($"Gasto fijo eliminado: {expenseId}");
                return success;
            }
            catch (Exception ex)
            {
                LogError($"Error eliminando gasto fijo {expenseId}", ex);
                throw;
            }
        }

        /// <summary>
        /// Desactiva un gasto fijo
        /// </summary>
        public async Task<bool> DeactivateFixedExpense(int expenseId)
        {
            try
            {
                var expense = await GetFixedExpenseById(expenseId);
                if (expense != null)
                {
                    expense.IsActive = false;
                    expense.UpdatedAt = DateTime.UtcNow;

                    await SupabaseClient
                        .From<FixedExpenseTable>()
                        .Where(e => e.Id == expenseId)
                        .Update(expense);

                    LogSuccess($"Gasto fijo desactivado: {expenseId}");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                LogError($"Error desactivando gasto fijo {expenseId}", ex);
                return false;
            }
        }

        /// <summary>
        /// Reactiva un gasto fijo previamente desactivado
        /// </summary>
        public async Task<bool> ReactivateFixedExpense(int expenseId)
        {
            try
            {
                var response = await SupabaseClient
                    .From<FixedExpenseTable>()
                    .Where(x => x.Id == expenseId)
                    .Set(x => x.IsActive, true)
                    .Set(x => x.UpdatedAt, DateTime.UtcNow)
                    .Update();

                bool success = response?.Models?.Any() == true;
                if (success) LogSuccess($"Gasto fijo reactivado: {expenseId}");
                return success;
            }
            catch (Exception ex)
            {
                LogError($"Error reactivando gasto fijo {expenseId}", ex);
                return false;
            }
        }

        /// <summary>
        /// Guarda un gasto fijo con fecha efectiva
        /// </summary>
        public async Task<bool> SaveFixedExpenseWithEffectiveDate(FixedExpenseTable expense, DateTime effectiveDate, int userId)
        {
            try
            {
                expense.UpdatedAt = DateTime.UtcNow;
                expense.EffectiveDate = effectiveDate;

                LogDebug($"Guardando gasto fijo con fecha efectiva: {expense.ExpenseType} ({effectiveDate:yyyy-MM-dd})");

                await SupabaseClient
                    .From<FixedExpenseTable>()
                    .Where(e => e.Id == expense.Id)
                    .Update(expense);

                LogSuccess($"Gasto fijo guardado con fecha efectiva: {expense.ExpenseType}");
                return true;
            }
            catch (Exception ex)
            {
                LogError($"Error guardando gasto fijo con fecha efectiva {expense.Id}", ex);
                return false;
            }
        }

        /// <summary>
        /// Calcula el total mensual de gastos fijos para una fecha específica
        /// </summary>
        public async Task<decimal> GetMonthlyExpensesTotal(DateTime monthDate)
        {
            try
            {
                var expenses = await GetEffectiveFixedExpenses(monthDate);
                var total = expenses.Sum(e => e.MonthlyAmount ?? 0);
                LogSuccess($"Total mensual de gastos fijos para {monthDate:yyyy-MM}: {total:C}");
                return total;
            }
            catch (Exception ex)
            {
                LogError($"Error calculando total mensual para {monthDate:yyyy-MM}", ex);
                throw;
            }
        }

        /// <summary>
        /// Obtiene estadísticas de gastos fijos
        /// </summary>
        public async Task<Dictionary<string, object>> GetFixedExpensesStats()
        {
            try
            {
                var allExpenses = await GetAllFixedExpenses();
                var activeExpenses = allExpenses.Where(e => e.IsActive).ToList();

                var stats = new Dictionary<string, object>
                {
                    ["TotalGastos"] = allExpenses.Count,
                    ["GastosActivos"] = activeExpenses.Count,
                    ["GastosInactivos"] = allExpenses.Count - activeExpenses.Count,
                    ["MontoMensualTotal"] = activeExpenses.Sum(e => e.MonthlyAmount ?? 0),
                    ["PromedioGasto"] = activeExpenses.Count > 0
                        ? activeExpenses.Average(e => e.MonthlyAmount ?? 0)
                        : 0
                };

                LogSuccess($"Estadísticas de gastos fijos calculadas");
                return stats;
            }
            catch (Exception ex)
            {
                LogError("Error calculando estadísticas de gastos fijos", ex);
                throw;
            }
        }

        /// <summary>
        /// Obtiene gastos fijos por tipo
        /// </summary>
        public async Task<List<FixedExpenseTable>> GetFixedExpensesByType(string expenseType)
        {
            try
            {
                var response = await SupabaseClient
                    .From<FixedExpenseTable>()
                    .Where(e => e.ExpenseType == expenseType)
                    .Where(e => e.IsActive == true)
                    .Get();

                var expenses = response?.Models ?? new List<FixedExpenseTable>();
                LogSuccess($"Gastos fijos obtenidos por tipo '{expenseType}': {expenses.Count}");
                return expenses;
            }
            catch (Exception ex)
            {
                LogError($"Error obteniendo gastos por tipo '{expenseType}'", ex);
                throw;
            }
        }

        /// <summary>
        /// Obtiene historial de cambios de un gasto fijo
        /// </summary>
        public async Task<List<FixedExpenseHistoryTable>> GetFixedExpenseHistory(int expenseId)
        {
            try
            {
                var response = await SupabaseClient
                    .From<FixedExpenseHistoryTable>()
                    .Where(h => h.ExpenseId == expenseId)
                    .Order(h => h.EffectiveDate, Postgrest.Constants.Ordering.Descending)
                    .Get();

                var history = response?.Models ?? new List<FixedExpenseHistoryTable>();
                LogSuccess($"Historial de gasto fijo obtenido: {history.Count} registros");
                return history;
            }
            catch (Exception ex)
            {
                LogError($"Error obteniendo historial de gasto {expenseId}", ex);
                throw;
            }
        }

        /// <summary>
        /// Verifica si existe un gasto fijo con el mismo tipo y descripción
        /// </summary>
        public async Task<bool> FixedExpenseExists(string expenseType, string description)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(expenseType)) return false;

                var response = await SupabaseClient
                    .From<FixedExpenseTable>()
                    .Filter("expense_type", Postgrest.Constants.Operator.ILike, expenseType.Trim())
                    .Filter("description", Postgrest.Constants.Operator.ILike, description?.Trim() ?? "")
                    .Get();

                return response?.Models?.Count > 0;
            }
            catch (Exception ex)
            {
                LogError("Error verificando existencia de gasto fijo", ex);
                return false;
            }
        }
    }
}
