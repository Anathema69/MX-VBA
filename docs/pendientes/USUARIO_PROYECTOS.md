# REQ 3: Usuario Proyectos - Mismos permisos que Coordinacion

**Estado:** COMPLETADO
**Fecha:** 2026-02-06

---

## Requerimiento

Dar al rol `proyectos` los mismos permisos que tiene el rol `coordinacion`.

## Resultado del analisis

El codigo C# ya trataba ambos roles identicamente en todos los archivos con checks `coordinacion || proyectos`. No se requirieron cambios en la logica de permisos.

## Bug encontrado y corregido

Los DataTriggers XAML del boton "Volver" en `OrdersManagementWindow.xaml` usaban nombres de rol legacy (`"coordinator"`, `"admin"`) que nunca coincidian con los roles reales v2.0.

### Archivo modificado
- `Views/OrdersManagementWindow.xaml` - Lineas 77-93 (DataTriggers del BackButton)

### Antes (bug)
```xaml
<DataTrigger Value="coordinator">  <!-- NUNCA coincide con "coordinacion" -->
<DataTrigger Value="admin">        <!-- NUNCA coincide con "direccion" -->
```

### Despues (corregido)
```xaml
<DataTrigger Value="coordinacion"> <!-- Cerrar Sesion -->
<DataTrigger Value="proyectos">    <!-- Cerrar Sesion -->
<!-- Default: "Volver" para direccion/administracion -->
```

## Verificacion de paridad completa

| Archivo | Check |
|---------|-------|
| `LoginWindow.xaml.cs:188` | `coordinacion \|\| proyectos` → Ordenes directo |
| `MainMenuWindow.xaml.cs:92` | `case coordinacion: case proyectos:` → Solo ordenes |
| `OrdersManagementWindow.xaml.cs` | Todos los checks con `\|\|` |
| `EditOrderWindow.xaml.cs:265` | Ambos → modo solo lectura |
| `ConfigurePermissions()` | Ambos → sin crear ordenes, sin columnas financieras |
| `BackButton_Click` | Ambos → cerrar sesion (sin menu principal) |
| `ConfigureStatusFilterComboBox()` | Ambos → solo estados 0, 1, 2 |
