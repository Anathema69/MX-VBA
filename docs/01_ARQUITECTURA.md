# Arquitectura

**Version:** 2.3.3 (abril 2026)
**Framework:** .NET 8.0 WPF (C#)
**Plataforma:** Windows 10/11

## Informacion general

| Atributo | Valor |
|---|---|
| Nombre | Sistema de Gestion de Proyectos |
| Framework | .NET 8.0 (WPF) |
| Lenguaje | C# |
| Base de datos | Supabase (PostgreSQL 17.4) |
| Blob storage | Cloudflare R2 (S3-compatible) + Supabase Storage |
| Auth | BCrypt local contra `users.password_hash` |
| Distribucion | Inno Setup self-contained + auto-update GitHub Releases |
| Empresa | IMA Mecatronica |
| Desarrollador | Zuri Dev |

## Capas

```
 ┌─────────────────────────────────────────────────────────┐
 │  Views (38 ventanas XAML)                               │
 │     LoginWindow, MainMenuWindow,                        │
 │     OrdersManagementWindow, EditOrderWindow,            │
 │     DriveV2Window, InventoryWindow,                     │
 │     VendorDashboard / VendorDashboard_V2, ... (38)      │
 └──────────────────────┬──────────────────────────────────┘
                        │  DataBinding + Commands
 ┌──────────────────────┴──────────────────────────────────┐
 │  ViewModels (MVVM parcial)                              │
 │     LoginViewModel, OrderViewModel, InvoiceViewModel,   │
 │     ExpenseViewModel, VendorCommissionViewModel, ...    │
 └──────────────────────┬──────────────────────────────────┘
                        │
 ┌──────────────────────┴──────────────────────────────────┐
 │  SupabaseService (Facade Singleton, ~55KB)              │
 │     Delega a 16 servicios especializados                │
 └──────────────────────┬──────────────────────────────────┘
                        │
 ┌────────────────┬─────┴───────────┬────────────────────┐
 │ Servicios de   │ Servicios de    │ Servicios de       │
 │ negocio        │ infraestructura │ almacenamiento     │
 │ ─────────────  │ ─────────────── │ ─────────────────  │
 │ OrderService   │ SessionTimeout  │ DriveService       │
 │ InvoiceService │ JsonLogger      │ (Cloudflare R2)    │
 │ ExpenseService │ UpdateService   │ FileWatcherService │
 │ FixedExpense   │ UserPreferences │ (Open-in-Place)    │
 │ PayrollService │                 │ StorageService     │
 │ AttendanceSvc  │                 │ (Supabase Storage) │
 │ VendorService  │ BaseSupabaseSvc │                    │
 │ ClientService  │ ServiceCache    │ InventoryService   │
 │ ContactService │ DataChangedEvt  │                    │
 │ SupplierSvc    │                 │                    │
 │ UserService    │                 │                    │
 └────────────────┴─────────────────┴────────────────────┘
                        │  Postgrest REST / S3 API / HTTP
 ┌──────────────────────┴──────────────────────────────────┐
 │  Supabase PostgreSQL 17.4                               │
 │     44 tablas, 15 vistas (+1 mat.), 73 funciones,       │
 │     44 triggers, 147 indices                            │
 │                                                         │
 │  Supabase Storage (bucket order-files)                  │
 │     Facturas de vendedores (Portal Ventas)              │
 │                                                         │
 │  Cloudflare R2 (bucket ima-drive)                       │
 │     Archivos CAD/CNC/docs (~500 MB, 2500+ archivos)     │
 │                                                         │
 │  GitHub Releases                                        │
 │     Instaladores .exe por version (auto-update)         │
 └─────────────────────────────────────────────────────────┘
```

## Estructura de carpetas

```
SistemaGestionProyectos2/
├── App.xaml / App.xaml.cs          Entry point, sesion, auto-update check
├── AssemblyInfo.cs
├── SistemaGestionProyectos2.csproj Version autoritativa
├── appsettings.json                Supabase, R2, timeout, logging
├── appsettings.production.json     / staging
├── switch-environment.bat          Copia el env correcto
├── installer.iss                   Inno Setup
├── build-release.bat               Build + sign + installer
├── sign-build.bat                  Authenticode post-build
├── create-cert.ps1 / install-cert  Cert dev local
├── ima-dev-cert.pfx                Certificado de firma
│
├── Controls/                       Controles custom (SessionTimeoutBanner)
│
├── Helpers/                        ShellThumbnailHelper, WindowHelper
│
├── Models/
│   ├── Database/                   28 archivos *Db.cs (postgrest)
│   │   ├── OrderDb, ClientDb, ContactDb, InvoiceDb,
│   │   ├── ExpenseDb, SupplierDb, VendorDb, UserDb,
│   │   ├── PayrollDb, FixedExpenseDb, AttendanceDb,
│   │   ├── VacationDb, HolidayDb, StatusDb, HistoryDb,
│   │   ├── OrderEjecutorDb, OrderFileDb,              <- Fase 4
│   │   ├── OrderGastoOperativoDb, OrderGastoIndirectoDb,
│   │   ├── OrderGastosViewDb,
│   │   ├── DriveFolderDb, DriveFileDb, DriveActivityDb,<- Fase 4
│   │   ├── InventoryCategoryDb, InventoryProductDb,   <- Fase 4
│   │   ├── InventoryMovementDb, InventoryViewModels,
│   │   └── AppVersionDb
│   ├── DTOs/                       DriveDTOs, InventoryDTOs, etc
│   ├── DataModels.cs / UserSession.cs
│   └── OrderViewModel.cs / InvoiceViewModel.cs / PayrollModels.cs
│
├── ViewModels/                     MVVM parcial (LoginViewModel, etc)
│
├── Services/
│   ├── SupabaseService.cs          Facade Singleton (~55KB)
│   ├── Core/
│   │   ├── BaseSupabaseService.cs
│   │   ├── ServiceCache.cs         ConcurrentDict + TTL
│   │   └── DataChangedEvent.cs
│   ├── Orders/OrderService.cs
│   ├── Invoices/InvoiceService.cs
│   ├── Expenses/ExpenseService.cs
│   ├── FixedExpenses/FixedExpenseService.cs
│   ├── Payroll/PayrollService.cs
│   ├── Attendance/AttendanceService.cs
│   ├── Clients/ClientService.cs
│   ├── Contacts/ContactService.cs
│   ├── Suppliers/SupplierService.cs
│   ├── Vendors/VendorService.cs
│   ├── Users/UserService.cs
│   ├── Drive/
│   │   ├── DriveService.cs         CRUD carpetas/archivos, R2, RPCs
│   │   └── FileWatcherService.cs   Open-in-Place auto-sync
│   ├── Storage/StorageService.cs   Supabase Storage (order-files)
│   ├── Inventory/InventoryService.cs
│   ├── Updates/UpdateService.cs    Auto-update + schtasks relaunch
│   ├── SessionTimeoutService.cs    Singleton, timer 1s
│   ├── JsonLoggerService.cs        Logs JSONL por sesion
│   ├── UserPreferencesService.cs
│   ├── AuthenticationService.cs
│   ├── *Converter.cs               (Role, Admin, Percentage)
│   └── OrderExtensions.cs
│
├── Views/                          38 ventanas XAML
│   (ver README.md principal para lista completa)
│
├── Tests/                          Stress tests, Drive workflow tests
│
├── ico-ima/                        7 iconos PNG
│
└── sql/                            Scripts SQL de deploy
    ├── update_app.sql              Insercion en app_versions (release)
    ├── bloque6_inventario.sql      Setup tablas Inventario
    ├── bloque6_seed.sql            Seed de categorias
    ├── bloque6_cleanup.sql
    ├── cleanup_drive_basura.sql    Limpieza archivos (~$, .tmp, .lck)
    ├── drive_scoped_search.sql
    ├── drive_v3_activity.sql
    ├── drive_v3_operations.sql
    ├── fix_gasto_operativo_formula.sql
    ├── fix_order_history_trigger.sql
    └── verify_drive_integrity.sql
```

## Patrones de diseno

### 1. Singleton
`SupabaseService`, `SessionTimeoutService`, `JsonLoggerService`, `FileWatcherService`.

```csharp
public static SupabaseService Instance {
    get {
        if (_instance == null) {
            lock (_lock) {
                if (_instance == null) _instance = new SupabaseService();
            }
        }
        return _instance;
    }
}
```

### 2. Facade
`SupabaseService` unifica acceso a los 16 servicios especializados. La UI solo conoce `SupabaseService.Instance.Method(...)`; los servicios especializados son detalle interno.

### 3. Repository / Service-per-entity
Cada servicio (`OrderService`, `ClientService`, ...) hereda de `BaseSupabaseService` y es el unico punto de acceso para su entidad. El postgrest client se inyecta por constructor.

### 4. MVVM parcial
Algunos modulos (Login, Orders, Expenses, Vendor) tienen ViewModels; otros (Drive, Inventario) manejan estado directo en code-behind por complejidad de UI.

### 5. Observer
`DataChangedEvent` (Services/Core/) permite a ventanas suscribirse a cambios de entidades. `SessionTimeoutService` emite `OnWarning` / `OnTimeout`.

### 6. Cache + TTL
`ServiceCache` (`ConcurrentDictionary<string, (object, DateTime)>`) con TTL por clave: 5 min clientes/proveedores, 30 min status tables, 60s counts.

## Dependencias NuGet

| Paquete | Version | Uso |
|---|---|---|
| `supabase-csharp` | 0.16.2 | Cliente postgrest + realtime |
| `BCrypt.Net-Next` | 4.0.3 | Hash de contrasenas |
| `AWSSDK.S3` | 3.7.405.3 | Cloudflare R2 (S3-compatible) |
| `Microsoft.Extensions.Configuration.Json` | 9.0.8 | Carga de `appsettings.json` |
| `Microsoft.Extensions.Configuration.Binder` | 9.0.8 | Binding a POCOs |

## Flujo de inicio

```
App.xaml.cs OnStartup:
  1. Cargar appsettings.json (+ environment variant si aplica)
  2. Inicializar JsonLoggerService -> log APPLICATION_START
  3. Registrar handlers de DispatcherUnhandledException / AppDomain
  4. Mostrar LoadingWindow
  5. SupabaseService.Instance.InitializeAsync()
     - Client postgrest con AnonKey
     - Inicializa servicios especializados
  6. Cerrar LoadingWindow, mostrar LoginWindow
  7. Tras login exitoso:
     - UpdateService.CheckForUpdate() (en background)
     - SessionTimeoutService.Start()
     - Navegar a pantalla inicial segun rol (ver 04_ROLES)
```

## Configuracion

`appsettings.json` (commiteado; al cliente se entrega dentro del .exe self-contained):

```json
{
  "Supabase":       { "Url": "...", "AnonKey": "..." },
  "Application":    { "Name": "...", "Version": "2.3.3", "Environment": "Production" },
  "DevMode":        { "Enabled": false, "AutoLogin": false, "SkipPassword": false },
  "Logging":        { "Enabled": true, "LogLevel": "Info", "RetentionDays": 30 },
  "SessionTimeout": { "InactivityMinutes": 30, "WarningBeforeMinutes": 5, "Enabled": true },
  "CloudflareR2":   { "AccountId": "...", "AccessKeyId": "...", "SecretAccessKey": "...", "BucketName": "ima-drive" },
  "Settings":       { "RememberLogin": true, "DefaultTheme": "Light", "AutoRefreshInterval": 30 }
}
```

**Nota de version:** `Application.Version` en `appsettings.json` debe coincidir con `<Version>` en `.csproj`. Si no, el auto-update entra en bucle. Ver [RELEASE_PROCESS.md](./RELEASE_PROCESS.md#checklist).

**Nota de seguridad:** la `AnonKey` y las credenciales de R2 van dentro del .exe distribuido. La AnonKey depende de RLS para protegerse; ver [RELEASE_PROCESS.md](./RELEASE_PROCESS.md) y el informe de seguridad pendiente.

## Siguientes documentos

- [02_MODELOS_DATOS.md](./02_MODELOS_DATOS.md) — resumen semantico de BD
- [03_SERVICIOS.md](./03_SERVICIOS.md) — metodos principales de cada servicio
- [04_ROLES_AUTENTICACION.md](./04_ROLES_AUTENTICACION.md) — 5 roles y permisos
- [05_FLUJOS_TRABAJO.md](./05_FLUJOS_TRABAJO.md) — ciclos de vida
