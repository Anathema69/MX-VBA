# REQ 4: Ajustes UI para Coordinacion/Proyectos

**Estado:** COMPLETADO
**Fecha:** 2026-02-06

---

## Requerimiento

1. Ocultar columna "Vendedor" para roles coordinacion/proyectos
2. Ocultar botones "Volver" y "Exportar", dejar solo "Cerrar Sesion"
3. Quitar MessageBox de confirmacion al cerrar sesion (todos los roles)

## Archivos modificados

### Views/OrdersManagementWindow.xaml
- Agregado `x:Name="VendorColumn"` a la columna VENDEDOR para poder controlarla desde code-behind
- Corregidos DataTriggers del BackButton: valores legacy (`"coordinator"`, `"admin"`) reemplazados por roles v2.0 (`"coordinacion"`, `"proyectos"`)

### Views/OrdersManagementWindow.xaml.cs

**ConfigurePermissions() - case coordinacion/proyectos:**
- `VendorColumn.Visibility = Collapsed` - Oculta columna vendedor
- `ExportButton.Visibility = Collapsed` - Oculta boton exportar

**BackButton_Click():**
- Eliminado `MessageBox.Show("¿Esta seguro que desea cerrar sesion?")` para coordinacion/proyectos
- Cierre de sesion directo sin confirmacion para todos los roles
