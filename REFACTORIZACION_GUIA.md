# 📘 Guía de Refactorización - Sistema de Gestión de Proyectos

## ✅ Estado Actual

### Completado
- ✅ Modelos de BD extraídos a `Models/Database/`
- ✅ `BaseSupabaseService` creado en `Services/Core/`
- ✅ `OrderService` creado como servicio modular de ejemplo
- ✅ Tests de integración creados en `Tests/SupabaseServiceIntegrationTests.cs`

### Estructura Creada

```
Models/
└── Database/
    ├── OrderDb.cs
    ├── ClientDb.cs
    ├── ContactDb.cs
    ├── InvoiceDb.cs
    ├── UserDb.cs
    ├── SupplierDb.cs
    ├── ExpenseDb.cs
    ├── VendorDb.cs
    ├── PayrollDb.cs
    ├── FixedExpenseDb.cs
    ├── StatusDb.cs
    └── HistoryDb.cs

Services/
├── Core/
│   └── BaseSupabaseService.cs
├── Orders/
│   └── OrderService.cs
└── SupabaseService.cs (Original - aún en uso)
```

## 🎯 Estrategia de Migración Incremental

### Opción 1: Migración Gradual (Recomendada)

Esta opción permite refactorizar módulo por módulo sin romper código existente.

#### Paso 1: Crear Servicios Restantes

Crear los siguientes servicios siguiendo el patrón de `OrderService`:

1. **ClientService** (`Services/Clients/ClientService.cs`)
   - Métodos: GetClients, GetActiveClients, CreateClient, UpdateClient, etc.

2. **ContactService** (`Services/Contacts/ContactService.cs`)
   - Métodos: GetContactsByClient, AddContact, UpdateContact, DeleteContact, etc.

3. **InvoiceService** (`Services/Invoices/InvoiceService.cs`)
   - Métodos: GetInvoicesByOrder, CreateInvoice, UpdateInvoice, etc.

4. **ExpenseService** (`Services/Expenses/ExpenseService.cs`)
   - Métodos: GetExpenses, CreateExpense, UpdateExpense, etc.

5. **SupplierService** (`Services/Suppliers/SupplierService.cs`)
   - Métodos: GetActiveSuppliers, CreateSupplier, UpdateSupplier, etc.

6. **PayrollService** (`Services/Payroll/PayrollService.cs`)
   - Métodos: GetActivePayroll, CreatePayroll, UpdatePayroll, etc.

7. **FixedExpenseService** (`Services/FixedExpenses/FixedExpenseService.cs`)
   - Métodos: GetActiveFixedExpenses, CreateFixedExpense, etc.

8. **VendorService** (`Services/Vendors/VendorService.cs`)
   - Métodos: GetVendors, GetVendorById, etc.

9. **UserService** (`Services/Users/UserService.cs`)
   - Métodos: GetUserByUsername, AuthenticateUser, etc.

#### Paso 2: Modificar SupabaseService para usar Patrón Facade

Modificar `SupabaseService.cs` para delegar a los servicios modulares:

```csharp
public class SupabaseService
{
    private static SupabaseService _instance;
    private static readonly object _lock = new object();
    private Client _supabaseClient;
    private bool _isInitialized = false;

    // Servicios modulares
    private OrderService _orderService;
    private ClientService _clientService;
    private ContactService _contactService;
    // ... etc

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
        // ... código de inicialización actual ...

        // Inicializar servicios modulares
        _orderService = new OrderService(_supabaseClient);
        _clientService = new ClientService(_supabaseClient);
        // ... etc
    }

    // Delegar métodos a servicios modulares
    public Task<List<OrderDb>> GetOrders(int limit = 100, int offset = 0, List<int> filterStatuses = null)
        => _orderService.GetOrders(limit, offset, filterStatuses);

    public Task<OrderDb> GetOrderById(int orderId)
        => _orderService.GetOrderById(orderId);

    // ... más delegaciones ...
}
```

#### Paso 3: Ejecutar Tests

Después de cada servicio creado:

