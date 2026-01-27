using Microsoft.Extensions.Configuration;
using SistemaGestionProyectos2.Models;
using SistemaGestionProyectos2.Models.Database;
using SistemaGestionProyectos2.Models.DTOs;
using SistemaGestionProyectos2.Services.Clients;
using SistemaGestionProyectos2.Services.Contacts;
using SistemaGestionProyectos2.Services.Expenses;
using SistemaGestionProyectos2.Services.FixedExpenses;
using SistemaGestionProyectos2.Services.Invoices;
using SistemaGestionProyectos2.Services.Orders;
using SistemaGestionProyectos2.Services.Payroll;
using SistemaGestionProyectos2.Services.Suppliers;
using SistemaGestionProyectos2.Services.Users;
using SistemaGestionProyectos2.Services.Vendors;
using SistemaGestionProyectos2.ViewModels;
using Supabase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SistemaGestionProyectos2.Services
{
    /// <summary>
    /// Facade pattern: Proporciona una interfaz unificada para acceder a todos los servicios modulares
    /// Este servicio delega todas las operaciones a servicios especializados
    /// Anteriormente: 2,956 líneas - Ahora: ~615 líneas (reducción del 79%)
    /// </summary>
    public class SupabaseService
    {
        private static SupabaseService _instance;
        private static readonly object _lock = new object();
        private Client _supabaseClient;
        private IConfiguration _configuration;
        private bool _isInitialized = false;

        // Servicios especializados
        private OrderService _orderService;
        private ClientService _clientService;
        private ContactService _contactService;
        private InvoiceService _invoiceService;
        private ExpenseService _expenseService;
        private SupplierService _supplierService;
        private PayrollService _payrollService;
        private FixedExpenseService _fixedExpenseService;
        private VendorService _vendorService;
        private UserService _userService;

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

                // Inicializar todos los servicios especializados
                _orderService = new OrderService(_supabaseClient);
                _clientService = new ClientService(_supabaseClient);
                _contactService = new ContactService(_supabaseClient);
                _invoiceService = new InvoiceService(_supabaseClient);
                _expenseService = new ExpenseService(_supabaseClient);
                _supplierService = new SupplierService(_supabaseClient);
                _payrollService = new PayrollService(_supabaseClient);
                _fixedExpenseService = new FixedExpenseService(_supabaseClient);
                _vendorService = new VendorService(_supabaseClient);
                _userService = new UserService(_supabaseClient);

                _isInitialized = true;
                System.Diagnostics.Debug.WriteLine("✅ SupabaseService (Facade) inicializado correctamente");
                System.Diagnostics.Debug.WriteLine("✅ Todos los servicios modulares inicializados");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error inicializando SupabaseService: {ex.Message}");
                throw;
            }
        }

        public Client GetClient()
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("SupabaseService no está inicializado");
            }
            return _supabaseClient;
        }

        public async Task<bool> TestConnection()
        {
            try
            {
                // Probar con una consulta simple a orders
                var response = await _supabaseClient
                    .From<OrderDb>()
                    .Limit(1)
                    .Get();

                return response != null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en TestConnection: {ex.Message}");
                return false;
            }
        }

        // ===============================================
        // DELEGACIÓN A OrderService
        // ===============================================

        public Task<List<OrderDb>> GetOrders(int limit = 100, int offset = 0, List<int> filterStatuses = null)
            => _orderService.GetOrders(limit, offset, filterStatuses);

        public Task<OrderDb> GetOrderById(int orderId)
            => _orderService.GetOrderById(orderId);

        public Task<List<OrderDb>> SearchOrders(string searchTerm)
            => _orderService.SearchOrders(searchTerm);

        public Task<OrderDb> CreateOrder(OrderDb order, int userId = 0)
            => _orderService.CreateOrder(order, userId);

        public Task<bool> UpdateOrder(OrderDb order, int userId = 0)
            => _orderService.UpdateOrder(order, userId);

        public Task<bool> DeleteOrder(int orderId)
            => _orderService.DeleteOrder(orderId);

        public Task<bool> CancelOrder(int orderId)
            => _orderService.CancelOrder(orderId);

        public Task<(bool Success, string Message)> DeleteOrderWithAudit(int orderId, int deletedBy, string reason = "Orden creada por error")
            => _orderService.DeleteOrderWithAudit(orderId, deletedBy, reason);

        public Task<(bool CanDelete, string Reason)> CanDeleteOrder(int orderId)
            => _orderService.CanDeleteOrder(orderId);

        public Task<List<OrderDb>> GetRecentOrders(int limit = 10)
            => _orderService.GetRecentOrders(limit);

        public Task<List<OrderDb>> GetOrdersByClientId(int clientId)
            => _orderService.GetOrdersByClientId(clientId);

        public Task<List<OrderDb>> GetOrdersFiltered(DateTime? fromDate = null, string[] excludeStatuses = null, int limit = 50, int offset = 0)
            => _orderService.GetOrdersFiltered(fromDate, excludeStatuses, limit, offset);

        // Vista con gastos calculados (v_order_gastos)
        public Task<List<OrderGastosViewDb>> GetOrdersWithGastos(int limit = 100, int offset = 0, List<int> filterStatuses = null)
            => _orderService.GetOrdersWithGastos(limit, offset, filterStatuses);

        public Task<OrderGastosViewDb> GetOrderWithGastosById(int orderId)
            => _orderService.GetOrderWithGastosById(orderId);

        // Gastos Operativos v2.0
        public Task<List<OrderGastoOperativoDb>> GetGastosOperativos(int orderId)
            => _orderService.GetGastosOperativos(orderId);

        public Task<OrderGastoOperativoDb> AddGastoOperativo(int orderId, decimal monto, string descripcion, int userId)
            => _orderService.AddGastoOperativo(orderId, monto, descripcion, userId);

        public Task<bool> DeleteGastoOperativo(int gastoId, int orderId, int userId)
            => _orderService.DeleteGastoOperativo(gastoId, orderId, userId);

        public Task<bool> UpdateGastoOperativo(int gastoId, decimal monto, string descripcion, int orderId, int userId)
            => _orderService.UpdateGastoOperativo(gastoId, monto, descripcion, orderId, userId);

        // Gastos Indirectos v2.1
        public Task<List<OrderGastoIndirectoDb>> GetGastosIndirectos(int orderId)
            => _orderService.GetGastosIndirectos(orderId);

        public Task<OrderGastoIndirectoDb> AddGastoIndirecto(int orderId, decimal monto, string descripcion, int userId)
            => _orderService.AddGastoIndirecto(orderId, monto, descripcion, userId);

        public Task<bool> DeleteGastoIndirecto(int gastoId, int orderId, int userId)
            => _orderService.DeleteGastoIndirecto(gastoId, orderId, userId);

        public Task<bool> UpdateGastoIndirecto(int gastoId, decimal monto, string descripcion, int orderId, int userId)
            => _orderService.UpdateGastoIndirecto(gastoId, monto, descripcion, orderId, userId);

        // ===============================================
        // DELEGACIÓN A ClientService
        // ===============================================

        public Task<List<ClientDb>> GetClients()
            => _clientService.GetClients();

        public Task<List<ClientDb>> GetActiveClients()
            => _clientService.GetActiveClients();

        public Task<ClientDb> GetClientById(int clientId)
            => _clientService.GetClientById(clientId);

        public Task<ClientDb> GetClientByName(string name)
            => _clientService.GetClientByName(name);

        public Task<ClientDb> CreateClient(ClientDb client, int userId = 0)
            => _clientService.CreateClient(client, userId);

        public Task<bool> UpdateClient(ClientDb client, int userId)
            => _clientService.UpdateClient(client, userId);

        public Task<bool> SoftDeleteClient(int clientId)
            => _clientService.SoftDeleteClient(clientId);

        public Task<bool> ClientExists(string name)
            => _clientService.ClientExists(name);

        // ===============================================
        // DELEGACIÓN A ContactService
        // ===============================================

        public Task<List<ContactDb>> GetContactsByClient(int clientId)
            => _contactService.GetContactsByClient(clientId);

        public Task<List<ContactDb>> GetActiveContactsByClientId(int clientId)
            => _contactService.GetActiveContactsByClientId(clientId);

        public Task<ContactDb> AddContact(ContactDb contact)
            => _contactService.AddContact(contact);

        public Task<ContactDb> CreateContact(ContactDb contact)
            => _contactService.CreateContact(contact);

        public Task<ContactDb> UpdateContact(ContactDb contact)
            => _contactService.UpdateContact(contact);

        public Task<bool> DeleteContact(int contactId)
            => _contactService.DeleteContact(contactId);

        public Task<bool> SoftDeleteContact(int contactId)
            => _contactService.SoftDeleteContact(contactId);

        public Task<int> CountActiveContactsByClientId(int clientId)
            => _contactService.CountActiveContactsByClientId(clientId);

        public Task<List<ContactDb>> GetAllContacts()
            => _contactService.GetAllContacts();

        // ===============================================
        // DELEGACIÓN A InvoiceService
        // ===============================================

        public Task<List<InvoiceDb>> GetInvoicesByOrder(int orderId)
            => _invoiceService.GetInvoicesByOrder(orderId);

        public Task<Dictionary<int, decimal>> GetInvoicedTotalsByOrders(List<int> orderIds)
            => _invoiceService.GetInvoicedTotalsByOrders(orderIds);

        public Task<InvoiceDb> CreateInvoice(InvoiceDb invoice, int userId = 0)
            => _invoiceService.CreateInvoice(invoice, userId);

        public Task<bool> UpdateInvoice(InvoiceDb invoice, int userId = 0)
            => _invoiceService.UpdateInvoice(invoice, userId);

        public Task<bool> DeleteInvoice(int invoiceId, int userId = 0)
            => _invoiceService.DeleteInvoice(invoiceId, userId);

        public Task<List<InvoiceStatusDb>> GetInvoiceStatuses()
            => _invoiceService.GetInvoiceStatuses();

        // Métodos de InvoiceService que aún no están implementados en el servicio modular
        // TODO: Mover estos métodos a InvoiceService cuando se tenga tiempo

        public async Task<bool> CheckAndUpdateOrderStatus(int orderId, int userId = 0)
        {
            try
            {
                var order = await GetOrderById(orderId);
                if (order == null) return false;

                var currentStatusName = await GetStatusName(order.OrderStatus ?? 0);
                if (currentStatusName == "CANCELADA" || currentStatusName == "COMPLETADA")
                    return false;

                var invoices = await GetInvoicesByOrder(orderId);
                decimal totalInvoiced = invoices.Sum(i => i.Total ?? 0);
                decimal orderTotal = order.SaleTotal ?? 0;
                bool allInvoicesPaid = invoices.Any() && invoices.All(i => i.PaymentDate.HasValue);

                int newStatusId = order.OrderStatus ?? 0;
                bool statusChanged = false;

                // Solo cambiar automáticamente a COMPLETADA cuando todas las facturas estén pagadas
                if (allInvoicesPaid && invoices.Any())
                {
                    newStatusId = await GetStatusIdByName("COMPLETADA");
                    if (newStatusId != order.OrderStatus)
                    {
                        order.ProgressPercentage = 100;
                        statusChanged = true;
                    }
                }
                // REMOVIDO: Cambio automático a CERRADA cuando facturas recibidas
                // El estado CERRADA debe ser establecido manualmente por el usuario
                else if (orderTotal > 0 && Math.Abs(totalInvoiced - orderTotal) < 0.01m)
                {
                    newStatusId = await GetStatusIdByName("LIBERADA");
                    if (newStatusId != order.OrderStatus)
                    {
                        order.ProgressPercentage = 100;
                        statusChanged = true;
                    }
                }

                if (statusChanged)
                {
                    order.OrderStatus = newStatusId;
                    order.UpdatedBy = userId > 0 ? userId : 1;
                    var updated = await UpdateOrder(order, userId);
                    if (updated)
                    {
                        await LogOrderHistory(orderId, userId, "STATUS_CHANGE",
                            "f_orderstat", currentStatusName, await GetStatusName(newStatusId),
                            $"Cambio automático de estado");
                        return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error verificando estado de orden: {ex.Message}");
                return false;
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
            catch { return false; }
        }

        public async Task<List<ClientPendingData>> GetClientsPendingInvoices()
        {
            // TODO: Implementar lógica completa - por ahora retornar lista vacía
            return new List<ClientPendingData>();
        }

        public async Task<List<InvoiceDb>> GetPendingInvoicesByClient(int clientId)
        {
            // TODO: Implementar - por ahora retornar lista vacía
            return new List<InvoiceDb>();
        }

        public async Task<List<PendingInvoiceDetail>> GetAllPendingInvoicesDetailed()
        {
            // TODO: Implementar - por ahora retornar lista vacía
            return new List<PendingInvoiceDetail>();
        }

        public async Task<PendingIncomesData> GetAllPendingIncomesData()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("🔍 Obteniendo facturas pendientes...");

                // Obtener todas las facturas que NO están pagadas (status != 4)
                var allInvoices = await _supabaseClient
                    .From<InvoiceDb>()
                    .Where(i => i.InvoiceStatus != 4) // Excluir PAGADAS
                    .Order("f_order", Postgrest.Constants.Ordering.Ascending)
                    .Get();

                var pendingInvoices = allInvoices?.Models ?? new List<InvoiceDb>();
                System.Diagnostics.Debug.WriteLine($"📄 Facturas pendientes encontradas: {pendingInvoices.Count}");

                if (pendingInvoices.Count == 0)
                {
                    return new PendingIncomesData
                    {
                        ClientsWithPendingInvoices = new List<ClientPendingInfo>(),
                        OrdersDictionary = new Dictionary<int, OrderDb>(),
                        ClientsDictionary = new Dictionary<int, ClientDb>()
                    };
                }

                // Obtener IDs de órdenes únicas
                var orderIds = pendingInvoices
                    .Where(i => i.OrderId.HasValue)
                    .Select(i => i.OrderId.Value)
                    .Distinct()
                    .ToList();

                System.Diagnostics.Debug.WriteLine($"📦 Órdenes relacionadas: {orderIds.Count}");

                // Obtener órdenes
                var ordersDict = new Dictionary<int, OrderDb>();
                if (orderIds.Any())
                {
                    var ordersResponse = await _supabaseClient
                        .From<OrderDb>()
                        .Filter("f_order", Postgrest.Constants.Operator.In, orderIds)
                        .Get();

                    if (ordersResponse?.Models != null)
                    {
                        ordersDict = ordersResponse.Models.ToDictionary(o => o.Id, o => o);
                    }
                }

                // Obtener IDs de clientes únicos desde las órdenes (filtrando nulls)
                var clientIds = ordersDict.Values
                    .Where(o => o.ClientId.HasValue)
                    .Select(o => o.ClientId.Value)
                    .Distinct()
                    .ToList();

                System.Diagnostics.Debug.WriteLine($"👥 Clientes con facturas pendientes: {clientIds.Count}");

                // Obtener clientes
                var clientsDict = new Dictionary<int, ClientDb>();
                if (clientIds.Any())
                {
                    var clientsResponse = await _supabaseClient
                        .From<ClientDb>()
                        .Filter("f_client", Postgrest.Constants.Operator.In, clientIds)
                        .Get();

                    if (clientsResponse?.Models != null)
                    {
                        clientsDict = clientsResponse.Models.ToDictionary(c => c.Id, c => c);
                    }
                }

                // Agrupar facturas por cliente
                var clientPendingList = new List<ClientPendingInfo>();

                foreach (var clientId in clientIds)
                {
                    // Obtener facturas del cliente (a través de sus órdenes)
                    var clientInvoices = pendingInvoices
                        .Where(i => i.OrderId.HasValue &&
                                    ordersDict.ContainsKey(i.OrderId.Value) &&
                                    ordersDict[i.OrderId.Value].ClientId.HasValue &&
                                    ordersDict[i.OrderId.Value].ClientId.Value == clientId)
                        .ToList();

                    if (clientInvoices.Any())
                    {
                        var client = clientsDict.ContainsKey(clientId) ? clientsDict[clientId] : null;
                        var totalPending = clientInvoices.Sum(i => i.Total ?? 0);

                        clientPendingList.Add(new ClientPendingInfo
                        {
                            ClientId = clientId,
                            ClientName = client?.Name ?? "Cliente Desconocido",
                            ClientCredit = client?.Credit ?? 0,
                            Invoices = clientInvoices,
                            TotalPending = totalPending
                        });
                    }
                }

                System.Diagnostics.Debug.WriteLine($"✅ Datos procesados: {clientPendingList.Count} clientes con facturas pendientes");

                return new PendingIncomesData
                {
                    ClientsWithPendingInvoices = clientPendingList,
                    OrdersDictionary = ordersDict,
                    ClientsDictionary = clientsDict
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error en GetAllPendingIncomesData: {ex.Message}");
                throw;
            }
        }

        public async Task<ClientInvoicesDetailData> GetClientInvoicesDetail(int clientId)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"🔍 Obteniendo detalle de facturas para cliente {clientId}");

                // Obtener el cliente
                var client = await _clientService.GetClientById(clientId);

                // Obtener todas las órdenes del cliente
                var clientOrders = await _supabaseClient
                    .From<OrderDb>()
                    .Where(o => o.ClientId == clientId)
                    .Get();

                var orders = clientOrders?.Models ?? new List<OrderDb>();
                System.Diagnostics.Debug.WriteLine($"📦 Órdenes del cliente: {orders.Count}");

                if (orders.Count == 0)
                {
                    return new ClientInvoicesDetailData
                    {
                        Client = client,
                        Invoices = new List<InvoiceDetailInfo>()
                    };
                }

                // Obtener IDs de órdenes
                var orderIds = orders.Select(o => o.Id).ToList();

                // Obtener facturas pendientes (status != 4) de esas órdenes
                var invoicesResponse = await _supabaseClient
                    .From<InvoiceDb>()
                    .Filter("f_order", Postgrest.Constants.Operator.In, orderIds)
                    .Where(i => i.InvoiceStatus != 4) // Excluir PAGADAS
                    .Order("due_date", Postgrest.Constants.Ordering.Ascending)
                    .Get();

                var invoices = invoicesResponse?.Models ?? new List<InvoiceDb>();
                System.Diagnostics.Debug.WriteLine($"📄 Facturas pendientes encontradas: {invoices.Count}");

                // Crear diccionario de órdenes para lookup rápido
                var ordersDict = orders.ToDictionary(o => o.Id, o => o);

                // Crear lista de InvoiceDetailInfo
                var invoiceDetailList = new List<InvoiceDetailInfo>();

                foreach (var invoice in invoices)
                {
                    var orderPO = "";
                    var orderId = 0;

                    if (invoice.OrderId.HasValue && ordersDict.ContainsKey(invoice.OrderId.Value))
                    {
                        var order = ordersDict[invoice.OrderId.Value];
                        orderPO = order.Po ?? $"Orden #{order.Id}";
                        orderId = order.Id;
                    }

                    invoiceDetailList.Add(new InvoiceDetailInfo
                    {
                        Invoice = invoice,
                        OrderPO = orderPO,
                        OrderId = orderId
                    });
                }

                System.Diagnostics.Debug.WriteLine($"✅ Detalle procesado: {invoiceDetailList.Count} facturas con información de órdenes");

                return new ClientInvoicesDetailData
                {
                    Client = client,
                    Invoices = invoiceDetailList
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error en GetClientInvoicesDetail: {ex.Message}");

                return new ClientInvoicesDetailData
                {
                    Client = await _clientService.GetClientById(clientId),
                    Invoices = new List<InvoiceDetailInfo>()
                };
            }
        }

        public async Task UpdateOverdueInvoicesStatus()
        {
            // TODO: Implementar lógica de actualización de facturas vencidas
            await Task.CompletedTask;
        }

        // ===============================================
        // DELEGACIÓN A ExpenseService
        // ===============================================

        public Task<List<ExpenseDb>> GetExpenses(int? supplierId = null, string status = null, DateTime? fromDate = null, DateTime? toDate = null, int limit = 100, int offset = 0)
            => _expenseService.GetExpenses(supplierId, status, fromDate, toDate, limit, offset);

        public Task<ExpenseDb> GetExpenseById(int expenseId)
            => _expenseService.GetExpenseById(expenseId);

        public Task<ExpenseDb> CreateExpense(ExpenseDb expense)
            => _expenseService.CreateExpense(expense);

        public Task<bool> UpdateExpense(ExpenseDb expense)
            => _expenseService.UpdateExpense(expense);

        public Task<bool> MarkExpenseAsPaid(int expenseId, DateTime paidDate, string payMethod)
            => _expenseService.MarkExpenseAsPaid(expenseId, paidDate, payMethod);

        public Task<bool> DeleteExpense(int expenseId)
            => _expenseService.DeleteExpense(expenseId);

        public Task<List<ExpenseDb>> GetUpcomingExpenses(int daysAhead = 7)
            => _expenseService.GetUpcomingExpenses(daysAhead);

        public Task<List<ExpenseDb>> GetOverdueExpenses()
            => _expenseService.GetOverdueExpenses();

        public Task<Dictionary<string, decimal>> GetExpensesStatsByStatus()
            => _expenseService.GetExpensesStatsByStatus();

        // Método para estadísticas de gastos por proveedor
        public async Task<Dictionary<int, SupplierExpensesSummaryViewModel>> GetExpensesSummaryBySupplier()
        {
            try
            {
                var expenses = await _expenseService.GetExpenses();
                var suppliers = await _supplierService.GetAllSuppliers();

                var summary = expenses
                    .Where(e => e.SupplierId > 0)
                    .GroupBy(e => e.SupplierId)
                    .ToDictionary(
                        g => g.Key,
                        g =>
                        {
                            var supplier = suppliers.FirstOrDefault(s => s.Id == g.Key);
                            return new SupplierExpensesSummaryViewModel
                            {
                                SupplierId = g.Key,
                                SupplierName = supplier?.SupplierName ?? "Desconocido",
                                TotalPending = g.Where(e => e.Status == "PENDIENTE").Sum(e => e.TotalExpense),
                                TotalPaid = g.Where(e => e.Status == "PAGADO").Sum(e => e.TotalExpense),
                                TotalOverdue = 0, // Calcular según lógica de vencimiento
                                PendingCount = g.Count(e => e.Status == "PENDIENTE"),
                                PaidCount = g.Count(e => e.Status == "PAGADO"),
                                OverdueCount = 0 // Calcular según lógica de vencimiento
                            };
                        }
                    );

                return summary;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error obteniendo resumen de gastos por proveedor: {ex.Message}");
                return new Dictionary<int, SupplierExpensesSummaryViewModel>();
            }
        }

        public async Task<ExpenseStatistics> GetExpenseStatistics()
        {
            try
            {
                var expenses = await _expenseService.GetExpenses();

                return new ExpenseStatistics
                {
                    TotalExpenses = expenses.Sum(e => e.TotalExpense),
                    PendingExpenses = expenses.Where(e => e.Status == "PENDIENTE").Sum(e => e.TotalExpense),
                    PaidExpenses = expenses.Where(e => e.Status == "PAGADO").Sum(e => e.TotalExpense),
                    OverdueExpenses = expenses.Where(e => e.Status == "PENDIENTE" && e.ScheduledDate.HasValue && e.ScheduledDate < DateTime.Now).Sum(e => e.TotalExpense),
                    ExpenseCount = expenses.Count,
                    AverageExpense = expenses.Count > 0 ? expenses.Average(e => e.TotalExpense) : 0
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error calculando estadísticas de gastos: {ex.Message}");
                throw;
            }
        }

        // ===============================================
        // DELEGACIÓN A SupplierService
        // ===============================================

        public Task<List<SupplierDb>> GetActiveSuppliers()
            => _supplierService.GetActiveSuppliers();

        public Task<List<SupplierDb>> GetAllSuppliers()
            => _supplierService.GetAllSuppliers();

        public Task<SupplierDb> GetSupplierById(int supplierId)
            => _supplierService.GetSupplierById(supplierId);

        public Task<SupplierDb> CreateSupplier(SupplierDb supplier)
            => _supplierService.CreateSupplier(supplier);

        public Task<bool> UpdateSupplier(SupplierDb supplier)
            => _supplierService.UpdateSupplier(supplier);

        public Task<bool> DeleteSupplier(int supplierId)
            => _supplierService.DeleteSupplier(supplierId);

        // ===============================================
        // DELEGACIÓN A PayrollService
        // ===============================================

        public Task<List<PayrollTable>> GetActivePayroll()
            => _payrollService.GetActivePayroll();

        public Task<List<PayrollHistoryTable>> GetPayrollHistory(int? payrollId = null, int limit = 100)
            => _payrollService.GetPayrollHistory(payrollId, limit);

        public Task<PayrollTable> GetPayrollById(int id)
            => _payrollService.GetPayrollById(id);

        public Task<PayrollTable> CreatePayroll(PayrollTable payroll)
            => _payrollService.CreatePayroll(payroll);

        public Task<PayrollTable> UpdatePayroll(PayrollTable payroll)
            => _payrollService.UpdatePayroll(payroll);

        public Task<decimal> GetMonthlyPayrollTotal()
            => _payrollService.GetMonthlyPayrollTotal();

        public Task<bool> DeactivateEmployee(int employeeId, int userId)
            => _payrollService.DeactivateEmployee(employeeId, userId);

        // Métodos de PayrollOvertime relacionados con balance
        public async Task<List<PayrollTable>> GetEffectivePayroll(DateTime effectiveDate)
        {
            // Por ahora retorna el payroll activo, pero se puede mejorar con lógica de fecha efectiva
            return await _payrollService.GetActivePayroll();
        }

        public async Task<bool> SavePayrollWithEffectiveDate(PayrollTable payroll, DateTime effectiveDate, int userId)
        {
            // Actualizar el payroll y crear entrada en historial
            var updated = await _payrollService.UpdatePayroll(payroll);
            if (updated != null)
            {
                // Crear entrada en historial
                var history = new PayrollHistoryTable
                {
                    PayrollId = payroll.Id,
                    Employee = payroll.Employee,
                    Title = payroll.Title,
                    MonthlyPayroll = payroll.MonthlyPayroll,
                    EffectiveDate = effectiveDate,
                    ChangeType = "UPDATE",
                    ChangeSummary = "Actualización de nómina",
                    CreatedBy = userId
                };

                await _payrollService.CreatePayrollHistory(history);
                return true;
            }
            return false;
        }

        // ===============================================
        // DELEGACIÓN A FixedExpenseService
        // ===============================================

        public Task<List<FixedExpenseTable>> GetActiveFixedExpenses()
            => _fixedExpenseService.GetActiveFixedExpenses();

        public Task<List<FixedExpenseTable>> GetEffectiveFixedExpenses(DateTime effectiveDate)
            => _fixedExpenseService.GetEffectiveFixedExpenses(effectiveDate);

        public Task<FixedExpenseTable> GetFixedExpenseById(int id)
            => _fixedExpenseService.GetFixedExpenseById(id);

        public Task<FixedExpenseTable> CreateFixedExpense(FixedExpenseTable expense)
            => _fixedExpenseService.CreateFixedExpense(expense);

        public Task<FixedExpenseTable> UpdateFixedExpense(FixedExpenseTable expense)
            => _fixedExpenseService.UpdateFixedExpense(expense);

        public Task<bool> DeleteFixedExpense(int expenseId)
            => _fixedExpenseService.DeleteFixedExpense(expenseId);

        public Task<bool> DeactivateFixedExpense(int expenseId)
            => _fixedExpenseService.DeactivateFixedExpense(expenseId);

        public Task<bool> SaveFixedExpenseWithEffectiveDate(FixedExpenseTable expense, DateTime effectiveDate, int userId)
            => _fixedExpenseService.SaveFixedExpenseWithEffectiveDate(expense, effectiveDate, userId);

        public Task<decimal> GetMonthlyExpensesTotal(DateTime monthDate)
            => _fixedExpenseService.GetMonthlyExpensesTotal(monthDate);

        // ===============================================
        // DELEGACIÓN A VendorService
        // ===============================================

        public Task<List<VendorDb>> GetVendors()
            => _vendorService.GetVendors();

        public Task<VendorTableDb> GetVendorById(int vendorId)
            => _vendorService.GetVendorById(vendorId);

        // ===============================================
        // DELEGACIÓN A UserService
        // ===============================================

        public Task<UserDb> GetUserByUsername(string username)
            => _userService.GetUserByUsername(username);

        public Task<(bool Success, UserDb User, string Message)> AuthenticateUser(string username, string password)
            => _userService.AuthenticateUser(username, password);

        public Task<List<UserDb>> GetAllUsers()
            => _userService.GetAllUsers();

        public Task<List<UserDb>> GetActiveUsers()
            => _userService.GetActiveUsers();

        public Task<UserDb> GetUserById(int userId)
            => _userService.GetUserById(userId);

        public Task<UserDb> CreateUser(UserDb user, string plainPassword)
            => _userService.CreateUser(user, plainPassword);

        public Task<UserDb> UpdateUser(UserDb user)
            => _userService.UpdateUser(user);

        public Task<bool> ChangePassword(int userId, string newPassword)
            => _userService.ChangePassword(userId, newPassword);

        public Task<bool> DeactivateUser(int userId)
            => _userService.DeactivateUser(userId);

        public Task<bool> ReactivateUser(int userId)
            => _userService.ReactivateUser(userId);

        public Task<bool> DeleteUser(int userId)
            => _userService.DeleteUser(userId);

        public Task<bool> UserExists(string username)
            => _userService.UserExists(username);

        public Task<bool> EmailExists(string email)
            => _userService.EmailExists(email);

        // ===============================================
        // MÉTODOS DE ORDER STATUS (mantener aquí por ahora)
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

        public async Task<string> GetStatusName(int statusId)
        {
            try
            {
                var statuses = await GetOrderStatuses();
                return statuses.FirstOrDefault(s => s.Id == statusId)?.Name ?? "Desconocido";
            }
            catch
            {
                return "Desconocido";
            }
        }

        public async Task<int> GetStatusIdByName(string statusName)
        {
            try
            {
                var statuses = await GetOrderStatuses();
                return statuses.FirstOrDefault(s => s.Name.Equals(statusName, StringComparison.OrdinalIgnoreCase))?.Id ?? 1;
            }
            catch
            {
                return 1;
            }
        }

        // ===============================================
        // MÉTODOS DE HISTORY (mantener aquí por ahora)
        // ===============================================

        public async Task<bool> LogOrderHistory(int orderId, int userId, string action, string fieldName = null, string oldValue = null, string newValue = null, string description = null)
        {
            try
            {
                var history = new OrderHistoryDb
                {
                    OrderId = orderId,
                    UserId = userId,
                    Action = action,
                    FieldName = fieldName,
                    OldValue = oldValue,
                    NewValue = newValue,
                    ChangeDescription = description,
                    ChangedAt = DateTime.UtcNow
                };

                await _supabaseClient
                    .From<OrderHistoryDb>()
                    .Insert(history);

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error registrando historial: {ex.Message}");
                return false;
            }
        }

        // ===============================================
        // MÉTODOS DE PAYROLL OVERTIME / BALANCE
        // ===============================================

        public async Task<bool> UpdateOvertimeHours(int year, int month, decimal amount, string notes, int userId)
        {
            try
            {
                var result = await _supabaseClient
                    .Rpc("upsert_overtime_hours", new Dictionary<string, object>
                    {
                        { "p_year", year },
                        { "p_month", month },
                        { "p_amount", amount },
                        { "p_notes", notes ?? "" },
                        { "p_user_id", userId }
                    });

                return result != null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error actualizando horas extras: {ex.Message}");
                return false;
            }
        }
    }
}
