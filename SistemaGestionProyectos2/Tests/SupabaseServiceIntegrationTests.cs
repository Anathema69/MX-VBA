using System;
using System.Diagnostics;
using System.Threading.Tasks;
using SistemaGestionProyectos2.Services;

namespace SistemaGestionProyectos2.Tests
{
    /// <summary>
    /// Tests de integración para validar funcionalidad de SupabaseService
    /// antes y después de la modularización
    /// </summary>
    public class SupabaseServiceIntegrationTests
    {
        private SupabaseService _service;

        public SupabaseServiceIntegrationTests()
        {
            _service = SupabaseService.Instance;
        }

        public async Task<bool> RunAllTests()
        {
            Console.WriteLine("=== INICIANDO TESTS DE INTEGRACIÓN ===\n");

            bool allPassed = true;

            // Test de conexión
            allPassed &= await TestConnection();

            // Test de órdenes
            allPassed &= await TestOrders();

            // Test de clientes
            allPassed &= await TestClients();

            // Test de contactos
            allPassed &= await TestContacts();

            // Test de facturas
            allPassed &= await TestInvoices();

            // Test de proveedores
            allPassed &= await TestSuppliers();

            // Test de gastos
            allPassed &= await TestExpenses();

            // Test de nómina
            allPassed &= await TestPayroll();

            // Test de vendedores
            allPassed &= await TestVendors();

            Console.WriteLine("\n=== RESULTADOS FINALES ===");
            Console.WriteLine(allPassed ? "✅ TODOS LOS TESTS PASARON" : "❌ ALGUNOS TESTS FALLARON");

            return allPassed;
        }

        private async Task<bool> TestConnection()
        {
            try
            {
                Console.WriteLine("🔌 [TEST] Conexión a Supabase...");
                var result = await _service.TestConnection();
                Console.WriteLine(result ? "✅ Conexión exitosa" : "❌ Error de conexión");
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> TestOrders()
        {
            try
            {
                Console.WriteLine("\n📦 [TEST] Órdenes...");

                // Obtener órdenes
                var orders = await _service.GetOrders(limit: 5);
                Console.WriteLine($"  - GetOrders: {orders.Count} órdenes obtenidas");

                if (orders.Count > 0)
                {
                    // Obtener orden por ID
                    var order = await _service.GetOrderById(orders[0].Id);
                    Console.WriteLine($"  - GetOrderById: Orden #{order?.Id} obtenida");

                    // Buscar órdenes
                    var searchResults = await _service.SearchOrders("PO");
                    Console.WriteLine($"  - SearchOrders: {searchResults.Count} resultados");
                }

                Console.WriteLine("✅ Test de Órdenes completado");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error en test de Órdenes: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> TestClients()
        {
            try
            {
                Console.WriteLine("\n👥 [TEST] Clientes...");

                var clients = await _service.GetClients();
                Console.WriteLine($"  - GetClients: {clients.Count} clientes obtenidos");

                var activeClients = await _service.GetActiveClients();
                Console.WriteLine($"  - GetActiveClients: {activeClients.Count} clientes activos");

                Console.WriteLine("✅ Test de Clientes completado");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error en test de Clientes: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> TestContacts()
        {
            try
            {
                Console.WriteLine("\n📞 [TEST] Contactos...");

                var clients = await _service.GetClients();
                if (clients.Count > 0)
                {
                    var contacts = await _service.GetContactsByClient(clients[0].Id);
                    Console.WriteLine($"  - GetContactsByClient: {contacts.Count} contactos obtenidos");

                    var activeContacts = await _service.GetActiveContactsByClientId(clients[0].Id);
                    Console.WriteLine($"  - GetActiveContactsByClientId: {activeContacts.Count} contactos activos");
                }

                Console.WriteLine("✅ Test de Contactos completado");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error en test de Contactos: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> TestInvoices()
        {
            try
            {
                Console.WriteLine("\n🧾 [TEST] Facturas...");

                var orders = await _service.GetOrders(limit: 1);
                if (orders.Count > 0)
                {
                    var invoices = await _service.GetInvoicesByOrder(orders[0].Id);
                    Console.WriteLine($"  - GetInvoicesByOrder: {invoices.Count} facturas obtenidas");
                }

                var statuses = await _service.GetInvoiceStatuses();
                Console.WriteLine($"  - GetInvoiceStatuses: {statuses.Count} estados obtenidos");

                Console.WriteLine("✅ Test de Facturas completado");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error en test de Facturas: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> TestSuppliers()
        {
            try
            {
                Console.WriteLine("\n🏭 [TEST] Proveedores...");

                var suppliers = await _service.GetActiveSuppliers();
                Console.WriteLine($"  - GetActiveSuppliers: {suppliers.Count} proveedores obtenidos");

                var allSuppliers = await _service.GetAllSuppliers();
                Console.WriteLine($"  - GetAllSuppliers: {allSuppliers.Count} proveedores totales");

                Console.WriteLine("✅ Test de Proveedores completado");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error en test de Proveedores: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> TestExpenses()
        {
            try
            {
                Console.WriteLine("\n💰 [TEST] Gastos...");

                var expenses = await _service.GetExpenses(limit: 5);
                Console.WriteLine($"  - GetExpenses: {expenses.Count} gastos obtenidos");

                var upcomingExpenses = await _service.GetUpcomingExpenses(7);
                Console.WriteLine($"  - GetUpcomingExpenses: {upcomingExpenses.Count} gastos próximos");

                var overdueExpenses = await _service.GetOverdueExpenses();
                Console.WriteLine($"  - GetOverdueExpenses: {overdueExpenses.Count} gastos vencidos");

                Console.WriteLine("✅ Test de Gastos completado");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error en test de Gastos: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> TestPayroll()
        {
            try
            {
                Console.WriteLine("\n💼 [TEST] Nómina...");

                var payroll = await _service.GetActivePayroll();
                Console.WriteLine($"  - GetActivePayroll: {payroll.Count} empleados activos");

                var total = await _service.GetMonthlyPayrollTotal();
                Console.WriteLine($"  - GetMonthlyPayrollTotal: ${total:N2}");

                var fixedExpenses = await _service.GetActiveFixedExpenses();
                Console.WriteLine($"  - GetActiveFixedExpenses: {fixedExpenses.Count} gastos fijos");

                Console.WriteLine("✅ Test de Nómina completado");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error en test de Nómina: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> TestVendors()
        {
            try
            {
                Console.WriteLine("\n🤝 [TEST] Vendedores...");

                var vendors = await _service.GetVendors();
                Console.WriteLine($"  - GetVendors: {vendors.Count} vendedores obtenidos");

                Console.WriteLine("✅ Test de Vendedores completado");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error en test de Vendedores: {ex.Message}");
                return false;
            }
        }
    }
}
