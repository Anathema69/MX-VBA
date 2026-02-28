# Bloque 5: Carpetas en la Nube por Orden

**Complejidad:** Alta
**Estado:** PENDIENTE
**Dependencias:** Decision arquitectonica sobre storage provider
**Bloques que dependen de este:** Bloque 3 (subida de archivos desde Portal Ventas)

---

## Requerimientos del Cliente

1. Cada orden debe tener una carpeta asociada en la nube (Supabase Storage o Cloudflare R2)
2. Quien configura el path: Direccion, Administracion, Operacion, Vendedor
3. Quien visualiza: todos segun su rol
4. Boton de doble accion:
   - Si tiene path -> abre la carpeta directamente
   - Si no tiene path -> permite elegir/crear la carpeta
   - Clic derecho -> editar el path existente
5. Sincronizar las carpetas actuales del cliente con el sistema

---

## Decision Arquitectonica: Storage Provider

### Opcion A: Supabase Storage

| Aspecto | Detalle |
|---------|---------|
| **Integracion** | Nativa, ya tenemos Supabase client |
| **SDK** | `supabase-csharp` ya incluye Storage API |
| **Costo** | 1GB gratis, luego $0.021/GB/mes |
| **Limite archivos** | 50MB por archivo (plan free), 5GB (pro) |
| **RLS** | Soporta Row Level Security en buckets |
| **CDN** | Incluido |
| **Pros** | Sin config adicional, mismo ecosistema |
| **Contras** | Limite de tamaño en plan free, vendor lock-in |

### Opcion B: Cloudflare R2

| Aspecto | Detalle |
|---------|---------|
| **Integracion** | Via S3-compatible API |
| **SDK** | AWSSDK.S3 (compatible) |
| **Costo** | 10GB gratis, luego $0.015/GB/mes, sin egress fees |
| **Limite archivos** | 5GB por archivo |
| **RLS** | No nativo, manejar en app |
| **CDN** | Integrado con Cloudflare |
| **Pros** | Mas barato a escala, sin costos de egress |
| **Contras** | Requiere cuenta Cloudflare, config adicional |

### Opcion C: Hibrida - Path externo (OneDrive/Google Drive/NAS del cliente)

| Aspecto | Detalle |
|---------|---------|
| **Integracion** | Solo se guarda el path/URL en BD |
| **Costo** | Cero (usa infra existente del cliente) |
| **Archivos** | Se abren con el explorador del sistema |
| **Pros** | El cliente ya tiene sus carpetas organizadas |
| **Contras** | No hay control de acceso desde la app, depende de conectividad |

**Recomendacion:** Opcion C como fase inicial (guardar path de carpetas existentes y abrirlas), con Opcion A (Supabase Storage) para subida de archivos nuevos. Esto satisface la necesidad inmediata del cliente de vincular sus carpetas actuales Y permite subir archivos nuevos.

---

## Diseno Tecnico

### Modelo de datos

```sql
-- Agregar campo a t_order para path de carpeta externa
ALTER TABLE t_order ADD COLUMN folder_path TEXT;
ALTER TABLE t_order ADD COLUMN folder_path_set_by INTEGER REFERENCES users(id);
ALTER TABLE t_order ADD COLUMN folder_path_set_at TIMESTAMP;

-- Tabla para archivos subidos al storage (archivos nuevos)
CREATE TABLE order_files (
    id SERIAL PRIMARY KEY,
    f_order INTEGER NOT NULL REFERENCES t_order(f_order) ON DELETE CASCADE,
    file_name VARCHAR(255) NOT NULL,
    file_path TEXT NOT NULL,           -- Path en storage (Supabase bucket)
    file_size BIGINT,                  -- Tamaño en bytes
    file_type VARCHAR(50),             -- MIME type
    uploaded_by INTEGER REFERENCES users(id),
    uploaded_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    is_deleted BOOLEAN DEFAULT FALSE   -- Soft delete
);

CREATE INDEX idx_order_files_order ON order_files(f_order);
CREATE INDEX idx_order_files_active ON order_files(f_order) WHERE is_deleted = FALSE;
```

### Bucket en Supabase Storage

```
Bucket: "order-files"
Estructura:
  order-files/
    {order_id}/
      {timestamp}_{filename}
```

### Servicio de Storage

```csharp
// Services/StorageService.cs
public class StorageService : BaseSupabaseService
{
    // --- Carpeta externa (path) ---
    Task<bool> SetFolderPath(int orderId, string path, int userId, CancellationToken ct);
    Task<string?> GetFolderPath(int orderId, CancellationToken ct);
    Task<bool> OpenFolder(int orderId);  // Process.Start() con el path

    // --- Archivos en storage ---
    Task<string> UploadFileAsync(int orderId, string localFilePath, int userId, CancellationToken ct);
    Task<List<OrderFileDb>> GetFilesAsync(int orderId, CancellationToken ct);
    Task<bool> DeleteFileAsync(int fileId, CancellationToken ct);
    Task<string> GetDownloadUrlAsync(int fileId, CancellationToken ct);
    Task<bool> DownloadFileAsync(int fileId, string localPath, CancellationToken ct);
}
```

