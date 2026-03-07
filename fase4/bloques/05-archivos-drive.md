# Bloque 5: Modulo ARCHIVOS (Drive IMA)

**Version:** Propuesta v1.0
**Fecha:** 2026-03-06
**Complejidad:** Alta
**Estado:** DISENO

---

## Resumen Ejecutivo

Modulo de gestion de archivos tipo Google Drive integrado en la plataforma. Permite a todos los usuarios navegar carpetas, subir/descargar archivos y vincular carpetas a ordenes de compra. Reemplaza el uso externo de carpetas locales/NAS con una solucion centralizada en la nube.

**Nombre del modulo en UI:** ARCHIVOS
**Nombre del bucket:** `ima-drive` (separado de `order-files` que es exclusivo del Portal Ventas)

---

## Contexto del Cliente

- Oficina pequena (~10 personas), confianza entre el equipo
- Carpeta actual "IMA MECATRONICA" con subcarpetas por orden (nombrado libre, sin estandar)
- Cada orden tiene una carpeta donde suben cualquier tipo de archivo y crean subcarpetas
- Se requiere acceso CRUD para todos los usuarios inicialmente (sin restricciones por rol)
- Vision futura: al crecer a mas oficinas, se implementaran permisos granulares con RLS

---

## Arquitectura: DB + Storage (Hibrida)

### Por que NO solo Supabase Storage?

Supabase Storage maneja archivos como objetos con paths planos (`bucket/path/to/file.pdf`). No soporta:
- Carpetas vacias (solo existen si contienen archivos)
- Metadatos personalizados (quien creo la carpeta, cuando)
- Vinculacion a ordenes de compra
- Renombrar carpetas sin mover todos los archivos
- Listado rapido de contenido (requiere listar TODOS los objetos y filtrar por prefijo)

### Solucion: Estructura en BD + Blobs en Storage

```
[BD PostgreSQL]                    [Supabase Storage]
drive_folders (arbol)    --->      ima-drive/
drive_files (metadatos)  --->        {folder_id}/{timestamp}_{file}
```

- **BD**: Estructura de carpetas (arbol), metadatos de archivos, vinculacion a ordenes
- **Storage**: Solo los archivos binarios (blobs), organizados por folder_id
- **Ventajas**: Navegacion instantanea, busqueda, renombrar carpetas sin mover blobs, vincular ordenes

---

## Esquema de Base de Datos

```sql
-- ============================================
-- BLOQUE 5: Modulo ARCHIVOS (Drive IMA)
-- ============================================

-- Carpetas (estructura de arbol auto-referencial)
CREATE TABLE drive_folders (
    id SERIAL PRIMARY KEY,
    parent_id INTEGER REFERENCES drive_folders(id) ON DELETE CASCADE,
    name VARCHAR(255) NOT NULL,
    linked_order_id INTEGER REFERENCES t_order(f_order) ON DELETE SET NULL,
    created_by INTEGER REFERENCES users(id),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(parent_id, name)  -- Sin duplicados en la misma carpeta
);

-- Archivos (metadatos en BD, blob en Storage)
CREATE TABLE drive_files (
    id SERIAL PRIMARY KEY,
    folder_id INTEGER NOT NULL REFERENCES drive_folders(id) ON DELETE CASCADE,
    file_name VARCHAR(255) NOT NULL,
    storage_path TEXT NOT NULL,       -- Path en bucket ima-drive
    file_size BIGINT,
    content_type VARCHAR(100),
    uploaded_by INTEGER REFERENCES users(id),
    uploaded_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(folder_id, file_name)     -- Sin duplicados en la misma carpeta
);

-- Indexes
CREATE INDEX idx_drive_folders_parent ON drive_folders(parent_id);
CREATE INDEX idx_drive_folders_order ON drive_folders(linked_order_id) WHERE linked_order_id IS NOT NULL;
CREATE INDEX idx_drive_files_folder ON drive_files(folder_id);

-- Carpeta raiz inicial
INSERT INTO drive_folders (id, parent_id, name, created_by)
VALUES (1, NULL, 'IMA MECATRONICA', NULL);
```

### Notas del esquema:
- `parent_id = NULL` indica carpeta raiz (solo una: "IMA MECATRONICA")
- `linked_order_id` permite vincular cualquier carpeta a una orden (relacion 1:1 opcional)
- `ON DELETE CASCADE` en parent_id: borrar carpeta borra todo su contenido (subcarpetas + archivos)
- `UNIQUE(parent_id, name)` previene carpetas/archivos con el mismo nombre en el mismo nivel
- No hay soft-delete: se borra de verdad (equipo pequeno, confianza)

---

## Diseno de UI

### Pantalla principal: DriveWindow

