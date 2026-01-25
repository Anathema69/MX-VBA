# Módulo Calendario de Personal

**Versión:** 1.3
**Fecha:** 25 de Enero 2026
**Estado:** Funcional - Listo para producción

---

## 1. Descripción

Sistema completo para gestión de asistencia del personal:
- **Asistencias**: Registro de llegadas puntuales (hora esperada: 08:00)
- **Retardos**: Llegadas tardías con cálculo automático de minutos
- **Faltas**: Ausencias justificadas o no justificadas
- **Vacaciones**: Períodos de descanso programados
- **Feriados**: Días festivos oficiales de México
- **Fines de Semana**: Configuración de días laborales

---

## 2. Estructura de Base de Datos

### 2.1 Tablas Principales

| Tabla | Descripción |
|-------|-------------|
| `t_attendance` | Registro diario de asistencia por empleado |
| `t_vacation` | Períodos de vacaciones con aprobación |
| `t_holiday` | Días feriados oficiales y personalizados |
| `t_workday_config` | Configuración de días laborales (L-V) |
| `t_attendance_audit` | Historial de todos los cambios |

### 2.2 Estados de Asistencia

```
┌─────────────┬────────────┬─────────────────────────────────┐
│ Estado      │ Color      │ Descripción                     │
├─────────────┼────────────┼─────────────────────────────────┤
│ ASISTENCIA  │ #10B981    │ Llegada puntual (verde)         │
│ RETARDO     │ #F59E0B    │ Llegada tardía (amarillo)       │
│ FALTA       │ #EF4444    │ Ausencia (rojo)                 │
│ VACACIONES  │ #8B5CF6    │ Período de descanso (morado)    │
│ SIN_REGISTRO│ #E5E7EB    │ Sin marcar (gris)               │
└─────────────┴────────────┴─────────────────────────────────┘
```

### 2.3 Diagrama de Relaciones

```
┌─────────────────┐     ┌─────────────────┐
│    t_payroll    │     │    t_holiday    │
│   (empleados)   │     │   (feriados)    │
└────────┬────────┘     └─────────────────┘
         │
         │ 1:N
         │
    ┌────┴────┐
    │         │
┌───▼───┐ ┌───▼───┐
│t_atten│ │t_vaca │
│dance  │ │tion   │
└───┬───┘ └───┬───┘
    │         │
    │ Trigger │
    │         │
┌───▼─────────▼───┐
│ t_attendance_   │
│     audit       │
└─────────────────┘
```

---

## 3. Sistema de Auditoría

### 3.1 ¿Por qué Triggers?

Usamos **triggers de PostgreSQL** para la auditoría porque:

| Ventaja | Descripción |
|---------|-------------|
| **Automático** | No requiere código adicional en la app |
| **Completo** | Captura TODOS los cambios, incluso directos en BD |
| **Seguro** | Imposible saltarse la auditoría |
| **Detallado** | Guarda valores antes y después |

### 3.2 Información Registrada

Cada cambio registra:
- ID del registro afectado
- Empleado involucrado
- Fecha de asistencia
- Tipo de acción (INSERT/UPDATE/DELETE)
- Valores anteriores y nuevos
- Usuario que hizo el cambio
- Timestamp del cambio

### 3.3 Consultar Historial

```sql
-- Ver historial de cambios de un empleado
SELECT * FROM v_attendance_history
WHERE employee_name = 'Juan García'
ORDER BY changed_at DESC;

-- Ver todos los cambios del día
SELECT * FROM t_attendance_audit
WHERE DATE(changed_at) = CURRENT_DATE;
```

---

## 4. Feriados y Días de Descanso

### 4.1 Feriados Oficiales México 2026

| Fecha | Nombre |
|-------|--------|
| 01/01 | Año Nuevo |
| 05/02 | Día de la Constitución |
| 21/03 | Natalicio de Benito Juárez |
| 01/05 | Día del Trabajo |
| 16/09 | Día de la Independencia |
| 20/11 | Revolución Mexicana |
| 25/12 | Navidad |

### 4.2 Configuración de Días Laborales

