# Servicios

**Version:** 2.3.3 (abril 2026)

Este documento lista los servicios especializados, sus metodos principales y su rol dentro del sistema. Para detalle de firmas exactas ver el codigo fuente en `SistemaGestionProyectos2/Services/`.

## Arquitectura

```
 SupabaseService (Facade Singleton)
  ├─ Orders/OrderService              Negocio: ordenes
  ├─ Invoices/InvoiceService          Negocio: facturacion
  ├─ Expenses/ExpenseService          Negocio: gastos a proveedores
  ├─ FixedExpenses/FixedExpenseService Negocio: gastos fijos mensuales
  ├─ Payroll/PayrollService           Nomina
  ├─ Attendance/AttendanceService     Calendario / asistencia
  ├─ Vendors/VendorService            Vendedores + comisiones
  ├─ Clients/ClientService
  ├─ Contacts/ContactService
  ├─ Suppliers/SupplierService
  ├─ Users/UserService                Auth + CRUD
  ├─ Drive/DriveService               Cloudflare R2 + metadata BD
  ├─ Drive/FileWatcherService         Open-in-Place auto-sync
  ├─ Storage/StorageService           Supabase Storage (order-files)
  ├─ Inventory/InventoryService       Stock + movimientos
  └─ Updates/UpdateService            Auto-update + schtasks relaunch

 Infraestructura (no parte del facade):
  ├─ SessionTimeoutService (Singleton)
  ├─ JsonLoggerService (Singleton)
  ├─ UserPreferencesService
  ├─ AuthenticationService
  └─ Core/
      ├─ BaseSupabaseService         Clase base + logging
      ├─ ServiceCache                ConcurrentDict + TTL
      └─ DataChangedEvent            Observer para refrescos cruzados
```

## SupabaseService (facade)

**Ubicacion:** `Services/SupabaseService.cs` (~55KB).
Patron Singleton + Facade. Expone metodos delegados a los servicios especializados.

```csharp
public static SupabaseService Instance { get; } // lazy, thread-safe
public Client SupabaseClient { get; }

// Inicializacion:
await Instance.InitializeAsync(); // carga config, crea Client, instancia servicios

// Delegacion:
public Task<List<OrderDb>> GetOrders(...) => _orderService.GetOrders(...);
public Task<OrderDb> CreateOrder(OrderDb order, int userId) => _orderService.CreateOrder(order, userId);
// ...
```

El backup `SupabaseService.cs.backup` (111KB) conserva la version previa a la extraccion por entidades; se mantiene como referencia historica.

## BaseSupabaseService

**Ubicacion:** `Services/Core/BaseSupabaseService.cs`

Clase base abstracta. Inyecta `Client` en constructor. Provee `LogDebug/LogError/LogSuccess` uniformes.

## OrderService

Gestion del ciclo de vida de ordenes.

| Metodo | Uso |
|---|---|
| `GetOrders(limit, offset, filterStatuses)` | Listado con paginacion y filtro por estados. |
| `GetOrderById(id)` | Detalle de una orden. |
| `SearchOrders(term)` | Busqueda por PO, cliente o descripcion. |
| `CreateOrder(order, userId)` | Inserta con status = 0 (CREADA). |
| `UpdateOrder(order, userId)` | Actualiza campos. |
| `DeleteOrderWithAudit(id, deletedBy, reason)` | Soft-delete con snapshot JSONB en `t_order_deleted`. |
| `CancelOrder(id)` | status = 5. |
| `GetOrdersByClientId(clientId)` |  |
| `GetRecentOrders(limit)` |  |
| `CanCreateInvoice(orderId)` | Valida que el estado permita facturar. |

