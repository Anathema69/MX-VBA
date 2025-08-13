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

        public async Task<List<OrderDb>> GetOrders(int limit = 100)
        {
            try
            {
                var response = await _supabaseClient
                    .From<OrderDb>()
                    .Select("*")
                    .Order("f_podate", Postgrest.Constants.Ordering.Descending)
                    .Limit(limit)
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

                // No intentar asignar CreatedBy ya que no existe en el modelo
                // Si necesitas tracking de usuario, deberás agregarlo al modelo OrderDb

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
                // No intentar asignar UpdatedBy ya que no existe en el modelo
                // Si necesitas tracking de usuario, deberás agregarlo al modelo OrderDb

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
                var response = await _supabaseClient
                    .From<UserDb>()
                    .Where(x => x.Role == "salesperson")
                    .Where(x => x.IsActive == true)
                    .Get();

                var users = response?.Models ?? new List<UserDb>();

                // Convertir UserDb a VendorDb
                return users.Select(u => VendorDb.FromUser(u)).ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error obteniendo vendedores: {ex.Message}");
                throw;
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
        // MÉTODOS PARA ÓRDENES CON PAGINACIÓN
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
        public string Po { get; set; } // Cambiado de OrderNumber a Po

        // Alias para compatibilidad
        public string OrderNumber => Po;

        [Column("f_quote")]
        public string Quote { get; set; } // Cambiado de QuotationNumber a Quote

        // Alias para compatibilidad
        public string QuotationNumber => Quote;

        [Column("f_podate")]
        public DateTime? PoDate { get; set; } // Cambiado de OrderDate a PoDate

        // Alias para compatibilidad
        public DateTime? OrderDate => PoDate;

        [Column("f_client")]
        public int? ClientId { get; set; }

        [Column("f_contact")]
        public int? ContactId { get; set; }

        [Column("f_description")]
        public string Description { get; set; }

        [Column("f_salesman")]
        public int? SalesmanId { get; set; } // Cambiado de VendorId a SalesmanId

        // Alias para compatibilidad
        public int? VendorId => SalesmanId;

        [Column("f_estdelivery")]
        public DateTime? EstDelivery { get; set; } // Cambiado de PromiseDate a EstDelivery

        // Alias para compatibilidad
        public DateTime? PromiseDate => EstDelivery;

        [Column("f_salesubtotal")]
        public decimal? SaleSubtotal { get; set; } // Cambiado de Subtotal a SaleSubtotal

        // Alias para compatibilidad
        public decimal Subtotal => SaleSubtotal ?? 0;

        [Column("f_saletotal")]
        public decimal? SaleTotal { get; set; } // Cambiado de Total a SaleTotal

        // Alias para compatibilidad
        public decimal Total => SaleTotal ?? 0;

        [Column("f_expense")]
        public decimal? Expense { get; set; }

        [Column("f_orderstat")]
        public int? OrderStatus { get; set; } // Cambiado de StatusId a OrderStatus

        // Alias para compatibilidad
        public int? StatusId => OrderStatus;

        // Campos adicionales que pueden estar en la BD
        public int ProgressPercentage { get; set; }
        public int OrderPercentage { get; set; }
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
        public string ContactName { get; set; } // Cambiado de Name a ContactName

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

        // Constructor desde UserDb
        public static VendorDb FromUser(UserDb user)
        {
            return new VendorDb
            {
                Id = user.Id,
                VendorName = user.FullName
            };
        }
    }
}