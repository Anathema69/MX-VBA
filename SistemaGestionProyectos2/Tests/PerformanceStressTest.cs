using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using SistemaGestionProyectos2.Services;
using SistemaGestionProyectos2.Services.Core;

namespace SistemaGestionProyectos2.Tests
{
    public record TestResult(string Name, string Category, bool Passed, long ElapsedMs, long ThresholdMs, string Error);

    public class PerformanceStressTest
    {
        private readonly SupabaseService _service;
        private readonly JsonLoggerService _logger;

        // IDs precargados para tests de lookup
        private int _testOrderId;
        private int _testClientId;

        public PerformanceStressTest()
        {
            _service = SupabaseService.Instance;
            _logger = JsonLoggerService.Instance;
        }

        /// <summary>
        /// Accede al campo protegido Cache via reflection y lo limpia
        /// </summary>
        private void ClearCache()
        {
            var cacheField = typeof(BaseSupabaseService)
                .GetField("Cache", BindingFlags.Static | BindingFlags.NonPublic);
            var cache = cacheField?.GetValue(null) as ServiceCache;
            cache?.Clear();
        }

        /// <summary>
        /// Ejecuta los 23 tests de rendimiento. Read-only contra la BD.
        /// </summary>
        public async Task<List<TestResult>> RunAllTests(Action<TestResult> onResult = null)
        {
            var results = new List<TestResult>();
            var totalSw = Stopwatch.StartNew();

            // Limpiar cache para garantizar cold starts
            ClearCache();

            // ========== CACHE (7 tests) ==========

            // 1. GetClients cold
            await RunTest(results, "GetClients (cold)", "Cache", 3000, async () =>
            {
                var data = await _service.GetClients();
                if (data == null) throw new Exception("GetClients retorno null");
            }, onResult);

            // 2. GetClients warm
            await RunTest(results, "GetClients (warm)", "Cache", 50, async () =>
            {
                var data = await _service.GetClients();
                if (data == null) throw new Exception("GetClients retorno null");
            }, onResult);

            // 3. GetVendors cold
            ClearCache();
            await RunTest(results, "GetVendors (cold)", "Cache", 3000, async () =>
            {
                var data = await _service.GetVendors();
                if (data == null) throw new Exception("GetVendors retorno null");
            }, onResult);

            // 4. GetVendors warm
            await RunTest(results, "GetVendors (warm)", "Cache", 50, async () =>
            {
                var data = await _service.GetVendors();
                if (data == null) throw new Exception("GetVendors retorno null");
            }, onResult);

            // 5. GetOrderStatuses cold
            ClearCache();
            await RunTest(results, "GetOrderStatuses (cold)", "Cache", 3000, async () =>
            {
                var data = await _service.GetOrderStatuses();
                if (data == null) throw new Exception("GetOrderStatuses retorno null");
            }, onResult);

            // 6. GetOrderStatuses warm
            await RunTest(results, "GetOrderStatuses (warm)", "Cache", 50, async () =>
            {
                var data = await _service.GetOrderStatuses();
                if (data == null) throw new Exception("GetOrderStatuses retorno null");
            }, onResult);

            // 7. Invalidate + reload
            await RunTest(results, "Invalidate + reload", "Cache", 3000, async () =>
            {
                ClearCache();
                var data = await _service.GetClients();
                if (data == null) throw new Exception("Reload despues de invalidacion fallo");
            }, onResult);

            // ========== SERVER FILTER (5 tests) ==========

            // 8. GetOrders paginado
            await RunTest(results, "GetOrders(100)", "ServerFilter", 3000, async () =>
            {
                var data = await _service.GetOrders(limit: 100);
                if (data == null) throw new Exception("GetOrders retorno null");
            }, onResult);

            // 9. GetOrdersFiltered por fecha
            await RunTest(results, "GetOrdersFiltered(fecha)", "ServerFilter", 3000, async () =>
            {
                var fromDate = DateTime.Now.AddMonths(-6);
                var data = await _service.GetOrdersFiltered(fromDate: fromDate, limit: 50);
                if (data == null) throw new Exception("GetOrdersFiltered retorno null");
            }, onResult);

            // 10. GetExpensesStatsByStatus (RPC)
            await RunTest(results, "GetExpensesStatsByStatus", "ServerFilter", 3000, async () =>
            {
                var data = await _service.GetExpensesStatsByStatus();
                if (data == null) throw new Exception("GetExpensesStatsByStatus retorno null");
            }, onResult);

            // 11. GetMonthlyPayrollTotal (RPC)
            await RunTest(results, "GetMonthlyPayrollTotal", "ServerFilter", 3000, async () =>
            {
                await _service.GetMonthlyPayrollTotal();
            }, onResult);

            // 12. GetExpenseStatistics (RPC)
            await RunTest(results, "GetExpenseStatistics", "ServerFilter", 3000, async () =>
            {
                var data = await _service.GetExpenseStatistics();
                if (data == null) throw new Exception("GetExpenseStatistics retorno null");
            }, onResult);

            // ========== QUERIES (7 tests) ==========

            // Precargar IDs para lookups (sin timing)
            await PreloadTestIds();

            // 13. GetOrderById
            await RunTest(results, "GetOrderById", "Queries", 2000, async () =>
            {
                if (_testOrderId == 0) throw new Exception("No hay ordenes para probar");
                var data = await _service.GetOrderById(_testOrderId);
                if (data == null) throw new Exception($"Orden {_testOrderId} no encontrada");
            }, onResult);

            // 14. GetInvoicesByOrder
            await RunTest(results, "GetInvoicesByOrder", "Queries", 2000, async () =>
            {
                if (_testOrderId == 0) throw new Exception("No hay ordenes para probar");
                var data = await _service.GetInvoicesByOrder(_testOrderId);
                if (data == null) throw new Exception("GetInvoicesByOrder retorno null");
            }, onResult);

            // 15. GetContactsByClient
            await RunTest(results, "GetContactsByClient", "Queries", 2000, async () =>
            {
                if (_testClientId == 0) throw new Exception("No hay clientes para probar");
                var data = await _service.GetContactsByClient(_testClientId);
                if (data == null) throw new Exception("GetContactsByClient retorno null");
            }, onResult);

            // 16. GetActiveSuppliers
            await RunTest(results, "GetActiveSuppliers", "Queries", 3000, async () =>
            {
                var data = await _service.GetActiveSuppliers();
                if (data == null) throw new Exception("GetActiveSuppliers retorno null");
            }, onResult);

            // 17. GetExpenses
            await RunTest(results, "GetExpenses", "Queries", 3000, async () =>
            {
                var data = await _service.GetExpenses(limit: 100);
                if (data == null) throw new Exception("GetExpenses retorno null");
            }, onResult);

            // 18. GetActivePayroll
            await RunTest(results, "GetActivePayroll", "Queries", 3000, async () =>
            {
                var data = await _service.GetActivePayroll();
                if (data == null) throw new Exception("GetActivePayroll retorno null");
            }, onResult);

            // 19. GetAllPendingIncomesData
            await RunTest(results, "GetAllPendingIncomesData", "Queries", 5000, async () =>
            {
                var data = await _service.GetAllPendingIncomesData();
                if (data == null) throw new Exception("GetAllPendingIncomesData retorno null");
            }, onResult);

            // ========== STRESS (4 tests) ==========

            // 20. 10x GetOrders secuenciales
            await RunTest(results, "10x GetOrders secuencial", "Stress", 10000, async () =>
            {
                for (int i = 0; i < 10; i++)
                {
                    var data = await _service.GetOrders(limit: 50);
                    if (data == null) throw new Exception($"GetOrders iteracion {i} retorno null");
                }
            }, onResult);

            // 21. 5x GetClients paralelos
            await RunTest(results, "5x GetClients paralelo", "Stress", 3000, async () =>
            {
                var tasks = Enumerable.Range(0, 5)
                    .Select(_ => _service.GetClients())
                    .ToArray();
                var results2 = await Task.WhenAll(tasks);
                if (results2.Any(r => r == null)) throw new Exception("Alguna llamada paralela retorno null");
            }, onResult);

            // 22. Cache clear + full reload
            await RunTest(results, "Cache clear + full reload", "Stress", 5000, async () =>
            {
                ClearCache();
                await Task.WhenAll(
                    _service.GetClients(),
                    _service.GetVendors(),
                    _service.GetOrderStatuses()
                );
            }, onResult);

            // 23. 3 queries simultaneas
            await RunTest(results, "Orders+Expenses+Clients paralelo", "Stress", 5000, async () =>
            {
                await Task.WhenAll(
                    _service.GetOrders(limit: 100),
                    _service.GetExpenses(limit: 100),
                    _service.GetClients()
                );
            }, onResult);

            totalSw.Stop();

            // Log resumen completo
            var summary = new
            {
                timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                total = results.Count,
                passed = results.Count(r => r.Passed),
                failed = results.Count(r => !r.Passed),
                totalElapsedMs = totalSw.ElapsedMilliseconds,
                tests = results.Select(r => new
                {
                    r.Name,
                    r.Category,
                    r.Passed,
                    r.ElapsedMs,
                    r.ThresholdMs,
                    r.Error
                }).ToList()
            };

            _logger.LogInfo("STRESS_TEST", "completed", summary);

            return results;
        }

        private async Task PreloadTestIds()
        {
            try
            {
                var orders = await _service.GetOrders(limit: 1);
                _testOrderId = orders?.FirstOrDefault()?.Id ?? 0;

                var clients = await _service.GetClients();
                _testClientId = clients?.FirstOrDefault()?.Id ?? 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[StressTest] Error precargando IDs: {ex.Message}");
            }
        }

        private async Task RunTest(
            List<TestResult> results,
            string name,
            string category,
            long thresholdMs,
            Func<Task> test,
            Action<TestResult> onResult)
        {
            var sw = Stopwatch.StartNew();
            string error = null;
            bool passed;

            try
            {
                await test();
                sw.Stop();
                passed = sw.ElapsedMilliseconds <= thresholdMs;
            }
            catch (Exception ex)
            {
                sw.Stop();
                passed = false;
                error = $"{ex.Message}\n{ex.StackTrace}";
            }

            var result = new TestResult(name, category, passed, sw.ElapsedMilliseconds, thresholdMs, error);
            results.Add(result);
            onResult?.Invoke(result);
        }
    }
}
