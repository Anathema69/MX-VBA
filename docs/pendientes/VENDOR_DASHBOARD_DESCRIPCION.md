# REQ 5: Agregar Descripcion de Orden al VendorDashboard + Simular Login Vendedor

**Estado:** COMPLETADO
**Fecha:** 2026-02-06

---

## Requerimiento

1. Mostrar la descripcion/detalle de la orden en las tarjetas del panel del vendedor (VendorDashboard)
2. Permitir simular login de vendedor sin conocer la contrasena (DevMode SkipPassword)
3. Cambiar boton "Volver" por "Cerrar Sesion" con estilo visible
4. Quitar MessageBox de confirmacion al cerrar sesion (consistente con Req 4)

## Archivos modificados

### Views/VendorDashboard.xaml.cs

**VendorCommissionCardViewModel:**
- Agregada propiedad `OrderDescription` al ViewModel

**LoadVendorCommissions():**
- Se pobla `OrderDescription = order?.Description ?? ""` al construir cada card

**LogoutButton_Click():**
- Eliminado MessageBox de confirmacion, cierre directo

### Views/VendorDashboard.xaml

**Tarjeta de comision - Bloque Cliente + Descripcion:**
- Cliente y Detalle agrupados en un bloque limpio sin barra lateral
- Detalle se oculta automaticamente si la orden no tiene descripcion (DataTrigger para "" y null)
- Patron visual: "Cliente: [nombre]" + "Detalle: [descripcion]" con labels de ancho fijo (60px)

**Boton Cerrar Sesion:**
- Reemplazado "Volver" por "Cerrar Sesion"
- Estilo: fondo semitransparente blanco (#44FFFFFF), esquinas redondeadas, hover con opacidad
- Template personalizado para integrarse con el header gradiente

### Views/LoginWindow.xaml.cs

**DevMode SkipPassword:**
- Agregado soporte para `DevMode:SkipPassword` en appsettings.json
- Cuando activo: busca usuario por username en BD sin validar contrasena (BCrypt)
- Auto-login funciona sin contrasena cuando SkipPassword esta activo
- Cuando desactivado: flujo normal de autenticacion con BCrypt intacto

### appsettings.json

- Agregado campo `"SkipPassword"` a seccion DevMode
- Estado actual: DevMode deshabilitado (Enabled: false, AutoLogin: false, SkipPassword: false)

## Diseno UX/UI

La descripcion se integra como texto plano debajo del cliente, sin barra lateral adicional.
Esto evita conflicto visual con la barra de status (roja/amarilla/verde) que ya existe
en el lateral izquierdo de cada tarjeta.

### Estructura final del card:
```
[Barra Status] | Orden: XXXX [BADGE]        | Comision  |
               | Cliente:  Nombre Cliente    | $X,XXX.XX |
               | Detalle:  Descripcion...    |           |
               | Fecha: dd/MM/yyyy           |           |
```
