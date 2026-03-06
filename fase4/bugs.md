# Fase 4 - Registro de Bugs

Bugs encontrados durante el desarrollo de Fase 4, tanto los contemplados por el cliente como los descubiertos durante implementacion.

---

## Formato

```
### BUG-XXX: Titulo descriptivo
**Bloque:** X | General
**Severidad:** critica | alta | media | baja
**Estado:** abierto | en progreso | resuelto | no reproducible
**Reportado por:** cliente | desarrollo
**Descripcion:** que ocurre
**Pasos para reproducir:** como reproducir
**Solucion:** descripcion de la solucion aplicada (cuando se resuelva)
**Commit:** hash del commit que lo resuelve
```

---

## Bugs Reportados por Cliente

### BUG-001: Cerrar sesion interrumpe screen share de Meet
**Bloque:** 1 (Cosmeticos)
**Severidad:** media
**Estado:** abierto
**Reportado por:** cliente
**Descripcion:** Al cerrar sesion en la plataforma, se interrumpe la pantalla compartida en Google Meet. Posiblemente la ventana pierde foco o se cierra/reabre de forma que el sistema operativo la desregistra del screen share.
**Hipotesis:** El flujo de logout probablemente cierra la ventana actual y abre LoginWindow como nueva ventana. Esto causa que Meet pierda el handle de la ventana compartida. Solucion potencial: navegar dentro de la misma ventana en lugar de cerrar y abrir otra, o mantener la ventana principal y solo cambiar el contenido.
**Pasos para reproducir:**
1. Iniciar sesion en la plataforma
2. Compartir pantalla de la app en Google Meet
3. Cerrar sesion
4. Observar que Meet deja de compartir la pantalla

### BUG-005: Portal Proveedores - No se puede eliminar gasto pagado
**Bloque:** General (no contemplado en Fase 4)
**Severidad:** alta
**Estado:** resuelto
**Reportado por:** cliente
**Descripcion:** Al registrar una factura/gasto para un proveedor, no se podia eliminar. El boton de eliminar (X rojo) estaba oculto para gastos con estado PAGADO. Proveedores con 0 dias de credito (ej: VCM) se veian especialmente afectados porque el trigger `auto_pay_zero_credit_expense` marca automaticamente el gasto como PAGADO al momento de crearlo.
**Problemas adicionales encontrados:**
- Status visual incoherente: gastos pagados mostraban "VENCIDO" (rojo) en vez de "PAGADO", porque el calculo comparaba fecha_vencimiento vs hoy sin considerar si ya estaba pagado.
- Header "TOTAL PENDIENTE" no cambiaba segun el tab activo (Pendiente/Pagado/Todos).
- Texto de fila seleccionada se volvia blanco e ilegible.
- Sin empty state cuando un filtro no tenia resultados (pantalla en blanco).
- Auditoria de DELETE no registraba quien eliminaba.
**Causa raiz:** Visibilidad del boton eliminar atada a `IsPayable = !IsPaid && IsReadOnly`, ocultandolo para gastos pagados.
**Solucion:**
1. Boton eliminar visible para todos los gastos (atado a `IsReadOnly` en vez de `IsPayable`), sin dialogo de confirmacion, con toast de notificacion.
2. Status visual corregido: gastos pagados muestran "PAGADO" (azul) con texto "hace X dias".
3. Headers dinamicos segun tab: "TOTAL PAGADO"/"PAGADOS TARDE"/"PAGADOS A TIEMPO" en tab Pagado.
4. Foreground forzado a DarkText en celdas seleccionadas.
5. Empty state con icono y mensaje contextual por filtro.
6. Auditoria: UPDATE de `updated_by` previo al DELETE para capturar quien elimina en el audit trail.
**Verificado con:** Comparacion BD antes/despues (consultas en `fase4/sql/`). Balance, audit trail y sumas cuadran correctamente.
**Pasos para reproducir:**
1. Abrir Portal de Proveedores
2. Seleccionar proveedor con 0 dias credito (ej: VCM)
3. Registrar un gasto
4. Intentar eliminarlo - boton X no aparecia

