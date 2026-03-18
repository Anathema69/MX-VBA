# Drive V3 - Experiencia de Archivos Nativa

**Fecha inicio:** 2026-03-17
**Version base:** v2.0.9 (DriveV2Window funcional)
**Estrategia:** A+B (Drive Mejorado + Open-in-Place)
**Estado:** PLANIFICACION

---

## Objetivo

Transformar el modulo ARCHIVOS de una ventana web-like a una experiencia que se sienta como una carpeta local del sistema. El usuario debe poder **ver**, **abrir**, **editar** y **compartir** archivos sin fricciones de descarga/subida manual.

---

## Dashboard de Progreso

| Fase | Nombre | Items | Estado |
|------|--------|-------|--------|
| V3-A | Preview & Thumbnails | 6 | COMPLETADO |
| V3-B | Recientes & Actividad | 4 | COMPLETADO |
| V3-C | Operaciones de Archivos | 5 | COMPLETADO |
| V3-D | Compartir & Enlaces | 4 | PENDIENTE |
| V3-E | Open-in-Place (edicion nativa) | 6 | PENDIENTE |
| V3-F | Cache Local & Sync Ligero | 5 | PENDIENTE |
| V3-G | Pulido UX & Cosmeticos | 5 | PENDIENTE |

**Progreso global:** 15/35 items (43%)

---

## Fase V3-A: Preview & Thumbnails

> **Impacto:** ALTO — Elimina la friccion #1: "para ver un archivo tengo que descargarlo"
> **Complejidad:** Media
> **Archivos principales:** `DriveV2Window.xaml.cs`, `DriveService.cs`

### Contexto

Actualmente el panel de detalle (sidebar derecho, 320px) muestra solo icono generico + metadatos. No hay forma de ver el contenido de un archivo sin descargarlo al equipo.

### Items

- [x] **A1: Preview de imagenes en panel de detalle** (17-Mar-2026)
  - BitmapImage con DecodePixelWidth=600, cache LRU en memoria (max 20)
  - Loading spinner mientras descarga, fallback a icono si falla
  - Clic en preview abre overlay fullscreen con flechas izq/derecha y Escape
  - Overlay con navegacion entre todas las imagenes de la carpeta

- [x] **A2: Preview de PDFs** (17-Mar-2026)
  - Decision: SIN WebView2 (150MB de runtime no justificado)
  - PDFs se abren con "Abrir con app del sistema" via `Process.Start` (descarga a temp)
  - Misma logica que A5 (boton "Abrir con" en detalle)

- [x] **A3: Preview de texto plano** (17-Mar-2026)
  - TextBlock readonly con FontFamily=Consolas, scroll horizontal+vertical
  - Descarga parcial (primeros 100KB) via `DownloadFilePartial` con ByteRange
  - Nota "Mostrando primeros 100 KB" si archivo truncado
  - Extensiones: .txt, .csv, .log, .json, .xml, .md, .ini, .cfg, .html, .css, .js, .sql

- [x] **A4: Thumbnails en vista Grid** (17-Mar-2026)
  - En MkFileCard: imagen real reemplaza icono para archivos de imagen
  - Fallback icon visible mientras carga, fade a thumbnail al completar
  - SemaphoreSlim(5) para limitar descargas paralelas
  - Cache en disco: `%LOCALAPPDATA%/IMA-Drive/thumbs/{fileId}.jpg` (JPEG q80)
  - Carga async con cancelacion via _cts

