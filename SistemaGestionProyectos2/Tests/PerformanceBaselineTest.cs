using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using SistemaGestionProyectos2.Services;

namespace SistemaGestionProyectos2.Tests
{
    public record BaselineResult(string Name, string Category, bool Passed, long ElapsedMs, long ThresholdMs, string Error);

    public class PerformanceBaselineTest
    {
        private readonly SupabaseService _service;
        private int _testOrderId;
        private int _testClientId;

        public PerformanceBaselineTest()
        {
            _service = SupabaseService.Instance;
        }

        public async Task<List<BaselineResult>> RunAllTests(Action<BaselineResult> onResult = null)
        {
            var results = new List<BaselineResult>();

            // ========== QUERIES SIMPLES (8 tests) ==========

            await RunTest(results, "GetClients", "Queries", 3000, async () =>
            {
                var data = await _service.GetClients();
                if (data == null) throw new Exception("null");
            }, onResult);

            await RunTest(results, "GetVendors", "Queries", 3000, async () =>
            {
                var data = await _service.GetVendors();
                if (data == null) throw new Exception("null");
            }, onResult);

            await RunTest(results, "GetOrderStatuses", "Queries", 3000, async () =>
            {
                var data = await _service.GetOrderStatuses();
                if (data == null) throw new Exception("null");
            }, onResult);

            await RunTest(results, "GetActiveSuppliers", "Queries", 3000, async () =>
            {
                var data = await _service.GetActiveSuppliers();
                if (data == null) throw new Exception("null");
            }, onResult);

            await RunTest(results, "GetExpenses(100)", "Queries", 3000, async () =>
            {
                var data = await _service.GetExpenses(limit: 100);
                if (data == null) throw new Exception("null");
            }, onResult);

            await RunTest(results, "GetActivePayroll", "Queries", 3000, async () =>
            {
                var data = await _service.GetActivePayroll();
                if (data == null) throw new Exception("null");
            }, onResult);

            await RunTest(results, "GetExpensesStatsByStatus", "Queries", 3000, async () =>
            {
                var data = await _service.GetExpensesStatsByStatus();
                if (data == null) throw new Exception("null");
            }, onResult);

            await RunTest(results, "GetMonthlyPayrollTotal", "Queries", 3000, async () =>
            {
                await _service.GetMonthlyPayrollTotal();
            }, onResult);

            // ========== PAGINACION Y FILTRO (2 tests) ==========

            await RunTest(results, "GetOrders(100)", "Filtros", 3000, async () =>
            {
                var data = await _service.GetOrders(limit: 100);
                if (data == null) throw new Exception("null");
            }, onResult);

            await RunTest(results, "GetOrdersFiltered(fecha)", "Filtros", 3000, async () =>
            {
                var fromDate = DateTime.Now.AddMonths(-6);
                var data = await _service.GetOrdersFiltered(fromDate: fromDate, limit: 50);
                if (data == null) throw new Exception("null");
            }, onResult);

            // ========== LOOKUPS POR ID (3 tests) ==========

            await PreloadTestIds();

            await RunTest(results, "GetOrderById", "Lookups", 2000, async () =>
            {
                if (_testOrderId == 0) throw new Exception("No hay ordenes");
                var data = await _service.GetOrderById(_testOrderId);
                if (data == null) throw new Exception("null");
            }, onResult);

            await RunTest(results, "GetInvoicesByOrder", "Lookups", 2000, async () =>
            {
                if (_testOrderId == 0) throw new Exception("No hay ordenes");
                var data = await _service.GetInvoicesByOrder(_testOrderId);
                if (data == null) throw new Exception("null");
            }, onResult);

            await RunTest(results, "GetContactsByClient", "Lookups", 2000, async () =>
            {
                if (_testClientId == 0) throw new Exception("No hay clientes");
                var data = await _service.GetContactsByClient(_testClientId);
                if (data == null) throw new Exception("null");
            }, onResult);

            // ========== QUERIES PESADAS (2 tests) ==========

            await RunTest(results, "GetExpenseStatistics", "Pesadas", 5000, async () =>
            {
                var data = await _service.GetExpenseStatistics();
                if (data == null) throw new Exception("null");
            }, onResult);

            await RunTest(results, "GetAllPendingIncomesData", "Pesadas", 5000, async () =>
            {
                var data = await _service.GetAllPendingIncomesData();
                if (data == null) throw new Exception("null");
            }, onResult);

            // ========== STRESS (3 tests) ==========

            await RunTest(results, "10x GetOrders secuencial", "Stress", 30000, async () =>
            {
                for (int i = 0; i < 10; i++)
                {
                    var data = await _service.GetOrders(limit: 50);
                    if (data == null) throw new Exception($"null iter {i}");
                }
            }, onResult);

            await RunTest(results, "5x GetClients paralelo", "Stress", 10000, async () =>
            {
                var tasks = Enumerable.Range(0, 5)
                    .Select(_ => _service.GetClients())
                    .ToArray();
                var r = await Task.WhenAll(tasks);
                if (r.Any(x => x == null)) throw new Exception("null en paralelo");
            }, onResult);

            await RunTest(results, "Orders+Expenses+Clients paralelo", "Stress", 10000, async () =>
            {
                await Task.WhenAll(
                    _service.GetOrders(limit: 100),
                    _service.GetExpenses(limit: 100),
                    _service.GetClients()
                );
            }, onResult);

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
            catch { }
        }

        private async Task RunTest(
            List<BaselineResult> results, string name, string category,
            long thresholdMs, Func<Task> test, Action<BaselineResult> onResult)
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
            var result = new BaselineResult(name, category, passed, sw.ElapsedMilliseconds, thresholdMs, error);
            results.Add(result);
            onResult?.Invoke(result);
        }
    }
}