### BUG-006: Calendario - No se puede modificar asistencia registrada
**Bloque:** General (no contemplado en Fase 4)
**Severidad:** alta
**Estado:** resuelto
**Reportado por:** cliente
**Descripcion:** Una vez registrada la asistencia de un trabajador, al intentar cambiarla (ej: de Asistencia a Falta o Retardo) no se aplicaba el cambio. El usuario veia error o simplemente no pasaba nada.
**Causa raiz:** Dos problemas:
1. `SaveAttendance` usaba `.Update()` con `.Set()` encadenado de Supabase Postgrest. El `.Update()` ejecutaba el SQL pero no retornaba modelos, y el check `response.Models.Count > 0` fallaba, disparando `throw "No se pudo guardar"`. Confirmado: query 3 del diagnostico mostro CERO registros con `updated_at != created_at` en toda la historia.
2. Al cambiar a FALTA (sin hora de entrada), `.Set(x => x.CheckInTime, null)` crasheaba con `ArgumentException: Expected Value to be of Type: String` porque Postgrest no puede serializar `null TimeSpan?`.
**Problemas adicionales encontrados:**
- Boton VACACIONES en fila marcaba status directamente, causando conflicto con el modulo de vacaciones (caso Cesar Vidales 3-Mar: tenia asistencia + vacacion aprobada simultaneamente).
- No habia forma de desmarcar un registro (tocar el mismo boton no hacia nada).
- Sin boton de actualizar/refrescar en el calendario.
**Solucion:**
1. Reemplazado `.Set()` encadenado por read-modify-write con modelo completo (`existing.Update<AttendanceTable>()`). Maneja null TimeSpan correctamente via serializacion JSON.
2. Desmarcar: tocar el mismo boton elimina el registro de la BD (nuevo `DeleteAttendance()` con auditoria).
3. Boton VACACIONES en fila convertido a icono indicador visual (sin accion). Vacaciones se gestionan exclusivamente desde el boton superior.
4. Agregado boton "Actualizar" al header del calendario (limpia cache, recarga desde BD).
**Verificado con:** Audit trail del 9-Mar confirma 18 operaciones (8 INSERT + 10 UPDATE) todas con `changed_by_user = "caaj"`. Updates entre status ASISTENCIA/FALTA/RETARDO/VACACIONES funcionan correctamente.

---

## Bugs Descubiertos en Desarrollo

### BUG-002: Barra de tareas cubierta en PendingIncomesDetailView
**Bloque:** 1 (Cosmeticos)
**Severidad:** baja
**Estado:** resuelto
**Reportado por:** desarrollo
**Descripcion:** PendingIncomesDetailView usaba WindowState="Maximized" sin MaxHeight, cubriendo la barra de tareas de Windows.
**Solucion:** Agregar `MaxHeight="{x:Static SystemParameters.MaximizedPrimaryScreenHeight}"` al XAML.

### BUG-003: Botones de volver con nombres inconsistentes
**Bloque:** 1 (Cosmeticos)
**Severidad:** baja
**Estado:** resuelto
**Reportado por:** desarrollo
**Descripcion:** 5 variantes de texto para botones de regreso: "Cerrar", "Regresar", "Volver al menú", "← Volver al menú", "⬅ Volver".
**Solucion:** Estandarizado a "← Volver" en todas las ventanas de navegacion. "Cancelar" en dialogs, "Cerrar Sesión" en logout.

### BUG-004: Ventanas desbordan en pantallas pequenas / multi-monitor
**Bloque:** 1 (Cosmeticos)
**Severidad:** media
**Estado:** resuelto
**Reportado por:** desarrollo
**Descripcion:** Al abrir la app en una laptop con pantalla pequena, las vistas se ven vacias o colapsadas. Especificamente al tener 2 monitores (ej. 27" + laptop), abrir una ventana hija desde un monitor secundario la dimensiona para el monitor primario.
**Causa raiz:** `SystemParameters.WorkArea` SIEMPRE devuelve el area del monitor primario. Cuando una ventana se abre en un monitor secundario, `MaximizeWithTaskbar()` la dimensionaba con las medidas del monitor equivocado.
**Solucion:** Se creo `Helpers/WindowHelper.cs` que usa Win32 API (`MonitorFromWindow` + `GetMonitorInfo`) para detectar el monitor REAL donde se encuentra la ventana y obtener su area de trabajo especifica. Se actualizo `MaximizeWithTaskbar()` en las 13 ventanas para delegar al helper, y se agrego hook `SourceInitialized` para re-aplicar el dimensionamiento cuando el handle de ventana esta disponible (garantizando deteccion correcta del monitor).

---
