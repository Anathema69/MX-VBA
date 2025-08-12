using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Supabase;
using Supabase.Interfaces;
using SistemaGestionProyectos2.Models;
using Postgrest.Models;
using Postgrest.Attributes;

namespace SistemaGestionProyectos2.Services
{
    public class SupabaseService
    {
        private static SupabaseService _instance;
        private Client _supabaseClient;
        private IConfiguration _configuration;
        private UserSession _currentUser;
        private bool _isInitialized = false;

        // Singleton pattern
        public static SupabaseService Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new SupabaseService();
                }
                return _instance;
            }
        }

        private SupabaseService()
        {
            InitializeConfiguration();
            InitializeClient().Wait();
        }

        private void InitializeConfiguration()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(System.IO.Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            _configuration = builder.Build();
        }

        private async Task InitializeClient()
        {
            try
            {
                var url = _configuration["Supabase:Url"];
                var key = _configuration["Supabase:AnonKey"];

                var options = new SupabaseOptions
                {
                    AutoConnectRealtime = true
                };

                _supabaseClient = new Client(url, key, options);
                await _supabaseClient.InitializeAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing Supabase: {ex.Message}");
            }
        }

        // Autenticación de usuario
        public async Task<(bool Success, UserSession User, string ErrorMessage)> AuthenticateUser(string username, string password)
        {
            try
            {
                // Buscar el usuario en la tabla users
                var response = await _supabaseClient
                    .From<UserDb>()
                    .Where(x => x.Username == username)
                    .Single();

                if (response != null)
                {
                    // Verificar contraseña (en producción usar hash)
                    // Por ahora comparación simple para pruebas
                    bool passwordValid = false;

                    // Para pruebas: verificar si es el password hasheado o texto plano
                    if (password == "admin123" && username == "admin")
                    {
                        passwordValid = true;
                    }
                    else if (password == "ima2025")
                    {
                        passwordValid = true;
                    }
                    // En producción: BCrypt.Net.BCrypt.Verify(password, response.PasswordHash);

                    if (passwordValid)
                    {
                        _currentUser = new UserSession
                        {
                            Id = response.Id,
                            Username = response.Username,
                            FullName = response.FullName,
                            Role = response.Role,
                            LoginTime = DateTime.Now
                        };

                        // Actualizar último login
                        response.LastLogin = DateTime.Now;
                        await _supabaseClient
                            .From<UserDb>()
                            .Where(x => x.Id == response.Id)
                            .Update(response);

                        return (true, _currentUser, null);
                    }
                    else
                    {
                        return (false, null, "Contraseña incorrecta");
                    }
                }
                else
                {
                    return (false, null, "Usuario no encontrado");
                }
            }
            catch (Exception ex)
            {
                return (false, null, $"Error de conexión: {ex.Message}");
            }
        }

        // Obtener lista de órdenes
        public async Task<List<OrderData>> GetOrders()
        {
            try
            {
                var response = await _supabaseClient
                    .From<OrderDb>()
                    .Select("*, t_client!inner(*), t_contact!inner(*), users!vendor_id(*), order_status!inner(*)")
                    .Order("f_podate", Postgrest.Constants.Ordering.Descending)
                    .Get();

                var orders = new List<OrderData>();

                foreach (var order in response.Models)
                {
                    orders.Add(new OrderData
                    {
                        Id = order.Id,
                        OrderNumber = order.OrderNumber,
                        OrderDate = order.OrderDate,
                        ClientName = order.Client?.Name ?? "Sin cliente",
                        Description = order.Description,
                        VendorName = order.Vendor?.FullName ?? "Sin vendedor",
                        PromiseDate = order.EstimatedDelivery,
                        ProgressPercentage = order.ProgressPercentage,
                        OrderPercentage = order.OrderPercentage,
                        Subtotal = order.Subtotal,
                        Total = order.Total,
                        Status = order.OrderStatus?.Name ?? "Sin estado",
                        Invoiced = order.Invoiced,
                        LastInvoiceDate = order.LastInvoiceDate
                    });
                }

                return orders;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting orders: {ex.Message}");
                return new List<OrderData>();
            }
        }

        // Crear nueva orden
        public async Task<bool> CreateOrder(OrderData orderData)
        {
            try
            {
                var newOrder = new OrderDb
                {
                    OrderNumber = orderData.OrderNumber,
                    QuotationNumber = orderData.QuotationNumber,
                    OrderDate = orderData.OrderDate,
                    ClientId = orderData.ClientId,
                    ContactId = orderData.ContactId,
                    Description = orderData.Description,
                    VendorId = orderData.VendorId,
                    Subtotal = orderData.Subtotal,
                    Total = orderData.Total,
                    Expense = orderData.Expense,
                    EstimatedDelivery = orderData.PromiseDate,
                    StatusId = 2, // EN PROCESO por defecto
                    ProgressPercentage = 0,
                    OrderPercentage = 0,
                    CreatedBy = _currentUser.Id
                };

                // Establecer el user_id para el trigger de auditoría
                await _supabaseClient.Rpc("set_config", new Dictionary<string, object>
                {
                    { "parameter", "app.current_user_id" },
                    { "value", _currentUser.Id.ToString() }
                });

                await _supabaseClient
                    .From<OrderDb>()
                    .Insert(newOrder);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating order: {ex.Message}");
                return false;
            }
        }

        // Actualizar orden
        public async Task<bool> UpdateOrder(OrderData orderData)
        {
            try
            {
                // Establecer el user_id para el trigger de auditoría
                await _supabaseClient.Rpc("set_config", new Dictionary<string, object>
                {
                    { "parameter", "app.current_user_id" },
                    { "value", _currentUser.Id.ToString() }
                });

                var orderToUpdate = await _supabaseClient
                    .From<OrderDb>()
                    .Where(x => x.Id == orderData.Id)
                    .Single();

                if (orderToUpdate != null)
                {
                    // Actualizar campos según permisos
                    if (_currentUser.Role == "admin")
                    {
                        // Admin puede actualizar todo
                        orderToUpdate.Subtotal = orderData.Subtotal;
                        orderToUpdate.Total = orderData.Total;
                        orderToUpdate.OrderPercentage = orderData.OrderPercentage;
                    }

                    // Coordinador y Admin pueden actualizar estos campos
                    orderToUpdate.ProgressPercentage = orderData.ProgressPercentage;
                    orderToUpdate.EstimatedDelivery = orderData.PromiseDate;
                    // orderToUpdate.StatusId = orderData.StatusId;

                    await _supabaseClient
                        .From<OrderDb>()
                        .Where(x => x.Id == orderData.Id)
                        .Update(orderToUpdate);

                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating order: {ex.Message}");
                return false;
            }
        }

        // Obtener clientes
        public async Task<List<ClientData>> GetClients()
        {
            try
            {
                var response = await _supabaseClient
                    .From<ClientDb>()
                    .Where(x => x.IsActive == true)
                    .Order("f_name", Postgrest.Constants.Ordering.Ascending)
                    .Get();

                return response.Models.Select(c => new ClientData
                {
                    Id = c.Id,
                    Name = c.Name
                }).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting clients: {ex.Message}");
                return new List<ClientData>();
            }
        }

        // Obtener contactos de un cliente
        public async Task<List<ContactData>> GetContactsByClient(int clientId)
        {
            try
            {
                var response = await _supabaseClient
                    .From<ContactDb>()
                    .Where(x => x.ClientId == clientId && x.IsActive == true)
                    .Get();

                return response.Models.Select(c => new ContactData
                {
                    Id = c.Id,
                    Name = c.Name,
                    Email = c.Email
                }).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting contacts: {ex.Message}");
                return new List<ContactData>();
            }
        }

        // Obtener vendedores
        public async Task<List<UserSession>> GetVendors()
        {
            try
            {
                var response = await _supabaseClient
                    .From<UserDb>()
                    .Where(x => x.Role == "salesperson" && x.IsActive == true)
                    .Order("full_name", Postgrest.Constants.Ordering.Ascending)
                    .Get();

                return response.Models.Select(v => new UserSession
                {
                    Id = v.Id,
                    Username = v.Username,
                    FullName = v.FullName,
                    Role = v.Role
                }).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting vendors: {ex.Message}");
                return new List<UserSession>();
            }
        }

        public UserSession GetCurrentUser() => _currentUser;
        public Client GetClient() => _supabaseClient;
        public IConfiguration GetConfiguration() => _configuration;
    }

    // Modelos de base de datos (mapeo con Supabase)
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

    [Table("t_order")]
    public class OrderDb : BaseModel
    {
        [PrimaryKey("f_order")]
        public int Id { get; set; }

        [Column("f_po")]
        public string OrderNumber { get; set; }

        [Column("f_quote")]
        public string QuotationNumber { get; set; }

        [Column("f_podate")]
        public DateTime OrderDate { get; set; }

        [Column("f_client")]
        public int ClientId { get; set; }

        [Column("f_contact")]
        public int ContactId { get; set; }

        [Column("f_description")]
        public string Description { get; set; }

        [Column("vendor_id")]
        public int VendorId { get; set; }

        [Column("f_salesubtotal")]
        public decimal Subtotal { get; set; }

        [Column("f_saletotal")]
        public decimal Total { get; set; }

        [Column("f_expense")]
        public decimal Expense { get; set; }

        [Column("f_estdelivery")]
        public DateTime EstimatedDelivery { get; set; }

        [Column("f_orderstat")]
        public int StatusId { get; set; }

        [Column("progress_percentage")]
        public int ProgressPercentage { get; set; }

        [Column("order_percentage")]
        public int OrderPercentage { get; set; }

        [Column("invoiced")]
        public bool Invoiced { get; set; }

        [Column("last_invoice_date")]
        public DateTime? LastInvoiceDate { get; set; }

        [Column("created_by")]
        public int CreatedBy { get; set; }

        // Relaciones
        [Reference(typeof(ClientDb))]
        public ClientDb Client { get; set; }

        [Reference(typeof(ContactDb))]
        public ContactDb Contact { get; set; }

        [Reference(typeof(UserDb))]
        public UserDb Vendor { get; set; }

        [Reference(typeof(OrderStatusDb))]
        public OrderStatusDb OrderStatus { get; set; }
    }

    [Table("t_client")]
    public class ClientDb : BaseModel
    {
        [PrimaryKey("f_client")]
        public int Id { get; set; }

        [Column("f_name")]
        public string Name { get; set; }

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
        public string Name { get; set; }

        [Column("f_email")]
        public string Email { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; }
    }

    [Table("order_status")]
    public class OrderStatusDb : BaseModel
    {
        [PrimaryKey("f_orderstatus")]
        public int Id { get; set; }

        [Column("f_name")]
        public string Name { get; set; }
    }
}