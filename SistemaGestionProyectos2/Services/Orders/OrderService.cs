using Postgrest.Responses;
using SistemaGestionProyectos2.Models.Database;
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

        public async Task<bool> DeleteOrder(int orderId)
        {
            try
            {
                await SupabaseClient
                    .From<OrderDb>()
                    .Where(x => x.Id == orderId)
                    .Delete();

                LogSuccess($"Orden eliminada: {orderId}");
                return true;
            }
            catch (Exception ex)
            {
                LogError($"Error eliminando orden {orderId}", ex);
                return false;
            }
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
    }
}