Por defecto:
- **Lunes a Viernes**: Días laborales
- **Sábado y Domingo**: Días de descanso

Modificable en `t_workday_config`:
```sql
-- Hacer sábado laboral
UPDATE t_workday_config
SET is_workday = TRUE
WHERE day_of_week = 6;
```

### 4.3 Función is_workday()

```sql
-- Verificar si una fecha es día laboral
SELECT is_workday('2026-01-25');  -- Retorna TRUE/FALSE
```

---

## 5. Scripts SQL

### 5.1 Orden de Ejecución

```bash
# Opción 1: Ejecutar script maestro
psql -f docs/cambios_ene26/calendario/00_EXECUTE_ALL.sql

# Opción 2: Ejecutar uno por uno
psql -f docs/cambios_ene26/calendario/01_CREATE_TABLES.sql
psql -f docs/cambios_ene26/calendario/02_CREATE_HOLIDAYS.sql
psql -f docs/cambios_ene26/calendario/03_CREATE_AUDIT.sql
psql -f docs/cambios_ene26/calendario/04_CREATE_VIEWS.sql
```

### 5.2 Descripción de Scripts

| Script | Contenido |
|--------|-----------|
| `00_EXECUTE_ALL.sql` | Script maestro (ejecuta todo) |
| `01_CREATE_TABLES.sql` | Tablas t_attendance, t_vacation |
| `02_CREATE_HOLIDAYS.sql` | Feriados, t_workday_config |
| `03_CREATE_AUDIT.sql` | Auditoría con triggers |
| `04_CREATE_VIEWS.sql` | Vistas para reportes |
| `98_FIX_AUDIT_SEQUENCE.sql` | Corregir secuencias desincronizadas |
| `99_CLEANUP_TEST_DATA.sql` | Limpieza de datos de prueba |

### 5.3 Limpieza de Datos de Prueba

Para eliminar todos los registros de prueba:

```sql
-- En Supabase SQL Editor:
DELETE FROM t_attendance_audit;
DELETE FROM t_attendance;
ALTER SEQUENCE t_attendance_id_seq RESTART WITH 1;
ALTER SEQUENCE t_attendance_audit_id_seq RESTART WITH 1;
```

---

## 6. Vistas Disponibles

| Vista | Descripción |
|-------|-------------|
| `v_attendance_monthly_summary` | Resumen mensual por empleado |
| `v_attendance_today` | Estado del día actual |
| `v_vacations_active` | Vacaciones activas y próximas |
| `v_attendance_stats` | Estadísticas generales del mes |
| `v_attendance_history` | Historial de cambios legible |

### 6.1 Funciones Útiles

```sql
-- Asistencia de una fecha específica
SELECT * FROM get_attendance_for_date('2026-01-25');

-- Calendario del mes con estadísticas
SELECT * FROM get_month_calendar(2026, 1);

-- Generar feriados para un año
SELECT generate_holidays_for_year(2027);
```

---

## 7. Archivos de la Aplicación

### 7.1 Modelos (Models/Database/)

| Archivo | Descripción |
|---------|-------------|
| `AttendanceDb.cs` | AttendanceTable, AttendanceViewModel, AttendanceMonthlyStats, CalendarDayInfo |
| `VacationDb.cs` | VacationTable, VacationViewModel |
| `HolidayDb.cs` | HolidayTable, WorkdayConfigTable, HolidayViewModel |

### 7.2 Servicios (Services/Attendance/)

| Archivo | Descripción |
|---------|-------------|
| `AttendanceService.cs` | Servicio completo para gestión de asistencia con cache optimizado |

**Métodos principales de AttendanceService:**

