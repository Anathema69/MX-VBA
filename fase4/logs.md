# Fase 4 - Log de Implementacion

Registro cronologico de cambios, decisiones tecnicas y hallazgos durante el desarrollo.

---

## Formato de entrada

```
### [FECHA] - Bloque X - Descripcion breve
**Tipo:** implementacion | decision | hallazgo | rollback
**Archivos:** lista de archivos modificados
**Detalle:** descripcion de lo realizado
**Notas:** observaciones adicionales
```

---

## Entradas

### 2026-02-27 - Bloque 2 - Rendimiento ya implementado
**Tipo:** documentacion
**Detalle:** Se documenta que los 6 batches de optimizacion de rendimiento ya fueron desarrollados en v2.0.4 (commit 77f55a2). Pendiente unicamente el deploy de scripts SQL en produccion.
**Scripts pendientes:**
- `sql/perf_indexes.sql`
- `sql/perf_server_aggregations.sql`
- `sql/perf_balance_materialized.sql`

### 2026-02-27 - Bloque 2 - Deploy SQL completado
**Tipo:** implementacion
**Detalle:** Scripts SQL de rendimiento ejecutados en produccion por el cliente.

### 2026-02-27 - Bloque 1 - Cosmeticos implementados
**Tipo:** implementacion
**Archivos modificados:**
- `Views/MainMenuWindow.xaml` - Renombrar portales
- `Views/MainMenuWindow.xaml.cs` - Renombrar textos visibles
- `Views/ExpenseManagementWindow.xaml` - Renombrar titulo
- `Views/VendorDashboard.xaml.cs` - Quitar MessageBox de logout
- `Views/OrdersManagementWindow.xaml` - Centrado, anchos, CanUserResizeColumns
- `Views/OrdersManagementWindow.xaml.cs` - Filtro ano default actual
- `Views/PendingIncomesDetailView.xaml` - Centrado columnas
- `Views/PayrollHistoryWindow.xaml` - Centrado columnas
- `Views/ClientManagementWindow.xaml` - Centrado columnas
- `App.xaml.cs` - Fix screen share: Hide() antes de Close()

**Compilacion:** 0 errores.

### 2026-02-27 - Bloque 1 - Bugs adicionales descubiertos y corregidos
**Tipo:** implementacion
**Hallazgos del desarrollador:**
1. Ventanas desbordan en pantallas pequenas al abrir
2. Botones de volver con nombres inconsistentes (5 variantes)
3. Barra de tareas cubierta en algunas ventanas

**Archivos modificados:**
- `Views/PendingIncomesDetailView.xaml` - Agregado MaxHeight para respetar barra de tareas
- `Views/ClientManagementWindow.xaml` - "Volver al menú" -> "Volver"
- `Views/VendorCommissionsWindow.xaml` - "← Volver al menú" -> "← Volver"
- `Views/BalanceWindowPro.xaml` - "Cerrar" -> "← Volver"
- `Views/StressTestWindow.xaml` - "Cerrar" -> "← Volver"
- `Views/PendingIncomesView.xaml` - "Regresar" -> "Volver"
- `Views/PendingIncomesDetailView.xaml` - "Regresar" -> "Volver"
- `Views/SupplierPendingView.xaml` - "Regresar" -> "Volver"
- `Views/SupplierPendingDetailView.xaml` - "Regresar" -> "Volver"
- `Views/InvoiceManagementWindow.xaml` - emoji "⬅" -> "←"
- `Views/OrdersManagementWindow.xaml` - emoji "⬅" -> "←"

**Detalle:**
- **Taskbar:** 12 de 13 ventanas grandes ya tenian MaximizeWithTaskbar(). Solo PendingIncomesDetailView (Maximized) le faltaba MaxHeight.
- **Botones:** Estandarizado a "← Volver" para navegacion. "Cancelar" para dialogs. "Cerrar Sesión" para logout.
- **Desbordes:** La mayoria de ventanas ya tienen MaximizeWithTaskbar() que ajusta al area de trabajo. El desborde inicial al abrir en pantalla pequena se resuelve porque MaximizeWithTaskbar() se ejecuta en el constructor antes de mostrar la ventana.

**Compilacion:** 0 errores.

### 2026-02-27 - Bloque 1 - Fix multi-monitor (BUG-004)
**Tipo:** implementacion
**Archivos creados:**
- `Helpers/WindowHelper.cs` - Helper con Win32 PInvoke (MonitorFromWindow + GetMonitorInfo + DPI scaling)