### UI en OrdersManagementWindow

**Nueva columna "CARPETA":**
- Icono de carpeta (vacia o con contenido)
- Color: gris si no tiene path, azul si tiene
- Clic izquierdo: abre carpeta si tiene path, abre selector si no tiene
- Clic derecho: menu contextual con "Editar ruta" y "Ver archivos"

**Dialog para configurar carpeta:**
- TextBox para pegar ruta (UNC path, URL, o ruta local)
- Boton "Explorar" para abrir FolderBrowserDialog
- Boton "Guardar" / "Cancelar"
- Preview de la ruta configurada

**Dialog para ver archivos subidos:**
- Lista de archivos con: nombre, tamaño, fecha, subido por
- Botones: Descargar, Eliminar (segun permisos)
- Boton "Subir archivo" (abre OpenFileDialog)
- Drag & drop zone (nice-to-have)

### Permisos

| Accion | Direccion | Admin | Operacion | Coordinacion | Vendedor |
|--------|-----------|-------|-----------|--------------|----------|
| Ver carpeta | Si | Si | Si | Si | Solo sus ordenes |
| Configurar path | Si | Si | Si | No | Solo sus ordenes |
| Subir archivos | Si | Si | Si | Si | Solo sus ordenes |
| Eliminar archivos | Si | Si | No | No | No |

---

## Sincronizacion de Carpetas del Cliente

El cliente tiene carpetas organizadas actualmente (probablemente en un NAS o servicio de nube). Se requiere:

1. **Fase inicial:** El cliente proporciona un mapeo de `orden -> ruta de carpeta`
2. **Migracion:** Script o proceso manual para asignar `folder_path` a cada orden existente
3. **Formato esperado:** CSV o Excel con columnas `numero_orden, ruta_carpeta`

```sql
-- Script de migracion ejemplo
UPDATE t_order SET folder_path = 'C:\Clientes\IMA\OC-2025-001' WHERE f_po = 'OC-2025-001';
-- O batch desde CSV importado
```

---

## Archivos a Crear

| Archivo | Tipo |
|---------|------|
| `Services/StorageService.cs` | Servicio de storage |
| `Models/Database/OrderFileDb.cs` | Modelo para archivos |
| `Views/FolderConfigDialog.xaml` | Dialog configurar carpeta |
| `Views/OrderFilesDialog.xaml` | Dialog ver/subir archivos |
| `sql/bloque5_carpetas.sql` | Script migracion BD |

## Archivos a Modificar

| Archivo | Cambio |
|---------|--------|
| `Models/DTOs/OrderViewModel.cs` | Agregar FolderPath, HasFolder, FileCount |
| `Views/OrdersManagementWindow.xaml` | Nueva columna CARPETA + context menu |
| `Views/OrdersManagementWindow.xaml.cs` | Handlers para abrir/configurar carpeta |
| `Views/VendorDashboard.xaml` | Boton clip (integra con Bloque 3) |
| `Services/SupabaseService.cs` | Registrar StorageService en facade |
| `Services/OrderService.cs` | Incluir folder_path en GetOrders |

---

## Checklist de Implementacion

### Fase A: Carpeta externa (path)
- [ ] Agregar columna folder_path a t_order en BD
- [ ] Crear modelo OrderFileDb
- [ ] Crear StorageService con metodos de path
- [ ] Agregar columna CARPETA en OrdersManagementWindow
- [ ] Crear FolderConfigDialog (UI para configurar path)
- [ ] Implementar doble accion en boton (abrir / configurar)
- [ ] Implementar clic derecho -> editar path
- [ ] Permisos por rol
- [ ] Script/proceso de sincronizacion de carpetas existentes

### Fase B: Archivos en storage (Supabase Storage)
- [ ] Crear bucket "order-files" en Supabase
- [ ] Implementar UploadFileAsync en StorageService
- [ ] Implementar GetFilesAsync, DeleteFileAsync, DownloadFileAsync
- [ ] Crear OrderFilesDialog (ver/subir archivos)
- [ ] Integrar con Portal Ventas (Bloque 3)
- [ ] Configurar RLS policies en bucket
- [ ] QA: Probar subida/descarga/eliminacion de archivos

---

## Criterios de Aceptacion

1. Cada orden puede tener una carpeta vinculada (path externo)
2. Al hacer clic en el boton de carpeta, se abre la carpeta si existe
3. Si no tiene carpeta, se muestra dialog para configurarla
4. Clic derecho permite editar la ruta
5. Se pueden subir archivos desde la plataforma al storage
6. Los archivos subidos son visibles y descargables por roles autorizados
7. Las carpetas existentes del cliente se migraron correctamente
8. Los permisos funcionan segun la matriz definida
