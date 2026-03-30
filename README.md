# Sistema de Gestion de Proyectos — IMA Mecatronica

> Sistema ERP interno para la gestion integral de ordenes de compra, finanzas, personal, archivos y inventario de IMA Mecatronica.

```
 ┌─────────────────────────────────────────────────────────────┐
 │                                                             │
 │     ██╗███╗   ███╗ █████╗                                   │
 │     ██║████╗ ████║██╔══██╗    M E C A T R O N I C A         │
 │     ██║██╔████╔██║███████║                                   │
 │     ██║██║╚██╔╝██║██╔══██║    Sistema de Gestion v2.3.1     │
 │     ██║██║ ╚═╝ ██║██║  ██║                                   │
 │     ╚═╝╚═╝     ╚═╝╚═╝  ╚═╝                                   │
 │                                                             │
 └─────────────────────────────────────────────────────────────┘
```

---

## Informacion General

| | |
|---|---|
| **Cliente** | IMA Mecatronica |
| **Stack** | .NET 8.0 WPF + C# + Supabase (PostgreSQL) + Cloudflare R2 |
| **Plataforma** | Windows 10/11 (escritorio) |
| **Version** | v2.3.1 (Mar 2026) |
| **Inicio** | Ago 2025 |
| **Commits** | 151 |
| **Arquitectura** | Layered: Views → ViewModels → SupabaseService Facade → Specialized Services → Supabase REST API |

---

## Modulos

```
 ┌──────────────────────────────────────────────────────────────────┐
 │                        MENU PRINCIPAL                            │
 │                                                                  │
 │   ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐       │
 │   │ Ordenes  │  │ Balance  │  │  Portal  │  │  Portal  │       │
 │   │    de    │  │  Anual   │  │  Ventas  │  │Proveedor │       │
 │   │  Compra  │  │          │  │          │  │          │       │
 │   └──────────┘  └──────────┘  └──────────┘  └──────────┘       │
 │                                                                  │
 │   ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐       │
 │   │ Ingresos │  │  Nomina  │  │   IMA    │  │Inventario│       │
 │   │Pendientes│  │ y Gastos │  │  Drive   │  │          │       │
 │   │          │  │  Fijos   │  │          │  │          │       │
 │   └──────────┘  └──────────┘  └──────────┘  └──────────┘       │
 │                                                                  │
 │   ┌──────────┐  ┌──────────┐  ┌──────────┐                     │
 │   │Calendario│  │ Gestion  │  │ Gestion  │                     │
 │   │ Personal │  │ Clientes │  │ Usuarios │                     │
 │   └──────────┘  └──────────┘  └──────────┘                     │
 └──────────────────────────────────────────────────────────────────┘
```

### Ordenes de Compra
Gestion completa del ciclo de vida de ordenes: creacion, seguimiento, facturacion, ejecutores asignados, vinculacion con archivos en la nube.

### Balance Anual
Vista consolidada de ingresos vs egresos con vista materializada para rendimiento. Filtros por periodo, exportacion.

### Portal Ventas
Dashboard para vendedores con gestion de comisiones, subida de facturas, preview con zoom/pan, liberacion de ordenes con stepper visual.

### Portal Proveedores / Cuentas por Pagar
Gastos pivoteados por proveedor, estados de pago, auditoria de eliminacion.

### Ingresos Pendientes
Seguimiento de pagos por cobrar con detalle por cliente.

### Nomina y Gastos Fijos
Gestion de empleados, asistencia, vacaciones, gastos fijos mensuales.

