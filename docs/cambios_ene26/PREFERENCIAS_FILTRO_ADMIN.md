# Persistencia de Filtro para Rol Administracion

**Version:** 1.0
**Fecha:** 25 de Enero 2026
**Modulo:** Gestion de Ordenes

---

## Descripcion

El sistema guarda la preferencia del filtro de estado de ordenes para usuarios con rol "administracion". Al cerrar sesion y volver a ingresar, el filtro seleccionado se mantiene.

## Comportamiento

| Rol | Filtro Guardado | Filtro por Defecto |
|-----|-----------------|-------------------|
| administracion | Si (archivo local) | CREADA |
| direccion | No | CREADA |
| coordinacion | No | EN PROCESO |
| proyectos | No | EN PROCESO |

## Ubicacion del Archivo

```
%APPDATA%\SistemaGestionProyectos2\preferences_admin.json
```

Ejemplo de contenido:
```json
{
  "OrdersStatusFilter": "LIBERADA",
  "LastUpdated": "2026-01-25T10:30:00"
}
```

## Flujo de Funcionamiento

```
┌─────────────────────────────────────────────────────────────┐
│                    INICIO DE SESION                         │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌─────────────────┐                                        │
│  │ Usuario inicia  │                                        │
│  │     sesion      │                                        │
│  └────────┬────────┘                                        │
│           │                                                 │
│           ▼                                                 │
│  ┌─────────────────┐    No     ┌─────────────────┐         │
│  │ Rol es          │──────────>│ Usar filtro     │         │
│  │ administracion? │           │ por defecto     │         │
│  └────────┬────────┘           └─────────────────┘         │
│           │ Si                                              │
│           ▼                                                 │
│  ┌─────────────────┐    No     ┌─────────────────┐         │
│  │ Existe archivo  │──────────>│ Usar CREADA     │         │
│  │ preferences?    │           │ por defecto     │         │
│  └────────┬────────┘           └─────────────────┘         │
│           │ Si                                              │
│           ▼                                                 │
│  ┌─────────────────┐                                        │
│  │ Cargar filtro   │                                        │
│  │ guardado        │                                        │
│  └─────────────────┘                                        │
│                                                             │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│                  CAMBIO DE FILTRO                           │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌─────────────────┐                                        │
│  │ Usuario cambia  │                                        │
│  │ filtro en combo │                                        │
│  └────────┬────────┘                                        │
│           │                                                 │
│           ▼                                                 │
│  ┌─────────────────┐    No     ┌─────────────────┐         │
│  │ Rol es          │──────────>│ Solo aplicar    │         │
│  │ administracion? │           │ filtro en UI    │         │
│  └────────┬────────┘           └─────────────────┘         │
│           │ Si                                              │
│           ▼                                                 │
│  ┌─────────────────┐                                        │
│  │ Guardar en      │                                        │
│  │ preferences.json│                                        │
│  └─────────────────┘                                        │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

## Archivos Involucrados

| Archivo | Descripcion |
|---------|-------------|
| `Services/UserPreferencesService.cs` | Servicio para leer/escribir preferencias |
| `Views/OrdersManagementWindow.xaml.cs` | Carga y guarda filtro segun rol |

## API del Servicio

```csharp
// Guardar filtro (solo funciona para rol "administracion")
UserPreferencesService.SaveOrdersStatusFilter(role, filterValue);

// Obtener filtro guardado (retorna null si no es admin o no existe)
string filter = UserPreferencesService.GetOrdersStatusFilter(role);

// Limpiar preferencias (para testing)
UserPreferencesService.ClearPreferences();
```

## Notas Importantes

1. **Solo rol administracion**: Otros roles ignoran el archivo de preferencias
2. **Por equipo**: Las preferencias se guardan localmente, no se sincronizan entre equipos
3. **Limpieza**: Si se elimina el archivo, vuelve al filtro por defecto (CREADA)

---

**Desarrollado por:** Equipo IMA Mecatronica