- [x] **A5: Preview de archivos Office** (17-Mar-2026)
  - Icono grande con colores propios (Word=#2B579A, Excel=#217346, PPT=#D24726)
  - Boton "Abrir con [Word/Excel/PowerPoint]" descarga a temp y abre con Process.Start
  - Toast de confirmacion al abrir

- [x] **A6: Preview de archivos CAD** (17-Mar-2026)
  - Icono grande con color naranja ingenieria (#FF6F00)
  - Gradiente personalizado en el panel de detalle

### Dependencias tecnicas
- `BitmapImage` (WPF nativo)
- `WebView2` (opcional, NuGet para preview PDF)
- R2 signed URLs via `S3Client.GetPreSignedURL()` (ya soportado por AWSSDK.S3)
- Carpeta temporal: `Path.Combine(Path.GetTempPath(), "IMA-Drive")`

### Esquema de cache de thumbnails
```
%TEMP%/IMA-Drive/
  preview/          ← archivos temporales para preview (PDF, Office)
  thumbs/           ← thumbnails de imagenes (persistente entre sesiones)
    {fileId}.jpg    ← 160x120px JPEG quality 80
```

---

## Fase V3-B: Recientes & Actividad

> **Impacto:** ALTO — Los usuarios siempre buscan "lo que estaban viendo hace rato"
> **Complejidad:** Baja-Media
> **Archivos principales:** `DriveV2Window.xaml.cs`, SQL nuevo

### Contexto

Actualmente no hay seccion de "Recientes" ni historial de actividad. El sidebar izquierdo tiene: Root, Ordenes Vinculadas, Pinned. Falta una seccion "Recientes" que muestre los ultimos archivos accedidos/subidos.

### Items

- [ ] **B1: Seccion "Recientes" en sidebar**
  - Nueva seccion en el sidebar izquierdo, debajo de la navegacion actual
  - Query: `SELECT * FROM drive_files ORDER BY uploaded_at DESC LIMIT 15`
  - Mostrar: icono tipo + nombre truncado + fecha relativa ("hace 2h", "ayer")
  - Clic en archivo: navegar a la carpeta que lo contiene + seleccionar el archivo (abrir panel detalle)
  - Actualizar al abrir la ventana y cuando se sube un archivo nuevo

- [ ] **B2: Tabla `drive_activity` + triggers**
  - Nueva tabla para trackear acciones (no solo uploads):
  ```sql
  CREATE TABLE drive_activity (
      id SERIAL PRIMARY KEY,
      user_id INTEGER REFERENCES users(id),
      action VARCHAR(20) NOT NULL,  -- 'upload', 'download', 'rename', 'delete', 'move', 'view'
      target_type VARCHAR(10) NOT NULL, -- 'file', 'folder'
      target_id INTEGER NOT NULL,
      target_name VARCHAR(255),
      folder_id INTEGER REFERENCES drive_folders(id) ON DELETE SET NULL,
      metadata JSONB,  -- datos extra (ej: {from_folder: 5, to_folder: 8} para moves)
      created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
  );
  CREATE INDEX idx_drive_activity_user ON drive_activity(user_id, created_at DESC);
  CREATE INDEX idx_drive_activity_folder ON drive_activity(folder_id, created_at DESC);
  CREATE INDEX idx_drive_activity_recent ON drive_activity(created_at DESC);
  ```
  - Insertar registros desde DriveService en cada operacion CRUD
  - Los registros de `drive_audit` existentes son a nivel trigger (BD); `drive_activity` es a nivel aplicacion (mas contexto)

- [ ] **B3: Feed de actividad en sidebar (colapsable)**
  - Seccion "Actividad reciente" debajo de "Recientes"
  - Mostrar ultimas 10 acciones con: avatar/iniciales del usuario + accion + nombre archivo + fecha relativa
  - Formato: "Juan subio contrato.pdf — hace 30 min"
  - Clic en la actividad navega al archivo/carpeta
  - Toggle colapsable para no ocupar demasiado espacio
  - Solo visible para roles con visibilidad amplia (direccion, administracion, coordinacion)

- [ ] **B4: "Mis archivos recientes" personales**
  - Registrar cuando el usuario actual descarga o abre un archivo (accion 'download' o 'view' en drive_activity)
  - Filtrar la seccion "Recientes" por el usuario logueado: mostrar solo SUS ultimos archivos accedidos
  - Toggle en el sidebar: "Mis recientes" vs "Todos" (default: mis recientes)
  - Esto permite a cada usuario tener su propio "historial" de archivos

### SQL necesario
```sql
-- Archivo: sql/drive_v3_activity.sql
CREATE TABLE drive_activity (...);
-- Indexes
-- No triggers (insercion desde la app)
```

---

## Fase V3-C: Operaciones de Archivos

> **Impacto:** ALTO — Mover archivos entre carpetas es una operacion basica que no existe
> **Complejidad:** Media
> **Archivos principales:** `DriveV2Window.xaml.cs`, `DriveService.cs`

### Contexto

Actualmente solo se puede: crear carpeta, subir, descargar, renombrar, eliminar. No se puede mover ni copiar archivos entre carpetas. El drag-drop solo funciona para subir archivos desde el escritorio.

### Items

- [ ] **C1: Mover archivos/carpetas (context menu)**
  - Context menu > "Mover a..." abre un dialog con arbol de carpetas (TreeView)
  - TreeView lazy-load: cargar hijos al expandir nodo (no cargar todo el arbol de golpe)
  - Al seleccionar destino: `UPDATE drive_files SET folder_id = @dest WHERE id = @fileId`
  - Para carpetas: `UPDATE drive_folders SET parent_id = @dest WHERE id = @folderId`
  - Validacion: no mover carpeta dentro de si misma ni dentro de un descendiente
  - SQL RPC: `validate_folder_move(folder_id, target_parent_id)` → (canMove, blockReason)
  - Multi-select: mover varios archivos a la vez
  - Toast de confirmacion: "3 archivos movidos a 'Planos'"

- [ ] **C2: Copiar archivos (context menu)**
  - Context menu > "Copiar a..." mismo dialog de arbol
  - Copia el registro en `drive_files` con nuevo `folder_id` + nuevo `storage_path`
  - Copia el blob en R2: `S3Client.CopyObjectAsync(source, dest)`
  - Nombre: si existe duplicado en destino, agregar " (copia)" al nombre
  - No aplica a carpetas (copiar arbol recursivo es complejo y raramente necesario)

- [ ] **C3: Drag-drop entre carpetas (interno)**
  - Arrastrar un archivo o carpeta desde el area de contenido hacia una carpeta visible
  - Visual feedback: carpeta destino se resalta en azul al hacer hover con drag
  - Drop = mover (misma logica que C1)
  - Solo entre carpetas visibles en la misma vista (no entre sidebar y contenido)
  - `AllowDrop=true` en cards de carpeta, handlers `DragEnter/DragLeave/Drop`

- [ ] **C4: Cortar/Copiar/Pegar con teclado**
  - `Ctrl+X` = cortar (marcar archivo, visual: opacity 0.5)
  - `Ctrl+C` = copiar (marcar archivo, visual: badge "copiado")
  - `Ctrl+V` = pegar en carpeta actual (ejecutar move o copy segun la operacion)
  - Estado global: `_clipboardFiles: List<(DriveFileDb, ClipOp)>` donde ClipOp = Cut | Copy
  - Limpiar clipboard al navegar si fue Cut exitoso

- [ ] **C5: Duplicar archivo (context menu rapido)**
  - Context menu > "Duplicar" crea copia en la misma carpeta
  - Nombre: "archivo (copia).ext" o "archivo (2).ext" si ya existe una copia
  - Misma logica que C2 pero sin dialog de seleccion de destino

### SQL necesario
```sql
-- Archivo: sql/drive_v3_operations.sql

-- RPC para validar movimiento de carpeta
CREATE OR REPLACE FUNCTION validate_folder_move(p_folder_id INTEGER, p_target_id INTEGER)
RETURNS TABLE(can_move BOOLEAN, block_reason TEXT) AS $$
DECLARE
    v_is_descendant BOOLEAN;
BEGIN
    -- No mover a si mismo
    IF p_folder_id = p_target_id THEN
        RETURN QUERY SELECT FALSE, 'No se puede mover una carpeta dentro de si misma'::TEXT;
        RETURN;
    END IF;

    -- No mover a un descendiente
    WITH RECURSIVE descendants AS (
        SELECT id FROM drive_folders WHERE parent_id = p_folder_id
        UNION ALL
        SELECT df.id FROM drive_folders df JOIN descendants d ON df.parent_id = d.id
    )
    SELECT EXISTS(SELECT 1 FROM descendants WHERE id = p_target_id) INTO v_is_descendant;

    IF v_is_descendant THEN
        RETURN QUERY SELECT FALSE, 'No se puede mover una carpeta dentro de un descendiente'::TEXT;
        RETURN;
    END IF;

    RETURN QUERY SELECT TRUE, NULL::TEXT;
END;
$$ LANGUAGE plpgsql;
```

### Diagrama de interaccion
```
[Context Menu: "Mover a..."]
  └─ [TreeView Dialog]
       ├─ IMA MECATRONICA
       │  ├─ Ordenes 2025
       │  │  ├─ OC-001 (Planos)  ← seleccionar destino
       │  │  └─ OC-002
       │  └─ Ordenes 2026
       └─ [Boton: Mover aqui]
            └─ UPDATE folder_id → Toast "Movido a Planos"
```

---

## Fase V3-D: Compartir & Enlaces

> **Impacto:** MEDIO-ALTO — Necesario para colaborar con personas externas (clientes, proveedores)
> **Complejidad:** Baja
> **Archivos principales:** `DriveService.cs`, `DriveV2Window.xaml.cs`

### Contexto

No existe forma de compartir un archivo con alguien que no tenga la app instalada. R2 ya soporta signed URLs con expiracion, solo falta la UI y la logica.

### Items

- [ ] **D1: Generar enlace temporal (signed URL)**
  - Context menu > "Compartir enlace"
  - Genera un R2 signed URL con expiracion configurable: 1 hora, 24 horas, 7 dias, 30 dias
  - Default: 24 horas
  - El enlace se copia automaticamente al clipboard
  - Toast: "Enlace copiado - expira en 24 horas"
  - Implementacion: `S3Client.GetPreSignedURL(bucket, key, expiration)`
  ```csharp
  public string GenerateShareLink(string storagePath, TimeSpan expiration)
  {
      var request = new GetPreSignedUrlRequest
      {
          BucketName = _bucketName,
          Key = storagePath,
          Expires = DateTime.UtcNow.Add(expiration)
      };
      return _s3Client.GetPreSignedURL(request);
  }
  ```

- [ ] **D2: Dialog de compartir con opciones**
  - Dialog modal (estilo IMA) con:
    - Preview del nombre del archivo + icono
    - Selector de expiracion: dropdown con 1h, 24h, 7d, 30d
    - Campo de texto readonly con el link generado
    - Boton "Copiar enlace" (con feedback visual: "Copiado!")
    - Boton "Abrir en navegador" (para probar el enlace)
  - Registrar en `drive_activity`: accion 'share', metadata: {expiration, url_hash}

- [ ] **D3: Compartir carpeta completa (ZIP)**
  - Context menu en carpeta > "Descargar como ZIP"
  - Recopilar todos los archivos de la carpeta (recursivo)
  - Descargar cada uno via R2 + crear ZIP en temp
  - Opcion 1: SaveFileDialog para guardar el ZIP localmente
  - Opcion 2: Subir el ZIP a R2 y generar signed URL
  - Limite: maximo 50 archivos o 500MB por ZIP (para no saturar la red)
  - Progress bar durante la descarga/creacion del ZIP
  - NuGet: `System.IO.Compression` (ya incluido en .NET 8)

- [ ] **D4: Historial de enlaces compartidos**
  - Tabla en BD (o recopilar de `drive_activity` WHERE action='share')
  - UI: seccion en panel de detalle del archivo mostrando enlaces activos
  - Mostrar: fecha de creacion, expiracion, estado (activo/expirado)
  - Posibilidad de revocar un enlace (no aplica a signed URLs de R2 — no se pueden revocar individualmente)
  - Alternativa: si se necesita revocacion, crear tabla `drive_shared_links` con flag `revoked` y un endpoint proxy que valide antes de redirigir

### Nota sobre seguridad
- Los signed URLs de R2 no se pueden revocar una vez generados
- La unica forma de "revocar" es rotar las API keys (afecta todos los enlaces)
- Para un MVP esto es aceptable; si el cliente necesita revocacion granular, se implementa un proxy en el futuro

---

## Fase V3-E: Open-in-Place (Edicion Nativa)

> **Impacto:** MUY ALTO — Este es el cambio que transforma la experiencia de "app web" a "carpeta local"
> **Complejidad:** Alta
> **Archivos principales:** Nuevo servicio `FileWatcherService.cs`, `DriveV2Window.xaml.cs`, `DriveService.cs`

### Contexto

Actualmente "abrir" un archivo = descargarlo con SaveFileDialog. El usuario tiene que elegir donde guardarlo, abrirlo manualmente, y si lo edita, subir la nueva version manualmente. Esto mata la productividad.

### Vision

Doble-clic en archivo → se descarga a temp → se abre con la app del sistema → el usuario edita → al guardar, automaticamente se sube la nueva version a R2. **Igual que OneDrive/Google Drive/Dropbox.**

### Items

- [ ] **E1: Directorio local temporal gestionado**
  - Crear carpeta `%LOCALAPPDATA%/IMA-Drive/open/` (persistente entre sesiones, no %TEMP% que se limpia)
  - Estructura: `{folderId}/{fileId}_{fileName}` (evitar colisiones de nombre)
  - Al abrir un archivo: descargar a esta ruta si no existe o si la version local es mas vieja que `uploaded_at`
  - Metadata local: archivo `%LOCALAPPDATA%/IMA-Drive/open/.manifest.json` con:
    ```json
    {
      "files": [
        {
          "fileId": 42,
          "localPath": "5/42_contrato.pdf",
          "remoteUploadedAt": "2026-03-15T10:30:00Z",
          "localModifiedAt": "2026-03-15T10:30:00Z",
          "size": 245000,
          "watching": true
        }
      ]
    }
    ```
  - Limpieza: eliminar archivos no accedidos en 7 dias (al iniciar la app)

- [ ] **E2: "Abrir" en vez de "Descargar" (doble-clic)**
  - Cambiar el doble-clic en archivo de `SaveFileDialog` a:
    1. Verificar si ya existe en cache local y esta actualizado
    2. Si no: descargar de R2 a la ruta gestionada
    3. Abrir con `Process.Start(new ProcessStartInfo(localPath) { UseShellExecute = true })`
    4. Iniciar FileSystemWatcher en el archivo
  - Conservar "Descargar" en context menu para cuando el usuario quiere elegir ubicacion
  - Visual feedback: icono de "abriendo" (spinner) mientras descarga, luego la app se abre

- [ ] **E3: FileWatcherService — detectar cambios y re-subir**
  - Nuevo servicio: `Services/Drive/FileWatcherService.cs`
  - Usa `FileSystemWatcher` para monitorear `%LOCALAPPDATA%/IMA-Drive/open/`
  - Al detectar cambio en un archivo monitoreado:
    1. Esperar 2 segundos (debounce — la app puede seguir escribiendo)
    2. Verificar que el archivo no esta bloqueado (retry 3 veces con 1s delay)
    3. Subir nueva version a R2 (mismo `storage_path`, overwrite)
    4. Actualizar `file_size` y `uploaded_at` en BD
    5. Toast en DriveV2Window (si esta abierta): "contrato.pdf actualizado automaticamente"
  - Singleton: una sola instancia del watcher para toda la app
  - Iniciar el watcher al abrir DriveV2Window, mantener vivo mientras la app esta abierta
  - Limpiar watchers de archivos que ya no existen localmente

  ```csharp
  public class FileWatcherService : IDisposable
  {
      private FileSystemWatcher _watcher;
      private readonly Dictionary<string, WatchedFile> _watchedFiles;
      private readonly DriveService _driveService;
      private readonly SemaphoreSlim _uploadLock = new(1);

      public event Action<string, string>? FileAutoUploaded; // (fileName, status)

      public void StartWatching(string localPath, int fileId, string storagePath);
      public void StopWatching(string localPath);
      public void Dispose();
  }
  ```

- [ ] **E4: Indicador visual de estado de sincronizacion**
  - En la card/row del archivo, mostrar un badge de estado:
    - Sin abrir: nada (default)
    - Abierto localmente: icono de "editando" (lapiz verde)
    - Sincronizando: spinner pequeno
    - Actualizado: checkmark verde (3 segundos, luego desaparece)
    - Error de sync: icono rojo con tooltip del error
  - En el panel de detalle: texto "Abierto localmente — los cambios se guardan automaticamente"

- [ ] **E5: Manejo de conflictos basico**
  - Caso: el archivo fue modificado en R2 por otro usuario mientras el usuario actual lo tenia abierto
  - Deteccion: antes de subir, comparar `uploaded_at` del servidor con el valor guardado en manifest
  - Si hay conflicto:
    - Dialog: "Este archivo fue modificado por [usuario] el [fecha]. Que desea hacer?"
    - Opciones: "Reemplazar con mi version" | "Descargar version del servidor" | "Guardar como copia"
  - "Guardar como copia": subir con nombre `archivo (conflicto Juan 2026-03-17).ext`
  - Para el MVP, los conflictos seran raros (equipo de 10 personas)

- [ ] **E6: Barra de estado global de sync**
  - En la barra inferior de DriveV2Window, mostrar estado del watcher:
    - "Todos los archivos sincronizados" (idle)
    - "Sincronizando contrato.pdf..." (en progreso)
    - "Error al sincronizar: contrato.pdf" (con boton "Reintentar")
  - Icono en la barra: nube con checkmark (sync ok) / nube con flecha (sync en progreso) / nube con X (error)
  - Al cerrar la ventana: si hay archivos pendientes de sync, preguntar "Hay cambios sin sincronizar. Cerrar de todos modos?"

### Diagrama de flujo
```
[Doble-clic en archivo]
  ├─ Existe en cache local?
  │  ├─ SI: Version actualizada?
  │  │  ├─ SI → Process.Start() + FileWatcher
  │  │  └─ NO → Descargar nueva version → Process.Start() + FileWatcher
  │  └─ NO → Descargar de R2 → Process.Start() + FileWatcher
  │
  [Usuario edita en Word/Excel/etc]
  │
  [FileWatcher: Changed event]
  ├─ Debounce 2s
  ├─ Verificar lock
  ├─ Verificar conflictos (uploaded_at)
  │  ├─ Sin conflicto → Upload a R2 → Toast "Actualizado"
  │  └─ Conflicto → Dialog de resolucion
  └─ Actualizar manifest.json
```

### Archivos nuevos
```
Services/Drive/FileWatcherService.cs   ← Servicio de monitoreo
Models/DTOs/WatchedFile.cs             ← Estado de un archivo monitoreado
```

---

## Fase V3-F: Cache Local & Sync Ligero

> **Impacto:** MEDIO — Mejora la velocidad percibida y permite trabajo semi-offline
> **Complejidad:** Media-Alta
> **Archivos principales:** Nuevo servicio `DriveCacheService.cs`

### Contexto

Cada vez que el usuario navega a una carpeta, se hace un HTTP call a Supabase. Los thumbnails se descargan cada vez. No hay cache de metadata entre sesiones.

### Items

- [ ] **F1: Cache de metadata entre sesiones (SQLite local)**
  - Archivo local: `%LOCALAPPDATA%/IMA-Drive/cache.db` (SQLite)
  - Tablas espejo: `cached_folders`, `cached_files` con campo `synced_at`
  - Al navegar a una carpeta:
    1. Mostrar datos del cache inmediatamente (si existen)
    2. En background, hacer fetch al servidor
    3. Si hay cambios: actualizar cache + UI (stale-while-revalidate)
  - Beneficio: navegacion instantanea en carpetas visitadas previamente
  - NuGet: `Microsoft.Data.Sqlite` (lightweight, ~500KB)

- [ ] **F2: Cache de thumbnails persistente**
  - Carpeta: `%LOCALAPPDATA%/IMA-Drive/thumbs/`
  - Al generar thumbnail (Fase A4): guardar en disco
  - Al renderizar cards: verificar cache local antes de descargar de R2
  - Invalidacion: si `uploaded_at` del archivo es mas reciente que el timestamp del thumbnail, regenerar
  - Limpieza: eliminar thumbnails de archivos que ya no existen (periodicamente, al iniciar la app)
  - Tamano maximo del cache: 200MB (configurable)

- [ ] **F3: Prefetch de carpetas frecuentes**
  - Al abrir DriveV2Window, en background cargar las carpetas del nivel 1 y 2
  - Esto permite que la navegacion hacia abajo sea instantanea (cache ya tiene los datos)
  - No cargar todo el arbol — solo los primeros 2 niveles + carpetas marcadas como favoritas
  - Usar `Task.Run()` fire-and-forget con `_cts` token

- [ ] **F4: Indicador de datos offline vs online**
  - Si se muestran datos del cache: mostrar banner sutil "Datos del cache — actualizando..."
  - Cuando los datos frescos llegan: el banner desaparece silenciosamente
  - Si no hay conexion: mostrar banner "Sin conexion — mostrando datos del cache"
  - Timeout de conexion: 5 segundos antes de mostrar "Sin conexion"

- [ ] **F5: Limpieza automatica del cache**
  - Al iniciar la app: verificar tamano total del cache
  - Si supera el limite (default 200MB): eliminar archivos LRU (least recently used)
  - Eliminar thumbnails de archivos que ya no existen en BD
  - Eliminar preview files mas viejos que 7 dias
  - Registrar en log: "Cache limpiado: X MB liberados"

### Archivos nuevos
```
Services/Drive/DriveCacheService.cs     ← Gestion del cache local
```

### Estructura del cache local
```
%LOCALAPPDATA%/IMA-Drive/
  cache.db              ← SQLite con metadata cacheada
  thumbs/               ← Thumbnails persistentes (JPEG)
    42.jpg
    105.jpg
  open/                 ← Archivos abiertos para edicion (Fase E)
    5/42_contrato.pdf
    .manifest.json
  logs/                 ← Logs de sync (opcional)
```

---

## Fase V3-G: Pulido UX & Cosmeticos

> **Impacto:** MEDIO — Mejora la experiencia sin agregar funcionalidad nueva
> **Complejidad:** Baja
> **Archivos principales:** `DriveV2Window.xaml`, `DriveV2Window.xaml.cs`

### Items

- [ ] **G1: Atajos de teclado estilo Explorador de Windows**
  - `F2` = Renombrar archivo/carpeta seleccionada
  - `Delete` = Eliminar (con confirmacion)
  - `F5` = Refrescar carpeta actual
  - `Ctrl+N` = Nueva carpeta
  - `Ctrl+U` = Subir archivo(s)
  - `Ctrl+F` = Focus en barra de busqueda
  - `Ctrl+A` = Seleccionar todos los archivos
  - `Backspace` = Volver a carpeta padre
  - `Enter` = Abrir carpeta o archivo seleccionado
  - `Alt+Left/Right` = Historial atras/adelante (ya existe con mouse buttons)
  - Implementar en `OnKeyDown` con switch sobre `e.Key` + `Keyboard.Modifiers`

- [ ] **G2: Breadcrumb mejorado con dropdown**
  - Al hacer clic en un segmento del breadcrumb: mostrar dropdown con carpetas hermanas
  - Ejemplo: clic en "Ordenes 2025" muestra dropdown con "Ordenes 2024", "Ordenes 2026", etc.
  - Esto permite navegar lateralmente sin volver al padre primero
  - Implementar como `Popup` con lista de folders del mismo nivel

- [ ] **G3: Status bar informativa**
  - Barra inferior con informacion contextual:
    - Carpeta: "12 carpetas, 45 archivos — 2.3 GB"
    - Seleccion: "3 archivos seleccionados — 1.2 MB"
    - Busqueda: "8 resultados para 'contrato'"
    - Sync: "Sincronizado" / "Sincronizando..." / "Sin conexion"
  - Transicion suave entre estados (fade)

- [ ] **G4: Empty states mejorados**
  - Carpeta vacia: ilustracion + "Esta carpeta esta vacia" + botones "Subir archivos" / "Nueva carpeta"
  - Sin resultados de busqueda: "No se encontraron archivos para '[query]'" + "Buscar en todo el Drive"
  - Sin conexion: icono de nube desconectada + "Comprueba tu conexion a internet"
  - Primer uso: "Bienvenido a IMA Drive" + pasos rapidos (crear carpeta, subir, vincular)

- [ ] **G5: Animaciones de transicion**
  - Al navegar entre carpetas: fade out contenido actual → fade in nuevo contenido (150ms)
  - Al subir archivo: la card nueva aparece con animacion scale 0→1 (200ms)
  - Al eliminar: la card desaparece con fade + scale 1→0 (200ms)
  - Ghost cards: pulse animation sutil en el progress bar
  - Implementar con `DoubleAnimation` y `Storyboard` de WPF

---

## Dependencias entre fases

```
V3-A (Preview)      ──────────────────────► V3-G (Pulido)
                                              ↑
V3-B (Recientes)   ──────────────────────────┘
                                              ↑
V3-C (Operaciones) ──────────────────────────┘
                                              ↑
V3-D (Compartir)   ──────────────────────────┘

V3-E (Open-in-Place) ──► V3-F (Cache local)
```

**Fases independientes (pueden hacerse en paralelo):**
- A, B, C, D son independientes entre si
- E y F son secuenciales (F depende de E)
- G se hace al final

**Orden de implementacion recomendado:**
1. **V3-A** (Preview) — impacto visual inmediato, abre la puerta a thumbnails
2. **V3-C** (Operaciones) — mover/copiar son operaciones basicas esperadas
3. **V3-B** (Recientes) — mejora navegacion sin cambios grandes
4. **V3-E** (Open-in-Place) — el game-changer, requiere servicio nuevo
5. **V3-D** (Compartir) — complementa con colaboracion externa
6. **V3-F** (Cache) — optimizacion, depende de que E este estable
7. **V3-G** (Pulido) — refinamiento final

---

## Consideraciones tecnicas

### NuGet nuevos (potenciales)
| Paquete | Fase | Uso | Tamano |
|---------|------|-----|--------|
| `Microsoft.Web.WebView2` | A2 | Preview PDF embebido | ~150MB runtime |
| `Microsoft.Data.Sqlite` | F1 | Cache local de metadata | ~500KB |

**Nota:** WebView2 es pesado. Evaluar si vale la pena vs abrir PDF en navegador del sistema.

### Impacto en el tamano de la app
- Sin WebView2: +~1MB (SQLite + logica adicional)
- Con WebView2: +~150MB (WebView2 runtime, si no esta instalado en el sistema)
- Recomendacion: comenzar SIN WebView2 y evaluar si los usuarios realmente necesitan preview de PDF in-app

### Archivos nuevos estimados
```
Services/Drive/FileWatcherService.cs     ← ~200 lineas
Services/Drive/DriveCacheService.cs      ← ~300 lineas
Models/DTOs/WatchedFile.cs               ← ~30 lineas
sql/drive_v3_activity.sql                ← ~30 lineas
sql/drive_v3_operations.sql              ← ~40 lineas
```

### Archivos a modificar
```
Services/Drive/DriveService.cs           ← Nuevos metodos (share, move, copy)
Services/SupabaseService.cs              ← Registrar nuevos servicios
Views/DriveV2Window.xaml                 ← Sidebar recientes, status bar, atajos
Views/DriveV2Window.xaml.cs              ← Preview, drag-drop interno, open-in-place
appsettings.json                         ← Config del cache local (paths, limites)
```

---

## Metricas de exito

| Metrica | Antes (V2) | Objetivo (V3) |
|---------|------------|---------------|
| Clics para ver un archivo | 3 (clic → save → abrir) | 1 (doble-clic) |
| Clics para editar y re-subir | 6+ (descargar → editar → guardar → subir → elegir carpeta → confirmar) | 1 (doble-clic, auto-sync) |
| Tiempo para encontrar archivo reciente | 15-30s (navegar arbol) | 2s (seccion Recientes) |
| Compartir archivo externamente | Imposible | 2 clics (compartir → copiar enlace) |
| Mover archivo entre carpetas | Imposible | 2 clics (context menu → seleccionar destino) |
| Preview de imagen | Imposible sin descargar | Instantaneo en panel lateral |

---

## Riesgos y mitigaciones

| Riesgo | Probabilidad | Impacto | Mitigacion |
|--------|-------------|---------|------------|
| FileSystemWatcher pierde eventos | Media | Alto | Polling periodico como backup (cada 30s verificar hash del archivo) |
| Conflictos de edicion simultanea | Baja | Medio | Dialog de resolucion + equipo pequeno = baja frecuencia |
| Cache crece sin control | Media | Bajo | Limpieza automatica al iniciar + limite configurable |
| WebView2 no disponible | Baja | Bajo | Fallback a "Abrir en navegador" |
| R2 signed URL expira mientras se visualiza | Baja | Bajo | Regenerar URL si falla la carga de preview |
| Antivirus bloquea FileWatcher | Baja | Alto | Documentar exclusion de carpeta IMA-Drive en manual de instalacion |