| Método | Descripción |
|--------|-------------|
| `GetAttendanceForDate(date)` | Obtiene asistencia de todos los empleados para una fecha |
| `SaveAttendance(attendance)` | Guarda o actualiza un registro de asistencia |
| `MarkAllPresent(date, userId, checkInTime)` | Marca asistencia masiva con hora de entrada |
| `GetMonthlyStats(year, month)` | Estadísticas del mes |
| `CalculateLateMinutes(checkIn, expected)` | Calcula minutos de retardo |
| `InvalidateCache()` | Invalida todos los caches |
| `GetActiveEmployees()` | Lista de empleados activos |
| `CreateVacation(vacation)` | Registra nuevas vacaciones |
| `GetActiveVacations()` | Vacaciones activas y próximas |
| `HasVacationConflict(...)` | Verifica conflictos de fechas |
| `CancelVacation(id, userId)` | Cancela una vacación |
| `CalculateWorkingDays(start, end)` | Días laborales entre dos fechas |

**Sistema de Cache:**
- `_employeesCache` - Cache de empleados activos (5 minutos TTL)
- `_holidaysCache` - Cache de feriados por año
- `_workdayConfigCache` - Cache de configuración de días laborales

### 7.3 Vistas (Views/)

| Archivo | Descripción |
|---------|-------------|
| `CalendarView.xaml` | Interfaz visual del calendario |
| `CalendarView.xaml.cs` | Lógica de la ventana con optimizaciones de rendimiento |

---

## 8. Optimizaciones de Rendimiento

### 8.1 Cache Multinivel

```
┌─────────────────────────────────────────────────────────┐
│                    CalendarView                          │
├─────────────────────────────────────────────────────────┤
│  _attendanceCache     │ Cache por fecha (yyyy-MM-dd)    │
│  _currentMonthStats   │ Estadísticas del mes actual     │
│  _calendarButtons     │ Referencias a botones del mes   │
└─────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────┐
│                  AttendanceService                       │
├─────────────────────────────────────────────────────────┤
│  _employeesCache      │ Empleados activos (5 min TTL)   │
│  _holidaysCache       │ Feriados del año                │
│  _workdayConfigCache  │ Configuración días laborales    │
└─────────────────────────────────────────────────────────┘
```

### 8.2 Actualización Parcial de UI

| Acción | Antes | Ahora |
|--------|-------|-------|
| Guardar asistencia | Recarga toda la BD | Actualiza cache local |
| Cambiar estado | Regenera toda la lista | Actualiza solo la tarjeta |
| Seleccionar fecha | Regenera calendario | Solo actualiza selección |
| Actualizar stats | Consulta BD | Incremento/decremento local |

### 8.3 Consultas Paralelas

```csharp
// Carga inicial optimizada con Task.WhenAll
var employeesTask = GetEmployeesCached();
var attendanceTask = GetAttendanceRecords(date);
var vacationsTask = GetActiveVacationsForDate(date);
var holidayTask = GetHolidayForDate(date);
var workdayTask = GetWorkdayConfig();

await Task.WhenAll(employeesTask, attendanceTask, vacationsTask, holidayTask, workdayTask);
```

---

## 9. Interfaz de Usuario

### 9.1 Calendario Dinámico

- Alineación correcta de días (Lunes=0, Domingo=6)
- Navegación por meses con flechas
- Resaltado del día actual y seleccionado
- Fines de semana en rojo

### 9.2 Lista de Empleados

- Tarjetas con avatar, nombre y cargo
- Badges de estado (Retardo X min, Falta, Vacaciones)
- Botones de acción: ✓ Asistencia, 🕐 Retardo, ✗ Falta, 🏖 Vacaciones
- **Protección de vacaciones**: Empleados en vacaciones tienen botones deshabilitados
- **Doble validación**: Si se intenta marcar asistencia a empleado de vacaciones, muestra advertencia

### 9.3 Diálogo de Retardo

```
┌─────────────────────────────────────┐
│  Registrar Hora de Entrada          │
│                                     │
│  Hora esperada: 08:00               │
│                                     │
│  ¿A qué hora llegó el empleado?     │
│                                     │
│     ┌────┐   ┌────┐                 │
│     │ 08 │ : │ 15 │                 │
│     └────┘   └────┘                 │
│                                     │
│  Formato 24 horas (ej: 08:30)       │
│                                     │
│  ⏰ Retardo de 15 minutos           │
│                                     │
│          [Cancelar] [Registrar]     │
└─────────────────────────────────────┘
```

