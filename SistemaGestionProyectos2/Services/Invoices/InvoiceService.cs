using Postgrest.Responses;
using SistemaGestionProyectos2.Models.Database;
using SistemaGestionProyectos2.Services.Core;
using Supabase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SistemaGestionProyectos2.Services.Invoices
{
    public class InvoiceService : BaseSupabaseService
    {
        public InvoiceService(Client supabaseClient) : base(supabaseClient) { }

        public async Task<List<InvoiceDb>> GetInvoicesByOrder(int orderId)
        {
            try
            {
                var response = await SupabaseClient
                    .From<InvoiceDb>()
                    .Where(x => x.OrderId == orderId)
                    .Order("f_invoicedate", Postgrest.Constants.Ordering.Ascending)
                    .Get();

                var invoices = response?.Models ?? new List<InvoiceDb>();
                LogSuccess($"Facturas obtenidas para orden {orderId}: {invoices.Count}");
                return invoices;
            }
            catch (Exception ex)
            {
                LogError($"Error obteniendo facturas de orden {orderId}", ex);
                throw;
            }
        }

        public async Task<Dictionary<int, decimal>> GetInvoicedTotalsByOrders(List<int> orderIds)
        {
            var result = new Dictionary<int, decimal>();

            try
            {
                if (orderIds == null || !orderIds.Any())
                    return result;

                var invoices = await SupabaseClient
                    .From<InvoiceDb>()
                    .Filter("f_order", Postgrest.Constants.Operator.In, orderIds)
                    .Get();

                if (invoices?.Models != null)
                {
                    var grouped = invoices.Models
                        .Where(i => i.OrderId.HasValue && i.Total.HasValue)
                        .GroupBy(i => i.OrderId.Value)
                        .Select(g => new
                        {
                            OrderId = g.Key,
                            Total = g.Sum(i => i.Total ?? 0)
                        });

                    foreach (var item in grouped)
                    {
                        result[item.OrderId] = item.Total;
                    }
                }

                LogSuccess($"Totales facturados calculados para {result.Count} órdenes");

                // Asegurar que todas las órdenes tengan un valor
                foreach (var orderId in orderIds)
                {
                    if (!result.ContainsKey(orderId))
                    {
                        result[orderId] = 0;
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                LogError("Error obteniendo totales facturados", ex);

                foreach (var orderId in orderIds)
                {
                    result[orderId] = 0;
                }

                return result;
            }
        }

        public async Task<InvoiceDb> CreateInvoice(InvoiceDb invoice, int userId = 0)
        {
            try
            {
                if (invoice.Total == null || invoice.Total == 0)
                {
                    invoice.Total = (invoice.Subtotal ?? 0) * 1.16m;
                }

                invoice.CreatedBy = userId > 0 ? userId : 1;

                if (invoice.InvoiceStatus == null)
                {
                    invoice.InvoiceStatus = 1;
                }

                LogDebug($"Creando factura para orden {invoice.OrderId}");

                var response = await SupabaseClient
                    .From<InvoiceDb>()
                    .Insert(invoice);

                if (response?.Models?.Count > 0)
                {
                    LogSuccess($"Factura creada: {invoice.Folio}");
                    return response.Models.First();
                }

                throw new Exception("No se pudo crear la factura");
            }
            catch (Exception ex)
            {
                LogError("Error creando factura", ex);
                throw;
            }
        }

        public async Task<bool> UpdateInvoice(InvoiceDb invoice, int userId = 0)
        {
            try
            {
                invoice.Total = (invoice.Subtotal ?? 0) * 1.16m;

                // Actualizar estado basado en fechas
                if (invoice.PaymentDate.HasValue)
                {
                    invoice.InvoiceStatus = 4; // PAGADA
                }
                else if (invoice.ReceptionDate.HasValue)
                {
                    if (invoice.DueDate.HasValue && DateTime.Now > invoice.DueDate.Value)
                    {
                        invoice.InvoiceStatus = 3; // VENCIDA
                    }
                    else
                    {
                        invoice.InvoiceStatus = 2; // PENDIENTE
                    }
                }
                else
                {
                    invoice.InvoiceStatus = 1; // CREADA
                }

                var response = await SupabaseClient
                    .From<InvoiceDb>()
                    .Where(x => x.Id == invoice.Id)
                    .Set(x => x.Folio, invoice.Folio)
                    .Set(x => x.InvoiceDate, invoice.InvoiceDate)
                    .Set(x => x.ReceptionDate, invoice.ReceptionDate)
                    .Set(x => x.Subtotal, invoice.Subtotal)
                    .Set(x => x.Total, invoice.Total)
                    .Set(x => x.InvoiceStatus, invoice.InvoiceStatus)
                    .Set(x => x.PaymentDate, invoice.PaymentDate)
                    .Set(x => x.DueDate, invoice.DueDate)
                    .Update();

                bool success = response?.Models?.Count > 0;
                if (success) LogSuccess($"Factura actualizada: {invoice.Folio}");
                return success;
            }
            catch (Exception ex)
            {
                LogError($"Error actualizando factura {invoice.Id}", ex);
                return false;
            }
        }

        public async Task<bool> DeleteInvoice(int invoiceId, int userId = 0)
        {
            try
            {
                await SupabaseClient
                    .From<InvoiceDb>()
                    .Where(x => x.Id == invoiceId)
                    .Delete();

                LogSuccess($"Factura eliminada: {invoiceId}");
                return true;
            }
            catch (Exception ex)
            {
                LogError($"Error eliminando factura {invoiceId}", ex);
                return false;
            }
        }

        public async Task<List<InvoiceStatusDb>> GetInvoiceStatuses()
        {
            try
            {
                var response = await SupabaseClient
                    .From<InvoiceStatusDb>()
                    .Order("display_order", Postgrest.Constants.Ordering.Ascending)
                    .Get();

                var statuses = response?.Models ?? new List<InvoiceStatusDb>();
                LogSuccess($"Estados de factura obtenidos: {statuses.Count}");
                return statuses;
            }
            catch (Exception ex)
            {
                LogError("Error obteniendo estados de factura", ex);
                throw;
            }
        }
    }
}
