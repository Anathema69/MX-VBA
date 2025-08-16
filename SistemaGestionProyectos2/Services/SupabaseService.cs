using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Supabase;
using Postgrest.Attributes;
using Postgrest.Models;

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

        public async Task<List<OrderDb>> GetOrders(int limit = 100, int offset = 0)
        {
            try
            {
                var response = await _supabaseClient
                    .From<OrderDb>()
                    .Select("*")
                    .Order("f_podate", Postgrest.Constants.Ordering.Descending)
                    .Range(offset, offset + limit - 1)
                    .Get();

                return response?.Models ?? new List<OrderDb>();
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
                    .Select("*")
                    .Order("f_name", Postgrest.Constants.Ordering.Ascending)
                    .Get();

                return response?.Models ?? new List<ClientDb>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error obteniendo clientes: {ex.Message}");
                throw;
            }
        }

        // ===============================================
        // MÉTODOS PARA CONTACTOS
        // ===============================================

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
                        .Select(g => new {
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

    // METODOS PARA FACTURAS

}