```
+------------------------------------------------------------------------+
| [<- Volver]    ARCHIVOS                    [+ Carpeta]  [Subir archivo] |
+------------------------------------------------------------------------+
|                                                                         |
|  IMA MECATRONICA  >  Ordenes 2025  >  OC-2025-001                     |
|                                                                         |
+------------------------------------------------------------------------+
|  Buscar: [______________________________]                               |
+------------------------------------------------------------------------+
|                                                                         |
|  +---------------+  +---------------+  +---------------+                |
|  |   [icono]     |  |   [icono]     |  |   [icono]     |               |
|  |   Planos      |  |  Cotizaciones |  |   Fotos       |               |
|  |   5 elementos |  |  2 elementos  |  |  12 elementos |               |
|  +---------------+  +---------------+  +---------------+                |
|                                                                         |
|  +---------------+  +---------------+  +---------------+                |
|  |   [icono]     |  |   [icono]     |  |   [icono]     |               |
|  |  Contrato.pdf |  |  Render.jpg   |  | Presupuesto   |               |
|  |    245 KB     |  |    1.2 MB     |  |  .xlsx  89 KB |               |
|  +---------------+  +---------------+  +---------------+                |
|                                                                         |
+------------------------------------------------------------------------+
| 6 elementos                                          Vinculada: OC-123 |
+------------------------------------------------------------------------+
```

### Principios de UX

1. **Familiar**: Misma mecanica que Google Drive / Explorador de Windows
2. **Limpio**: Cards uniformes, sin exceso de informacion
3. **Rapido**: Doble-clic para navegar, breadcrumb para retroceder
4. **Contextual**: Menu clic-derecho para acciones secundarias

### Elementos de la interfaz

**Header:**
- Boton "Volver" (al menu principal)
- Titulo "ARCHIVOS"
- Boton "+ Carpeta" (crea subcarpeta en ubicacion actual)
- Boton "Subir archivo" (abre OpenFileDialog, soporta seleccion multiple)

**Breadcrumb:**
- Muestra la ruta actual: `IMA MECATRONICA > Ordenes 2025 > OC-2025-001`
- Cada segmento es clickeable para navegar hacia atras
- Icono de casa/home al inicio para volver a la raiz

**Area de contenido (WrapPanel con cards):**
- Carpetas primero (ordenadas alfabeticamente), luego archivos (por fecha desc)
- Card de carpeta: icono carpeta + nombre + conteo de elementos
- Card de archivo: icono por tipo (pdf/img/doc/xls/generico) + nombre + tamano
- Doble-clic en carpeta: navega adentro
- Doble-clic en archivo: descarga al equipo
- Clic-derecho: menu contextual

**Barra de estado:**
- Conteo de elementos en la carpeta actual
- Si la carpeta esta vinculada a una orden, muestra "Vinculada: OC-XXX"

### Menu contextual (clic derecho)

**Sobre carpeta:**
- Abrir
- Renombrar
- Vincular a Orden... (abre selector de ordenes)
- Desvincular de Orden (si ya esta vinculada)
- Eliminar

**Sobre archivo:**
- Descargar
- Renombrar
- Eliminar

**Sobre area vacia:**
- Nueva carpeta
- Subir archivo

### Iconos por tipo de archivo

| Extension | Icono | Color |
|-----------|-------|-------|
| Carpeta | Carpeta | #FFC107 (amarillo) |
| .pdf | Documento | #E53935 (rojo) |
| .jpg/.png/.gif | Imagen | #43A047 (verde) |
| .doc/.docx | Word | #1565C0 (azul) |
| .xls/.xlsx | Excel | #2E7D32 (verde oscuro) |
| .txt | Texto | #757575 (gris) |
| Otros | Generico | #9E9E9E (gris) |

---

## Integracion con Manejo de Ordenes

### Columna "CARPETA" en OrdersManagementWindow

Se agrega una columna con un boton-icono por cada orden:

```
| # OC | Cliente | ... | CARPETA | EJECUTOR |
|------|---------|-----|---------|----------|
| 001  | PEMEX   | ... |  [ic]   | Juan     |
| 002  | CFE     | ... |  [ic]   | Pedro    |
```

**Comportamiento del boton:**
- Icono carpeta GRIS si la orden NO tiene carpeta vinculada
- Icono carpeta AZUL si la orden SI tiene carpeta vinculada
- Clic izquierdo:
  - Si tiene carpeta vinculada: abre DriveWindow navegando directamente a esa carpeta
  - Si NO tiene carpeta vinculada: abre DriveWindow en la raiz para que el usuario navegue y vincule
- Tooltip: muestra el nombre de la carpeta vinculada (o "Sin carpeta vinculada")

### Flujo de vinculacion (desde DriveWindow)

