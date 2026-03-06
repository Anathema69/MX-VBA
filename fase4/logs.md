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

---