**Archivos modificados (13 views):**
- Todas las ventanas con MaximizeWithTaskbar(): CalendarView, ClientManagement, ExpenseManagement, InvoiceManagement, MainMenu, OrdersManagement, PayrollManagement, PendingIncomesView, SupplierPendingView, SupplierPendingDetailView, VendorCommissions, VendorManagement, VendorDashboard
- Cada MaximizeWithTaskbar() ahora delega a `WindowHelper.MaximizeToCurrentMonitor(this)`
- Cada constructor agrega `SourceInitialized += (s, e) => MaximizeWithTaskbar()` para re-aplicar con handle real

**Archivos XAML:**
- `PendingIncomesDetailView.xaml` - Agregado MaxHeight para Maximized sin taskbar overlap

**Compilacion:** 0 errores.

### 2026-03-05 - General - BUG-005: Portal Proveedores no permite eliminar gasto pagado
**Tipo:** bugfix (reportado por cliente)
**Archivos modificados:**
- `Views/SupplierPendingDetailView.xaml` - Boton eliminar visible para todos (IsReadOnly vs IsPayable), labels con x:Name para headers dinamicos, DataTrigger para status PAGADO (azul), fix foreground en celdas seleccionadas, empty state para filtros sin resultados
- `Views/SupplierPendingDetailView.xaml.cs` - Status PAGADO para gastos con PaidDate, headers dinamicos segun tab (TOTAL PAGADO/PAGADOS TARDE/PAGADOS A TIEMPO), delete con auditoria (updated_by antes de delete), empty state handler, separacion de totalPending vs totalPaid vs totalPaidLate

**Diagnostico:** Consultas SQL en `fase4/sql/` confirmaron: trigger `auto_pay_zero_credit_expense` auto-paga gastos de proveedores con 0 dias credito, ocultando el boton eliminar. Verificacion antes/despues con balance confirmó integridad.
**Compilacion:** 0 errores.

### 2026-03-05 - General - BUG-006: Calendario no permite modificar asistencia
**Tipo:** bugfix (reportado por cliente)
**Archivos modificados:**
- `Services/Attendance/AttendanceService.cs` - Update reescrito con read-modify-write (modelo completo) en vez de .Set() encadenado. Nuevo metodo DeleteAttendance() para desmarcar con auditoria.
- `Views/CalendarView.xaml` - Boton Actualizar agregado al header
- `Views/CalendarView.xaml.cs` - Desmarcar (mismo boton = eliminar registro), boton VACACIONES convertido a icono indicador visual, guard para status VACACIONES, handler RefreshButton_Click
- `Models/Database/AttendanceDb.cs` - Sin cambios (modelo ya correcto)

**Causa raiz:** Postgrest .Set() no acepta null TimeSpan? (crash al cambiar a FALTA). Ademas .Update() no retornaba modelos, disparando throw falso.
**Diagnostico:** Query mostro 0 UPDATEs en toda la historia. Post-fix: 16 UPDATEs exitosos confirmados en audit trail.
**Compilacion:** 0 errores.

### 2026-03-05 - General - Propuesta mejora modulo vacaciones
**Tipo:** documentacion
**Archivos creados:**
- `fase4/propuesta_vacaciones_calendario.md` - 3 opciones de diseño para integrar vacaciones desde calendario, con estimaciones y plan de integridad de datos
**Origen:** Caso Cesar Vidales 3-Mar (asistencia + vacacion simultanea). Pendiente definir con cliente cual prevalece.

### 2026-03-06 - Bloque 3 - Portal Ventas con subida de archivos (Storage)
**Tipo:** implementacion
**Version:** v2.0.6

**Archivos creados:**
- `sql/bloque3_storage.sql` - Tabla order_files + indexes
- `Models/Database/OrderFileDb.cs` - Modelo Postgrest
- `Services/Storage/StorageService.cs` - Upload, download, list, delete via Supabase Storage

**Archivos modificados:**
- `Services/SupabaseService.cs` - StorageService en facade (7 delegaciones)
- `Views/VendorDashboard.xaml` + `.cs` - Rediseno completo con galeria inline, thumbnails, preview con navegacion
- `Views/VendorCommissionsWindow.xaml` + `.cs` - Galeria inline para admin, preview fullscreen

**Decisiones:** Supabase Storage (free tier 1GB). CRUD por estado: draft/pending = todo, paid = solo ver/descargar. Thumbnails lazy. Toggle collapsible con hover.

**Pendiente:** Confirmar si "Solicitar Liberacion" debe cambiar estado de la orden a LIBERADA(2).