1. Usuario navega a la carpeta deseada
2. Clic derecho > "Vincular a Orden..."
3. Se abre un dialog simple con ComboBox de ordenes (filtrable por numero OC)
4. Selecciona la orden > Guardar
5. La carpeta queda vinculada (se guarda `linked_order_id` en `drive_folders`)
6. Desde OrdersManagement, el icono de esa orden cambia a azul

### Flujo de vinculacion (desde OrdersManagement)

1. Usuario hace clic en el icono carpeta GRIS de una orden
2. Se abre DriveWindow con un banner superior: "Seleccione una carpeta para vincular a OC-XXX"
3. El usuario navega a la carpeta deseada
4. Hace clic en boton "Vincular esta carpeta" (visible solo en modo seleccion)
5. Se vincula y se cierra DriveWindow, el icono cambia a azul

---

## Servicio: DriveService

```csharp
// Services/Drive/DriveService.cs
public class DriveService : BaseSupabaseService
{
    private const string BucketName = "ima-drive";

    // --- Carpetas ---
    Task<List<DriveFolderDb>> GetChildFolders(int? parentId, CancellationToken ct);
    Task<DriveFolderDb> CreateFolder(string name, int? parentId, int userId, CancellationToken ct);
    Task<bool> RenameFolder(int folderId, string newName, CancellationToken ct);
    Task<bool> DeleteFolder(int folderId, CancellationToken ct);
    Task<DriveFolderDb> GetFolderById(int folderId, CancellationToken ct);
    Task<List<DriveFolderDb>> GetBreadcrumb(int folderId, CancellationToken ct);

    // --- Archivos ---
    Task<List<DriveFileDb>> GetFilesByFolder(int folderId, CancellationToken ct);
    Task<DriveFileDb> UploadFile(string localPath, int folderId, int userId, CancellationToken ct);
    Task<byte[]> DownloadFile(int fileId, CancellationToken ct);
    Task<bool> RenameFile(int fileId, string newName, CancellationToken ct);
    Task<bool> DeleteFile(int fileId, CancellationToken ct);

    // --- Vinculacion con ordenes ---
    Task<bool> LinkFolderToOrder(int folderId, int orderId, CancellationToken ct);
    Task<bool> UnlinkFolder(int folderId, CancellationToken ct);
    Task<DriveFolderDb?> GetFolderByOrder(int orderId, CancellationToken ct);

    // --- Busqueda ---
    Task<List<DriveFileDb>> SearchFiles(string query, CancellationToken ct);
}
```

---

## Modelos

```csharp
// Models/Database/DriveFolderDb.cs
[Table("drive_folders")]
public class DriveFolderDb : BaseModel
{
    [PrimaryKey("id")] public int Id { get; set; }
    [Column("parent_id")] public int? ParentId { get; set; }
    [Column("name")] public string Name { get; set; }
    [Column("linked_order_id")] public int? LinkedOrderId { get; set; }
    [Column("created_by")] public int? CreatedBy { get; set; }
    [Column("created_at")] public DateTime? CreatedAt { get; set; }
    [Column("updated_at")] public DateTime? UpdatedAt { get; set; }
}

// Models/Database/DriveFileDb.cs
[Table("drive_files")]
public class DriveFileDb : BaseModel
{
    [PrimaryKey("id")] public int Id { get; set; }
    [Column("folder_id")] public int FolderId { get; set; }
    [Column("file_name")] public string FileName { get; set; }
    [Column("storage_path")] public string StoragePath { get; set; }
    [Column("file_size")] public long? FileSize { get; set; }
    [Column("content_type")] public string ContentType { get; set; }
    [Column("uploaded_by")] public int? UploadedBy { get; set; }
    [Column("uploaded_at")] public DateTime? UploadedAt { get; set; }
}
```

---

## Archivos a Crear

| Archivo | Tipo |
|---------|------|
| `sql/bloque5_drive.sql` | Script BD (tablas + indexes + seed) |
| `Models/Database/DriveFolderDb.cs` | Modelo carpeta |
| `Models/Database/DriveFileDb.cs` | Modelo archivo |
| `Services/Drive/DriveService.cs` | Servicio completo |
| `Views/DriveWindow.xaml` + `.cs` | Pantalla principal del drive |

## Archivos a Modificar

| Archivo | Cambio |
|---------|--------|
| `Views/MainMenuWindow.xaml` | Boton "ARCHIVOS" en el menu |
| `Views/MainMenuWindow.xaml.cs` | Handler + permisos (todos acceden) |
| `Views/OrdersManagementWindow.xaml` | Columna "CARPETA" con boton-icono |
| `Views/OrdersManagementWindow.xaml.cs` | Handler para abrir DriveWindow por orden |
| `Services/SupabaseService.cs` | Registrar DriveService en facade |