```bash
# Compilar
dotnet build

# Ejecutar tests (si implementas la ejecución)
# Los tests en Tests/SupabaseServiceIntegrationTests.cs verificarán que todo funciona
```

### Opción 2: Migración Completa (Más Riesgosa)

Reemplazar completamente `SupabaseService.cs` y actualizar todas las referencias en:
- ViewModels
- Views
- Otros servicios

⚠️ **No recomendado** sin un sistema de pruebas robusto.

## 📝 Template para Crear Nuevos Servicios

```csharp
using Postgrest.Responses;
using SistemaGestionProyectos2.Models.Database;
using SistemaGestionProyectos2.Services.Core;
using Supabase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SistemaGestionProyectos2.Services.[NombreModulo]
{
    public class [Nombre]Service : BaseSupabaseService
    {
        public [Nombre]Service(Client supabaseClient) : base(supabaseClient) { }

        // Métodos públicos aquí
        public async Task<List<[Modelo]Db>> Get[Modelos]()
        {
            try
            {
                var response = await SupabaseClient
                    .From<[Modelo]Db>()
                    .Get();

                var items = response?.Models ?? new List<[Modelo]Db>();
                LogSuccess($"[Modelos] obtenidos: {items.Count}");
                return items;
            }
            catch (Exception ex)
            {
                LogError("Error obteniendo [modelos]", ex);
                throw;
            }
        }

        // ... más métodos ...
    }
}
```

## 🧪 Cómo Ejecutar los Tests

Los tests están en `Tests/SupabaseServiceIntegrationTests.cs`. Para ejecutarlos:

1. **Opción Manual**: Crear una ventana de test temporal o agregar un botón en la UI:

```csharp
// En MainMenuWindow.xaml.cs o donde prefieras
private async void TestButton_Click(object sender, RoutedEventArgs e)
{
    var tests = new SupabaseServiceIntegrationTests();
    bool success = await tests.RunAllTests();
    MessageBox.Show(success ? "✅ Todos los tests pasaron" : "❌ Algunos tests fallaron");
}
```

2. **Opción Consola**: Agregar método Main temporal en `SupabaseServiceIntegrationTests.cs`

## 🔄 Proceso Recomendado

1. **Crear un servicio** (ej: ClientService)
2. **Modificar SupabaseService** para delegar a ese servicio
3. **Compilar**: `dotnet build`
4. **Probar manualmente** las funciones de clientes en la UI
5. **Repetir** con el siguiente servicio

## 📊 Progreso Actual

- [x] Análisis y propuesta
- [x] Extracción de modelos
- [x] BaseSupabaseService
- [x] OrderService (ejemplo)
- [ ] ClientService
- [ ] ContactService
- [ ] InvoiceService
- [ ] ExpenseService
- [ ] SupplierService
- [ ] PayrollService
- [ ] FixedExpenseService
- [ ] VendorService
- [ ] UserService
- [ ] Refactorizar SupabaseService como Facade
- [ ] Tests completos
- [ ] Eliminar código duplicado del SupabaseService original

## ✨ Beneficios de la Refactorización

- **Mantenibilidad**: Código más fácil de encontrar y modificar
- **Testabilidad**: Servicios aislados más fáciles de probar
- **Escalabilidad**: Agregar nuevas funciones sin afectar otros módulos
- **Claridad**: Responsabilidades bien definidas
- **Colaboración**: Múltiples desarrolladores pueden trabajar en módulos separados

## 🚨 Notas Importantes

1. **No elimines el SupabaseService.cs original** hasta que todos los servicios estén migrados
2. **Usa Git** para hacer commits después de cada servicio creado
3. **Prueba cada módulo** antes de continuar con el siguiente
4. **Los modelos en `Models/Database/`** ya están listos para usar
5. **Importa los namespaces correctos**: `using SistemaGestionProyectos2.Models.Database;`

## 📞 ¿Necesitas ayuda?

Si encuentras problemas durante la migración, revisa:
1. El ejemplo de `OrderService.cs`
2. El `BaseSupabaseService.cs` para métodos helper
3. Los modelos en `Models/Database/` para la estructura correcta
