# Bloque 3: Portal Ventas - Gestion de Archivos y Liberacion

**Complejidad:** Media
**Estado:** PENDIENTE
**Dependencias:** Bloque 5 (comparte infraestructura de storage)
**Archivos principales:** `Views/VendorDashboard.xaml`, `Views/VendorDashboard.xaml.cs`

---

## Requerimientos del Cliente

1. Boton de clip para subir archivos (.txt, imagen, documento) por orden a carpeta en la nube
2. Boton "Liberar" para cambiar estado de orden a "Liberada"
3. Quitar tarjeta "Total Pagado" del dashboard
4. Quitar msgbox al cerrar sesion como vendedor

---

## Analisis Tecnico por Item

### 3.1 Boton clip para subir archivos

**Estado actual:** VendorDashboard muestra lista de comisiones con info de orden. No tiene funcionalidad de archivos.

**Implementacion:**
- Agregar boton con icono de clip (Segoe MDL2: `&#xE723;`) en cada item de comision
- Al hacer clic: abrir `OpenFileDialog` con filtros para .txt, .png, .jpg, .pdf, .doc, .docx
- Subir archivo al storage en la nube vinculado a la orden
- Mostrar indicador visual de archivos existentes (badge con conteo)

**Nota:** La infraestructura de storage se define en Bloque 5. Aqui solo se implementa la UI y la llamada al servicio. Se puede desarrollar en paralelo con un servicio interface/stub que se concrete en Bloque 5.

**Archivos a crear/modificar:**
- `Views/VendorDashboard.xaml` - Agregar boton clip en template de cada comision
- `Views/VendorDashboard.xaml.cs` - Handler para upload
- `Services/StorageService.cs` - Nuevo servicio (se crea en Bloque 5, aqui se consume)

### 3.2 Boton "Liberar" orden

**Estado actual:** Las ordenes cambian de estado desde EditOrderWindow. El vendedor no puede cambiar estado desde su portal.

**Implementacion:**
- Agregar boton "Liberar" (verde, icono check) en cada item de comision cuyo estado NO sea ya "Liberada", "Completada" o "Cancelada"
- Solo visible si la orden esta en "Creada" o "En Proceso"
- Al hacer clic: confirmacion simple (no MessageBox bloqueante, usar dialog in-page o Snackbar)
- Llamar a `OrderService.UpdateOrder()` con `f_orderstat = 2` (LIBERADA)
- Refrescar la lista tras el cambio
- Registrar en `order_history` quien libero (ya lo hace el trigger existente)

**Logica de permisos:**
- Solo el vendedor asignado a la orden puede liberarla
- Validar en backend con `f_salesman` == usuario logueado

**Archivos a modificar:**
- `Views/VendorDashboard.xaml` - Boton Liberar en template
- `Views/VendorDashboard.xaml.cs` - Handler con confirmacion y llamada a servicio
- `Services/OrderService.cs` - Metodo `ReleaseOrder(int orderId, int userId)` (wrapper de UpdateOrder)

### 3.3 Quitar tarjeta "Total Pagado"

**Estado actual:** VendorDashboard tiene 4 summary cards:
1. POR COMPLETAR (rojo)
2. EN PROCESO DE PAGO (naranja)
3. TOTAL PAGADO (verde)
4. TOTAL COMISIONES (contador)

**Implementacion:**
- Eliminar el tercer card ("Total Pagado") del XAML
- Redistribuir las 3 cards restantes (UniformGrid Columns="3" o ajustar layout)
- Eliminar la propiedad/binding `TotalPaid` del code-behind si ya no se usa

**Archivo:** `Views/VendorDashboard.xaml`

### 3.4 Quitar msgbox al cerrar sesion

**Estado actual:** En `VendorDashboard.xaml.cs`, el `LogoutButton_Click` muestra un `MessageBox.Show()` de confirmacion antes de cerrar sesion.

**Implementacion:**
- Eliminar el `MessageBox.Show()` del handler de logout
- Cerrar sesion directamente al hacer clic en el boton
- Mantener la logica de limpieza de sesion y navegacion a LoginWindow

**Archivo:** `Views/VendorDashboard.xaml.cs`

---

## Cambios en Base de Datos

Ninguno directo. Los cambios de storage se manejan en Bloque 5.
El estado "Liberada" (id=2) ya existe en la tabla `order_status`.

---

## Checklist de Implementacion

- [ ] 3.1 Disenar UI del boton clip en VendorDashboard
- [ ] 3.1 Implementar OpenFileDialog con filtros
- [ ] 3.1 Integrar con StorageService (Bloque 5)
- [ ] 3.2 Agregar boton "Liberar" con logica de visibilidad
- [ ] 3.2 Implementar ReleaseOrder en OrderService
- [ ] 3.2 Confirmacion no-bloqueante para liberar
- [ ] 3.3 Eliminar card "Total Pagado"
- [ ] 3.3 Redistribuir layout de cards restantes
- [ ] 3.4 Eliminar MessageBox de logout
- [ ] QA: Probar flujo completo de vendedor

---

## Criterios de Aceptacion

1. El vendedor puede subir archivos (.txt, .png, .jpg, .pdf, .doc) a cada orden desde su portal
2. El vendedor puede cambiar el estado de su orden a "Liberada" con un solo clic + confirmacion
3. El dashboard muestra solo 3 cards (sin "Total Pagado")
4. Al cerrar sesion no aparece ningun mensaje emergente
5. Los archivos subidos son accesibles desde Manejo de Ordenes (Bloque 5)

---

## Notas de Implementacion

**Estrategia de desarrollo paralelo con Bloque 5:**
Se puede definir una interfaz `IStorageService` con metodos `UploadFile()`, `GetFiles()`, `DeleteFile()` y usar una implementacion stub mientras Bloque 5 no este listo. Esto permite avanzar en la UI sin depender de la decision Supabase Storage vs Cloudflare R2.

```csharp
// Interface propuesta
public interface IStorageService
{
    Task<string> UploadFileAsync(int orderId, string filePath, CancellationToken ct);
    Task<List<StorageFile>> GetFilesAsync(int orderId, CancellationToken ct);
    Task<bool> DeleteFileAsync(string fileId, CancellationToken ct);
    Task<string> GetDownloadUrlAsync(string fileId, CancellationToken ct);
}
```