---

## Plan de Implementacion

### Fase 5A: Base de datos + Modelos (30 min)
- [ ] Crear `sql/bloque5_drive.sql` con tablas, indexes, seed
- [ ] Crear `DriveFolderDb.cs` y `DriveFileDb.cs`
- [ ] Ejecutar SQL en BD

### Fase 5B: DriveService (1-2 hrs)
- [ ] Crear `Services/Drive/DriveService.cs`
- [ ] CRUD carpetas (crear, renombrar, eliminar, listar hijos, breadcrumb)
- [ ] CRUD archivos (subir, descargar, renombrar, eliminar, listar por carpeta)
- [ ] Vinculacion carpeta-orden (link, unlink, get by order)
- [ ] Registrar en SupabaseService facade

### Fase 5C: DriveWindow - UI principal (2-3 hrs)
- [ ] Crear DriveWindow con layout (header, breadcrumb, area de contenido, status bar)
- [ ] WrapPanel con cards para carpetas y archivos
- [ ] Navegacion: doble-clic carpeta, breadcrumb clickeable
- [ ] Botones: Nueva Carpeta, Subir Archivo
- [ ] Menu contextual: Renombrar, Eliminar, Descargar, Vincular a Orden
- [ ] Iconos por tipo de archivo
- [ ] SafeLoadAsync + CancellationToken + OnClosed cleanup
- [ ] Agregar boton "ARCHIVOS" en MainMenuWindow (visible para todos)

### Fase 5D: Integracion con Ordenes (1 hr)
- [ ] Columna "CARPETA" en OrdersManagementWindow
- [ ] Icono gris/azul segun vinculacion
- [ ] Clic abre DriveWindow (en carpeta vinculada o en modo seleccion)
- [ ] Modo seleccion: banner + boton "Vincular esta carpeta"

### Fase 5E: Polish + QA (1 hr)
- [ ] Drag & drop para subir archivos (nice-to-have)
- [ ] Busqueda basica por nombre
- [ ] Validacion de nombres duplicados
- [ ] Manejo de errores (archivo muy grande, sin conexion)
- [ ] Test manual completo

---

## Consideraciones de Storage

### Limites Supabase (Plan Free)
- 1 GB total entre todos los buckets
- 50 MB por archivo
- El bucket `order-files` ya consume espacio (fotos de comisiones)

### Estimacion de uso
- ~10 usuarios, ~50 ordenes/ano
- ~5-10 archivos por orden (PDFs, fotos, docs)
- Promedio 500 KB por archivo
- **Estimado anual: ~250-500 MB**

### Recomendacion
- Iniciar con plan free (1 GB compartido)
- Monitorear uso mensualmente
- Si superan 800 MB: upgrade a Pro ($25/mo = 100 GB, 5 GB por archivo)
- Alternativa futura: migrar a Cloudflare R2 ($0.015/GB, sin egress fees)

---

## Diferencias vs Spec Original (05-carpetas.md)

| Aspecto | Spec original | Propuesta nueva |
|---------|---------------|-----------------|
| Concepto | Path externo + Storage | Drive completo en la nube |
| Carpetas | Solo path guardado en t_order | Arbol de carpetas en BD |
| Navegacion | Abrir explorador del SO | File explorer dentro de la app |
| Bucket | Compartido con order-files | `ima-drive` dedicado |
| Permisos | Matriz por rol | CRUD para todos (fase inicial) |
| UI | Dialog simple | Ventana completa tipo Google Drive |
| Subcarpetas | No contemplado | Soporte completo (infinitas) |
| Vinculacion | 1 campo en t_order | Relacion FK en drive_folders |

La propuesta nueva es mas ambiciosa pero:
- Se alinea mejor con lo que el cliente realmente necesita
- No depende de que el usuario tenga acceso a una carpeta local/NAS
- Centraliza todo en un solo lugar accesible desde cualquier maquina
- El esfuerzo adicional es justificado por la usabilidad

---

## Criterios de Aceptacion

1. El modulo ARCHIVOS aparece en el menu principal para todos los usuarios
2. Se puede navegar carpetas con doble-clic y breadcrumb
3. Se pueden crear subcarpetas en cualquier nivel
4. Se pueden subir uno o mas archivos a cualquier carpeta
5. Se pueden descargar archivos al equipo local
6. Se pueden renombrar y eliminar carpetas y archivos
7. Se puede vincular cualquier carpeta a una orden de compra
8. Desde Manejo de Ordenes, el boton de carpeta abre la carpeta vinculada
9. La interfaz es limpia, rapida y familiar (tipo Google Drive)
10. Funciona para todos los roles sin restriccion
