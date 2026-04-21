# Sistema de Gestion de Proyectos — IMA Mecatronica

> Sistema ERP interno de escritorio para la gestion integral de ordenes de compra, finanzas, personal, archivos en la nube e inventario de IMA Mecatronica.

```
 ┌─────────────────────────────────────────────────────────────┐
 │                                                             │
 │     ██╗███╗   ███╗ █████╗                                   │
 │     ██║████╗ ████║██╔══██╗    M E C A T R O N I C A         │
 │     ██║██╔████╔██║███████║                                  │
 │     ██║██║╚██╔╝██║██╔══██║    Sistema de Gestion v2.3.3     │
 │     ██║██║ ╚═╝ ██║██║  ██║                                  │
 │     ╚═╝╚═╝     ╚═╝╚═╝  ╚═╝                                  │
 │                                                             │
 └─────────────────────────────────────────────────────────────┘
```

---

## Informacion General

| | |
|---|---|
| **Cliente** | IMA Mecatronica |
| **Desarrollo** | Zuri Dev |
| **Stack** | .NET 8.0 WPF + C# + Supabase (PostgreSQL 17.4) + Cloudflare R2 |
| **Plataforma** | Windows 10 / 11 (escritorio, self-contained) |
| **Version actual** | **v2.3.3** (abril 2026) |
| **Inicio** | Agosto 2025 |
| **Commits** | 161 |
| **Ultima fase** | Fase 4 (feb-mar 2026) — completada |
| **Arquitectura** | Layered: Views (XAML) → ViewModels (MVVM parcial) → SupabaseService (Facade Singleton) → 16 servicios especializados → Supabase REST / R2 S3 API |
| **Distribucion** | Inno Setup self-contained + auto-update via GitHub Releases |
| **Firma de codigo** | Authenticode con certificado IMA (`ima-dev-cert.pfx`) |
| **Repositorio** | [github.com/Anathema69/MX-VBA](https://github.com/Anathema69/MX-VBA) |

---

## Modulos Principales

```
 ┌──────────────────────────────────────────────────────────────────┐
 │                        MENU PRINCIPAL                            │
 │                                                                  │
 │   ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐         │
 │   │ Ordenes  │  │ Balance  │  │  Portal  │  │  Portal  │         │
 │   │    de    │  │  Anual   │  │  Ventas  │  │Proveedor │         │
 │   │  Compra  │  │          │  │          │  │          │         │
 │   └──────────┘  └──────────┘  └──────────┘  └──────────┘         │
 │                                                                  │
 │   ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐         │
 │   │ Ingresos │  │  Nomina  │  │   IMA    │  │Inventario│         │
 │   │Pendientes│  │ y Gastos │  │  Drive   │  │          │         │
 │   │          │  │  Fijos   │  │          │  │          │         │
 │   └──────────┘  └──────────┘  └──────────┘  └──────────┘         │
 │                                                                  │
 │   ┌──────────┐  ┌──────────┐  ┌──────────┐                       │
 │   │Calendario│  │ Gestion  │  │ Gestion  │                       │
 │   │ Personal │  │ Clientes │  │ Usuarios │                       │
 │   └──────────┘  └──────────┘  └──────────┘                       │
 └──────────────────────────────────────────────────────────────────┘
```

### 1. Ordenes de Compra
Gestion completa del ciclo de vida de ordenes:
- Estados: CREADA (0) → EN_PROCESO (1) → LIBERADA (2) → CERRADA (3) → COMPLETADA (4). CANCELADA (5) terminal.
- Asignacion de vendedor con snapshot de tasa de comision por orden.
- **Columna Ejecutor** (Fase 4): asignacion M:N de empleados de nomina a la orden (`order_ejecutores`), chips coloridos con iniciales, dialogo de seleccion tipo Notion/Linear.
- **Columna Carpeta** (Fase 4): vinculacion a una carpeta de IMA Drive, icono con acceso directo.
- Gastos operativos con comision incluida + gastos indirectos + gastos de material (proveedores).
- Soft-delete con snapshot JSONB en `t_order_deleted`.
- Vista materializada `v_order_gastos` para performance.

### 2. Balance Anual
Vista consolidada mensual de ingresos vs egresos:
- Ingresos: facturas PAGADAS del mes.
- Egresos: gastos variables + nomina efectiva + gastos fijos efectivos + overtime + ajustes.
- Vista materializada `v_balance_completo`, refresh manual.
- Filtros por periodo, exportacion.

### 3. Portal Ventas V2 (Fase 4)
Dashboard para vendedores (rol `ventas`):
- Cards compactas con indicadores de comision por estado.
- Galeria de facturas subidas con preview modal (zoom 50-500%, pan, doble clic reset).
- Boton "Liberar Orden" con stepper visual de 3 pasos (LIBERADA → REVISION → PAGO).
- Subida de facturas a Supabase Storage (bucket `order-files`).
- Optimistic UI: orden desaparece de la lista instantaneamente al liberar (rollback si falla BD).

### 4. Portal Proveedores / Cuentas por Pagar
Pivoteado por proveedor con estados PENDIENTE / PAGADO / VENCIDO:
- Trigger `auto_pay_zero_credit_expense` marca como PAGADO instantaneo si el proveedor tiene credito 0.
- Auditoria completa en `t_expense_audit` (incluye DELETE con captura de `updated_by` previo).
- Empty state contextual por filtro.
- Headers dinamicos segun tab (TOTAL PENDIENTE / TOTAL PAGADO / PAGADOS TARDE / PAGADOS A TIEMPO).

### 5. Ingresos Pendientes
Seguimiento de pagos por cobrar:
- Agrupacion por cliente con totales.
- Detalle por factura con registro de pagos (fecha, metodo, monto).
- Recalculo automatico de estado de orden al registrar pago total.

### 6. Nomina y Gastos Fijos
Gestion de empleados y costos recurrentes:
- Empleados con salario semanal/mensual, beneficios, seguro social.
- Historial de cambios con fecha efectiva (`t_payroll_history`).
- Gastos fijos mensuales con historial efectivo (`t_fixed_expenses_history`).
- Overtime mensual con auditoria (`t_overtime_hours` + `_audit`).

### 7. IMA Drive — Gestion de Archivos en la Nube (Fase 4)
Sistema de archivos sobre Cloudflare R2 (10 GB free, sin costo de egress):
- Navegacion tipo Google Drive con breadcrumb y historial atras/adelante.
- Vista cuadricula y lista con ordenamiento por nombre / tipo / tamano / fecha.
- CRUD completo de carpetas y archivos, mover / copiar / duplicar / renombrar.
- Upload multiple paralelo (5 simultaneos) con ghost cards de progreso.
- Descarga de carpetas completas como ZIP (streaming, no carga todo a RAM).
- Vinculacion de carpetas a ordenes via `drive_folders.linked_order_id`.
- Busqueda scoped (dentro de carpeta actual) o global, resultados agrupados.
- Recientes (mis archivos / todos) con feed de actividad.
- Filtros por tipo: PDFs, Imagenes, CAD, Hojas de calculo, Videos.
- Atajos Explorer: Ctrl+X/C/V, F2 renombrar, Del eliminar.
- Indicador de almacenamiento global y cache local.
- **Open-in-Place**: doble clic abre con la app asociada + auto-sync al guardar via `FileWatcherService` con debounce 2s.
- **Soporte CAD/CNC** (13 extensiones): .ipt, .iam, .sldprt, .sldasm, .dwg, .dxf, .step, .stp, .igs, .mcam, .mcx-*.
  - Sub-filtros CAD en sidebar (Ensambles / Piezas / Planos / Modelos 3D / CNC).
  - Descarga de contexto: al abrir ensamble, descarga todas las piezas de la carpeta.
  - Thumbnails via Windows Shell si Inventor / SolidWorks instalados.
  - Filtro automatico de basura (`~$`, `.db`, `.lck`, `.tmp`).
- Drag-drop de carpetas desde Windows con deteccion de duplicados.
- Diagnostico R2 ↔ BD (detecta huerfanos, ofrece limpieza).

### 8. Inventario (Fase 4)
Control de stock:
- 8 categorias pre-configuradas (tornillos, cables, conectores, herramientas, etc).
- Productos con stock, ubicacion, codigo, minimo de alerta.
- Ajustes de stock con tipo auto-detectado (ENTRADA / SALIDA / AJUSTE) en `inventory_movements`.
- Auditoria de cambios en `inventory_audit`.
- Creacion / edicion inline sin dialogos modales.
- Alertas visuales de stock bajo.
- Filtros por ubicacion, stock bajo, busqueda.

### 9. Calendario de Personal
Asistencia, vacaciones, feriados:
- Estados: ASISTENCIA, RETARDO, FALTA, VACACIONES, FERIADO, DESCANSO.
- Vista mensual por empleado.
- Vacaciones con flujo de aprobacion (PENDIENTE → APROBADA / RECHAZADA).
- Feriados configurables con recurrencia anual.
- Configuracion de dias laborales por dia de la semana.
- Auditoria completa de cambios en asistencia y vacaciones.

### 10. Gestion de Clientes / Vendedores / Usuarios
CRUD con soft-delete:
- Clientes con contactos (uno marcado `is_primary`) y dias de credito.
- Vendedores con tasa de comision por defecto y usuario asociado.
- Usuarios con role y BCrypt password hash (gestion solo para rol `direccion`).

---

## Roles y Permisos

5 roles diferenciados por modulo (constraint en BD: `role IN ('direccion','administracion','proyectos','coordinacion','ventas')`).

```
 ┌──────────────┬───────┬───────┬───────┬───────┬───────┐
 │   Modulo     │ Dir.  │ Admin │ Coord │ Proy. │ Vent. │
 ├──────────────┼───────┼───────┼───────┼───────┼───────┤
 │ Menu Princ.  │  ✅   │  ✅   │  ─    │  ─    │  ─    │
 │ Ordenes      │  ✅   │  ✅   │  ✅*  │  ✅*  │  ─    │
 │ Gastos (OC)  │  ✅   │  ─    │  ─    │  ─    │  ─    │
 │ Balance      │  ✅   │  ✅   │  ─    │  ─    │  ─    │
 │ Portal Vent. │  ✅   │  ─    │  ─    │  ─    │  ✅   │
 │ Proveedores  │  ✅   │  ✅   │  ─    │  ─    │  ─    │
 │ Ingresos     │  ✅   │  ✅   │  ─    │  ─    │  ─    │
 │ Nomina       │  ✅   │  ✅   │  ─    │  ─    │  ─    │
 │ IMA Drive    │  ✅   │  ✅   │  ✅   │  ✅   │  ✅   │
 │ Inventario   │  ✅   │  ✅   │  ✅   │  ✅   │  ✅   │
 │ Calendario   │  ✅   │  ✅   │  ─    │  ─    │  ─    │
 │ Clientes     │  ✅   │  ✅   │  ─    │  ─    │  ─    │
 │ Comisiones   │  ✅   │  ✅   │  ─    │  ─    │  ─    │
 │ Usuarios     │  ✅   │  ─    │  ─    │  ─    │  ─    │
 └──────────────┴───────┴───────┴───────┴───────┴───────┘

 * Coordinacion / Proyectos: solo estados 0-2 (CREADA / EN_PROCESO /
   LIBERADA). Sin columnas de gasto, subtotal, total ni facturado.
   No llegan al menu principal: login abre directo a Ordenes.
```

**Diferencia Direccion vs Administracion**: solo Direccion ve columnas de gasto material / operativo / indirecto en Ordenes, y accede a Gestion de Usuarios.

Pantalla inicial por rol:
- `direccion` / `administracion` → `MainMenuWindow`
- `coordinacion` / `proyectos` → `OrdersManagementWindow`
- `ventas` → `VendorDashboard_V2`

Detalle completo: [docs/04_ROLES_AUTENTICACION.md](docs/04_ROLES_AUTENTICACION.md).

---

## Arquitectura

```
 ┌─────────────────────────────────────────────────────────┐
 │  CLIENTE (WPF .NET 8, Windows)                          │
 │                                                         │
 │  Views (38 XAML)                                        │
 │    │ LoginWindow, MainMenuWindow,                       │
 │    │ OrdersManagementWindow, EditOrderWindow,           │
 │    │ DriveV2Window, InventoryWindow,                    │
 │    │ VendorDashboard_V2, BalanceWindowPro,              │
 │    │ CalendarView, PayrollManagementView, ... (38)      │
 │    │                                                    │
 │    ├── ViewModels (MVVM parcial)                        │
 │    │   LoginVM, OrderVM, InvoiceVM, ExpenseVM,          │
 │    │   VendorCommissionVM, SupplierExpensesVM           │
 │    │                                                    │
 │    └── SupabaseService (Facade Singleton, ~55KB)        │
 │           │                                             │
 │           ├── OrderService                              │
 │           ├── InvoiceService                            │
 │           ├── ExpenseService / FixedExpenseService      │
 │           ├── PayrollService / AttendanceService        │
 │           ├── VendorService                             │
 │           ├── ClientService / ContactService            │
 │           ├── SupplierService                           │
 │           ├── UserService                               │
 │           ├── DriveService ────── Cloudflare R2 (S3)    │
 │           ├── FileWatcherService  (Open-in-Place sync)  │
 │           ├── StorageService ──── Supabase Storage       │
 │           ├── InventoryService                          │
 │           ├── UpdateService ───── GitHub Releases       │
 │           └── Core/                                     │
 │               ├── BaseSupabaseService (clase base)      │
 │               ├── ServiceCache (ConcurrentDict + TTL)   │
 │               └── DataChangedEvent (observer cruzado)   │
 │                                                         │
 │  Infraestructura (singletons fuera del facade):         │
 │    SessionTimeoutService, JsonLoggerService,            │
 │    UserPreferencesService                               │
 │                                                         │
 └──────────────────────┬──────────────────────────────────┘
                        │ HTTPS (Postgrest REST API)
 ┌──────────────────────┴──────────────────────────────────┐
 │  BACKEND (Supabase Cloud)                               │
 │                                                         │
 │  PostgreSQL 17.4                                        │
 │    ├── 44 tablas                                        │
 │    ├── 15 vistas + 1 materializada                      │
 │    ├── 33 funciones RPC + 36 trigger + 4 huerfanas      │
 │    ├── 44 triggers activos en 19 tablas                 │
 │    └── 147 indices (44 PK + 14 UNIQUE + 89 regular)     │
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

 ┌─────────────────────────────────────────────────────────┐
 │  GITHUB RELEASES                                        │
 │    └── Instaladores .exe por version (~50-55 MB c/u)    │
 │        https://github.com/Anathema69/MX-VBA/releases    │
 └─────────────────────────────────────────────────────────┘
```

### Patrones de diseno

| Patron | Donde |
|---|---|
| Singleton | `SupabaseService`, `SessionTimeoutService`, `JsonLoggerService`, `FileWatcherService` |
| Facade | `SupabaseService` unifica 16 servicios especializados |
| Repository | Cada servicio hereda `BaseSupabaseService`, 1 entidad = 1 servicio |
| MVVM (parcial) | Login, Orders, Expenses, Vendor con ViewModels explicitos; Drive / Inventario con code-behind por complejidad UI |
| Observer | `DataChangedEvent` entre ventanas; `SessionTimeoutService` emite OnWarning / OnTimeout |
| Cache + TTL | `ServiceCache` (ConcurrentDict), 5 min listados, 30 min status tables, 60s counts |

### Dependencias NuGet

| Paquete | Version | Uso |
|---|---|---|
| `supabase-csharp` | 0.16.2 | Cliente postgrest + realtime |
| `BCrypt.Net-Next` | 4.0.3 | Hashing de contrasenas |
| `AWSSDK.S3` | 3.7.405.3 | Cloudflare R2 (S3-compatible) |
| `Microsoft.Extensions.Configuration.Json` | 9.0.8 | Carga de `appsettings.json` |
| `Microsoft.Extensions.Configuration.Binder` | 9.0.8 | Binding a POCOs |

---

## Estructura del Repositorio

```
MX-VBA/
├── README.md                                 Este archivo
│
├── SistemaGestionProyectos2.sln              Solucion .NET
│
├── SistemaGestionProyectos2/                 Codigo fuente (.NET 8 WPF)
│   ├── App.xaml / App.xaml.cs                Entry point, sesion, auto-update check
│   ├── AssemblyInfo.cs
│   ├── SistemaGestionProyectos2.csproj       Version autoritativa + target SignAssembly
│   ├── appsettings.json                      Base (Supabase, R2, timeout, logging)
│   ├── appsettings.production.json           Variant produccion
│   ├── appsettings.staging.json              Variant staging (URL distinta)
│   ├── switch-environment.bat                Copia el variant correcto sobre base
│   ├── installer.iss                         Script Inno Setup
│   ├── build-release.bat                     Build + sign + installer automatizado
│   ├── sign-build.bat                        Firma Authenticode post-build
│   ├── create-cert.ps1 / install-cert.ps1    Cert dev local (setup inicial)
│   ├── exclude-wdac.ps1                      Exclusion WDAC
│   ├── ima-dev-cert.pfx                      Certificado de firma (password: ima2026)
│   ├── app.ico                               Icono de app
│   ├── Controls/
│   │   └── SessionTimeoutBanner.xaml         Banner de advertencia de timeout
│   ├── Helpers/
│   │   ├── ShellThumbnailHelper.cs           Thumbnails de archivos via Windows Shell
│   │   └── WindowHelper.cs                   Monitor global de input para timeout
│   ├── Models/
│   │   ├── Database/                         28 modelos postgrest (*Db.cs)
│   │   │   ├── OrderDb, ClientDb, ContactDb, InvoiceDb,
│   │   │   ├── ExpenseDb, SupplierDb, VendorDb, UserDb,
│   │   │   ├── PayrollDb, FixedExpenseDb, AttendanceDb,
│   │   │   ├── VacationDb, HolidayDb, StatusDb, HistoryDb,
│   │   │   ├── OrderEjecutorDb, OrderFileDb,             <- Fase 4
│   │   │   ├── OrderGastoOperativoDb, OrderGastoIndirectoDb,
│   │   │   ├── OrderGastosViewDb,
│   │   │   ├── DriveFolderDb, DriveFileDb, DriveActivityDb,<- Fase 4
│   │   │   ├── InventoryCategoryDb, InventoryProductDb,  <- Fase 4
│   │   │   ├── InventoryMovementDb, InventoryViewModels,
│   │   │   └── AppVersionDb
│   │   ├── DTOs/                             DriveDTOs, InventoryDTOs, etc
│   │   ├── DataModels.cs
│   │   ├── UserSession.cs
│   │   ├── OrderViewModel.cs
│   │   ├── InvoiceViewModel.cs
│   │   └── PayrollModels.cs
│   ├── ViewModels/                           ViewModels MVVM (BaseVM, LoginVM, ...)
│   ├── Services/
│   │   ├── SupabaseService.cs                Facade Singleton (~55KB)
│   │   ├── SupabaseService.cs.backup         Legacy pre-extraccion (111KB)
│   │   ├── Core/
│   │   │   ├── BaseSupabaseService.cs
│   │   │   ├── ServiceCache.cs
│   │   │   └── DataChangedEvent.cs
│   │   ├── Orders/OrderService.cs
│   │   ├── Invoices/InvoiceService.cs
│   │   ├── Expenses/ExpenseService.cs
│   │   ├── FixedExpenses/FixedExpenseService.cs
│   │   ├── Payroll/PayrollService.cs
│   │   ├── Attendance/AttendanceService.cs
│   │   ├── Vendors/VendorService.cs
│   │   ├── Clients/ClientService.cs
│   │   ├── Contacts/ContactService.cs
│   │   ├── Suppliers/SupplierService.cs
│   │   ├── Users/UserService.cs
│   │   ├── Drive/
│   │   │   ├── DriveService.cs               CRUD + R2 + RPCs Drive
│   │   │   └── FileWatcherService.cs         Open-in-Place auto-sync
│   │   ├── Storage/StorageService.cs         Supabase Storage (order-files)
│   │   ├── Inventory/InventoryService.cs
│   │   ├── Updates/UpdateService.cs          Auto-update + schtasks relaunch
│   │   ├── SessionTimeoutService.cs
│   │   ├── JsonLoggerService.cs              Logs JSONL por sesion
│   │   ├── UserPreferencesService.cs
│   │   ├── AuthenticationService.cs
│   │   ├── *Converter.cs                     (Role, Admin, Percentage)
│   │   └── OrderExtensions.cs
│   ├── Views/                                38 ventanas XAML
│   ├── Tests/                                Stress tests + Drive workflow tests
│   ├── ico-ima/                              7 iconos PNG
│   └── sql/                                  11 scripts SQL
│       ├── update_app.sql                    Registrar nueva version en app_versions
│       ├── bloque6_inventario.sql            Setup tablas Inventario (Fase 4)
│       ├── bloque6_seed.sql                  Seed 8 categorias
│       ├── bloque6_cleanup.sql
│       ├── cleanup_drive_basura.sql          Limpieza archivos ~$, .tmp, .lck
│       ├── drive_scoped_search.sql
│       ├── drive_v3_activity.sql
│       ├── drive_v3_operations.sql
│       ├── fix_gasto_operativo_formula.sql
│       ├── fix_order_history_trigger.sql
│       ├── verify_drive_integrity.sql        8 queries de integridad R2 vs BD
│       └── F_update_order_status_from_invoices.txt
│
├── db-docs/                                  Documentacion auto-generada de BD
│   ├── .env                                  Credenciales DB (NO commiteado)
│   ├── test_connection.py                    Smoke test
│   ├── 01_tables.py .. 07_diagrama_er.py     7 scripts Python (psycopg2)
│   ├── venv/                                 Virtualenv local
│   └── output/                               Markdown generado (regenerable)
│       ├── 01_tablas.md                      44 tablas con columnas, tipos, FK, indices
│       ├── 02_relaciones.md                  68 FKs, cascadas, tablas aisladas
│       ├── 03_vistas.md                      15 vistas + 1 materializada
│       ├── 04_funciones_triggers.md          73 funciones + 44 triggers
│       ├── 05_indexes.md                     147 indices (33 sin uso)
│       ├── 06_rls_policies.md                Estado RLS por tabla + policies
│       └── 07_diagrama_er.md                 3 diagramas mermaid (completo, simpl., modulos)
│
├── docs/                                     Documentacion tecnica interna
│   ├── README.md                             Indice + fuentes canonicas de verdad
│   ├── 01_ARQUITECTURA.md                    Capas, patrones, estructura, dependencias
│   ├── 02_MODELOS_DATOS.md                   Resumen semantico por modulo de BD
│   ├── 03_SERVICIOS.md                       Metodos de cada servicio, UIPI/schtasks
│   ├── 04_ROLES_AUTENTICACION.md             5 roles, matriz de permisos, BCrypt
│   ├── 05_FLUJOS_TRABAJO.md                  16 flujos incluyendo Drive, Inventario, V2
│   ├── FLUJO_COMISIONES.md                   draft/pending/paid + Portal Ventas V2
│   ├── RELEASE_PROCESS.md                    Proceso real GitHub Releases + checklist
│   └── _archive/                             Docs historicos (BD-IMA legacy)
│
└── fase4/                                    Documentacion Fase 4 (feb-mar 2026)
    ├── README.md                             Dashboard + estado final 100%
    ├── bloques/                              Specs tecnicas (01-06)
    ├── drive-v3/                             Plan Drive V3 (7 fases A-G)
    ├── mejoras-drive/                        Mejoras post-produccion (7/7)
    ├── Modulo de Inventario/                 Mockup Figma + capturas
    ├── _capturas/                            Capturas del cliente
    ├── bugs.md                               Tracking de bugs
    ├── logs.md                               Log cronologico de implementacion
    ├── plan-ux-drive.md                      Plan rediseno UX
    ├── MANUAL_INSTALACION.md                 Manual para el cliente
    ├── IMA-Drive-Mejoras-Diseno.md
    └── propuesta_vacaciones_calendario.md
```

---

## Base de Datos

```
 ┌────────────────────────────────────────────────────────────────┐
 │  44 TABLAS (agrupadas por modulo)                              │
 ├────────────────────────────────────────────────────────────────┤
 │                                                                │
 │  USUARIOS / SISTEMA (4)      ORDENES (7)                       │
 │  ───────────────────         ───────────                       │
 │  users                       t_order                           │
 │  app_versions                order_status                      │
 │  audit_log                   order_history                     │
 │  t_workday_config            order_gastos_operativos           │
 │                              order_gastos_indirectos           │
 │                              order_ejecutores         ← Fase 4 │
 │                              order_files              ← Fase 4 │
 │                              t_order_deleted                   │
 │                                                                │
 │  CLIENTES (2)                FACTURAS (3)                      │
 │  ─────────                   ─────────                         │
 │  t_client                    t_invoice                         │
 │  t_contact                   invoice_status                    │
 │                              invoice_audit                     │
 │                                                                │
 │  GASTOS / PROVEED. (3)       COMISIONES (3)                    │
 │  ──────────────────          ────────────                      │
 │  t_expense                   t_vendor                          │
 │  t_expense_audit             t_vendor_commission_payment       │
 │  t_supplier                  t_commission_rate_history         │
 │                                                                │
 │  NOMINA (5)                  CALENDARIO (6)                    │
 │  ──────                      ───────────                       │
 │  t_payroll                   t_attendance                      │
 │  t_payroll_history           t_attendance_audit                │
 │  t_overtime_hours            t_vacation                        │
 │  t_overtime_hours_audit      t_vacation_audit                  │
 │  t_payrollovertime (legacy)  t_holiday                         │
 │                              (t_workday_config arriba)         │
 │                                                                │
 │  BALANCE (3)                 IMA DRIVE (4)    ← Fase 4         │
 │  ─────────                   ─────────                         │
 │  t_fixed_expenses            drive_folders                     │
 │  t_fixed_expenses_history    drive_files                       │
 │  t_balance_adjustments       drive_activity                    │
 │                              drive_audit                       │
 │                                                                │
 │  INVENTARIO (4)              ← Fase 4                          │
 │  ──────────────                                                │
 │  inventory_categories                                          │
 │  inventory_products                                            │
 │  inventory_movements                                           │
 │  inventory_audit                                               │
 └────────────────────────────────────────────────────────────────┘
```

**Cifras actuales** (regeneradas 2026-04-20 desde Supabase en vivo):
- **44 tablas** · **15 vistas + 1 materializada** · **73 funciones** (33 RPC + 36 trigger + 4 huerfanas) · **44 triggers** en 19 tablas · **147 indices** (33 sin uso, 4.9 MB total) · **68 FKs**.

**Vistas clave**:
- `v_order_gastos` — ordenes con gastos calculados (critica para OrdersManagementWindow).
- `v_balance_completo` — balance mensual materializado (ingresos + gastos + utilidad).
- `v_income` — detalle de ingresos por factura con fecha efectiva de pago.
- `v_attendance_today` / `v_attendance_monthly_summary` / `v_vacations_active` — calendario.

**Documentacion auto-generada**: ejecutar los 7 scripts en `db-docs/` regenera el markdown completo de tablas, relaciones, vistas, funciones, triggers, indices, RLS y diagrama ER en `db-docs/output/`.

```bash
cd db-docs
./venv/Scripts/python.exe 01_tables.py
./venv/Scripts/python.exe 02_relaciones.py
# ... (7 scripts en total)
```

---

## Servicios (16 especializados)

| Servicio | Responsabilidad |
|---|---|
| `OrderService` | CRUD ordenes, paginacion, filtros por estado, cancelacion, soft-delete con snapshot |
| `InvoiceService` | Facturas, calculo de `due_date`, totales facturados por lote |
| `ExpenseService` | Gastos a proveedores, auditoria de eliminacion, filtros por supplier/status/fecha |
| `FixedExpenseService` | Gastos fijos mensuales con historial efectivo |
| `PayrollService` | Empleados, historial de cambios, total mensual |
| `AttendanceService` | Asistencias, vacaciones, feriados, overtime |
| `VendorService` | Vendedores + setup de comisiones |
| `ClientService` | CRUD clientes con cache 5 min |
| `ContactService` | Contactos de clientes |
| `SupplierService` | Proveedores con cache |
| `UserService` | Auth BCrypt, CRUD usuarios, soft-delete |
| `DriveService` | CRUD carpetas/archivos, R2, vinculacion con ordenes, busqueda, diagnostico |
| `FileWatcherService` | Open-in-Place, debounce 2s, manifest SHA256, deteccion Save-As CAD |
| `StorageService` | Supabase Storage (bucket `order-files`) + URLs firmadas |
| `InventoryService` | Categorias, productos, movimientos, ajuste de stock |
| `UpdateService` | Auto-update, descarga desde GitHub Releases, **schtasks /rl limited** para relaunch |

Infraestructura (fuera del facade): `SessionTimeoutService`, `JsonLoggerService`, `UserPreferencesService`, `AuthenticationService`.

Detalle de metodos: [docs/03_SERVICIOS.md](docs/03_SERVICIOS.md).

---

## Instalacion y Desarrollo

### Requisitos
- Windows 10 / 11
- .NET 8.0 SDK
- Visual Studio 2022+ o VS Code con C# Dev Kit
- Inno Setup 6 (para generar instalador)
- `gh` CLI (para crear releases)
- Python 3.12+ con `psycopg2` y `python-dotenv` (para regenerar `db-docs/output/`)

### Configuracion inicial
1. Clonar el repo.
2. Configurar `SistemaGestionProyectos2/appsettings.json` con credenciales Supabase y R2.
3. Crear `db-docs/.env` con credenciales directas a Postgres:
   ```
   DB_HOST=aws-0-us-east-1.pooler.supabase.com
   DB_PORT=6543
   DB_NAME=postgres
   DB_USER=postgres.<project-ref>
   DB_PASSWORD=<password>
   ```
4. Compilar: `dotnet build`
5. Ejecutar: `dotnet run --project SistemaGestionProyectos2`

### Entornos
`switch-environment.bat` copia el variant correcto sobre `appsettings.json`:
```bash
cd SistemaGestionProyectos2
./switch-environment.bat       # menu interactivo: 1=Produccion, 2=Staging
```

> **Nota**: al momento de este README, los variants `production.json` y `staging.json` no incluyen las secciones `CloudflareR2` y `DevMode` del base. Pendiente de sincronizar.

### Firma de codigo (una sola vez por maquina de build)
```powershell
./SistemaGestionProyectos2/install-cert.ps1     # requiere admin
```

El target `SignAssembly` del csproj dispara `sign-build.bat` automaticamente tras cada build si existe `ima-dev-cert.pfx`.

---

## Proceso de Release

Resumen (detalle completo en [docs/RELEASE_PROCESS.md](docs/RELEASE_PROCESS.md)):

```bash
# 1. Bump de version en 3 archivos:
#    - SistemaGestionProyectos2.csproj (Version, AssemblyVersion, FileVersion)
#    - installer.iss (AppVersion, OutputBaseFilename)
#    - sql/update_app.sql (v_version, v_release_notes, v_changelog)

# 2. Publicar self-contained
taskkill /F /IM SistemaGestionProyectos2.exe
cd SistemaGestionProyectos2
dotnet clean
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false

# 3. Generar instalador
"/c/Program Files (x86)/Inno Setup 6/ISCC.exe" installer.iss

# 4. Publicar en GitHub Releases
gh release create v2.3.X installer/SistemaGestionProyectos-v2.3.X-Setup.exe \
    --title "v2.3.X" --notes "Resumen corto"

# 5. Registrar en Supabase
#    SQL Editor -> ejecutar sql/update_app.sql
```

El instalador queda en `SistemaGestionProyectos2/installer/` (~50-55 MB). Ignorado por git.

### Auto-update

La app verifica versiones al iniciar sesion:
1. Lee la version local desde `Assembly.GetName().Version` (viene del `.csproj`).
2. Consulta `SELECT * FROM app_versions WHERE is_latest=true AND is_active=true`.
3. Si la version en BD es mayor, muestra `UpdateAvailableWindow`.
4. Descarga el instalador desde `download_url` (asset de GitHub Releases) a `%TEMP%`.
5. Genera un script `.bat` que:
   - Cierra la app (`taskkill /F`).
   - Ejecuta el instalador en modo silent.
   - **Relanza via `schtasks /rl limited`** para garantizar integridad MEDIA (no elevada).

**Por que `schtasks`**: tras el instalador elevado por UAC, `Process.Start` heredaria el token elevado. UIPI bloquearia drag-drop desde Explorer hacia IMA Drive. Solo `schtasks` con `/rl limited` o Shell COM pueden des-elevar un proceso hijo en Windows. Ver [docs/03_SERVICIOS.md#updateservice](docs/03_SERVICIOS.md#updateservice-con-fixes-de-abril-2026) para el detalle de los 5 commits iterativos que llevaron a esta solucion.

---

## Rendimiento

```
 ┌──────────────────────────────────────────────────────────┐
 │  BENCHMARKS (Sao Paulo → CDMX)                           │
 │                                                          │
 │  Navegacion Drive (cold)         258 ms                  │
 │  Navegacion Drive (cache-hit)      9 ms  ↓ 96.6%         │
 │  Seleccion vendedor (Portal)       1 ms  ↓ 99.7%         │
 │  Stats de 100 carpetas           1 query (antes 200)     │
 │  Breadcrumb                      1 CTE  (antes N seq)    │
 │  Eliminar carpeta grande         2 queries (antes N)     │
 │  Analisis sync carpeta           2 queries (antes N)     │
 │  GetAllFilesFlat                 paginado (antes max 1000)│
 └──────────────────────────────────────────────────────────┘
```

---

## Historial de Fases y Releases

| Fase | Periodo | Descripcion |
|---|---|---|
| 1 | Ago-Sep 2025 | Estructura base, ordenes, clientes, facturacion |
| 2 | Oct-Nov 2025 | Balance, nomina, gastos, calendario |
| 3 | Dic 2025 - Ene 2026 | Portal Ventas V1, Proveedores, Ingresos, optimizacion |
| **4** | **Feb-Mar 2026** | **IMA Drive, Inventario, Ejecutor, Portal Ventas V2, UX/UI** |

### Releases recientes

| Version | Fecha | Highlights |
|---|---|---|
| **v2.3.3** | Abr 2026 | Fix drag-drop intermitente + tests automatizados |
| v2.3.2 | Abr 2026 | Fix drag-drop desde Ordenes (race condition) |
| v2.3.1 | Mar 2026 | Sincronizacion de carpetas + UI mejorada |
| v2.3.0 | Mar 2026 | IMA Drive mejoras CAD + ventana unica |
| v2.2.0 | Mar 2026 | Modulo Inventario + IMA Drive produccion |
| v2.1.1 | Mar 2026 | Open-in-Place + UX fixes |
| v2.1.0 | Mar 2026 | Drive V3 F+G + Modo Produccion |
| v2.0.9 | Mar 2026 | Modulo Inventario (mockup) |
| v2.0.8 | Mar 2026 | Portal Proveedores + cache fix |
| v2.0.7 | Mar 2026 | IMA Drive v1 (Cloudflare R2) |
| v2.0.6 | Mar 2026 | Portal Ventas V1 + Storage |
| v2.0.5 | Feb 2026 | Cosmeticos Fase 3 + rendimiento |
| v2.0.4 | Feb 2026 | Correccion formula gasto operativo |
| v2.0.2 | Ene 2026 | Nueva formula utilidad + fix version Assembly |
| v2.0.1 | Ene 2026 | Gastos de ordenes en balance |
| v2.0.0 | Ene 2026 | Auditoria de gastos |

Historial completo: `git log --oneline --grep "^release:"`.

### Iteracion mas reciente (abril 2026)

Los ultimos 5 commits antes de `v2.3.3` fueron fixes iterativos al auto-update para resolver UIPI bloqueando drag-drop:

```
d44710d fix: relaunch post-update via schtasks /rl limited (des-elevacion real)
0bdc11c fix: auto-update relanza app sin elevacion via script auxiliar
3d38fff fix: quitar Verb=runas del auto-update (UIPI bloqueaba drag-drop)
bcd58e6 fix: app lanzaba elevada tras auto-update (UIPI bloqueaba drag-drop)
4c93493 fix: restaurar handlers drag-drop en XAML (AddHandler no funciona en Release)
```

---

## Documentacion

Fuente canonica de verdad para cada tipo de consulta:

| Pregunta | Donde mirar |
|---|---|
| Version actual | `SistemaGestionProyectos2/SistemaGestionProyectos2.csproj` (campo `<Version>`) |
| Estructura de BD | [`db-docs/output/`](db-docs/output/) (regenerable desde Supabase en vivo) |
| Capas y patrones | [docs/01_ARQUITECTURA.md](docs/01_ARQUITECTURA.md) |
| Modelos por modulo | [docs/02_MODELOS_DATOS.md](docs/02_MODELOS_DATOS.md) |
| Metodos de servicios | [docs/03_SERVICIOS.md](docs/03_SERVICIOS.md) |
| Roles y permisos | [docs/04_ROLES_AUTENTICACION.md](docs/04_ROLES_AUTENTICACION.md) |
| Flujos de negocio | [docs/05_FLUJOS_TRABAJO.md](docs/05_FLUJOS_TRABAJO.md) |
| Ciclo de comisiones | [docs/FLUJO_COMISIONES.md](docs/FLUJO_COMISIONES.md) |
| Proceso de release | [docs/RELEASE_PROCESS.md](docs/RELEASE_PROCESS.md) |
| Fase 4 dashboard | [fase4/README.md](fase4/README.md) |

Si hay conflicto entre `docs/` y el codigo / BD, **prevalece el codigo / BD**.

---

## Herramientas de Desarrollo

- **`db-docs/`**: 7 scripts Python para autodoc de BD (tablas, relaciones, vistas, funciones, indices, RLS, diagrama ER). Ejecutar tras cambios de BD.
- **`sql/verify_drive_integrity.sql`**: 8 queries para verificar integridad R2 vs BD (huerfanos, duplicados, tamanos).
- **`sql/cleanup_drive_basura.sql`**: limpieza de archivos basura (`~$*`, `.db`, `.lck`, `.tmp`).
- **Boton "Diagnosticar"** en Drive (solo usuario `caaj`): compara R2 vs BD en vivo, detecta huerfanos, ofrece limpieza.
- **Boton "Tests"** en sidebar de Drive (solo usuario `caaj`): abre `StressTestWindow` con tests de drag-drop y workflow del Drive.
- **Instalador**: Inno Setup con cert Authenticode + firma automatica post-build.
- **Auto-update**: notificacion en-app con descarga desde GitHub Releases.

---

## Deuda Tecnica y Riesgos Abiertos

| # | Area | Severidad | Detalle |
|---|---|:---:|---|
| 1 | **RLS Supabase** | Alta | 43 de 44 tablas sin RLS. La unica con RLS (`order_ejecutores`) tiene policies `USING true` / `WITH CHECK true`. Cualquier usuario con la app puede leer/escribir cualquier tabla via REST. |
| 2 | **Credenciales en repo** | Alta | `appsettings*.json` commiteado con AnonKey + R2 SecretAccessKey. Pendiente: rotar + template + ajustar `.gitignore`. |
| 3 | **`switch-environment.bat`** | Media | Los variants `production.json` y `staging.json` perdieron las secciones `CloudflareR2` y `DevMode`. Usar el bat hoy rompe IMA Drive. |
| 4 | `SupabaseService.cs.backup` | Baja | Archivo legacy de 111 KB pre-extraccion por entidades. Se conserva por referencia historica. |
| 5 | 33 indices sin uso | Baja | Detectado en regeneracion 2026-04-20 (4.9 MB total). Candidatos a limpieza si la BD crece. |
| 6 | MVVM parcial | Baja | Drive e Inventario manejan estado en code-behind. Funciona; no urge refactorizar. |

---

## Contacto

- **Empresa:** IMA Mecatronica
- **Desarrollo:** Zuri Dev
- **Soporte:** WhatsApp / Workana
- **Repositorio:** [github.com/Anathema69/MX-VBA](https://github.com/Anathema69/MX-VBA)