**Características:**
- TextBox editable con validación en tiempo real
- **Hora**: Solo permite 00-23 (no deja escribir valores mayores)
- **Minutos**: Solo permite 00-59 (no deja escribir valores mayores)
- Considera texto seleccionado al validar (reemplazo correcto)
- Preview en tiempo real del retardo calculado
- Tecla Enter para guardar, Escape para cancelar
- Auto-avance de hora a minutos al completar 2 dígitos
- Cálculo automático: `retardo = horaLlegada - horaEsperada`

### 9.4 Diálogo de Vacaciones

```
┌──────────────────────────────────────────┐
│  Registrar Vacaciones                    │
│                                          │
│  Empleado *                              │
│  ┌──────────────────────────────────┐    │
│  │ Juan García López            ▼   │    │
│  └──────────────────────────────────┘    │
│                                          │
│  Fecha Inicio *                          │
│  ┌──────────────────────────────────┐    │
│  │ 27/01/2026                   📅  │    │
│  └──────────────────────────────────┘    │
│                                          │
│  Fecha Fin *                             │
│  ┌──────────────────────────────────┐    │
│  │ 02/02/2026                   📅  │    │
│  └──────────────────────────────────┘    │
│                                          │
│  📅 5 días laborales                     │
│                                          │
│  Observaciones (opcional)                │
│  ┌──────────────────────────────────┐    │
│  │ Viaje familiar                   │    │
│  └──────────────────────────────────┘    │
│                                          │
│          [ Cancelar ] [ ✓ Registrar ]    │
└──────────────────────────────────────────┘
```

**Características:**
- ComboBox con lista de empleados activos
- DatePicker para fechas inicio/fin
- Cálculo automático de días laborales (excluye fines de semana)
- Validación de conflictos de fechas
- Auto-aprobación desde calendario

### 9.5 Tarjetas de Resumen

| Card | Color | Muestra |
|------|-------|---------|
| Asistencias | Verde | Total del mes |
| Retardos | Amarillo | Total del mes |
| Faltas | Rojo | Total del mes |
| Vacaciones | Morado | Días del mes |

---

## 10. Próximos Pasos

### Fase 1: Base de Datos ✅
- [x] Diseño de tablas
- [x] Scripts SQL
- [x] Sistema de auditoría
- [x] Feriados México 2026

### Fase 2: Modelos C# ✅
- [x] Crear AttendanceDb.cs
- [x] Crear VacationDb.cs
- [x] Crear HolidayDb.cs
- [x] Crear AttendanceService.cs con cache

### Fase 3: Conexión UI ✅
- [x] Cargar empleados desde t_payroll
- [x] Cargar/guardar asistencias
- [x] Renderizado dinámico de lista de empleados
- [x] Botones de estado con click handlers
- [x] Dialog para hora de retardo (TextBox editable)
- [x] Calendario dinámico con navegación
- [x] Optimizaciones de rendimiento
- [x] Cache multinivel
- [x] Actualización parcial de UI

### Fase 4: Vacaciones ✅
- [x] Modal de vacaciones con selección de empleado
- [x] Selector de fecha inicio y fin (DatePicker)
- [x] Cálculo automático de días laborales
- [x] Validación de conflictos de fechas
- [x] Campo de observaciones opcional
- [x] Auto-aprobación desde calendario
- [x] Bloqueo de asistencia para empleados de vacaciones

### Fase 5: DevMode y Validaciones ✅
- [x] Auto-login configurable
- [x] Auto-apertura de módulo calendario
- [x] Validación en tiempo real de hora (00-23)
- [x] Validación en tiempo real de minutos (00-59)
- [x] Manejo correcto de texto seleccionado en validación

### Fase 6: Pendientes
- [ ] Reporte mensual por empleado
- [ ] Exportar a Excel
- [ ] Gráficas de asistencia
- [ ] Gestión de vacaciones (aprobar/rechazar/cancelar)

---

## 11. Permisos por Rol

