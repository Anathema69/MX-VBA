using Microsoft.Extensions.Configuration;
using Postgrest.Attributes;
using Postgrest.Interfaces;
using Postgrest.Models;
using Postgrest.Responses;
using SistemaGestionProyectos2.ViewModels;
using Supabase;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

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

        public async Task<SupplierDb> CreateSupplier(SupplierDb supplier)
        {
            try
            {
                supplier.CreatedAt = DateTime.Now;
                supplier.UpdatedAt = DateTime.Now;
                supplier.IsActive = true;

                var response = await _supabaseClient
                    .From<SupplierDb>()
                    .Insert(supplier);

                return response?.Models?.FirstOrDefault();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creando proveedor: {ex.Message}");
                throw;
            }
        }

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
                System.Diagnostics.Debug.WriteLine($"Error actualizando proveedor: {ex.Message}");
                return false;
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

        public async Task<ExpenseDb> CreateExpense(ExpenseDb expense, int creditDays)
        {
            try
            {
                // Calcular fecha programada
                expense.ScheduledDate = expense.ExpenseDate.AddDays(creditDays);

                // Establecer valores por defecto
                expense.Status = "PENDIENTE";
                expense.CreatedAt = DateTime.Now;
                expense.UpdatedAt = DateTime.Now;
                expense.CreatedBy = GetCurrentUserId(); // Implementar según tu sistema de usuarios

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

        [Column("f_client")]
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

    [Table("t_client")]
    public class ClientDb : BaseModel
    {
        [PrimaryKey("f_client")]
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

        [Column("f_client")]
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

    // NUEVOS CAMBIOS PARA FACTURAS
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