### IMA Drive
Sistema de archivos en la nube con Cloudflare R2. Navegacion tipo Google Drive, Open-in-Place con auto-sync, sincronizacion de carpetas, soporte CAD/CNC, thumbnails, busqueda scoped. [Detalle completo →](fase4/README.md#1-ima-drive--gestion-de-archivos-en-la-nube)

### Inventario
Gestion de categorias y productos con control de stock, alertas de stock bajo, movimientos y auditoria.

### Calendario de Personal
Asistencia diaria, registro de horas, vacaciones, vista mensual por empleado.

### Gestion de Clientes / Vendedores / Usuarios
CRUD de entidades con contactos, roles y permisos por modulo.

---

## Roles y Permisos

```
 ┌──────────────┬───────┬───────┬───────┬───────┬───────┐
 │   Modulo     │ Dir.  │ Admin │ Coord │ Proy. │ Vent. │
 ├──────────────┼───────┼───────┼───────┼───────┼───────┤
 │ Ordenes      │  ✅   │  ✅   │  ✅   │  ✅   │  ─    │
 │ Balance      │  ✅   │  ✅   │  ─    │  ─    │  ─    │
 │ Portal Ventas│  ✅   │  ─    │  ─    │  ─    │  ✅   │
 │ Proveedores  │  ✅   │  ✅   │  ─    │  ─    │  ─    │
 │ Ingresos     │  ✅   │  ✅   │  ─    │  ─    │  ─    │
 │ Nomina       │  ✅   │  ✅   │  ─    │  ─    │  ─    │
 │ IMA Drive    │  ✅   │  ✅   │  ✅   │  ✅   │  ✅   │
 │ Inventario   │  ✅   │  ✅   │  ✅   │  ✅   │  ✅   │
 │ Calendario   │  ✅   │  ✅   │  ─    │  ─    │  ─    │
 │ Usuarios     │  ✅   │  ─    │  ─    │  ─    │  ─    │
 └──────────────┴───────┴───────┴───────┴───────┴───────┘
```

---

## Arquitectura

```
 ┌─────────────────────────────────────────────────────────┐
 │  CLIENTE (WPF .NET 8, Windows)                          │
 │                                                         │
 │  Views (38 XAML)                                        │
 │    │                                                    │
 │    ├── ViewModels (parcial MVVM)                        │
 │    │                                                    │
 │    └── SupabaseService (Facade Singleton)               │
 │           │                                             │
 │           ├── OrderService                              │
 │           ├── InvoiceService                            │
 │           ├── ExpenseService                            │
 │           ├── PayrollService                            │
 │           ├── VendorService                             │
 │           ├── ClientService / SupplierService           │
 │           ├── AttendanceService                         │
 │           ├── DriveService ──── Cloudflare R2 (AWSSDK)  │
 │           ├── FileWatcherService (auto-sync local)      │
 │           ├── StorageService ── Supabase Storage         │
 │           ├── InventoryService                          │
 │           ├── UpdateService (auto-update)               │
 │           └── ServiceCache (ConcurrentDict + TTL)       │
 │                                                         │
 └──────────────────────┬──────────────────────────────────┘
                        │ HTTPS (Postgrest REST API)
 ┌──────────────────────┴──────────────────────────────────┐
 │  BACKEND (Supabase Cloud)                               │
 │                                                         │
 │  PostgreSQL 15                                          │
 │    ├── 39 tablas                                        │
 │    ├── 13 vistas (3 materializadas)                     │
 │    ├── 19 funciones RPC                                 │
 │    ├── 34 triggers                                      │
 │    └── 106 indices                                      │
 │                                                         │
 │  Storage                                                │
 │    └── Bucket order-files (facturas vendedores)         │
 │                                                         │
 └─────────────────────────────────────────────────────────┘

 ┌─────────────────────────────────────────────────────────┐
 │  CLOUDFLARE R2                                          │
 │    └── Bucket ima-drive (archivos CAD/CNC/docs)         │
 │        ~500 MB, 2500+ archivos, S3-compatible API       │
 └─────────────────────────────────────────────────────────┘
```

---

## Estructura del Repositorio

```
MX-VBA/
├── README.md                            Este archivo
├── GUIA_RAPIDA_RELEASE.md               Checklist para nuevas versiones
│
├── SistemaGestionProyectos2/            Codigo fuente
│   ├── App.xaml / App.xaml.cs           Entry point, session, auto-update
│   ├── Views/                           38 ventanas WPF (.xaml + .cs)
│   ├── Models/                          38 modelos (Database/ + DTOs/)
│   ├── ViewModels/                      ViewModels parciales
│   ├── Services/                        31 archivos de servicio
│   │   ├── Core/                        BaseService, Cache, DataChanged
│   │   ├── Drive/                       DriveService, FileWatcherService
│   │   ├── Inventory/                   InventoryService
│   │   ├── Storage/                     StorageService
│   │   ├── Updates/                     UpdateService
│   │   ├── Orders/ Invoices/ Expenses/  Servicios de negocio
│   │   ├── Payroll/ Attendance/         Nomina y asistencia
│   │   ├── Clients/ Vendors/ Suppliers/ Entidades
│   │   └── SupabaseService.cs           Facade (punto de entrada unico)
│   ├── Helpers/                         ShellThumbnailHelper, WindowHelper
│   ├── Tests/                           Performance + Workflow tests
│   ├── ico-ima/                         7 iconos PNG
│   ├── sql/                             11 scripts BD
│   ├── installer.iss                    Inno Setup
│   ├── build-release.bat                Build + sign automatizado
│   ├── appsettings.json                 Configuracion
│   └── SistemaGestionProyectos2.csproj  Proyecto .NET 8
│
├── db-docs/                             Documentacion automatica de BD
│   ├── 01_tables.py ... 07_diagrama.py  7 scripts Python
│   └── output/                          Markdown generado (7 archivos)
│
├── docs/                                Documentacion general
│   ├── RELEASE_PROCESS.md               Proceso detallado de release
│   ├── MANUAL_INSTALACION.md            Manual para el cliente
│   └── PROCESO_ACTUALIZACION.md         Como funciona el auto-update
│
└── fase4/                               Documentacion Fase 4
    ├── README.md                        Dashboard + estado final
    ├── bloques/                          Specs tecnicas (01-06)
    ├── drive-v3/                         Plan Drive V3 (7 fases)
    ├── mejoras-drive/                    Mejoras post-produccion
    ├── Modulo de Inventario/             Mockup Figma
    ├── _capturas/                        Capturas del cliente
    ├── bugs.md                           Tracking de bugs
    ├── logs.md                           Log de implementacion
    └── plan-ux-drive.md                  Plan rediseno UX
```

---

## Base de Datos

```
 ┌────────────────────────────────────────────────────────────────┐
 │  39 TABLAS                                                     │
 ├────────────────────────────────────────────────────────────────┤
 │                                                                │
 │  NEGOCIO           FINANZAS          PERSONAL                  │
 │  ─────────         ────────          ────────                  │
 │  t_order           t_invoice         t_payroll                 │
 │  t_client          t_expense         t_attendance              │
 │  t_vendor          t_vendor_comm.    users                     │
 │  t_supplier        t_vendor_comm_pay                           │
 │  order_ejecutores  pending_incomes                             │
 │  order_files                                                   │
 │                                                                │
 │  DRIVE             INVENTARIO        SISTEMA                   │
 │  ─────             ──────────        ───────                   │
 │  drive_folders     inventory_cat.    app_versions              │
 │  drive_files       inventory_prod.   t_order_status            │
 │  drive_activity    inventory_move.   t_invoice_status          │
 │  drive_audit       inventory_audit   t_expense_status          │
 │                                                                │
 └────────────────────────────────────────────────────────────────┘
```

**Documentacion auto-generada:** Ejecutar scripts en `db-docs/` genera Markdown completo de tablas, relaciones, vistas, funciones, triggers, indices, RLS y diagrama ER en `db-docs/output/`.

---

## Instalacion y Desarrollo

### Requisitos
- Windows 10/11
- .NET 8.0 SDK
- Visual Studio 2022+ (o VS Code con C# extension)
- Inno Setup 6 (para generar instalador)

### Configuracion

1. Clonar el repositorio
2. Configurar `appsettings.json` con credenciales de Supabase y Cloudflare R2
3. Compilar: `dotnet build`
4. Ejecutar: `dotnet run --project SistemaGestionProyectos2`

### Generar Release

Ver [GUIA_RAPIDA_RELEASE.md](GUIA_RAPIDA_RELEASE.md) para el proceso completo:

```
1. Actualizar version (.csproj + installer.iss + update_app.sql)
2. dotnet publish -c Release -r win-x64 --self-contained
3. ISCC.exe installer.iss
4. gh release create vX.Y.Z installer.exe
5. Ejecutar update_app.sql en Supabase
```

### Actualizacion Automatica
La app verifica versiones al iniciar sesion contra la tabla `app_versions` en Supabase. Si hay una version nueva, muestra dialogo con changelog y descarga el instalador desde GitHub Releases.

---

## Historial de Fases

| Fase | Periodo | Descripcion |
|------|---------|-------------|
| 1 | Ago-Sep 2025 | Estructura base, ordenes, clientes, facturacion |
| 2 | Oct-Nov 2025 | Balance, nomina, gastos, calendario |
| 3 | Dic 2025 - Ene 2026 | Portal Ventas, Proveedores, Ingresos, optimizacion |
| **4** | **Feb-Mar 2026** | **IMA Drive, Inventario, Ejecutor, Portal V2, UX/UI** |

---

## Contacto

- **Empresa:** IMA Mecatronica
- **Desarrollo:** Zuri Dev
- **Repositorio:** [github.com/Anathema69/MX-VBA](https://github.com/Anathema69/MX-VBA)