| Rol | Acceso |
|-----|--------|
| direccion | Completo (CRUD + aprobar vacaciones) |
| administracion | Completo (CRUD + aprobar vacaciones) |
| otros | Sin acceso |

---

## 12. Modo Desarrollo (DevMode)

Para acelerar pruebas durante desarrollo, se puede habilitar auto-login y auto-apertura del calendario.

### 12.1 Configuración en appsettings.json

```json
{
  "DevMode": {
    "Enabled": true,
    "AutoLogin": true,
    "Username": "caaj",
    "Password": "anathema",
    "AutoOpenModule": "calendar"
  }
}
```

### 12.2 Opciones Disponibles

| Opción | Tipo | Descripción |
|--------|------|-------------|
| `Enabled` | bool | Activa/desactiva modo desarrollo |
| `AutoLogin` | bool | Login automático al iniciar |
| `Username` | string | Usuario para auto-login |
| `Password` | string | Contraseña para auto-login |
| `AutoOpenModule` | string | Módulo a abrir: `"calendar"` o vacío para menú principal |

### 12.3 Flujo con DevMode Activo

```
Inicio App → Login automático → Carga → CalendarView (directo)
```

**Nota:** Desactivar DevMode antes de producción:
```json
"DevMode": {
  "Enabled": false,
  "AutoLogin": false
}
```

---

## 13. Troubleshooting

### Error: "Unknown criterion type"
**Causa:** Postgrest no acepta filtros booleanos directos.
**Solución:** Se usa LINQ para filtrar después de obtener datos.

### Calendario muestra días incorrectos
**Causa:** Cálculo de día de semana incorrecto.
**Solución:** Usar `((int)date.DayOfWeek + 6) % 7` para Lunes=0.

### Lentitud al registrar asistencia
**Causa:** Recarga completa de BD después de cada acción.
**Solución:** Implementar cache local y actualización parcial de UI.

### Botones no cambian de color
**Causa:** Template no bindea Background correctamente.
**Solución:** Usar `{TemplateBinding Background}` en el Border del template.

### Error: "cannot insert into column total_days"
**Causa:** `total_days` es columna generada en PostgreSQL.
**Solución:** Agregar `ShouldSerializeTotalDays() => false` en VacationTable.

### Campo de minutos no permite escribir
**Causa:** Validación no consideraba texto seleccionado.
**Solución:** Calcular texto resultante considerando `SelectionStart` y `SelectionLength`.

---

## 14. Historial de Cambios

### v1.3 (25/01/2026)
- **Validación en tiempo real mejorada**: Hora (00-23) y minutos (00-59) no permiten escribir valores fuera de rango
- **Considera texto seleccionado**: Validación calcula correctamente cuando hay texto seleccionado
- **Bloqueo de vacaciones**: Doble validación para impedir marcar asistencia a empleados de vacaciones
- **DevMode mejorado**: Auto-login y auto-apertura del calendario para pruebas rápidas
- **Fix columna generada**: `total_days` en vacaciones ya no se envía al servidor (es calculada por PostgreSQL)
- Nueva opción `AutoOpenModule` en appsettings.json

### v1.2 (25/01/2026)
- Módulo de vacaciones completo
- Diálogo para registrar vacaciones con DatePicker
- Validación de hora (solo números, rango 00-23:00-59)
- Cálculo automático de días laborales
- Validación de conflictos de fechas
- Script para corregir secuencias desincronizadas (98_FIX_AUDIT_SEQUENCE.sql)

### v1.1 (25/01/2026)
- Optimizaciones de rendimiento con cache multinivel
- Diálogo de retardo mejorado con TextBox editable
- Actualización parcial de UI (solo tarjeta afectada)
- Calendario dinámico sin regeneración completa
- Script de limpieza de datos de prueba
- Cálculo automático de minutos de retardo

### v1.0 (25/01/2026)
- Versión inicial
- Scripts SQL completos
- Modelos C# y servicio
- UI básica funcional

---

## 15. Contacto

**Desarrollado para:** IMA Mecatrónica
**Repositorio:** github.com/Anathema69/MX-VBA
