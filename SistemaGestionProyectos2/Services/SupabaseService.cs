using Microsoft.Extensions.Configuration;
using Postgrest.Attributes;
using Postgrest.Interfaces;
using Postgrest.Models;
using Postgrest.Responses;
using SistemaGestionProyectos2.Models;
using SistemaGestionProyectos2.ViewModels;
using Supabase;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using static Postgrest.Constants;

namespace SistemaGestionProyectos2.Services
{
    // Singleton Service para Supabase
    public class SupabaseService
    {
        private static SupabaseService _instance;
        private static readonly object _lock = new object();
        private Client _supabaseClient;
        private IConfiguration _configuration;
        private bool _isInitialized = false;

        // Singleton Pattern
        public static SupabaseService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new SupabaseService();
                        }
                    }
                }
                return _instance;
            }
        }

        private SupabaseService()
        {
            InitializeAsync().Wait();
        }

        private async Task InitializeAsync()
        {
            try
            {
                // Cargar configuración
                var builder = new ConfigurationBuilder()
                    .SetBasePath(System.IO.Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

                _configuration = builder.Build();

                var url = _configuration["Supabase:Url"];
                var key = _configuration["Supabase:AnonKey"];

                if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(key))
                {
                    throw new Exception("Credenciales de Supabase no configuradas en appsettings.json");
                }

                var options = new SupabaseOptions
                {
                    AutoConnectRealtime = false
                };

                _supabaseClient = new Client(url, key, options);
                await _supabaseClient.InitializeAsync();

                _isInitialized = true;
                System.Diagnostics.Debug.WriteLine("✅ Supabase inicializado correctamente");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error inicializando Supabase: {ex.Message}");
                throw;
            }
        }

        public Client GetClient()
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("Supabase no está inicializado");
            }
            return _supabaseClient;
        }

        // ===============================================
        // MÉTODOS PARA ÓRDENES
        // ===============================================

        public async Task<List<OrderDb>> GetOrders(int limit = 100, int offset = 0, List<int> filterStatuses = null)
        {
            try
            {
                ModeledResponse<OrderDb> response;

                if (filterStatuses != null && filterStatuses.Count > 0)
                {
                    // Para el coordinador: usar Filter con OR
                    if (filterStatuses.Count == 3 && filterStatuses.Contains(0) && filterStatuses.Contains(1) && filterStatuses.Contains(2))
                    {
                        // Construir filtro OR para estados 0, 1, 2
                        response = await _supabaseClient
                            .From<OrderDb>()
                            .Select("*")
                            .Filter("f_orderstat", Postgrest.Constants.Operator.In, filterStatuses.ToArray())
                            .Order("f_podate", Postgrest.Constants.Ordering.Descending)
                            .Range(offset, offset + limit - 1)
                            .Get();
                    }
                    else
                    {
                        // Para filtros individuales
                        response = await _supabaseClient
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
                    // Sin filtro - obtener todas (para admin)
                    response = await _supabaseClient
                        .From<OrderDb>()
                        .Select("*")
                        .Order("f_podate", Postgrest.Constants.Ordering.Descending)
                        .Range(offset, offset + limit - 1)
                        .Get();
                }

                var orders = response?.Models ?? new List<OrderDb>();

                System.Diagnostics.Debug.WriteLine($"📊 Órdenes obtenidas: {orders.Count}");
                if (filterStatuses != null)
                {
                    System.Diagnostics.Debug.WriteLine($"   Con filtro de estados: [{string.Join(", ", filterStatuses)}]");
                }

                return orders;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error obteniendo órdenes: {ex.Message}");
                throw;
            }
        }

        public async Task<OrderDb> GetOrderById(int orderId)
        {
            try
            {
                var response = await _supabaseClient
                    .From<OrderDb>()
                    .Where(x => x.Id == orderId)
                    .Single();

                return response;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error obteniendo orden {orderId}: {ex.Message}");
                throw;
            }
        }

        public async Task<List<OrderDb>> SearchOrders(string searchTerm)
        {
            try
            {
                var response = await _supabaseClient
                    .From<OrderDb>()
                    .Filter("f_po", Postgrest.Constants.Operator.ILike, $"%{searchTerm}%")
                    .Get();

                return response?.Models ?? new List<OrderDb>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error buscando órdenes: {ex.Message}");
                throw;
            }
        }

        public async Task<OrderDb> CreateOrder(OrderDb order, int userId = 0)
        {
            try
            {
                // Asegurar fechas válidas
                if (order.PoDate == null || order.PoDate == default)
                    order.PoDate = DateTime.Now;

                // Asegurar valores por defecto
                if (order.ProgressPercentage == 0)
                    order.ProgressPercentage = 0;

                if (order.OrderPercentage == 0)
                    order.OrderPercentage = 0;

                // ⭐ CAMBIO IMPORTANTE: Establecer los campos de auditoría
                order.CreatedBy = userId > 0 ? userId : 1;
                order.UpdatedBy = userId > 0 ? userId : 1;

                // Log para debug
                System.Diagnostics.Debug.WriteLine($"Creando orden en Supabase:");
                System.Diagnostics.Debug.WriteLine($"  PO: {order.Po}");
                System.Diagnostics.Debug.WriteLine($"  Cliente: {order.ClientId}");
                System.Diagnostics.Debug.WriteLine($"  Created By: {order.CreatedBy}");
                System.Diagnostics.Debug.WriteLine($"  Updated By: {order.UpdatedBy}");

                var response = await _supabaseClient
                    .From<OrderDb>()
                    .Insert(order);

                if (response?.Models?.Count > 0)
                {
                    return response.Models.First();
                }

                throw new Exception("No se pudo crear la orden");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creando orden: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> UpdateOrder(OrderDb order, int userId = 0)
        {
            try
            {
                // ⭐ CAMBIO IMPORTANTE: Establecer quien actualiza
                order.UpdatedBy = userId > 0 ? userId : 1;

                System.Diagnostics.Debug.WriteLine($"📝 Actualizando orden {order.Id}:");
                System.Diagnostics.Debug.WriteLine($"   Progress: {order.ProgressPercentage}%");
                System.Diagnostics.Debug.WriteLine($"   Order%: {order.OrderPercentage}%");
                System.Diagnostics.Debug.WriteLine($"   Estado: {order.OrderStatus}");
                System.Diagnostics.Debug.WriteLine($"   👤 Updated By: {order.UpdatedBy}");

                var response = await _supabaseClient
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

                return response?.Models?.Count > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error actualizando orden: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DeleteOrder(int orderId)
        {
            try
            {
                await _supabaseClient
                    .From<OrderDb>()
                    .Where(x => x.Id == orderId)
                    .Delete();

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error eliminando orden: {ex.Message}");
                return false;
            }
        }

        // ===============================================
        // MÉTODOS PARA CLIENTES
        // ===============================================

        public async Task<List<ClientDb>> GetClients()
        {
            try
            {
                var response = await _supabaseClient
                    .From<ClientDb>()
                    .Where(c => c.IsActive == true)  // Agregar este filtro
                    .Order("f_name", Postgrest.Constants.Ordering.Ascending)
                    .Get();

                return response?.Models ?? new List<ClientDb>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}");
                return new List<ClientDb>();
            }
        }

        // ===============================================
        // MÉTODOS PARA CONTACTOS
        // ===============================================

        public async Task<List<ContactDb>> GetContactsByClientId(int clientId)
        {
            try
            {
                var response = await _supabaseClient
                    .From<ContactDb>()
                    .Where(c => c.ClientId == clientId)
                    .Where(c => c.IsActive == true)  // Agregar este filtro
                    .Order("is_primary", Postgrest.Constants.Ordering.Descending)
                    .Order("f_contactname", Postgrest.Constants.Ordering.Ascending)
                    .Get();

                return response?.Models ?? new List<ContactDb>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}");
                return new List<ContactDb>();
            }
        }

        public async Task<ContactDb> AddContact(ContactDb contact)
        {
            try
            {
                contact.IsActive = true;
                System.Diagnostics.Debug.WriteLine($"📇 Creando contacto: {contact.ContactName}");

                var response = await _supabaseClient
                    .From<ContactDb>()
                    .Insert(contact);

                if (response?.Models?.Count > 0)
                {
                    return response.Models.First();
                }
                throw new Exception("No se pudo crear el contacto");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error creando contacto: {ex.Message}");
                throw;
            }
        }

        // Método para actualizar un contacto existente
        public async Task<ContactDb> UpdateContact(ContactDb contact)
        {
            try
            {
                // Si el contacto se marca como principal, desmarcar otros
                if (contact.IsPrimary)
                {
                    var allContacts = await GetContactsByClientId(contact.ClientId);
                    foreach (var c in allContacts.Where(c => c.Id != contact.Id && c.IsPrimary))
                    {
                        c.IsPrimary = false;
                        await _supabaseClient
                            .From<ContactDb>()
                            .Where(x => x.Id == c.Id)
                            .Set(x => x.IsPrimary, false)
                            .Update();
                    }
                }

                var response = await _supabaseClient
                    .From<ContactDb>()
                    .Where(c => c.Id == contact.Id)
                    .Set(c => c.ContactName, contact.ContactName)
                    .Set(c => c.Position, contact.Position)
                    .Set(c => c.Email, contact.Email)
                    .Set(c => c.Phone, contact.Phone)
                    .Set(c => c.IsPrimary, contact.IsPrimary)
                    .Set(c => c.IsActive, contact.IsActive)
                    .Update();

                return response.Models.FirstOrDefault();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al actualizar contacto: {ex.Message}", ex);
            }
        }

        // Método para eliminar un contacto
        public async Task<bool> DeleteContact(int contactId)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"🗑️ Desactivando contacto ID: {contactId}");

                // Soft delete - cambiar is_active a false
                var response = await _supabaseClient
                    .From<ContactDb>()
                    .Where(c => c.Id == contactId)
                    .Set(c => c.IsActive, false)
                    .Update();

                return response?.Models?.Count > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error desactivando contacto: {ex.Message}");
                return false;
            }
        }



        public async Task<List<ContactDb>> GetContactsByClient(int clientId)
        {
            try
            {
                var response = await _supabaseClient
                    .From<ContactDb>()
                    .Where(x => x.ClientId == clientId)
                    .Get();

                return response?.Models ?? new List<ContactDb>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error obteniendo contactos: {ex.Message}");
                throw;
            }
        }

        // ===============================================
        // MÉTODOS PARA USUARIOS
        // ===============================================

        public async Task<UserDb> GetUserByUsername(string username)
        {
            try
            {
                var response = await _supabaseClient
                    .From<UserDb>()
                    .Where(x => x.Username == username)
                    .Single();

                return response;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error obteniendo usuario: {ex.Message}");
                return null;
            }
        }

        public async Task<(bool Success, UserDb User, string Message)> AuthenticateUser(string username, string password)
        {
            try
            {
                // Buscar usuario por username
                var response = await _supabaseClient
                    .From<UserDb>()
                    .Where(x => x.Username == username)
                    .Single();

                if (response == null)
                {
                    return (false, null, "Usuario no encontrado");
                }

                // Verificar contraseña con BCrypt
                bool isPasswordValid = BCrypt.Net.BCrypt.Verify(password, response.PasswordHash);

                if (!isPasswordValid)
                {
                    return (false, null, "Contraseña incorrecta");
                }

                // Verificar si el usuario está activo
                if (!response.IsActive)
                {
                    return (false, null, "Usuario desactivado");
                }

                // Actualizar último login
                await _supabaseClient
                    .From<UserDb>()
                    .Where(x => x.Id == response.Id)
                    .Set(x => x.LastLogin, DateTime.Now)
                    .Update();

                return (true, response, "Login exitoso");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en autenticación: {ex.Message}");
                return (false, null, $"Error: {ex.Message}");
            }
        }

        public async Task<List<VendorDb>> GetVendors()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("📋 Obteniendo vendedores de t_vendor...");

                var response = await _supabaseClient
                    .From<VendorTableDb>()
                    .Where(x => x.IsActive == true)
                    .Order("f_vendorname", Postgrest.Constants.Ordering.Ascending)
                    .Get();

                var vendors = response?.Models ?? new List<VendorTableDb>();

                System.Diagnostics.Debug.WriteLine($"✅ Vendedores encontrados: {vendors.Count}");

                foreach (var v in vendors)
                {
                    System.Diagnostics.Debug.WriteLine($"   - {v.VendorName} (ID: {v.Id})");
                }

                // Convertir VendorTableDb a VendorDb para compatibilidad
                return vendors.Select(v => new VendorDb
                {
                    Id = v.Id,
                    VendorName = v.VendorName
                }).ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error obteniendo vendedores: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"   Stack: {ex.StackTrace}");
                throw new Exception($"Error al cargar vendedores: {ex.Message}", ex);
            }
        }

        // ===============================================
        // MÉTODOS PARA ESTADOS DE ÓRDENES
        // ===============================================

        public async Task<List<OrderStatusDb>> GetOrderStatuses()
        {
            try
            {
                var response = await _supabaseClient
                    .From<OrderStatusDb>()
                    .Order("display_order", Postgrest.Constants.Ordering.Ascending)
                    .Get();

                return response?.Models ?? new List<OrderStatusDb>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error obteniendo estados: {ex.Message}");
                throw;
            }
        }

        // ===============================================
        // MÉTODOS DE UTILIDAD
        // ===============================================

        public async Task<bool> TestConnection()
        {
            try
            {
                var response = await _supabaseClient
                    .From<OrderDb>()
                    .Select("*")
                    .Limit(1)
                    .Get();

                return true;
            }
            catch
            {
                return false;
            }
        }

        // ===============================================
        // MÉTODOS PARA FACTURAS
        // ===============================================
        public async Task<List<InvoiceDb>> GetInvoicesByOrder(int orderId)
        {
            try
            {
                var response = await _supabaseClient
                    .From<InvoiceDb>()
                    .Where(x => x.OrderId == orderId)
                    .Order("f_invoicedate", Postgrest.Constants.Ordering.Ascending)
                    .Get();

                return response?.Models ?? new List<InvoiceDb>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error obteniendo facturas: {ex.Message}");
                throw;
            }
        }

        // Agregar en SupabaseService.cs después del método GetInvoicesByOrder

        public async Task<Dictionary<int, decimal>> GetInvoicedTotalsByOrders(List<int> orderIds)
        {
            var result = new Dictionary<int, decimal>();

            try
            {
                if (orderIds == null || !orderIds.Any())
                    return result;

                // Obtener todas las facturas de las órdenes especificadas
                var invoices = await _supabaseClient
                    .From<InvoiceDb>()
                    .Filter("f_order", Postgrest.Constants.Operator.In, orderIds)
                    .Get();

                if (invoices?.Models != null)
                {
                    // Agrupar por orden y sumar totales
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

                System.Diagnostics.Debug.WriteLine($"📊 Totales facturados calculados para {result.Count} órdenes");

                // Asegurar que todas las órdenes tengan un valor (0 si no tienen facturas)
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
                System.Diagnostics.Debug.WriteLine($"Error obteniendo totales facturados: {ex.Message}");

                // Retornar diccionario con ceros en caso de error
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
                // Calcular el total con IVA si no está establecido
                if (invoice.Total == null || invoice.Total == 0)
                {
                    invoice.Total = (invoice.Subtotal ?? 0) * 1.16m;
                }

                // Establecer el usuario creador
                invoice.CreatedBy = userId > 0 ? userId : 1;

                // Estado inicial: CREADA (1)
                if (invoice.InvoiceStatus == null)
                {
                    invoice.InvoiceStatus = 1;
                }

                var response = await _supabaseClient
                    .From<InvoiceDb>()
                    .Insert(invoice);

                if (response?.Models?.Count > 0)
                {
                    return response.Models.First();
                }

                throw new Exception("No se pudo crear la factura");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creando factura: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> UpdateInvoice(InvoiceDb invoice, int userId = 0)
        {
            try
            {
                // Recalcular el total con IVA
                invoice.Total = (invoice.Subtotal ?? 0) * 1.16m;

                // Actualizar el estado basado en las fechas
                if (invoice.PaymentDate.HasValue)
                {
                    invoice.InvoiceStatus = 4; // PAGADA
                }
                else if (invoice.ReceptionDate.HasValue)
                {
                    // Verificar si está vencida
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

                var response = await _supabaseClient
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

                return response?.Models?.Count > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error actualizando factura: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DeleteInvoice(int invoiceId, int userId = 0)
        {
            try
            {
                // TODO: Registrar en tabla de auditoría antes de eliminar
                await _supabaseClient
                    .From<InvoiceDb>()
                    .Where(x => x.Id == invoiceId)
                    .Delete();

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error eliminando factura: {ex.Message}");
                return false;
            }
        }

        public async Task<List<InvoiceStatusDb>> GetInvoiceStatuses()
        {
            try
            {
                var response = await _supabaseClient
                    .From<InvoiceStatusDb>()
                    .Order("display_order", Postgrest.Constants.Ordering.Ascending)
                    .Get();

                return response?.Models ?? new List<InvoiceStatusDb>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error obteniendo estados de factura: {ex.Message}");
                throw;
            }
        }

        // ===============================================
        // MÉTODOS PARA MANEJO DE ESTADOS
        // ===============================================

        public async Task<bool> CheckAndUpdateOrderStatus(int orderId, int userId = 0)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"🔄 Verificando estado de orden {orderId}...");

                // Obtener la orden
                var order = await GetOrderById(orderId);
                if (order == null) return false;

                // Obtener el estado actual
                var currentStatusName = await GetStatusName(order.OrderStatus ?? 0);
                System.Diagnostics.Debug.WriteLine($"   Estado actual: {currentStatusName}");

                // Si ya está en estado final, no hacer nada
                if (currentStatusName == "CANCELADA" || currentStatusName == "COMPLETADA")
                {
                    System.Diagnostics.Debug.WriteLine($"   ℹ️ Orden en estado final, no se requieren cambios");
                    return false;
                }

                // Obtener todas las facturas de la orden
                var invoices = await GetInvoicesByOrder(orderId);

                // Calcular totales
                decimal totalInvoiced = invoices.Sum(i => i.Total ?? 0);
                decimal orderTotal = order.SaleTotal ?? 0;
                bool allInvoicesReceived = invoices.Any() && invoices.All(i => i.ReceptionDate.HasValue);
                bool allInvoicesPaid = invoices.Any() && invoices.All(i => i.PaymentDate.HasValue);

                System.Diagnostics.Debug.WriteLine($"   Total orden: {orderTotal:C}");
                System.Diagnostics.Debug.WriteLine($"   Total facturado: {totalInvoiced:C}");
                System.Diagnostics.Debug.WriteLine($"   Todas recibidas: {allInvoicesReceived}");
                System.Diagnostics.Debug.WriteLine($"   Todas pagadas: {allInvoicesPaid}");

                int newStatusId = order.OrderStatus ?? 0;
                bool statusChanged = false;

                // Lógica de cambio de estado automático
                if (allInvoicesPaid && invoices.Any())
                {
                    // COMPLETADA - Todas las facturas pagadas
                    newStatusId = await GetStatusIdByName("COMPLETADA");
                    if (newStatusId != order.OrderStatus)
                    {
                        order.ProgressPercentage = 100; // Avance al 100%
                        statusChanged = true;
                        System.Diagnostics.Debug.WriteLine($"   ✅ Cambiando a COMPLETADA");
                    }
                }
                else if (allInvoicesReceived && invoices.Any())
                {
                    // CERRADA - Todas las facturas recibidas
                    newStatusId = await GetStatusIdByName("CERRADA");
                    if (newStatusId != order.OrderStatus)
                    {
                        statusChanged = true;
                        System.Diagnostics.Debug.WriteLine($"   ✅ Cambiando a CERRADA");
                    }
                }
                else if (orderTotal > 0 && Math.Abs(totalInvoiced - orderTotal) < 0.01m) // Tolerancia de 1 centavo
                {
                    // LIBERADA - Facturación al 100%
                    newStatusId = await GetStatusIdByName("LIBERADA");
                    if (newStatusId != order.OrderStatus)
                    {
                        order.ProgressPercentage = 100; // Avance al 100%
                        statusChanged = true;
                        System.Diagnostics.Debug.WriteLine($"   ✅ Cambiando a LIBERADA (100% facturado)");
                    }
                }

                // Si cambió el estado, actualizar
                if (statusChanged)
                {
                    order.OrderStatus = newStatusId;
                    order.UpdatedBy = userId > 0 ? userId : 1;

                    var updated = await UpdateOrder(order, userId);

                    if (updated)
                    {
                        // Registrar en historial
                        await LogOrderHistory(orderId, userId, "STATUS_CHANGE",
                            "f_orderstat",
                            currentStatusName,
                            await GetStatusName(newStatusId),
                            $"Cambio automático de estado");

                        System.Diagnostics.Debug.WriteLine($"   ✅ Estado actualizado exitosamente");
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error verificando estado de orden: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> CanCreateInvoice(int orderId)
        {
            try
            {
                var order = await GetOrderById(orderId);
                if (order == null) return false;

                var statusName = await GetStatusName(order.OrderStatus ?? 0);
                //saber el orderstatus  de los estados, si es 1,2,3,4

                var numStatus = order.OrderStatus ?? 0;

                // Solo permitir facturas en estado 'EN PROCESO' hasta 'COMPLETADA', que es del estado 1 -> 4
                return numStatus >= 1 && numStatus <= 4;


            }
            catch
            {
                return false;
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
                var status = statuses?.FirstOrDefault(s =>
                    s.Name.Equals(statusName, StringComparison.OrdinalIgnoreCase));
                return status?.Id ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        public async Task<bool> LogOrderHistory(int orderId, int userId, string action,
            string fieldName = null, string oldValue = null, string newValue = null,
            string description = null)
        {
            try
            {
                var history = new OrderHistoryDb
                {
                    OrderId = orderId,
                    UserId = userId > 0 ? userId : 1,
                    Action = action,
                    FieldName = fieldName,
                    OldValue = oldValue,
                    NewValue = newValue,
                    ChangeDescription = description,
                    IpAddress = "127.0.0.1", // En producción obtener la IP real
                    ChangedAt = DateTime.Now
                };

                var response = await _supabaseClient
                    .From<OrderHistoryDb>()
                    .Insert(history);

                System.Diagnostics.Debug.WriteLine($"📝 Historial registrado: {action} en orden {orderId}");
                return response?.Models?.Count > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error registrando historial: {ex.Message}");
                return false;
            }
        }


        // ===============================================
        // MÉTODOS PARA CLIENTES 
        // ===============================================

        public async Task<ClientDb> CreateClient(ClientDb client, int userId = 0)
        {
            try
            {
                // Establecer campos de auditoría
                client.CreatedBy = userId > 0 ? userId : 1;
                client.UpdatedBy = userId > 0 ? userId : 1;

                // Valores por defecto
                if (client.Credit == 0)
                    client.Credit = 30; // 30 días por defecto

                client.IsActive = true;

                System.Diagnostics.Debug.WriteLine($"📋 Creando cliente: {client.Name}");

                var response = await _supabaseClient
                    .From<ClientDb>()
                    .Insert(client);

                if (response?.Models?.Count > 0)
                {
                    return response.Models.First();
                }

                throw new Exception("No se pudo crear el cliente");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error creando cliente: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> ClientExists(string name)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name)) return false;

                // Normalizar el nombre para la comparación (quitar espacios, convertir a mayúsculas)
                string normalizedName = name.Trim().ToUpper();

                // Buscar clientes con nombre similar (case insensitive)
                var response = await _supabaseClient
                    .From<ClientDb>()
                    .Filter("f_name", Postgrest.Constants.Operator.ILike, normalizedName)
                    .Get();

                // Si encontramos algún resultado, el cliente ya existe
                return response?.Models?.Count > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error verificando cliente: {ex.Message}");
                return false;
            }
        }

        // Método para obtener un cliente por nombre
        public async Task<ClientDb> GetClientByName(string name)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name)) return null;

                string normalizedName = name.Trim().ToUpper();

                var response = await _supabaseClient
                    .From<ClientDb>()
                    .Filter("f_name", Postgrest.Constants.Operator.ILike, normalizedName)
                    .Single();

                return response;
            }
            catch
            {
                return null;
            }
        }

        public async Task<ClientDb> GetClientById(int clientId)
        {
            try
            {
                var response = await _supabaseClient
                    .From<ClientDb>()
                    .Where(x => x.Id == clientId)
                    .Single();

                return response;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error obteniendo cliente {clientId}: {ex.Message}");
                return null;
            }
        }

        // ===============================================
        // MÉTODOS PARA CONTACTOS 
        // ===============================================

        public async Task<ContactDb> CreateContact(ContactDb contact)
        {
            try
            {
                contact.IsActive = true;

                System.Diagnostics.Debug.WriteLine($"📇 Creando contacto: {contact.ContactName} para cliente {contact.ClientId}");

                var response = await _supabaseClient
                    .From<ContactDb>()
                    .Insert(contact);

                if (response?.Models?.Count > 0)
                {
                    return response.Models.First();
                }

                throw new Exception("No se pudo crear el contacto");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error creando contacto: {ex.Message}");
                throw;
            }
        }



        public async Task<List<ContactDb>> GetAllContacts()
        {
            try
            {
                var response = await _supabaseClient
                    .From<ContactDb>()
                    .Where(c => c.IsActive == true)  // Agregar este filtro
                    .Order("f_contactname", Postgrest.Constants.Ordering.Ascending)
                    .Get();

                return response?.Models ?? new List<ContactDb>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}");
                return new List<ContactDb>();
            }
        }

        // ========== MÉTODOS CRUD PARA CLIENTES ==========
        // Agregar estos métodos

        // Obtener órdenes por cliente
        public async Task<List<OrderDb>> GetOrdersByClientId(int clientId)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"📋 Obteniendo órdenes del cliente ID: {clientId}");

                var response = await _supabaseClient
                    .From<OrderDb>()
                    .Where(o => o.ClientId == clientId)
                    .Get();

                return response?.Models ?? new List<OrderDb>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error obteniendo órdenes del cliente: {ex.Message}");
                return new List<OrderDb>();
            }
        }

        // Obtener solo clientes activos
        public async Task<List<ClientDb>> GetActiveClients()
        {
            try
            {
                var response = await _supabaseClient
                    .From<ClientDb>()
                    .Where(c => c.IsActive == true)
                    .Order("f_name", Postgrest.Constants.Ordering.Ascending)
                    .Get();

                return response?.Models ?? new List<ClientDb>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error obteniendo clientes activos: {ex.Message}");
                return new List<ClientDb>();
            }
        }

        // Actualizar cliente
        public async Task<bool> UpdateClient(ClientDb client, int userId)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"📝 Actualizando cliente: {client.Name}");

                var response = await _supabaseClient
                    .From<ClientDb>()
                    .Where(c => c.Id == client.Id)
                    .Set(c => c.Name, client.Name)
                    .Set(c => c.TaxId, client.TaxId ?? "")
                    .Set(c => c.Phone, client.Phone ?? "")
                    .Set(c => c.Address1, client.Address1 ?? "")
                    .Set(c => c.Credit, client.Credit)
                    .Set(c => c.UpdatedAt, DateTime.Now)
                    .Set(c => c.UpdatedBy, userId)
                    .Update();

                return response?.Models?.Count > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error actualizando cliente: {ex.Message}");
                throw;
            }
        }

        // Soft delete de cliente
        public async Task<bool> SoftDeleteClient(int clientId)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"🗑️ Desactivando cliente ID: {clientId}");

                var response = await _supabaseClient
                    .From<ClientDb>()
                    .Where(c => c.Id == clientId)
                    .Set(c => c.IsActive, false)
                    .Set(c => c.UpdatedAt, DateTime.Now)

                    .Update();

                return response?.Models?.Count > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error desactivando cliente: {ex.Message}");
                return false;
            }
        }

        // ========== MÉTODOS CRUD PARA CONTACTOS ==========

        // Obtener solo contactos activos de un cliente
        public async Task<List<ContactDb>> GetActiveContactsByClientId(int clientId)
        {
            try
            {
                var response = await _supabaseClient
                    .From<ContactDb>()
                    .Where(c => c.ClientId == clientId)
                    .Where(c => c.IsActive == true)
                    .Order("is_primary", Postgrest.Constants.Ordering.Descending)
                    .Order("f_contactname", Postgrest.Constants.Ordering.Ascending)
                    .Get();

                return response?.Models ?? new List<ContactDb>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error obteniendo contactos activos: {ex.Message}");
                return new List<ContactDb>();
            }
        }


        // Soft delete de contacto
        public async Task<bool> SoftDeleteContact(int contactId)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"🗑️ Desactivando contacto ID: {contactId}");

                var response = await _supabaseClient
                    .From<ContactDb>()
                    .Where(c => c.Id == contactId)
                    .Set(c => c.IsActive, false)
                    .Update();

                return response?.Models?.Count > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error desactivando contacto: {ex.Message}");
                return false;
            }
        }

        // Contar contactos activos de un cliente
        public async Task<int> CountActiveContactsByClientId(int clientId)
        {
            try
            {
                var response = await _supabaseClient
                    .From<ContactDb>()
                    .Where(c => c.ClientId == clientId)
                    .Where(c => c.IsActive == true)
                    .Get();

                return response?.Models?.Count ?? 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error contando contactos: {ex.Message}");
                return 0;
            }
        }

        // ============= MÉTODOS PARA PROVEEDORES =============
        public async Task<List<SupplierDb>> GetActiveSuppliers()
        {
            try
            {
                var response = await _supabaseClient
                    .From<SupplierDb>()
                    .Where(s => s.IsActive == true)
                    .Order("f_suppliername", Postgrest.Constants.Ordering.Ascending)
                    .Get();

                return response?.Models ?? new List<SupplierDb>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error obteniendo proveedores: {ex.Message}");
                throw;
            }
        }

        public async Task<SupplierDb> GetSupplierById(int supplierId)
        {
            try
            {
                var response = await _supabaseClient
                    .From<SupplierDb>()
                    .Where(s => s.Id == supplierId)
                    .Single();

                return response;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error obteniendo proveedor {supplierId}: {ex.Message}");
                return null;
            }
        }

        

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
                ModeledResponse<ExpenseDb> response;

                // Construir la consulta base
                var query = _supabaseClient.From<ExpenseDb>().Select("*");

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
                response = await query
                    .Order("f_expensedate", Postgrest.Constants.Ordering.Descending)
                    .Order("f_expense", Postgrest.Constants.Ordering.Descending)
                    .Range(offset, offset + limit - 1)
                    .Get();

                var expenses = response?.Models ?? new List<ExpenseDb>();

                System.Diagnostics.Debug.WriteLine($"💰 Gastos obtenidos: {expenses.Count}");
                if (supplierId.HasValue)
                {
                    System.Diagnostics.Debug.WriteLine($"   Proveedor ID: {supplierId.Value}");
                }
                if (!string.IsNullOrEmpty(status))
                {
                    System.Diagnostics.Debug.WriteLine($"   Estado: {status}");
                }

                return expenses;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error obteniendo gastos: {ex.Message}");
                throw;
            }
        }

        public async Task<ExpenseDb> GetExpenseById(int expenseId)
        {
            try
            {
                var response = await _supabaseClient
                    .From<ExpenseDb>()
                    .Where(e => e.Id == expenseId)
                    .Single();

                return response;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error obteniendo gasto {expenseId}: {ex.Message}");
                return null;
            }
        }

        public async Task<ExpenseDb> CreateExpense(ExpenseDb expense)
        {
            try
            {
                

                // Establecer valores por defecto
                expense.Status = "PENDIENTE";
                expense.CreatedAt = DateTime.Now;
                expense.UpdatedAt = DateTime.Now;                

                var response = await _supabaseClient
                    .From<ExpenseDb>()
                    .Insert(expense);

                return response?.Models?.FirstOrDefault();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creando gasto: {ex.Message}");
                throw;
            }
        }

        // Obtener órdenes recientes para el combox de las ordenes
        public async Task<List<OrderDb>> GetRecentOrders(int limit = 10)
        {
            try
            {
                var response = await _supabaseClient
                    .From<OrderDb>()
                    .Select("*")
                    .Order("f_order", Postgrest.Constants.Ordering.Descending)
                    .Limit(limit)
                    .Get();

                return response?.Models ?? new List<OrderDb>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error obteniendo órdenes recientes: {ex.Message}");
                return new List<OrderDb>();
            }
        }

        public async Task<bool> UpdateExpense(ExpenseDb expense)
        {
            try
            {
                expense.UpdatedAt = DateTime.Now;

                var response = await _supabaseClient
                    .From<ExpenseDb>()
                    .Where(e => e.Id == expense.Id)
                    .Update(expense);

                return response?.Models?.Any() == true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error actualizando gasto: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> MarkExpenseAsPaid(int expenseId, DateTime paidDate, string payMethod)
        {
            try
            {
                var expense = await GetExpenseById(expenseId);
                if (expense == null) return false;

                expense.Status = "PAGADO";
                expense.PaidDate = paidDate;
                expense.PayMethod = payMethod;
                expense.UpdatedAt = DateTime.Now;

                return await UpdateExpense(expense);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error marcando gasto como pagado: {ex.Message}");
                return false;
            }
        }


        public async Task<bool> DeleteExpense(int expenseId)
        {
            try
            {
                await _supabaseClient
                    .From<ExpenseDb>()
                    .Where(e => e.Id == expenseId)
                    .Delete();

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error eliminando gasto: {ex.Message}");
                return false;
            }
        }

        public async Task<Dictionary<int, SupplierExpensesSummaryViewModel>> GetExpensesSummaryBySupplier()
        {
            try
            {
                var expenses = await GetExpenses();
                var suppliers = await GetActiveSuppliers();

                var summaryDict = new Dictionary<int, SupplierExpensesSummaryViewModel>();

                foreach (var supplier in suppliers)
                {
                    var supplierExpenses = expenses.Where(e => e.SupplierId == supplier.Id).ToList();

                    if (supplierExpenses.Any())
                    {
                        var summary = new SupplierExpensesSummaryViewModel
                        {
                            SupplierId = supplier.Id,
                            SupplierName = supplier.SupplierName,
                            Expenses = new ObservableCollection<ExpenseViewModel>()
                        };

                        foreach (var expense in supplierExpenses)
                        {
                            summary.Expenses.Add(ConvertToViewModel(expense, supplier));
                        }

                        summary.UpdateSummary();
                        summaryDict[supplier.Id] = summary;
                    }
                }

                return summaryDict;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error obteniendo resumen de gastos: {ex.Message}");
                throw;
            }
        }

        public async Task<List<ExpenseDb>> GetUpcomingExpenses(int daysAhead = 7)
        {
            try
            {
                var futureDate = DateTime.Now.Date.AddDays(daysAhead);

                var response = await _supabaseClient
                    .From<ExpenseDb>()
                    .Where(e => e.Status == "PENDIENTE")
                    .Where(e => e.ScheduledDate != null)
                    .Where(e => e.ScheduledDate <= futureDate)
                    .Order("f_scheduleddate", Postgrest.Constants.Ordering.Ascending)
                    .Get();

                return response?.Models ?? new List<ExpenseDb>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error obteniendo gastos próximos: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Obtiene gastos vencidos
        /// </summary>
        public async Task<List<ExpenseDb>> GetOverdueExpenses()
        {
            try
            {
                var today = DateTime.Now.Date;

                var response = await _supabaseClient
                    .From<ExpenseDb>()
                    .Where(e => e.Status == "PENDIENTE")
                    .Where(e => e.ScheduledDate != null)
                    .Where(e => e.ScheduledDate < today)
                    .Order("f_scheduleddate", Postgrest.Constants.Ordering.Ascending)
                    .Get();

                return response?.Models ?? new List<ExpenseDb>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error obteniendo gastos vencidos: {ex.Message}");
                throw;
            }
        }

        public async Task<List<OrderDb>> GetOrdersFiltered(
    DateTime? fromDate = null,
    string[] excludeStatuses = null,
    int limit = 50,
    int offset = 0)
        {
            try
            {
                // Obtener todas las órdenes recientes
                var response = await _supabaseClient
                    .From<OrderDb>()
                    .Order(o => o.PoDate, Postgrest.Constants.Ordering.Descending)
                    .Get();

                var orders = response?.Models ?? new List<OrderDb>();

                // Filtrar en memoria si es necesario
                if (fromDate.HasValue)
                {
                    orders = orders.Where(o => o.PoDate >= fromDate.Value).ToList();
                }

                // Si necesitas excluir estados, puedes hacerlo aquí
                // (aunque es menos eficiente que hacerlo en la query)

                // Aplicar paginación
                orders = orders.Skip(offset).Take(limit).ToList();

                return orders;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al obtener órdenes filtradas: {ex.Message}");
                return new List<OrderDb>();
            }
        }

        // Método GetRecentOrders modificado para soportar offset
        public async Task<List<OrderDb>> GetRecentOrders(int limit = 10, int offset = 0)
        {
            try
            {
                var response = await _supabaseClient
                    .From<OrderDb>()
                    .Order(o => o.PoDate, Postgrest.Constants.Ordering.Descending)
                    .Limit(limit)
                    .Offset(offset)
                    .Get();

                return response?.Models ?? new List<OrderDb>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al obtener órdenes recientes: {ex.Message}");
                return new List<OrderDb>();
            }
        }

        // ============= MÉTODOS AUXILIARES =============

        /// <summary>
        /// Convierte ExpenseDb a ExpenseViewModel
        /// </summary>
        private ExpenseViewModel ConvertToViewModel(ExpenseDb expense, SupplierDb supplier = null)
        {
            return new ExpenseViewModel
            {
                ExpenseId = expense.Id,
                SupplierId = expense.SupplierId,
                SupplierName = supplier?.SupplierName ?? "Proveedor Desconocido",
                Description = expense.Description,
                ExpenseDate = expense.ExpenseDate,
                TotalExpense = expense.TotalExpense,
                ScheduledDate = expense.ScheduledDate,
                Status = expense.Status,
                PaidDate = expense.PaidDate,
                PayMethod = expense.PayMethod,
                OrderId = expense.OrderId,
                ExpenseCategory = expense.ExpenseCategory
            };
        }

        /// <summary>
        /// Obtiene el ID del usuario actual (implementar según tu sistema)
        /// </summary>
        private string GetCurrentUserId()
        {
            // TODO: Implementar según tu sistema de autenticación
            // Por ejemplo:
            // return _currentUser?.Id?.ToString() ?? "system";
            return "system";
        }

        /// <summary>
        /// Obtiene estadísticas generales de gastos
        /// </summary>
        public async Task<ExpenseStatistics> GetExpenseStatistics()
        {
            try
            {
                var expenses = await GetExpenses();

                return new ExpenseStatistics
                {
                    TotalPending = expenses.Where(e => e.Status == "PENDIENTE").Sum(e => e.TotalExpense),
                    TotalPaid = expenses.Where(e => e.Status == "PAGADO").Sum(e => e.TotalExpense),
                    TotalOverdue = expenses.Where(e => e.Status == "PENDIENTE" &&
                                                     e.ScheduledDate.HasValue &&
                                                     e.ScheduledDate.Value < DateTime.Now.Date)
                                          .Sum(e => e.TotalExpense),
                    PendingCount = expenses.Count(e => e.Status == "PENDIENTE"),
                    PaidCount = expenses.Count(e => e.Status == "PAGADO"),
                    OverdueCount = expenses.Count(e => e.Status == "PENDIENTE" &&
                                                      e.ScheduledDate.HasValue &&
                                                      e.ScheduledDate.Value < DateTime.Now.Date)
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error obteniendo estadísticas: {ex.Message}");
                throw;
            }
        }

        public class ExpenseStatistics
        {
            public decimal TotalPending { get; set; }
            public decimal TotalPaid { get; set; }
            public decimal TotalOverdue { get; set; }
            public int PendingCount { get; set; }
            public int PaidCount { get; set; }
            public int OverdueCount { get; set; }

            public decimal GrandTotal => TotalPending + TotalPaid;
        }



        // Obtener todos los proveedores (activos e inactivos)
        public async Task<List<SupplierDb>> GetAllSuppliers()
        {
            try
            {
                var response = await _supabaseClient
                    .From<SupplierDb>()
                    .Order(s => s.SupplierName, Postgrest.Constants.Ordering.Ascending)
                    .Get();

                return response?.Models ?? new List<SupplierDb>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error al obtener proveedores: {ex.Message}");
                return new List<SupplierDb>();
            }
        }

        // Crear proveedor
        public async Task<SupplierDb> CreateSupplier(SupplierDb supplier)
        {
            try
            {
                supplier.CreatedAt = DateTime.Now;
                supplier.UpdatedAt = DateTime.Now;

                var response = await _supabaseClient
                    .From<SupplierDb>()
                    .Insert(supplier);

                return response?.Models?.FirstOrDefault();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error al crear proveedor: {ex.Message}");
                return null;
            }
        }

        // Actualizar proveedor
        public async Task<bool> UpdateSupplier(SupplierDb supplier)
        {
            try
            {
                supplier.UpdatedAt = DateTime.Now;

                var response = await _supabaseClient
                    .From<SupplierDb>()
                    .Where(s => s.Id == supplier.Id)
                    .Update(supplier);

                return response?.Models?.Any() == true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error al actualizar proveedor: {ex.Message}");
                return false;
            }
        }

        // Eliminar proveedor
        public async Task<bool> DeleteSupplier(int supplierId)
        {
            try
            {
                await _supabaseClient
                    .From<SupplierDb>()
                    .Where(s => s.Id == supplierId)
                    .Delete();

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error al eliminar proveedor: {ex.Message}");
                return false;
            }
        }

        // PARA LOS INGRESOS PENDIENTES
        // Clase auxiliar para los datos agregados de clientes con facturas pendientes
        public class ClientPendingData
        {
            public int ClientId { get; set; }
            public string ClientName { get; set; }
            public decimal TotalPending { get; set; }
        }

        // Obtener clientes con facturas pendientes (datos agregados)
        public async Task<List<ClientPendingData>> GetClientsPendingInvoices()
        {
            try
            {
                var result = new List<ClientPendingData>();

                // Obtener todas las facturas que no estén pagadas
                var invoicesResponse = await _supabaseClient
                    .From<InvoiceDb>()
                    .Select("*")
                    .Not("f_invoicestat", Postgrest.Constants.Operator.Equals, 4) // No pagadas
                    .Get();

                if (invoicesResponse?.Models == null || !invoicesResponse.Models.Any())
                    return result;

                // Filtrar facturas sin fecha de pago en memoria
                var pendingInvoices = invoicesResponse.Models
                    .Where(i => i.PaymentDate == null || !i.PaymentDate.HasValue)
                    .ToList();

                if (!pendingInvoices.Any())
                    return result;

                // Obtener las órdenes relacionadas
                var orderIds = pendingInvoices
                    .Where(i => i.OrderId.HasValue)
                    .Select(i => i.OrderId.Value)
                    .Distinct()
                    .ToList();

                var ordersResponse = await _supabaseClient
                    .From<OrderDb>()
                    .Select("*")
                    .Get();

                var orders = ordersResponse?.Models?
                    .Where(o => orderIds.Contains(o.Id))
                    .ToDictionary(o => o.Id);

                if (orders == null || !orders.Any())
                    return result;

                // Obtener los clientes
                var clientIds = orders.Values
                    .Where(o => o.ClientId.HasValue)
                    .Select(o => o.ClientId.Value)
                    .Distinct()
                    .ToList();

                var clientsResponse = await _supabaseClient
                    .From<ClientDb>()
                    .Select("*")
                    .Get();

                var clients = clientsResponse?.Models?
                    .Where(c => clientIds.Contains(c.Id))
                    .ToDictionary(c => c.Id);

                if (clients == null || !clients.Any())
                    return result;

                // Agrupar por cliente
                var groupedByClient = pendingInvoices
                    .Where(i => i.OrderId.HasValue && orders.ContainsKey(i.OrderId.Value))
                    .GroupBy(i => orders[i.OrderId.Value].ClientId)
                    .Where(g => g.Key.HasValue && clients.ContainsKey(g.Key.Value))
                    .Select(g => new ClientPendingData
                    {
                        ClientId = g.Key.Value,
                        ClientName = clients[g.Key.Value].Name,
                        TotalPending = g.Sum(i => i.Total ?? 0)
                    })
                    .OrderByDescending(c => c.TotalPending)
                    .ToList();

                return groupedByClient;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al obtener clientes con facturas pendientes: {ex.Message}");
                throw;
            }
        }

        // Obtener facturas pendientes de un cliente específico
        public async Task<List<InvoiceDb>> GetPendingInvoicesByClient(int clientId)
        {
            try
            {
                // Primero obtener las órdenes del cliente
                var ordersResponse = await _supabaseClient
                    .From<OrderDb>()
                    .Select("*")
                    .Where(o => o.ClientId == clientId)
                    .Get();

                var orderIds = ordersResponse?.Models?.Select(o => o.Id).ToList() ?? new List<int>();

                if (!orderIds.Any())
                    return new List<InvoiceDb>();

                // Obtener todas las facturas no pagadas
                var invoicesResponse = await _supabaseClient
                    .From<InvoiceDb>()
                    .Select("*")
                    .Not("f_invoicestat", Postgrest.Constants.Operator.Equals, 4) // No pagadas
                    .Order("due_date", Postgrest.Constants.Ordering.Ascending)
                    .Get();

                // Filtrar en memoria: por órdenes del cliente y sin fecha de pago
                var pendingInvoices = invoicesResponse?.Models?
                    .Where(i => i.OrderId.HasValue &&
                               orderIds.Contains(i.OrderId.Value) &&
                               i.PaymentDate == null)
                    .ToList() ?? new List<InvoiceDb>();

                return pendingInvoices;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al obtener facturas pendientes del cliente {clientId}: {ex.Message}");
                throw;
            }
        }
        // Obtener todas las facturas pendientes con información completa
        public async Task<List<PendingInvoiceDetail>> GetAllPendingInvoicesDetailed()
        {
            try
            {
                var result = new List<PendingInvoiceDetail>();

                // Obtener todas las facturas no pagadas
                var invoicesResponse = await _supabaseClient
                    .From<InvoiceDb>()
                    .Select("*")
                    .Not("f_invoicestat", Postgrest.Constants.Operator.Equals, 4) // No pagadas
                    .Order("due_date", Postgrest.Constants.Ordering.Ascending)
                    .Get();

                if (invoicesResponse?.Models == null)
                    return result;

                // Filtrar facturas sin fecha de pago
                var pendingInvoices = invoicesResponse.Models
                    .Where(i => i.PaymentDate == null)
                    .ToList();

                if (!pendingInvoices.Any())
                    return result;

                // Obtener órdenes
                var orderIds = pendingInvoices
                    .Where(i => i.OrderId.HasValue)
                    .Select(i => i.OrderId.Value)
                    .Distinct()
                    .ToList();

                var ordersResponse = await _supabaseClient
                    .From<OrderDb>()
                    .Select("*")
                    .Get();

                var orders = ordersResponse?.Models?
                    .Where(o => orderIds.Contains(o.Id))
                    .ToDictionary(o => o.Id);

                // Obtener clientes
                var clientIds = orders?.Values
                    .Where(o => o.ClientId.HasValue)
                    .Select(o => o.ClientId.Value)
                    .Distinct()
                    .ToList() ?? new List<int>();

                var clientsResponse = await _supabaseClient
                    .From<ClientDb>()
                    .Select("*")
                    .Get();

                var clients = clientsResponse?.Models?
                    .Where(c => clientIds.Contains(c.Id))
                    .ToDictionary(c => c.Id);

                foreach (var invoice in pendingInvoices)
                {
                    if (!invoice.OrderId.HasValue || orders == null || !orders.ContainsKey(invoice.OrderId.Value))
                        continue;

                    var order = orders[invoice.OrderId.Value];
                    if (!order.ClientId.HasValue || clients == null || !clients.ContainsKey(order.ClientId.Value))
                        continue;

                    var client = clients[order.ClientId.Value];

                    var detail = new PendingInvoiceDetail
                    {
                        InvoiceId = invoice.Id,
                        Folio = invoice.Folio,
                        Total = invoice.Total ?? 0,
                        InvoiceDate = invoice.InvoiceDate,
                        ReceptionDate = invoice.ReceptionDate,
                        DueDate = invoice.DueDate,
                        ClientId = client.Id,
                        ClientName = client.Name,
                        ClientCredit = client.Credit,
                        OrderPO = order.Po,
                        OrderId = order.Id
                    };

                    // Calcular estado basado en fecha de vencimiento
                    if (invoice.DueDate.HasValue)
                    {
                        var daysUntilDue = (invoice.DueDate.Value - DateTime.Today).Days;

                        if (daysUntilDue < 0)
                        {
                            detail.Status = "VENCIDA";
                            detail.DaysOverdue = Math.Abs(daysUntilDue);
                        }
                        else if (daysUntilDue <= 7)
                        {
                            detail.Status = "POR VENCER";
                            detail.DaysUntilDue = daysUntilDue;
                        }
                        else
                        {
                            detail.Status = "AL CORRIENTE";
                            detail.DaysUntilDue = daysUntilDue;
                        }
                    }
                    else
                    {
                        detail.Status = "SIN FECHA";
                    }

                    result.Add(detail);
                }

                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al obtener facturas pendientes detalladas: {ex.Message}");
                throw;
            }
        }

        public async Task<PendingIncomesData> GetAllPendingIncomesData()
        {
            try
            {
                // Ejecutar todas las consultas en paralelo
                var tasksDict = new Dictionary<string, Task>();

                // Task 1: Obtener todas las facturas no pagadas
                var invoicesTask = _supabaseClient
                    .From<InvoiceDb>()
                    .Select("*")
                    .Not("f_invoicestat", Postgrest.Constants.Operator.Equals, 4)
                    .Get();

                // Task 2: Obtener todas las órdenes
                var ordersTask = _supabaseClient
                    .From<OrderDb>()
                    .Select("*")
                    .Get();

                // Task 3: Obtener todos los clientes
                var clientsTask = _supabaseClient
                    .From<ClientDb>()
                    .Select("*")
                    .Get();

                // Esperar todas las tareas
                await Task.WhenAll(invoicesTask, ordersTask, clientsTask);

                // Procesar resultados
                var invoices = invoicesTask.Result?.Models ?? new List<InvoiceDb>();
                var orders = ordersTask.Result?.Models ?? new List<OrderDb>();
                var clients = clientsTask.Result?.Models ?? new List<ClientDb>();

                // Filtrar facturas sin fecha de pago
                var pendingInvoices = invoices.Where(i => i.PaymentDate == null).ToList();

                // Crear diccionarios para búsqueda rápida
                var ordersDict = orders.ToDictionary(o => o.Id);
                var clientsDict = clients.ToDictionary(c => c.Id);

                // Agrupar por cliente
                var clientGroups = new Dictionary<int, List<InvoiceDb>>();

                foreach (var invoice in pendingInvoices)
                {
                    if (invoice.OrderId.HasValue && ordersDict.ContainsKey(invoice.OrderId.Value))
                    {
                        var order = ordersDict[invoice.OrderId.Value];
                        if (order.ClientId.HasValue && clientsDict.ContainsKey(order.ClientId.Value))
                        {
                            if (!clientGroups.ContainsKey(order.ClientId.Value))
                                clientGroups[order.ClientId.Value] = new List<InvoiceDb>();

                            clientGroups[order.ClientId.Value].Add(invoice);
                        }
                    }
                }

                // Construir resultado
                var result = new PendingIncomesData
                {
                    ClientsWithPendingInvoices = new List<ClientPendingInfo>(),
                    OrdersDictionary = ordersDict,
                    ClientsDictionary = clientsDict
                };

                foreach (var kvp in clientGroups)
                {
                    var client = clientsDict[kvp.Key];
                    var clientInvoices = kvp.Value;

                    var clientInfo = new ClientPendingInfo
                    {
                        ClientId = client.Id,
                        ClientName = client.Name,
                        ClientCredit = client.Credit,
                        Invoices = clientInvoices,
                        TotalPending = clientInvoices.Sum(i => i.Total ?? 0)
                    };

                    result.ClientsWithPendingInvoices.Add(clientInfo);
                }

                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al obtener datos: {ex.Message}");
                throw;
            }
        }

        public async Task<ClientInvoicesDetailData> GetClientInvoicesDetail(int clientId)
        {
            try
            {
                // Ejecutar consultas en paralelo
                var clientTask = _supabaseClient
                    .From<ClientDb>()
                    .Select("*")
                    .Where(c => c.Id == clientId)
                    .Get(); // Cambiar Single() por Get()

                var ordersTask = _supabaseClient
                    .From<OrderDb>()
                    .Select("*")
                    .Where(o => o.ClientId == clientId)
                    .Get();

                var invoicesTask = _supabaseClient
                    .From<InvoiceDb>()
                    .Select("*")
                    .Not("f_invoicestat", Postgrest.Constants.Operator.Equals, 4)
                    .Get();

                // Esperar todas las tareas
                await Task.WhenAll(clientTask, ordersTask, invoicesTask);

                // Corregir esta línea
                var client = clientTask.Result?.Models?.FirstOrDefault();
                var orders = ordersTask.Result?.Models ?? new List<OrderDb>();
                var invoices = invoicesTask.Result?.Models ?? new List<InvoiceDb>();

                // Crear diccionario de órdenes para búsqueda rápida
                var ordersDict = orders.ToDictionary(o => o.Id);

                // Filtrar facturas pendientes de este cliente
                var clientInvoices = new List<InvoiceDetailInfo>();

                foreach (var invoice in invoices.Where(i => i.PaymentDate == null))
                {
                    if (invoice.OrderId.HasValue && ordersDict.ContainsKey(invoice.OrderId.Value))
                    {
                        var order = ordersDict[invoice.OrderId.Value];

                        var invoiceDetail = new InvoiceDetailInfo
                        {
                            Invoice = invoice,
                            OrderPO = order.Po ?? $"ORD-{order.Id}",
                            OrderId = order.Id
                        };

                        clientInvoices.Add(invoiceDetail);
                    }
                }

                return new ClientInvoicesDetailData
                {
                    Client = client,
                    Invoices = clientInvoices.OrderBy(i => i.Invoice.DueDate).ToList()
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al obtener detalle del cliente: {ex.Message}");
                throw;
            }
        }

        // Clases auxiliares
        public class ClientInvoicesDetailData
        {
            public ClientDb Client { get; set; }
            public List<InvoiceDetailInfo> Invoices { get; set; }
        }

        public class InvoiceDetailInfo
        {
            public InvoiceDb Invoice { get; set; }
            public string OrderPO { get; set; }
            public int OrderId { get; set; }
        }

        // Clases auxiliares
        public class PendingIncomesData
        {
            public List<ClientPendingInfo> ClientsWithPendingInvoices { get; set; }
            public Dictionary<int, OrderDb> OrdersDictionary { get; set; }
            public Dictionary<int, ClientDb> ClientsDictionary { get; set; }
        }

        public class ClientPendingInfo
        {
            public int ClientId { get; set; }
            public string ClientName { get; set; }
            public int ClientCredit { get; set; }
            public List<InvoiceDb> Invoices { get; set; }
            public decimal TotalPending { get; set; }
        }

        // Clase auxiliar para el detalle completo de facturas pendientes
        public class PendingInvoiceDetail
        {
            public int InvoiceId { get; set; }
            public string Folio { get; set; }
            public decimal Total { get; set; }
            public DateTime? InvoiceDate { get; set; }
            public DateTime? ReceptionDate { get; set; }
            public DateTime? DueDate { get; set; }
            public int ClientId { get; set; }
            public string ClientName { get; set; }
            public int ClientCredit { get; set; }
            public string OrderPO { get; set; }
            public int OrderId { get; set; }
            public string Status { get; set; }
            public int DaysOverdue { get; set; }
            public int DaysUntilDue { get; set; }
        }

        // Actualizar el estado de las facturas vencidas automáticamente
        public async Task UpdateOverdueInvoicesStatus()
        {
            try
            {
                // Obtener todas las facturas que no están pagadas ni ya marcadas como vencidas
                var invoicesResponse = await _supabaseClient
                    .From<InvoiceDb>()
                    .Select("*")
                    .Not("f_invoicestat", Postgrest.Constants.Operator.Equals, 4) // No pagadas
                    .Not("f_invoicestat", Postgrest.Constants.Operator.Equals, 3) // No ya marcadas como vencidas
                    .Get();

                if (invoicesResponse?.Models == null || !invoicesResponse.Models.Any())
                    return;

                // Filtrar en memoria las que están vencidas
                var overdueInvoices = invoicesResponse.Models
                    .Where(i => i.DueDate.HasValue && i.DueDate.Value < DateTime.Today)
                    .ToList();

                if (overdueInvoices.Any())
                {
                    foreach (var invoice in overdueInvoices)
                    {
                        invoice.InvoiceStatus = 3; // Estado VENCIDA
                        await _supabaseClient
                            .From<InvoiceDb>()
                            .Where(i => i.Id == invoice.Id)
                            .Update(invoice);
                    }

                    System.Diagnostics.Debug.WriteLine($"Se actualizaron {overdueInvoices.Count} facturas a estado VENCIDA");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al actualizar facturas vencidas: {ex.Message}");
            }
        }

        // ===============================================
        // ========== MÉTODOS DE NÓMINA ==========

        public async Task<List<PayrollTable>> GetActivePayroll()
        {
            try
            {
                var response = await _supabaseClient
                    .From<PayrollTable>()
                    .Where(x => x.IsActive == true)
                    .Order(x => x.Employee, Postgrest.Constants.Ordering.Ascending)  // Sin "Supabase."
                    .Get();
                return response?.Models ?? new List<PayrollTable>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting payroll: {ex.Message}");
                throw;
            }
        }
        public async Task<List<PayrollHistoryTable>> GetPayrollHistory(int? payrollId = null)
        {
            try
            {
                if (payrollId.HasValue)
                {
                    var response = await _supabaseClient
                        .From<PayrollHistoryTable>()
                        .Where(x => x.PayrollId == payrollId.Value)
                        .Order(x => x.EffectiveDate, Postgrest.Constants.Ordering.Descending)  // Sin "Supabase."
                        .Limit(100)
                        .Get();

                    return response?.Models ?? new List<PayrollHistoryTable>();
                }
                else
                {
                    var response = await _supabaseClient
                        .From<PayrollHistoryTable>()
                        .Order(x => x.EffectiveDate, Postgrest.Constants.Ordering.Descending)  // Sin "Supabase."
                        .Limit(100)
                        .Get();

                    return response?.Models ?? new List<PayrollHistoryTable>();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting payroll history: {ex.Message}");
                throw;
            }
        }

        public async Task<PayrollTable> GetPayrollById(int id)
        {
            try
            {
                var response = await _supabaseClient
                    .From<PayrollTable>()
                    .Where(x => x.Id == id)
                    .Single();

                return response;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting payroll by id: {ex.Message}");
                throw;
            }
        }

        public async Task<PayrollTable> CreatePayroll(PayrollTable payroll)
        {
            try
            {
                payroll.CreatedAt = DateTime.Now;
                payroll.UpdatedAt = DateTime.Now;
                payroll.IsActive = true;

                var response = await _supabaseClient
                    .From<PayrollTable>()
                    .Insert(payroll);

                return response?.Models?.FirstOrDefault();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating payroll: {ex.Message}");
                throw;
            }
        }

        public async Task<PayrollTable> UpdatePayroll(PayrollTable payroll)
        {
            try
            {
                payroll.UpdatedAt = DateTime.Now;

                var response = await _supabaseClient
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

                return response?.Models?.FirstOrDefault();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating payroll: {ex.Message}");
                throw;
            }
        }

        
        public async Task<decimal> GetMonthlyPayrollTotal()
        {
            try
            {
                var payrolls = await GetActivePayroll();
                return payrolls.Sum(p => p.MonthlyPayroll ?? 0);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error calculating payroll total: {ex.Message}");
                throw;
            }
        }

        // Desactivar empleado (soft delete)
        public async Task<bool> DeactivateEmployee(int employeeId, int userId)
        {
            try
            {
                var response = await _supabaseClient
                    .From<PayrollTable>()
                    .Where(x => x.Id == employeeId)
                    .Set(x => x.IsActive, false)
                    .Set(x => x.UpdatedBy, userId)
                    .Set(x => x.UpdatedAt, DateTime.Now)
                    .Update();

                return response?.Models?.Any() ?? false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deactivating employee: {ex.Message}");
                throw;
            }
        }


        // Para obtener el salario vigente en una fecha específica

        public async Task<List<PayrollTable>> GetEffectivePayroll(DateTime effectiveDate)
        {
            try
            {
                var response = await _supabaseClient
                    .From<PayrollTable>()
                    .Where(p => p.IsActive == true)
                    .Get();

                var payrollList = response.Models;

                // Para cada empleado, verificar si hay cambios en el historial
                foreach (var payroll in payrollList)
                {
                    var historyResponse = await _supabaseClient
                        .From<PayrollHistoryTable>()
                        .Filter("f_payroll", Postgrest.Constants.Operator.Equals, payroll.Id)
                        .Filter("effective_date", Postgrest.Constants.Operator.LessThanOrEqual, effectiveDate.ToString("yyyy-MM-dd"))
                        .Order("effective_date", Postgrest.Constants.Ordering.Descending)
                        .Limit(1)
                        .Get();

                    if (historyResponse.Models.Any())
                    {
                        var latestChange = historyResponse.Models.First();
                        payroll.MonthlyPayroll = latestChange.MonthlyPayroll;
                    }
                }

                return payrollList;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting effective payroll: {ex.Message}");
                throw;
            }
        }

        // Para guardar un cambio con fecha efectiva
        public async Task<bool> SavePayrollWithEffectiveDate(
    PayrollTable payroll,
    DateTime effectiveDate,
    int userId)
        {
            try
            {
                // 1. Actualizar el registro actual
                payroll.UpdatedAt = DateTime.Now;
                payroll.UpdatedBy = userId;

                var updateResponse = await _supabaseClient
                    .From<PayrollTable>()
                    .Where(p => p.Id == payroll.Id)
                    .Update(payroll);

                // 2. Crear registro en historial
                var history = new PayrollHistoryTable
                {
                    PayrollId = payroll.Id,
                    Employee = payroll.Employee,
                    Title = payroll.Title,
                    MonthlyPayroll = payroll.MonthlyPayroll,
                    EffectiveDate = effectiveDate,
                    ChangeType = "SALARY_CHANGE",
                    ChangeSummary = $"Cambio de salario efectivo desde {effectiveDate:dd/MM/yyyy}",
                    CreatedBy = userId
                };

                await _supabaseClient.From<PayrollHistoryTable>().Insert(history);

                // 3. Actualizar balances futuros
                await UpdateFutureBalances(effectiveDate, userId);

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving with effective date: {ex.Message}");
                return false;
            }
        }

        // Actualizar balances futuros
        private async Task UpdateFutureBalances(DateTime fromDate, int userId)
        {
            for (int i = 0; i < 3; i++)
            {
                var monthDate = new DateTime(fromDate.Year, fromDate.Month, 1).AddMonths(i);

                // Obtener totales vigentes
                var payrollList = await GetEffectivePayroll(monthDate);
                var totalPayroll = payrollList.Sum(p => p.MonthlyPayroll ?? 0);

                var expenses = await GetEffectiveFixedExpenses(monthDate);
                var totalExpenses = expenses.Sum(e => e.MonthlyAmount ?? 0);

                // Buscar registro existente
                var existingResponse = await _supabaseClient
                    .From<PayrollOvertimeTable>()
                    .Filter("f_date", Postgrest.Constants.Operator.Equals, monthDate.ToString("yyyy-MM-dd"))
                    .Get();

                if (existingResponse.Models.Any())
                {
                    var existing = existingResponse.Models.First();
                    existing.Payroll = totalPayroll;
                    existing.FixedExpense = totalExpenses;

                    await _supabaseClient
                        .From<PayrollOvertimeTable>()
                        .Where(po => po.Id == existing.Id)
                        .Update(existing);
                }
                else
                {
                    var newRecord = new PayrollOvertimeTable
                    {
                        Date = monthDate,
                        Payroll = totalPayroll,
                        FixedExpense = totalExpenses,
                        Overtime = 0,
                        CreatedBy = userId
                    };
                    await _supabaseClient.From<PayrollOvertimeTable>().Insert(newRecord);
                }
            }
        }

        public async Task<List<FixedExpenseTable>> GetEffectiveFixedExpenses(DateTime effectiveDate)
        {
            try
            {
                var response = await _supabaseClient
                    .From<FixedExpenseTable>()
                    .Where(e => e.IsActive == true)
                    .Get();

                return response.Models;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting effective expenses: {ex.Message}");
                return new List<FixedExpenseTable>();
            }
        }

        // ========== MÉTODOS DE GASTOS FIJOS ==========

        public async Task<List<FixedExpenseTable>> GetActiveFixedExpenses()
        {
            try
            {
                var response = await _supabaseClient
                    .From<FixedExpenseTable>()
                    .Where(x => x.IsActive == true)
                    .Order(x => x.ExpenseType, Postgrest.Constants.Ordering.Ascending)
                    .Get();

                return response?.Models ?? new List<FixedExpenseTable>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting fixed expenses: {ex.Message}");
                throw;
            }
        }

        public async Task<FixedExpenseTable> CreateFixedExpense(FixedExpenseTable expense)
        {
            try
            {
                expense.CreatedAt = DateTime.Now;
                expense.UpdatedAt = DateTime.Now;
                expense.IsActive = true;

                var response = await _supabaseClient
                    .From<FixedExpenseTable>()
                    .Insert(expense);

                return response?.Models?.FirstOrDefault();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating fixed expense: {ex.Message}");
                throw;
            }
        }

        public async Task<FixedExpenseTable> UpdateFixedExpense(FixedExpenseTable expense)
        {
            try
            {
                expense.UpdatedAt = DateTime.Now;

                var response = await _supabaseClient
                    .From<FixedExpenseTable>()
                    .Where(x => x.Id == expense.Id)
                    .Set(x => x.ExpenseType, expense.ExpenseType)
                    .Set(x => x.Description, expense.Description)
                    .Set(x => x.MonthlyAmount, expense.MonthlyAmount)
                    .Set(x => x.UpdatedAt, expense.UpdatedAt)
                    .Update();

                return response?.Models?.FirstOrDefault();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating fixed expense: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> DeleteFixedExpense(int expenseId)
        {
            try
            {
                var response = await _supabaseClient
                    .From<FixedExpenseTable>()
                    .Where(x => x.Id == expenseId)
                    .Set(x => x.IsActive, false)
                    .Set(x => x.UpdatedAt, DateTime.Now)
                    .Update();

                return response?.Models?.Any() ?? false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting fixed expense: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> DeactivateFixedExpense(int expenseId)
        {
            try
            {
                var expense = await GetFixedExpenseById(expenseId);
                if (expense != null)
                {
                    expense.IsActive = false;
                    expense.UpdatedAt = DateTime.Now;

                    await _supabaseClient
                        .From<FixedExpenseTable>()
                        .Where(e => e.Id == expenseId)
                        .Update(expense);

                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deactivating expense: {ex.Message}");
                return false;
            }
        }


        // En SupabaseService.cs, simplifica SaveFixedExpenseWithEffectiveDate:

        public async Task<bool> SaveFixedExpenseWithEffectiveDate(FixedExpenseTable expense,DateTime effectiveDate,int userId)
        {
            try
            {
                // Solo actualizar el registro principal
                // El trigger se encargará del historial
                expense.UpdatedAt = DateTime.Now;
                expense.EffectiveDate = effectiveDate;

                await _supabaseClient
                    .From<FixedExpenseTable>()
                    .Where(e => e.Id == expense.Id)
                    .Update(expense);

                // Actualizar balances futuros (opcional)
                await UpdateFutureBalances(effectiveDate, userId);

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}");
                return false;
            }
        }


        // Versión optimizada que no recalcula todo
        private async Task UpdateFutureBalancesOptimized(DateTime fromDate, int userId)
        {
            try
            {
                // Solo actualizar los próximos 3 meses desde la fecha efectiva
                var monthsToUpdate = new List<DateTime>();
                for (int i = 0; i < 3; i++)
                {
                    monthsToUpdate.Add(new DateTime(fromDate.Year, fromDate.Month, 1).AddMonths(i));
                }

                // Obtener todos los registros existentes de una vez
                var existingRecords = await _supabaseClient
                    .From<PayrollOvertimeTable>()
                    .Filter("f_date", Postgrest.Constants.Operator.GreaterThanOrEqual, fromDate.ToString("yyyy-MM-dd"))
                    .Filter("f_date", Postgrest.Constants.Operator.LessThanOrEqual, fromDate.AddMonths(3).ToString("yyyy-MM-dd"))
                    .Get();

                foreach (var monthDate in monthsToUpdate)
                {
                    // Calcular totales para ese mes específico
                    var payrollTotal = await GetMonthlyPayrollTotal(monthDate);
                    var expensesTotal = await GetMonthlyExpensesTotal(monthDate);

                    var existing = existingRecords.Models.FirstOrDefault(r => r.Date.Date == monthDate.Date);

                    if (existing != null)
                    {
                        // Solo actualizar si cambió
                        if (Math.Abs((existing.Payroll ?? 0) - payrollTotal) > 0.01m ||
                            Math.Abs((existing.FixedExpense ?? 0) - expensesTotal) > 0.01m)
                        {
                            existing.Payroll = payrollTotal;
                            existing.FixedExpense = expensesTotal;

                            await _supabaseClient
                                .From<PayrollOvertimeTable>()
                                .Where(po => po.Id == existing.Id)
                                .Update(existing);
                        }
                    }
                    else
                    {
                        // Crear nuevo solo si no existe
                        var newRecord = new PayrollOvertimeTable
                        {
                            Date = monthDate,
                            Payroll = payrollTotal,
                            FixedExpense = expensesTotal,
                            Overtime = 0,
                            CreatedBy = userId
                        };
                        await _supabaseClient.From<PayrollOvertimeTable>().Insert(newRecord);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating future balances: {ex.Message}");
            }
        }

        // Métodos auxiliares para cálculos específicos
        private async Task<decimal> GetMonthlyPayrollTotal(DateTime monthDate)
        {
            var payrollList = await GetEffectivePayroll(monthDate);
            return payrollList.Sum(p => p.MonthlyPayroll ?? 0);
        }

        private async Task<decimal> GetMonthlyExpensesTotal(DateTime monthDate)
        {
            var expenses = await GetEffectiveFixedExpenses(monthDate);
            return expenses.Sum(e => e.MonthlyAmount ?? 0);
        }


        // Para obtener un gasto fijo por ID
        public async Task<FixedExpenseTable> GetFixedExpenseById(int id)
        {
            try
            {
                var response = await _supabaseClient
                    .From<FixedExpenseTable>()
                    .Where(e => e.Id == id)
                    .Single();

                return response;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting expense by id: {ex.Message}");
                return null;
            }
        }

    }

}

    // ===============================================
    // MODELOS DE BASE DE DATOS
    // ===============================================

    [Table("t_order")]
    public class OrderDb : BaseModel
    {
        [PrimaryKey("f_order")]
        public int Id { get; set; }

        [Column("f_po")]
        public string Po { get; set; }

        [Column("f_quote")]
        public string Quote { get; set; }

        [Column("f_podate")]
        public DateTime? PoDate { get; set; }

        [Column("f_supabaseClient")]
        public int? ClientId { get; set; }

        [Column("f_contact")]
        public int? ContactId { get; set; }

        [Column("f_description")]
        public string Description { get; set; }

    
        [Column("f_salesman")]
        public int? SalesmanId { get; set; }

        [Column("f_estdelivery")]
        public DateTime? EstDelivery { get; set; }

        [Column("f_salesubtotal")]
        public decimal? SaleSubtotal { get; set; }

        [Column("f_saletotal")]
        public decimal? SaleTotal { get; set; }

        [Column("f_expense")]
        public decimal? Expense { get; set; }

        [Column("f_orderstat")]
        public int? OrderStatus { get; set; }

        [Column("progress_percentage")]
        public int ProgressPercentage { get; set; }

        [Column("order_percentage")]
        public int OrderPercentage { get; set; }

        // Campos de auditoría - ESTOS SON LOS NUEVOS/MODIFICADOS
        [Column("created_by")]
        public int? CreatedBy { get; set; }

        [Column("updated_by")]
        public int? UpdatedBy { get; set; }

        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        // Se agregó en la BD la columna de comisión, esta será usada para calcular la comisión del vendedor
        // Visible solo en el portal de vendedor

        [Column("f_commission_rate")]
        public decimal? CommissionRate { get; set; }
    }

    [Table("t_supabaseClient")]
    public class ClientDb : BaseModel
    {
        [PrimaryKey("f_supabaseClient")]
        public int Id { get; set; }

        [Column("f_name")]
        public string Name { get; set; }

        [Column("f_address1")]
        public string Address1 { get; set; }

        [Column("f_address2")]
        public string Address2 { get; set; }

        [Column("f_credit")]
        public int Credit { get; set; }

        [Column("tax_id")]
        public string TaxId { get; set; }

        [Column("phone")]
        public string Phone { get; set; }

        [Column("email")]
        public string Email { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; }

        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        [Column("created_by")]
        public int? CreatedBy { get; set; }

        [Column("updated_by")]
        public int? UpdatedBy { get; set; }
    }

    [Table("t_contact")]
    public class ContactDb : BaseModel
    {
        [PrimaryKey("f_contact")]
        public int Id { get; set; }

        [Column("f_supabaseClient")]
        public int ClientId { get; set; }

        [Column("f_contactname")]
        public string ContactName { get; set; }

        [Column("f_email")]
        public string Email { get; set; }

        [Column("f_phone")]
        public string Phone { get; set; }

        [Column("position")]
        public string Position { get; set; }

        [Column("is_primary")]
        public bool IsPrimary { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; }
    }

    [Table("users")]
    public class UserDb : BaseModel
    {
        [PrimaryKey("id")]
        public int Id { get; set; }

        [Column("username")]
        public string Username { get; set; }

        [Column("email")]
        public string Email { get; set; }

        [Column("password_hash")]
        public string PasswordHash { get; set; }

        [Column("full_name")]
        public string FullName { get; set; }

        [Column("role")]
        public string Role { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; }

        [Column("last_login")]
        public DateTime? LastLogin { get; set; }
    }

    [Table("order_status")]
    public class OrderStatusDb : BaseModel
    {
        [PrimaryKey("f_orderstatus")]
        public int Id { get; set; }

        [Column("f_name")]
        public string Name { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; }

        [Column("display_order")]
        public int DisplayOrder { get; set; }
    }

    // Clase VendorDb para compatibilidad con el código existente
    public class VendorDb
    {
        public int Id { get; set; }
        public string VendorName { get; set; }

        // Constructor desde UserDb (ya no se usa)
        public static VendorDb FromUser(UserDb user)
        {
            return new VendorDb
            {
                Id = user.Id,
                VendorName = user.FullName
            };
        }
    }

    // Modelo real de la tabla t_vendor
    [Table("t_vendor")]
    public class VendorTableDb : BaseModel
    {
        [PrimaryKey("f_vendor")]
        public int Id { get; set; }

        [Column("f_vendorname")]
        public string VendorName { get; set; }

        [Column("f_user_id")]
        public int? UserId { get; set; }

        [Column("f_commission_rate")]
        public decimal? CommissionRate { get; set; }  

        [Column("f_phone")]
        public string Phone { get; set; }

        [Column("f_email")]
        public string Email { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; }

        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }
    }

    
    [Table("t_invoice")]
    public class InvoiceDb : BaseModel
    {
        [PrimaryKey("f_invoice")]
        public int Id { get; set; }

        [Column("f_order")]
        public int? OrderId { get; set; }

        [Column("f_folio")]
        public string Folio { get; set; }

        [Column("f_invoicedate")]
        public DateTime? InvoiceDate { get; set; }

        [Column("f_receptiondate")]
        public DateTime? ReceptionDate { get; set; }  // Fecha recibo factura

        [Column("f_subtotal")]
        public decimal? Subtotal { get; set; }

        [Column("f_total")]
        public decimal? Total { get; set; }

        [Column("f_invoicestat")]
        public int? InvoiceStatus { get; set; }

        [Column("f_paymentdate")]
        public DateTime? PaymentDate { get; set; }  // Fecha pago real

        [Column("due_date")]
        public DateTime? DueDate { get; set; }  // Pago programado (calculado)

        [Column("payment_method")]
        public string PaymentMethod { get; set; }

        [Column("payment_reference")]
        public string PaymentReference { get; set; }

        [Column("balance_due")]
        public decimal? BalanceDue { get; set; }

        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        [Column("created_by")]
        public int? CreatedBy { get; set; }
    }

    [Table("invoice_status")]
    public class InvoiceStatusDb : BaseModel
    {
        [PrimaryKey("f_invoicestat")]
        public int Id { get; set; }

        [Column("f_name")]
        public string Name { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; }

        [Column("display_order")]
        public int DisplayOrder { get; set; }
    }

    // Agregar después de la clase InvoiceStatusDb

    [Table("order_history")]
    public class OrderHistoryDb : BaseModel
    {
        [PrimaryKey("id")]
        public int Id { get; set; }

        [Column("order_id")]
        public int OrderId { get; set; }

        [Column("user_id")]
        public int UserId { get; set; }

        [Column("action")]
        public string Action { get; set; }

        [Column("field_name")]
        public string FieldName { get; set; }

        [Column("old_value")]
        public string OldValue { get; set; }

        [Column("new_value")]
        public string NewValue { get; set; }

        [Column("change_description")]
        public string ChangeDescription { get; set; }

        [Column("ip_address")]
        public string IpAddress { get; set; }

        [Column("changed_at")]
        public DateTime? ChangedAt { get; set; }
    }



    [Table("t_vendor_commission_payment")]
    public class VendorCommissionPaymentDb : BaseModel
    {
        [PrimaryKey("id")]
        public int Id { get; set; }

        [Column("f_order")]
        public int OrderId { get; set; }

        [Column("f_vendor")]
        public int VendorId { get; set; }

        [Column("commission_amount")]
        public decimal CommissionAmount { get; set; }

        [Column("commission_rate")]
        public decimal CommissionRate { get; set; }

        [Column("payment_status")]
        public string PaymentStatus { get; set; } // "pending" o "paid"

        [Column("payment_date")]
        public DateTime? PaymentDate { get; set; }

        [Column("payment_reference")]
        public string PaymentReference { get; set; }

        [Column("notes")]
        public string Notes { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }

        [Column("created_by")]
        public int? CreatedBy { get; set; }

        [Column("updated_by")]
        public int? UpdatedBy { get; set; }
    }

    // PARA PROVEEDORES Y GASTOS
    [Table("t_supplier")]
    public class SupplierDb : BaseModel
    {
        [PrimaryKey("f_supplier")]
        public int Id { get; set; }

        [Column("f_suppliername")]
        public string SupplierName { get; set; }

        [Column("f_credit")]
        public int CreditDays { get; set; }

        [Column("tax_id")]
        public string TaxId { get; set; }

        [Column("phone")]
        public string Phone { get; set; }

        [Column("email")]
        public string Email { get; set; }

        [Column("address")]
        public string Address { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
    

}

    [Table("t_expense")]
    public class ExpenseDb : BaseModel
    {
        [PrimaryKey("f_expense")]
        public int Id { get; set; }

        [Column("f_supplier")]
        public int SupplierId { get; set; }

        [Column("f_description")]
        public string Description { get; set; }

        [Column("f_expensedate")]
        public DateTime ExpenseDate { get; set; }

        [Column("f_totalexpense")]
        public decimal TotalExpense { get; set; }

        [Column("f_scheduleddate")]
        public DateTime? ScheduledDate { get; set; }

        [Column("f_status")]
        public string Status { get; set; }

        [Column("f_paiddate")]
        public DateTime? PaidDate { get; set; }

        [Column("f_paymethod")]
        public string PayMethod { get; set; }

        [Column("f_order")]
        public int? OrderId { get; set; }

        [Column("expense_category")]
        public string ExpenseCategory { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }

        [Column("created_by")]
        public string CreatedBy { get; set; }

    }

    [Table("t_payroll")]
    public class PayrollTable : BaseModel
{
    [PrimaryKey("f_payroll")]
    public int Id { get; set; }

    [Column("f_employee")]
    public string Employee { get; set; }

    [Column("f_title")]
    public string Title { get; set; }

    [Column("f_hireddate")]
    public DateTime? HiredDate { get; set; }

    [Column("f_range")]
    public string Range { get; set; }

    [Column("f_condition")]
    public string Condition { get; set; }

    [Column("f_lastraise")]
    public DateTime? LastRaise { get; set; }

    [Column("f_sspayroll")]
    public decimal? SSPayroll { get; set; }

    [Column("f_weeklypayroll")]
    public decimal? WeeklyPayroll { get; set; }

    [Column("f_socialsecurity")]
    public decimal? SocialSecurity { get; set; }

    [Column("f_benefits")]
    public string Benefits { get; set; }

    [Column("f_benefitsamount")]
    public decimal? BenefitsAmount { get; set; }

    [Column("f_monthlypayroll")]
    public decimal? MonthlyPayroll { get; set; }

    [Column("employee_code")]
    public string EmployeeCode { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("created_by")]
    public int? CreatedBy { get; set; }

    [Column("updated_by")]
    public int? UpdatedBy { get; set; }
}

    [Table("t_payroll_history")]
    public class PayrollHistoryTable : BaseModel
{
    [PrimaryKey("id")]
    public int Id { get; set; }

    [Column("f_payroll")]
    public int PayrollId { get; set; }

    [Column("f_employee")]
    public string Employee { get; set; }

    [Column("f_title")]
    public string Title { get; set; }

    [Column("f_monthlypayroll")]
    public decimal? MonthlyPayroll { get; set; }

    [Column("effective_date")]
    public DateTime EffectiveDate { get; set; }

    [Column("change_type")]
    public string ChangeType { get; set; }

    [Column("change_summary")]
    public string ChangeSummary { get; set; }

    [Column("created_by")]
    public int? CreatedBy { get; set; }

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }
}


    // Tabla para gastos fijos
    [Table("t_fixed_expenses")]
    public class FixedExpenseTable : BaseModel
{
    [PrimaryKey("id")]
    public int Id { get; set; }

    [Column("expense_type")]
    public string ExpenseType { get; set; }

    [Column("description")]
    public string Description { get; set; }

    [Column("monthly_amount")]
    public decimal? MonthlyAmount { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }

    [Column("created_by")]
    public int? CreatedBy { get; set; }

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("effective_date")]
    public DateTime? EffectiveDate { get; set; } // Fecha en que el gasto fijo entra en vigor
} // fin de la calse FixedExpenseTable


[Table("t_payrollovertime")]
    public class PayrollOvertimeTable : BaseModel
    {
        [PrimaryKey("f_payrollovertime")]
        public int Id { get; set; }

        [Column("f_date")]
        public DateTime Date { get; set; }

        [Column("f_payroll")]
        public decimal? Payroll { get; set; }

        [Column("f_overtime")]
        public decimal? Overtime { get; set; }

        [Column("f_fixedexpense")]
        public decimal? FixedExpense { get; set; }

        [Column("created_by")]
        public int? CreatedBy { get; set; }

        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }
    }

    [Table("t_fixed_expenses_history")]
    public class FixedExpenseHistoryTable : BaseModel
    {
        [PrimaryKey("id")]
        public int Id { get; set; }

        [Column("expense_id")]
        public int ExpenseId { get; set; }

        [Column("description")]
        public string Description { get; set; }

        [Column("monthly_amount")]
        public decimal? MonthlyAmount { get; set; }

        [Column("effective_date")]
        public DateTime EffectiveDate { get; set; }

        [Column("change_type")]
        public string ChangeType { get; set; }

        [Column("change_summary")]
        public string ChangeSummary { get; set; }

        [Column("created_by")]
        public int? CreatedBy { get; set; }

        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }
    }