**Compilacion:** 0 errores.

### 2026-03-07 - Bloque 5 - Modulo ARCHIVOS (Drive IMA) con Cloudflare R2
**Tipo:** implementacion
**Version:** v2.0.7

**Decision arquitectonica:** Se descarto Supabase Storage (1GB free, 50MB/archivo) en favor de Cloudflare R2 (10GB free, 5GB/archivo, sin egress fees). Bucket `ima-drive` separado de `order-files` (Portal Ventas). Estructura hibrida: BD para arbol de carpetas + metadatos, R2 solo para blobs.

**Archivos creados:**
- `sql/bloque5_drive.sql` - Tablas drive_folders + drive_files, indexes, trigger updated_at, funciones breadcrumb/child_count, seed raiz
- `sql/bloque5_audit.sql` - Tabla drive_audit + 6 triggers (INSERT/UPDATE/DELETE en folders y files) + fix timestamps NULL
- `sql/consulta_drive_audit.sql` - Consultas de auditoria (cronologia, resumen, por usuario, estado actual)
- `Models/Database/DriveFolderDb.cs` - Modelo Postgrest carpeta
- `Models/Database/DriveFileDb.cs` - Modelo Postgrest archivo
- `Services/Drive/DriveService.cs` - Servicio completo: CRUD carpetas, CRUD archivos (R2 via AWSSDK.S3), vinculacion a ordenes, breadcrumb, batch delete, purge
- `Views/DriveWindow.xaml` + `.cs` - UI tipo Google Drive: breadcrumb clickeable, cards por tipo, menu contextual, upload con indicador de progreso, mouse back/forward (XButton1/XButton2), modo seleccion para vincular ordenes

**Archivos modificados:**
- `SistemaGestionProyectos2.csproj` - Agregado AWSSDK.S3
- `appsettings.json` - Seccion CloudflareR2 (AccountId, AccessKeyId, SecretAccessKey, BucketName)
- `Services/SupabaseService.cs` - DriveService registrado en facade (17 delegaciones)
- `Views/MainMenuWindow.xaml` + `.cs` - Boton ARCHIVOS (naranja, badge BETA, al final del menu)
- `Views/LoginWindow.xaml.cs` - Soporte AutoOpenModule "archivos"

**Decisiones tecnicas:**
- R2 requiere `DisablePayloadSigning = true` en PutObjectRequest (no soporta STREAMING-AWS4-HMAC-SHA256-PAYLOAD)
- Delete de carpetas con contenido: primero recolecta todos los storage paths recursivamente, luego DELETE en BD (CASCADE), luego batch delete en R2 (fire-and-forget en background thread)
- Vinculacion orden-carpeta: solo carpetas del primer nivel (hijos directos de raiz), relacion 1:1 (ordenes ya vinculadas se excluyen del ComboBox)
- Breadcrumb: handler separado `BreadcrumbItem_Click` en vez de async lambda (evita problemas de closure)
- Timestamps NULL: Postgrest envia campos como null explicito anulando el DEFAULT. Solucion: trigger BEFORE INSERT que fuerza CURRENT_TIMESTAMP

**Pendiente Bloque 5:**
- Fase 5D: Columna CARPETA en OrdersManagementWindow (icono gris/azul por orden)
- Fase 5E: Drag & drop, busqueda, quitar boton Purgar R2 temporal

**Compilacion:** 0 errores.

### 2026-03-08 - Bloque 5 - Drive V2 Mockup (solo UI)
**Tipo:** implementacion
**Archivos creados:**
- `Views/DriveV2Window.xaml` + `.cs` - Rediseno completo basado en Figma: sidebar con nav + filtros + indicador storage, topbar con busqueda + toggle grid/list, breadcrumb, cards de carpeta con accent color + stats + orden vinculada, cards de archivo con preview + badge extension, panel de detalle lateral, panel de upload con progreso, drag & drop overlay, empty state con spinner animado

**Detalle:** Mockup UI sin logica de BD. Layout tipo Google Drive profesional con 3 columnas (sidebar 256px + contenido + panel derecho 320px).

### 2026-03-09 - Bloque 5 - Drive V2 Logica completa + eliminar V1
**Tipo:** implementacion
**Archivos eliminados:**
- `Views/DriveWindow.xaml` + `.cs` - Version 1 del Drive eliminada

