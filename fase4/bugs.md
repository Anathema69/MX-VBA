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
