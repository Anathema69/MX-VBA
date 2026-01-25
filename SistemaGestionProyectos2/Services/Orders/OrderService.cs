using Postgrest.Responses;
using SistemaGestionProyectos2.Models.Database;
using SistemaGestionProyectos2.Models.DTOs;
using SistemaGestionProyectos2.Services.Core;
using Supabase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SistemaGestionProyectos2.Services.Orders
{
    public class OrderService : BaseSupabaseService
    {
        public OrderService(Client supabaseClient) : base(supabaseClient) { }

        public async Task<List<OrderDb>> GetOrders(int limit = 100, int offset = 0, List<int> filterStatuses = null)
        {
            try
            {
                ModeledResponse<OrderDb> response;

                if (filterStatuses != null && filterStatuses.Count > 0)
                {
                    if (filterStatuses.Count == 3 && filterStatuses.Contains(0) && filterStatuses.Contains(1) && filterStatuses.Contains(2))
                    {
                        response = await SupabaseClient
                            .From<OrderDb>()
                            .Select("*")
                            .Filter("f_orderstat", Postgrest.Constants.Operator.In, filterStatuses.ToArray())
                            .Order("f_podate", Postgrest.Constants.Ordering.Descending)
                            .Range(offset, offset + limit - 1)
                            .Get();
                    }
                    else
                    {
                        response = await SupabaseClient
                            .From<OrderDb>()
                            .Select("*")
                            .Filter("f_orderstat", Postgrest.Constants.Operator.Equals, filterStatuses[0])
                            .Order("f_podate", Postgrest.Constants.Ordering.Descending)
                            .Range(offset, offset + limit - 1)
                            .Get();
                    }
                }
                else
                {
                    response = await SupabaseClient
                        .From<OrderDb>()
                        .Select("*")
                        .Order("f_podate", Postgrest.Constants.Ordering.Descending)
                        .Range(offset, offset + limit - 1)
                        .Get();
                }

                var orders = response?.Models ?? new List<OrderDb>();
                LogSuccess($"Órdenes obtenidas: {orders.Count}");
                return orders;
            }
            catch (Exception ex)
            {
                LogError("Error obteniendo órdenes", ex);
                throw;
            }
        }

        public async Task<OrderDb> GetOrderById(int orderId)
        {
            try
            {
                var response = await SupabaseClient
                    .From<OrderDb>()
                    .Where(x => x.Id == orderId)
                    .Single();
                return response;
            }
            catch (Exception ex)
            {
                LogError($"Error obteniendo orden {orderId}", ex);
                throw;
            }
        }

        public async Task<List<OrderDb>> SearchOrders(string searchTerm)
        {
            try
            {
                var response = await SupabaseClient
                    .From<OrderDb>()
                    .Filter("f_po", Postgrest.Constants.Operator.ILike, $"%{searchTerm}%")
                    .Get();
                return response?.Models ?? new List<OrderDb>();
            }
            catch (Exception ex)
            {
                LogError("Error buscando órdenes", ex);
                throw;
            }
        }

        public async Task<OrderDb> CreateOrder(OrderDb order, int userId = 0)
        {
            try
            {
                if (order.PoDate == null || order.PoDate == default)
                    order.PoDate = DateTime.Now;

                if (order.ProgressPercentage == 0)
                    order.ProgressPercentage = 0;

                if (order.OrderPercentage == 0)
                    order.OrderPercentage = 0;

                order.CreatedBy = userId > 0 ? userId : 1;
                order.UpdatedBy = userId > 0 ? userId : 1;

                LogDebug($"Creando orden PO: {order.Po}");

                var response = await SupabaseClient
                    .From<OrderDb>()
                    .Insert(order);

                if (response?.Models?.Count > 0)
                {
                    LogSuccess($"Orden creada: {order.Po}");
                    return response.Models.First();
                }

                throw new Exception("No se pudo crear la orden");
            }
            catch (Exception ex)
            {
                LogError("Error creando orden", ex);
                throw;
            }
        }

        public async Task<bool> UpdateOrder(OrderDb order, int userId = 0)
        {
            try
            {
                order.UpdatedBy = userId > 0 ? userId : 1;

                var response = await SupabaseClient
                    .From<OrderDb>()
                    .Where(x => x.Id == order.Id)
                    .Set(x => x.Po, order.Po)
                    .Set(x => x.Quote, order.Quote)
                    .Set(x => x.PoDate, order.PoDate)
                    .Set(x => x.ClientId, order.ClientId)
                    .Set(x => x.ContactId, order.ContactId)
                    .Set(x => x.Description, order.Description)
                    .Set(x => x.SalesmanId, order.SalesmanId)
                    .Set(x => x.EstDelivery, order.EstDelivery)
                    .Set(x => x.ProgressPercentage, order.ProgressPercentage)
                    .Set(x => x.OrderPercentage, order.OrderPercentage)
                    .Set(x => x.SaleSubtotal, order.SaleSubtotal)
                    .Set(x => x.SaleTotal, order.SaleTotal)
                    .Set(x => x.Expense, order.Expense)
                    .Set(x => x.OrderStatus, order.OrderStatus)
                    .Set(x => x.UpdatedBy, order.UpdatedBy)
                    .Set(x => x.GastoOperativo, order.GastoOperativo)
                    .Set(x => x.GastoIndirecto, order.GastoIndirecto)
                    .Update();

                bool success = response?.Models?.Count > 0;
                if (success) LogSuccess($"Orden actualizada: {order.Id}");
                return success;
            }
            catch (Exception ex)
            {
                LogError($"Error actualizando orden {order.Id}", ex);
                return false;
            }
        }

        public async Task<(bool Success, string Message)> DeleteOrderWithAudit(int orderId, int deletedBy, string reason = "Orden creada por error")
        {
            try
            {
                LogDebug($"🗑️ Intentando eliminar orden {orderId} con auditoría");

                // Llamar a la función SQL que valida y elimina
                var response = await SupabaseClient.Rpc<List<DeleteOrderResult>>(
                    "delete_order_with_audit",
                    new Dictionary<string, object>
                    {
                        { "p_order_id", orderId },
                        { "p_deleted_by", deletedBy },
                        { "p_reason", reason }
                    });

                if (response != null && response.Count > 0)
                {
                    var result = response[0];
                    if (result.Success)
                    {
                        LogSuccess($"✅ {result.Message}");
                    }
                    else
                    {
                        LogDebug($"⚠️ {result.Message}");
                    }
                    return (result.Success, result.Message);
                }

                return (false, "No se recibió respuesta del servidor");
            }
            catch (Exception ex)
            {
                LogError($"Error eliminando orden {orderId}", ex);
                return (false, $"Error: {ex.Message}");
            }
        }

        public async Task<(bool CanDelete, string Reason)> CanDeleteOrder(int orderId)
        {
            try
            {
                var response = await SupabaseClient.Rpc<List<CanDeleteOrderResult>>(
                    "can_delete_order",
                    new Dictionary<string, object>
                    {
                        { "p_order_id", orderId }
                    });

                if (response != null && response.Count > 0)
                {
                    var result = response[0];
                    return (result.CanDelete, result.Reason);
                }

                return (false, "No se pudo verificar el estado de la orden");
            }
            catch (Exception ex)
            {
                LogError($"Error verificando si se puede eliminar orden {orderId}", ex);
                return (false, $"Error: {ex.Message}");
            }
        }

        // Mantener el método simple para compatibilidad (deprecated)
        [Obsolete("Use DeleteOrderWithAudit en su lugar")]
        public async Task<bool> DeleteOrder(int orderId)
        {
            var result = await DeleteOrderWithAudit(orderId, 1, "Eliminación legacy");
            return result.Success;
        }

        public async Task<bool> CancelOrder(int orderId)
        {
            try
            {
                LogDebug($"🔄 Cancelando orden {orderId}");

                // Actualizar estado a CANCELADO (status = 5)
                // Usar Filter con el nombre de columna de BD en lugar de Where
                var response = await SupabaseClient
                    .From<OrderDb>()
                    .Filter("f_order", Postgrest.Constants.Operator.Equals, orderId)
                    .Set(x => x.OrderStatus, 5)
                    .Update();

                bool success = response?.Models?.Count > 0;
                if (success)
                {
                    LogSuccess($"✅ Orden {orderId} cancelada exitosamente. Nuevos modelos retornados: {response.Models.Count}");

                    // Log del estado actualizado
                    var updatedOrder = response.Models.FirstOrDefault();
                    if (updatedOrder != null)
                    {
                        LogDebug($"   Estado actualizado: {updatedOrder.OrderStatus}");
                    }
                }
                else
                {
                    LogError($"❌ No se pudo cancelar la orden {orderId}: Respuesta vacía o nula", null);
                    LogDebug($"   Response is null: {response == null}");
                    LogDebug($"   Models is null: {response?.Models == null}");
                    LogDebug($"   Models count: {response?.Models?.Count ?? 0}");
                }

                return success;
            }
            catch (Exception ex)
            {
                LogError($"❌ Error cancelando orden {orderId}", ex);
                LogDebug($"   Exception: {ex.GetType().Name}");
                LogDebug($"   Message: {ex.Message}");
                if (ex.InnerException != null)
                {
                    LogDebug($"   Inner Exception: {ex.InnerException.Message}");
                }
                return false;
            }
        }

        public async Task<List<OrderDb>> GetOrdersByClientId(int clientId)
        {
            try
            {
                var response = await SupabaseClient
                    .From<OrderDb>()
                    .Where(o => o.ClientId == clientId)
                    .Get();
                return response?.Models ?? new List<OrderDb>();
            }
            catch (Exception ex)
            {
                LogError($"Error obteniendo órdenes del cliente {clientId}", ex);
                return new List<OrderDb>();
            }
        }

        public async Task<List<OrderDb>> GetRecentOrders(int limit = 10, int offset = 0)
        {
            try
            {
                var response = await SupabaseClient
                    .From<OrderDb>()
                    .Order(o => o.PoDate, Postgrest.Constants.Ordering.Descending)
                    .Limit(limit)
                    .Offset(offset)
                    .Get();
                return response?.Models ?? new List<OrderDb>();
            }
            catch (Exception ex)
            {
                LogError("Error obteniendo órdenes recientes", ex);
                return new List<OrderDb>();
            }
        }

        public async Task<List<OrderDb>> GetOrdersFiltered(DateTime? fromDate = null, string[] excludeStatuses = null, int limit = 50, int offset = 0)
        {
            try
            {
                var response = await SupabaseClient
                    .From<OrderDb>()
                    .Order(o => o.PoDate, Postgrest.Constants.Ordering.Descending)
                    .Get();

                var orders = response?.Models ?? new List<OrderDb>();

                if (fromDate.HasValue)
                {
                    orders = orders.Where(o => o.PoDate >= fromDate.Value).ToList();
                }

                orders = orders.Skip(offset).Take(limit).ToList();
                return orders;
            }
            catch (Exception ex)
            {
                LogError("Error obteniendo órdenes filtradas", ex);
                return new List<OrderDb>();
            }
        }

        public async Task<List<OrderStatusDb>> GetOrderStatuses()
        {
            try
            {
                var response = await SupabaseClient
                    .From<OrderStatusDb>()
                    .Where(s => s.IsActive == true)
                    .Order("display_order", Postgrest.Constants.Ordering.Ascending)
                    .Get();
                return response?.Models ?? new List<OrderStatusDb>();
            }
            catch (Exception ex)
            {
                LogError("Error obteniendo estados de órdenes", ex);
                throw;
            }
        }

        public async Task<string> GetStatusName(int statusId)
        {
            try
            {
                var statuses = await GetOrderStatuses();
                var status = statuses?.FirstOrDefault(s => s.Id == statusId);
                return status?.Name ?? "DESCONOCIDO";
            }
            catch
            {
                return "DESCONOCIDO";
            }
        }

        public async Task<int> GetStatusIdByName(string statusName)
        {
            try
            {
                var statuses = await GetOrderStatuses();
                var status = statuses?.FirstOrDefault(s => s.Name.Equals(statusName, StringComparison.OrdinalIgnoreCase));
                return status?.Id ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        public async Task<bool> CanCreateInvoice(int orderId)
        {
            try
            {
                var order = await GetOrderById(orderId);
                if (order == null) return false;

                var numStatus = order.OrderStatus ?? 0;
                return numStatus >= 1 && numStatus <= 4;
            }
            catch
            {
                return false;
            }
        }

        #region Vista con Gastos Calculados

        /// <summary>
        /// Obtiene órdenes con gastos de material calculados desde la vista v_order_gastos
        /// </summary>
        public async Task<List<OrderGastosViewDb>> GetOrdersWithGastos(int limit = 100, int offset = 0, List<int> filterStatuses = null)
        {
            try
            {
                ModeledResponse<OrderGastosViewDb> response;

                if (filterStatuses != null && filterStatuses.Count > 0)
                {
                    response = await SupabaseClient
                        .From<OrderGastosViewDb>()
                        .Select("*")
                        .Filter("f_orderstat", Postgrest.Constants.Operator.In, filterStatuses.ToArray())
                        .Order("f_podate", Postgrest.Constants.Ordering.Descending)
                        .Range(offset, offset + limit - 1)
                        .Get();
                }
                else
                {
                    response = await SupabaseClient
                        .From<OrderGastosViewDb>()
                        .Select("*")
                        .Order("f_podate", Postgrest.Constants.Ordering.Descending)
                        .Range(offset, offset + limit - 1)
                        .Get();
                }

                var orders = response?.Models ?? new List<OrderGastosViewDb>();
                LogSuccess($"Órdenes con gastos obtenidas: {orders.Count}");
                return orders;
            }
            catch (Exception ex)
            {
                LogError("Error obteniendo órdenes con gastos", ex);
                throw;
            }
        }

        /// <summary>
        /// Obtiene una orden específica con gastos calculados
        /// </summary>
        public async Task<OrderGastosViewDb> GetOrderWithGastosById(int orderId)
        {
            try
            {
                var response = await SupabaseClient
                    .From<OrderGastosViewDb>()
                    .Filter("f_order", Postgrest.Constants.Operator.Equals, orderId)
                    .Single();
                return response;
            }
            catch (Exception ex)
            {
                LogError($"Error obteniendo orden con gastos {orderId}", ex);
                throw;
            }
        }

        #endregion

        #region Gastos Operativos v2.0

        /// <summary>
        /// Obtiene los gastos operativos detallados de una orden
        /// </summary>
        public async Task<List<OrderGastoOperativoDb>> GetGastosOperativos(int orderId)
        {
            try
            {
                var response = await SupabaseClient
                    .From<OrderGastoOperativoDb>()
                    .Where(g => g.OrderId == orderId)
                    .Order("fecha_gasto", Postgrest.Constants.Ordering.Descending)
                    .Get();

                return response?.Models ?? new List<OrderGastoOperativoDb>();
            }
            catch (Exception ex)
            {
                LogError($"Error obteniendo gastos operativos de orden {orderId}", ex);
                return new List<OrderGastoOperativoDb>();
            }
        }

        /// <summary>
        /// Agrega un gasto operativo a una orden y devuelve el registro creado
        /// </summary>
        public async Task<OrderGastoOperativoDb> AddGastoOperativo(int orderId, decimal monto, string descripcion, int userId)
        {
            try
            {
                LogDebug($"Insertando gasto operativo: orden={orderId}, monto={monto}, desc={descripcion}");

                var ahora = DateTime.Now;
                var gasto = new OrderGastoOperativoDb
                {
                    OrderId = orderId,
                    Monto = monto,
                    Descripcion = descripcion,
                    FechaGasto = ahora,
                    CreatedAt = ahora,
                    CreatedBy = userId
                };

                var response = await SupabaseClient
                    .From<OrderGastoOperativoDb>()
                    .Insert(gasto);

                if (response?.Models?.Count > 0)
                {
                    LogSuccess($"Gasto operativo agregado a orden {orderId}: {monto:C} (ID: {response.Models[0].Id})");
                    return response.Models[0];
                }

                LogError($"Insert de gasto operativo no retornó registros para orden {orderId}", null);
                throw new Exception($"No se pudo insertar el gasto operativo en orden {orderId}");
            }
            catch (Exception ex)
            {
                LogError($"Error agregando gasto operativo a orden {orderId}", ex);
                throw; // Re-lanzar para que el llamador sepa que falló
            }
        }

        /// <summary>
        /// Elimina un gasto operativo
        /// </summary>
        public async Task<bool> DeleteGastoOperativo(int gastoId, int orderId, int userId)
        {
            try
            {
                await SupabaseClient
                    .From<OrderGastoOperativoDb>()
                    .Where(g => g.Id == gastoId)
                    .Delete();

                // Recalcular total
                await RecalcularGastoOperativo(orderId, userId);
                LogSuccess($"Gasto operativo {gastoId} eliminado");
                return true;
            }
            catch (Exception ex)
            {
                LogError($"Error eliminando gasto operativo {gastoId}", ex);
                return false;
            }
        }

        /// <summary>
        /// Actualiza un gasto operativo existente
        /// </summary>
        public async Task<bool> UpdateGastoOperativo(int gastoId, decimal monto, string descripcion, int orderId, int userId)
        {
            try
            {
                var response = await SupabaseClient
                    .From<OrderGastoOperativoDb>()
                    .Where(g => g.Id == gastoId)
                    .Single();

                if (response != null)
                {
                    response.Monto = monto;
                    response.Descripcion = descripcion;
                    response.UpdatedBy = userId;
                    response.UpdatedAt = DateTime.Now;

                    await SupabaseClient
                        .From<OrderGastoOperativoDb>()
                        .Update(response);

                    // Recalcular total de gastos operativos
                    await RecalcularGastoOperativo(orderId, userId);
                    LogSuccess($"Gasto operativo {gastoId} actualizado");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                LogError($"Error actualizando gasto operativo {gastoId}", ex);
                return false;
            }
        }

        /// <summary>
        /// Recalcula el total de gastos operativos de una orden
        /// </summary>
        private async Task RecalcularGastoOperativo(int orderId, int userId)
        {
            try
            {
                var gastos = await GetGastosOperativos(orderId);
                var total = gastos.Sum(g => g.Monto);

                await SupabaseClient
                    .From<OrderDb>()
                    .Where(o => o.Id == orderId)
                    .Set(o => o.GastoOperativo, total)
                    .Set(o => o.UpdatedBy, userId)
                    .Update();

                LogDebug($"Gasto operativo total de orden {orderId} actualizado: {total:C}");
            }
            catch (Exception ex)
            {
                LogError($"Error recalculando gasto operativo de orden {orderId}", ex);
            }
        }

        #endregion

        #region Gastos Indirectos v2.1

        /// <summary>
        /// Obtiene los gastos indirectos detallados de una orden
        /// </summary>
        public async Task<List<OrderGastoIndirectoDb>> GetGastosIndirectos(int orderId)
        {
            try
            {
                var response = await SupabaseClient
                    .From<OrderGastoIndirectoDb>()
                    .Where(g => g.OrderId == orderId)
                    .Order("fecha_gasto", Postgrest.Constants.Ordering.Descending)
                    .Get();

                return response?.Models ?? new List<OrderGastoIndirectoDb>();
            }
            catch (Exception ex)
            {
                LogError($"Error obteniendo gastos indirectos de orden {orderId}", ex);
                return new List<OrderGastoIndirectoDb>();
            }
        }

        /// <summary>
        /// Agrega un gasto indirecto a una orden y devuelve el registro creado
        /// </summary>
        public async Task<OrderGastoIndirectoDb> AddGastoIndirecto(int orderId, decimal monto, string descripcion, int userId)
        {
            try
            {
                LogDebug($"Insertando gasto indirecto: orden={orderId}, monto={monto}, desc={descripcion}");

                var ahora = DateTime.Now;
                var gasto = new OrderGastoIndirectoDb
                {
                    OrderId = orderId,
                    Monto = monto,
                    Descripcion = descripcion,
                    FechaGasto = ahora,
                    CreatedAt = ahora,
                    CreatedBy = userId
                };

                var response = await SupabaseClient
                    .From<OrderGastoIndirectoDb>()
                    .Insert(gasto);

                if (response?.Models?.Count > 0)
                {
                    LogSuccess($"Gasto indirecto agregado a orden {orderId}: {monto:C} (ID: {response.Models[0].Id})");
                    return response.Models[0];
                }

                LogError($"Insert de gasto indirecto no retornó registros para orden {orderId}", null);
                throw new Exception($"No se pudo insertar el gasto indirecto en orden {orderId}");
            }
            catch (Exception ex)
            {
                LogError($"Error agregando gasto indirecto a orden {orderId}", ex);
                throw;
            }
        }

        /// <summary>
        /// Elimina un gasto indirecto
        /// </summary>
        public async Task<bool> DeleteGastoIndirecto(int gastoId, int orderId, int userId)
        {
            try
            {
                await SupabaseClient
                    .From<OrderGastoIndirectoDb>()
                    .Where(g => g.Id == gastoId)
                    .Delete();

                // Recalcular total
                await RecalcularGastoIndirecto(orderId, userId);
                LogSuccess($"Gasto indirecto {gastoId} eliminado");
                return true;
            }
            catch (Exception ex)
            {
                LogError($"Error eliminando gasto indirecto {gastoId}", ex);
                return false;
            }
        }

        /// <summary>
        /// Actualiza un gasto indirecto existente
        /// </summary>
        public async Task<bool> UpdateGastoIndirecto(int gastoId, decimal monto, string descripcion, int orderId, int userId)
        {
            try
            {
                var response = await SupabaseClient
                    .From<OrderGastoIndirectoDb>()
                    .Where(g => g.Id == gastoId)
                    .Single();

                if (response != null)
                {
                    response.Monto = monto;
                    response.Descripcion = descripcion;
                    response.UpdatedBy = userId;
                    response.UpdatedAt = DateTime.Now;

                    await SupabaseClient
                        .From<OrderGastoIndirectoDb>()
                        .Update(response);

                    // Recalcular total de gastos indirectos
                    await RecalcularGastoIndirecto(orderId, userId);
                    LogSuccess($"Gasto indirecto {gastoId} actualizado");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                LogError($"Error actualizando gasto indirecto {gastoId}", ex);
                return false;
            }
        }

        /// <summary>
        /// Recalcula el total de gastos indirectos de una orden
        /// </summary>
        private async Task RecalcularGastoIndirecto(int orderId, int userId)
        {
            try
            {
                var gastos = await GetGastosIndirectos(orderId);
                var total = gastos.Sum(g => g.Monto);

                await SupabaseClient
                    .From<OrderDb>()
                    .Where(o => o.Id == orderId)
                    .Set(o => o.GastoIndirecto, total)
                    .Set(o => o.UpdatedBy, userId)
                    .Update();

                LogDebug($"Gasto indirecto total de orden {orderId} actualizado: {total:C}");
            }
            catch (Exception ex)
            {
                LogError($"Error recalculando gasto indirecto de orden {orderId}", ex);
            }
        }

        #endregion
    }
}