Estados en [05_FLUJOS_TRABAJO.md](./05_FLUJOS_TRABAJO.md#1-ciclo-de-vida-de-una-orden).

## UserService

Autenticacion y CRUD de usuarios.

| Metodo | Uso |
|---|---|
| `AuthenticateUser(username, password)` | Verifica con BCrypt. Retorna `(bool success, UserDb, string msg)`. |
| `GetUserByUsername / GetUserById` |  |
| `GetActiveUsers()` / `GetUsersByRole(role)` |  |
| `CreateUser(user, plainPassword)` | Hashea con `BCrypt.HashPassword`. |
| `UpdateUser(user)` / `ChangePassword(userId, newPassword)` |  |
| `DeactivateUser(id)` / `ReactivateUser(id)` | Soft-delete. |

## ClientService / ContactService / SupplierService / VendorService

CRUD estandar de cada entidad. Todos usan cache TTL (5 min para listados). `VendorService` ademas gestiona el setup de comisiones (ver [FLUJO_COMISIONES.md](./FLUJO_COMISIONES.md)).

## InvoiceService

| Metodo | Uso |
|---|---|
| `GetInvoicesByOrder(orderId)` |  |
| `GetInvoicedTotalsByOrders(orderIds)` | Totales facturados por lote de ordenes. |
| `CreateInvoice(invoice, userId)` | Inserta factura. `due_date` = `invoice_date + client.credit_days`. |
| `UpdateInvoice(invoice, userId)` / `DeleteInvoice(id, userId)` |  |
| `GetInvoiceStatuses()` |  |

Estados: PENDIENTE(1), ENVIADA(2), VENCIDA(3), PAGADA(4).

## ExpenseService / FixedExpenseService

`ExpenseService` gestiona gastos a proveedores con filtro por supplier/status/fecha. `FixedExpenseService` gestiona gastos fijos mensuales (renta, servicios, etc) con historial y fecha efectiva.

Trigger relevante: `auto_pay_zero_credit_expense` marca como PAGADO automaticamente gastos cuyo proveedor tiene `f_credit = 0`.

## PayrollService / AttendanceService

Nomina y calendario. `PayrollService` gestiona empleados, historial de cambios, total mensual. `AttendanceService` gestiona asistencias, vacaciones, feriados, overtime.

## DriveService (Fase 4)

**Ubicacion:** `Services/Drive/DriveService.cs`

Gestion de archivos en Cloudflare R2 + metadatos en Supabase (`drive_folders`, `drive_files`, `drive_activity`).

### Carpetas
| Metodo | Uso |
|---|---|
| `GetChildFolders(parentId)` | Listado de subcarpetas. |
| `GetFolderById(id)` |  |
| `CreateFolder(name, parentId, userId)` |  |
| `RenameFolder(id, newName)` |  |
| `DeleteFolder(id)` | Recursivo en R2 y BD. |
| `MoveFolder(id, targetParentId)` |  |
| `ValidateFolderMove(id, targetId)` | Previene ciclos. |
| `GetBreadcrumb(folderId)` | CTE recursivo. |
| `GetFolderStats(parentId)` | Stats bulk (file count, subfolder count, total size). |
| `GetFolderTree()` | Arbol completo (RPC `get_folder_tree`). |

### Archivos
| Metodo | Uso |
|---|---|
| `GetFilesByFolder(folderId)` |  |
| `UploadFile(localPath, folderId, userId)` | Sube blob a R2 + registra en BD. |
| `DownloadFile(fileId)` / `DownloadFileToLocal(fileId, path)` |  |
| `DownloadFileToStream(r2Key, stream)` | Para streaming grande. |
| `DownloadFilePartial(fileId, maxBytes)` | Preview rapido sin descargar todo. |
| `RenameFile` / `DeleteFile` / `MoveFile` / `CopyFile` / `DuplicateFile` |  |
| `ReuploadFile(fileId, localPath, userId)` | Version nueva tras edicion local (Open-in-Place). |
| `GetFileById(fileId)` |  |

### Vinculacion con ordenes
| Metodo | Uso |
|---|---|
| `LinkFolderToOrder(folderId, orderId)` | Asigna `drive_folders.linked_order_id`. |
| `UnlinkFolder(folderId)` |  |
| `GetFolderByOrder(orderId)` |  |
| `GetLinkedFolderIds(orderIds)` | Bulk para la columna CARPETA de OrdersManagementWindow. |
| `ValidateFolderLink(folderId)` | Valida restricciones antes de vincular. |
| `GetOrdersByIds(orderIds)` | Info de ordenes para mostrar en Drive. |

### Busqueda / recientes / actividad
| Metodo | Uso |
|---|---|
| `SearchInFolder(folderId, query)` | Scoped (dentro de subarbol). |
| `SearchFolders(query)` / `SearchFiles(query)` | Global. |
| `GetRecentFiles(limit)` / `GetRecentActivity(limit, userId)` | Feed sidebar. |
| `LogActivity(userId, action, targetType, targetId, ...)` | Inserta en `drive_activity`. |

### Almacenamiento y operaciones administrativas
| Metodo | Uso |
|---|---|
| `GetTotalStorageBytes()` | Para indicador global. |
| `GetAllFoldersFlat() / GetAllFilesFlat()` | Paginado, para diagnostico. |
| `DiagnoseOrphans()` | Compara R2 vs BD, retorna huerfanos R2 y huerfanos BD. |
| `CleanR2Orphans(keys)` | Limpieza controlada. |
| `PurgeAllR2Files()` | Solo dev; uso peligroso. |
| `CollectAllFilesRecursive(folderId)` | Para delete recursivo. |

## FileWatcherService (Fase 4)

**Ubicacion:** `Services/Drive/FileWatcherService.cs`

Maneja Open-in-Place: doble clic en un archivo del Drive -> descarga a `%LOCALAPPDATA%/IMA-Drive/` -> abre con la app asociada de Windows -> `FileSystemWatcher` detecta guardado -> sube nueva version a R2 con debounce 2s.

| Metodo | Uso |
|---|---|
| `OpenFile(file)` | Orquesta descarga + abrir + watch. |
| `DownloadContext(siblings, contextDir, onProgress)` | Descarga piezas asociadas antes de abrir un ensamble CAD. |
| `ForceReupload(fileId)` | Usuario fuerza subida de version local. |
| `RedownloadServerVersion(fileId)` | Usuario descarta cambios locales y recupera del servidor. |

Manifest JSON local en `%LOCALAPPDATA%/IMA-Drive/manifest/` con hashes SHA256 para deteccion de conflictos. Badges visuales en DriveV2Window:
- Verde = archivo abierto
- Azul = sincronizando
- Check = synced
- Rojo = error

## StorageService (Fase 4)

**Ubicacion:** `Services/Storage/StorageService.cs`

Supabase Storage. Bucket `order-files`. Se usa desde Portal Ventas para subir facturas asociadas a comisiones.

| Metodo | Uso |
|---|---|
| `UploadFile(localPath, orderId, uploadedBy, vendorId?, commissionId?)` | Sube archivo a Storage + registra en `order_files`. |
| `DownloadFile(storagePath)` | Bytes crudos. |
| `GetSignedUrl(storagePath, expiresInSeconds = 3600)` | URL temporal para preview. |
| `GetFilesByOrder(orderId)` / `GetFilesByCommission(commissionId)` |  |
| `DeleteFile(fileId, storagePath)` | BD + Storage. |
| `GetFileCountByCommission(id)` / `GetFileCountsByCommissions(ids)` | Para badges. |

## InventoryService (Fase 4)

**Ubicacion:** `Services/Inventory/InventoryService.cs`

Modulo de inventario (categorias + productos + movimientos).

### Categorias
`GetCategories()`, `GetCategorySummary()`, `CreateCategory(cat)`, `UpdateCategory(cat)`, `DeleteCategory(id, userId)`.

### Productos
`GetProductsByCategory(catId)`, `CreateProduct(p)`, `UpdateProduct(p)`, `DeleteProduct(id, userId)`.

### Stock
`AdjustStock(productId, newStock, userId, notes)` — inserta movimiento en `inventory_movements`. Retorna `StockAdjustResult`.

### Consultas
`GetStats()` — dashboard. `GetLocations(categoryId?)` — autocompletado. `GetMovements(productId, limit)` — historial.

## UpdateService (con fixes de abril 2026)

**Ubicacion:** `Services/Updates/UpdateService.cs`

Auto-update contra la tabla `app_versions`.

| Metodo | Uso |
|---|---|
| `CheckForUpdate()` | Retorna `(bool available, AppVersionDb newVersion, string message)`. Compara version del `Assembly` contra `app_versions.version` donde `is_latest=true`. |
| `DownloadUpdate(version, progress)` | Descarga desde `download_url` (GitHub Releases asset) a `%TEMP%`. |
| `InstallUpdate(installerPath, silent)` | Genera script .bat que: detiene la app, lanza el instalador, **relaunch con schtasks** para des-elevar. |

### Mecanismo de relaunch post-update (abril 2026)

Windows UIPI bloquea drag-drop desde el Explorador hacia la app si esta corre con integridad alta. Tras auto-update, la app quedaba elevada y `DataObject` del Explorer no llegaba a IMA Drive. Solucion final (commits `d44710d`, `0bdc11c`, `3d38fff`, `bcd58e6`):

```
InstallUpdate genera un script .bat que:
  1) Detiene la app actual
  2) Ejecuta el instalador en modo silent
  3) Registra tarea programada temporal:
       schtasks /create /tn "<taskName>" /tr "<appExePath>" /sc once /st 00:00 /f /rl limited
     (/rl limited garantiza nivel de integridad medio = no elevado)
  4) Dispara la tarea:
       schtasks /run /tn "<taskName>"
  5) Borra la tarea:
       schtasks /delete /tn "<taskName>" /f
```

Motivo: solo `schtasks` y `Shell COM` pueden des-elevar un proceso hijo en Windows. Un simple `Process.Start()` hereda el token elevado del padre.

Commit adicional `4c93493`: los handlers drag-drop en `DriveV2Window` deben declararse en XAML (no solo `AddHandler` en code-behind), porque Release optimiza fuera los handlers que solo se adjuntan por codigo.

## SessionTimeoutService

**Ubicacion:** `Services/SessionTimeoutService.cs`. Singleton.

Config en `appsettings.json`:
```json
"SessionTimeout": { "InactivityMinutes": 30, "WarningBeforeMinutes": 5, "Enabled": true }
```

Eventos:
- `OnWarning` — faltan 5 minutos.
- `OnTimeout` — logout forzado.
- `OnTimerTick` — cada segundo, con segundos restantes.

Input global monitoreado via `WindowHelper` (mouse/teclado). Cualquier interaccion dispara `ResetTimer`.

## JsonLoggerService

**Ubicacion:** `Services/JsonLoggerService.cs`. Singleton.

Logs por sesion en `%LOCALAPPDATA%/SistemaGestionProyectos/logs/sessions/session_YYYY-MM-DD_HH-mm-ss/`:
- `session.json` — eventos
- `session_info.json` — metadata (usuario, version, duracion)

Metodos:
- `LogInfo / LogWarning / LogError / LogDebug(module, action, data)`
- `LogLogin(username, success, userId, role)`
- `CloseSessionAsync()` — al cerrar la app.

Formato JSONL por linea. Retencion configurable (`RetentionDays`).

## ServiceCache (Core)

**Ubicacion:** `Services/Core/ServiceCache.cs`

`ConcurrentDictionary<string, (object value, DateTime expiresAt)>`. TTL por clave. API:
```csharp
cache.Get<T>(key);
cache.Set<T>(key, value, TimeSpan ttl);
cache.Invalidate(key);
cache.InvalidatePrefix("clients:");
cache.InvalidateAll();
```

Usado por `ClientService`, `SupplierService`, `VendorService`, etc.

## DataChangedEvent (Core)

**Ubicacion:** `Services/Core/DataChangedEvent.cs`

Patron observer para notificar entre ventanas. Por ejemplo, `OrdersManagementWindow` se suscribe a `EntityChanged("order", orderId)` para refrescar la fila cuando `EditOrderWindow` guarda.

## AuthenticationService / UserPreferencesService

Utilidades menores. `AuthenticationService` encapsula la llamada a `UserService.AuthenticateUser`. `UserPreferencesService` persiste preferencias UI en registro de Windows.

## Converters

En `Services/`:
- `RoleToVisibilityConverter` — Role -> Visibility por parametro.
- `AdminVisibilityConverter` — visibilidad solo para `direccion`/`administracion`.
- `PercentageConverter` — decimal -> "12.5%".

## Buenas practicas observadas

1. **Cada servicio hereda** `BaseSupabaseService` y loggea con metodos uniformes.
2. **Siempre async/await.** Nunca `.Result` o `.Wait()` en UI.
3. **Cache con TTL** para listados largos de lectura frecuente.
4. **ViewModels delgados.** La logica vive en servicios.
5. **Auditoria integrada.** `created_by` / `updated_by` se setean siempre en servicios de negocio.
6. **Try/catch + log** en todos los metodos de servicio; errores se propagan como retornos `(bool, T, msg)` cuando la UI necesita distinguir exito de fallo.