**Archivos modificados:**
- `Views/DriveV2Window.xaml` + `.cs` - Logica completa conectada a BD + R2. Caches de stats/ordenes/usuarios. Navegacion con historial back/forward. Creacion inline de carpetas. Upload con panel de progreso. Busqueda con debounce. Vista grid/list toggle.
- `Views/MainMenuWindow.xaml` + `.cs` - Actualizado para abrir DriveV2Window
- `Views/LoginWindow.xaml.cs` - AutoOpenModule apunta a DriveV2Window

**Decision:** Se elimino DriveWindow v1 completamente. DriveV2Window usa el mismo DriveService sin cambios.

### 2026-03-09 - Bloque 5 - Drive V2 Rediseno UI + correcciones UX
**Tipo:** implementacion
**Archivos modificados:**
- `Views/DriveV2Window.xaml` - Ajustes de layout y estilos
- `Views/DriveV2Window.xaml.cs` - Correcciones UX, limpieza de codigo

### 2026-03-10 - Bloque 3 - Portal Ventas V2 dashboard vendedor
**Tipo:** implementacion
**Archivos creados:**
- `Views/VendorDashboard_V2.xaml` + `.cs` - Rediseno completo del dashboard vendedor

**Archivos modificados:**
- `Views/VendorCommissionsWindow.xaml` + `.cs` - Mejoras preview + galeria admin
- `Views/VendorDashboard.xaml` + `.cs` - Ajustes menores
- `Views/MainMenuWindow.xaml` - Actualizado
- `Views/LoginWindow.xaml.cs` - Actualizado
- `Services/Drive/DriveService.cs` - Fix menor
- `Services/Storage/StorageService.cs` - Fix menor

**Compilacion:** 0 errores.

### 2026-03-10 - Bloque 5 - Fase 5D: Columna CARPETA + iconos PNG + datos reales
**Tipo:** implementacion

**Archivos creados:**
- `ico-ima/folder_off.png` - Icono carpeta sin vincular (24x24 PNG)
- `ico-ima/folder_on.png` - Icono carpeta vinculada (24x24 PNG)
- `ico-ima/folder_pin.png` - Icono carpeta principal en Drive (24x24 PNG)
- `fase4/dirve_test/upload_to_drive.py` - Script Python para subida masiva a R2 + BD
- `fase4/dirve_test/link_folders_to_orders.py` - Script Python para vincular carpetas a ordenes
- `fase4/dirve_test/.gitignore` - Excluye venv, 2026/, report

**Archivos modificados:**
- `Models/OrderViewModel.cs` - LinkedFolderId, HasLinkedFolder, FolderIconSource, FolderTooltip
- `Views/OrdersManagementWindow.xaml` - Columna CARPETA separada con icono PNG (folder_off/folder_on)
- `Views/OrdersManagementWindow.xaml.cs` - LoadLinkedFoldersBatch(), FolderButton_Click (consulta BD fresca), refresh DataGrid con CollectionViewSource reset
- `Views/DriveV2Window.xaml.cs` - Constructor DriveV2Window(user, folderId) para navegacion directa, MkFolderIco usa folder_pin.png, modo seleccion bloquea carpetas vinculadas (opacity+badge), busqueda global con ILike (pendiente ajustes UX), LinkThisFolder valida carpeta no ocupada
- `Services/Drive/DriveService.cs` - SearchFolders/SearchFiles (ILike global, limit 30/50), GetLinkedFolderIds fix ToDictionary crash con TryAdd (caso 1:N Rack V2.0/V2.1)
- `Services/SupabaseService.cs` - SearchDriveFolders, SearchDriveFiles delegaciones
- `SistemaGestionProyectos2.csproj` - Resource Include ico-ima/*.png
- `appsettings.json` - AutoLogin temporal con usuario caaj

**Datos reales subidos:**
- 520MB, 1268 archivos, 111 carpetas (estructura 2026: Ene/Feb/Mar)
- 23 carpetas vinculadas a ordenes automaticamente
- 5 carpetas sin match (Junco/Mesa Servicios, Engicom/RFQ-47337, Lennox/Fixture planta2, Lennox/valvula reversible, Shot&Shot/Rack V2.1)

**Bugs corregidos:**
- ToDictionary crash cuando 2 carpetas vinculadas a misma orden (Rack V2.0 + V2.1 -> orden 1176)
- Iconos siempre grises: View.Refresh() no re-evaluaba computed properties -> fix con CollectionViewSource reset
- FolderButton_Click: ahora consulta BD fresca antes de decidir abrir vs vincular

**Pendiente:**
- Busqueda global requiere ajustes UX (no navega al resultado, no muestra ubicacion)
- Quitar temporales: Purgar R2, Benchmark, auto-login

**Compilacion:** 0 errores.

---
