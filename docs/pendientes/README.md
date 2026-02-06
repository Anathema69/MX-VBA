# Propuestas Pendientes de Confirmacion

Esta carpeta contiene documentacion detallada de implementaciones pendientes de aprobacion.
Cada archivo describe el contexto completo, archivos involucrados, y pasos exactos de implementacion
para que Claude Code (u otro agente de IA) pueda retomar el trabajo sin perder contexto.

## Propuestas Activas

| # | Archivo | Estado | Descripcion |
|---|---------|--------|-------------|
| 1 | (implementado en codigo) | COMPLETADO | Filtro por anio y mes en Ordenes (f_podate) |
| 2 | `GASTO_OPERATIVO_COMISION.md` | COMPLETADO | Incluir comision del vendedor en gasto operativo (Opcion C) |
| 3 | `USUARIO_PROYECTOS.md` | COMPLETADO | Rol proyectos con mismos permisos que coordinacion |
| 4 | `UI_COORDINACION_PROYECTOS.md` | COMPLETADO | Ocultar vendedor, exportar y quitar msgbox cierre sesion |
| 5 | `VENDOR_DASHBOARD_DESCRIPCION.md` | COMPLETADO | Descripcion de orden en VendorDashboard + login sin password |

## Flujo de trabajo

1. Se crea el documento con la propuesta y analisis de impacto
2. Se espera confirmacion del usuario/cliente
3. Al confirmar, el agente IA lee este documento y ejecuta la implementacion
4. Una vez implementado, se mueve a `implementadas/` o se elimina
