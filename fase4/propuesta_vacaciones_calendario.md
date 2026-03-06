# Propuesta: Integracion Calendario - Modulo de Vacaciones

**Origen:** Durante la correccion del Bug-006 (calendario), se identifico que el boton de vacaciones en cada fila del calendario marcaba directamente el status sin pasar por el modulo de vacaciones, causando conflictos de datos (ej: empleado con asistencia + vacacion aprobada el mismo dia).

**Estado actual:** El boton de vacaciones en fila fue convertido a icono indicador visual. Las vacaciones se gestionan exclusivamente desde el boton superior "Programar Vacaciones".

---

## Problema identificado

1. El usuario ve una fila bloqueada por vacaciones pero no tiene acceso rapido para gestionarlas
2. Si las vacaciones fueron programadas por error, debe salir del calendario, buscar el modulo de vacaciones, encontrar al empleado y cancelar
3. No hay forma rapida de reprogramar fechas sin cancelar y volver a crear

## Propuesta de mejora

### Opcion A: Click en icono abre dialogo pre-llenado (recomendada)

Al hacer click en el icono de vacaciones de la fila:
- Si el empleado **NO tiene vacaciones** ese dia: abre el dialogo "Programar Vacaciones" con el empleado y fecha pre-seleccionados
- Si el empleado **SI tiene vacaciones** ese dia: abre un mini-dialogo con las opciones:
  - Ver detalle (fechas, quien aprobo, notas)
  - Reprogramar (editar fechas)
  - Cancelar vacaciones

### Opcion B: Click derecho con menu contextual

Click derecho sobre la fila del empleado con vacaciones muestra un ContextMenu:
- "Ver vacaciones de [Nombre]" -> detalle
- "Reprogramar vacaciones" -> editar fechas
- "Cancelar vacaciones" -> confirmacion + eliminar

### Opcion C: Ambas combinadas

- Click izquierdo en icono: abre dialogo de programar/ver segun estado
- Click derecho en fila: menu contextual con acciones rapidas

## Cambios tecnicos estimados

### Backend (AttendanceService / nuevo VacationService)
- `GetVacationForEmployeeAndDate(employeeId, date)` - obtener vacacion activa
- `UpdateVacation(vacationId, startDate, endDate)` - reprogramar
- `CancelVacation(vacationId, userId)` - cancelar con auditoria
- Regla de integridad: al aprobar vacacion, eliminar registros de asistencia del rango

### Frontend (CalendarView)
- Convertir icono vacaciones de Border a Button con handler condicional
- Dialogo de detalle/reprogramar/cancelar vacaciones
- Refrescar calendario despues de cambios en vacaciones

### Base de datos
- `t_vacation_audit` ya existe y captura INSERT/UPDATE/DELETE
- Posible nueva columna `cancelled_by` y `cancelled_at` en `t_vacation`
- Trigger para limpiar `t_attendance` al aprobar vacacion en un rango

## Impacto en integridad de datos

El caso Cesar Vidales (3-Mar) evidencio que puede coexistir un registro en `t_attendance` y otro en `t_vacation` para el mismo empleado/fecha. La solucion definitiva requiere:

1. **Constraint o trigger**: al insertar/aprobar vacacion, eliminar asistencias del rango
2. **Constraint o trigger inverso**: al registrar asistencia, verificar que no hay vacacion aprobada
3. **Migracion**: limpiar datos existentes con conflictos

## Prioridad sugerida

Media - No es bloqueante para la operacion diaria (el boton superior funciona), pero mejora significativamente la experiencia del usuario que gestiona asistencia diariamente.

## Estimacion

- Opcion A: ~4-6 horas desarrollo + testing
- Opcion B: ~2-3 horas
- Opcion C: ~6-8 horas
- Integridad de datos (triggers): ~2-3 horas adicionales
