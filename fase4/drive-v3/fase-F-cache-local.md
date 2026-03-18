# Fase V3-F: Cache Local & Sync Ligero

**Estado:** PENDIENTE
**Prioridad:** 6
**Depende de:** Fase V3-E (Open-in-Place)
**Archivos clave:** Nuevo `DriveCacheService.cs`

---

## Checklist de implementacion

### F1: Cache de metadata entre sesiones (SQLite)
- [ ] NuGet: `Microsoft.Data.Sqlite` (~500KB)
- [ ] Archivo: `%LOCALAPPDATA%/IMA-Drive/cache.db`
- [ ] Nuevo servicio: `Services/Drive/DriveCacheService.cs`
- [ ] Crear tablas SQLite al inicializar:
  ```sql
  CREATE TABLE IF NOT EXISTS cached_folders (
      id INTEGER PRIMARY KEY,
      parent_id INTEGER,
      name TEXT NOT NULL,
      linked_order_id INTEGER,
      created_by INTEGER,
      synced_at TEXT NOT NULL
  );
  CREATE TABLE IF NOT EXISTS cached_files (
      id INTEGER PRIMARY KEY,
      folder_id INTEGER NOT NULL,
      file_name TEXT NOT NULL,
      storage_path TEXT NOT NULL,
      file_size INTEGER,
      content_type TEXT,
      uploaded_by INTEGER,
      uploaded_at TEXT,
      synced_at TEXT NOT NULL
  );
  CREATE TABLE IF NOT EXISTS cache_meta (
      key TEXT PRIMARY KEY,
      value TEXT
  );
  ```
- [ ] Patron stale-while-revalidate:
  1. `GetCachedFolderContent(folderId)` → retorna datos locales inmediatamente
  2. En background: `FetchAndUpdateCache(folderId)` → query servidor + update cache
  3. Si hay cambios: evento `CacheUpdated(folderId)` → UI se re-renderiza
- [ ] Invalidar cache de una carpeta cuando se hace CRUD en ella
- [ ] Al iniciar: verificar version del schema (migraciones)

### F2: Cache de thumbnails persistente
- [ ] Carpeta: `%LOCALAPPDATA%/IMA-Drive/thumbs/`
- [ ] Al generar thumbnail (reusar logica de Fase A4): guardar a disco
- [ ] `GetCachedThumbnail(fileId) → string? localPath`
- [ ] `SaveThumbnail(fileId, BitmapImage img)` → guardar como JPEG q80
- [ ] Invalidacion: comparar `uploaded_at` del archivo con timestamp del thumbnail
- [ ] Si `uploaded_at` > thumb timestamp: regenerar thumbnail
- [ ] Registro en SQLite: tabla `cached_thumbnails(file_id, path, created_at)`

### F3: Prefetch de carpetas frecuentes
- [ ] Al abrir DriveV2Window: background task para precargar nivel 1 y 2
- [ ] `PreloadFolderTree(maxDepth: 2)`:
  1. Cargar hijos del root
  2. Para cada hijo: cargar sus hijos
  3. Guardar todo en cache SQLite
- [ ] Tambien precargar carpetas marcadas como "favoritas" (si se implementa feature)
- [ ] Usar `Task.Run()` con `_cts` token
- [ ] No bloquear la UI — todo en background
- [ ] Si el prefetch tarda >10s, cancelar (red lenta)

### F4: Indicador de datos offline vs online
- [ ] Al mostrar datos del cache: banner sutil en la parte superior del contenido
- [ ] "Mostrando datos guardados — actualizando..." (color gris claro, texto muted)
- [ ] Cuando datos frescos llegan: ocultar banner con fade-out
- [ ] Si no hay conexion despues de 5s timeout: "Sin conexion — mostrando datos guardados"
- [ ] Probar conexion: `HttpClient.GetAsync(supabaseUrl, timeout: 5s)`
- [ ] No mostrar banner si la conexion es rapida (<500ms)

### F5: Limpieza automatica del cache
- [ ] Al iniciar la app: `CleanupCache()` en background
- [ ] Verificar tamano total: thumbs/ + cache.db + open/
- [ ] Limite default: 200MB (configurable en appsettings.json)
- [ ] Si excede: eliminar thumbnails LRU hasta bajar del limite
- [ ] Eliminar entries de cache para archivos que ya no existen:
  - Query servidor: `SELECT id FROM drive_files WHERE id IN (...cachedIds)`
  - Eliminar del cache los IDs que no esten en el resultado
  - Ejecutar esto max 1 vez al dia (guardar last_cleanup_date en cache_meta)
- [ ] Eliminar archivos en open/ no accedidos en >7 dias
- [ ] Log: "Cache limpiado: X MB liberados"

---

## Estructura del cache

```
%LOCALAPPDATA%/IMA-Drive/
├── cache.db                 ← SQLite (metadata de carpetas y archivos)
├── thumbs/                  ← Thumbnails JPEG persistentes
│   ├── 42.jpg
│   ├── 105.jpg
│   └── ...
├── open/                    ← Archivos abiertos para edicion (Fase E)
│   ├── 5/
│   │   └── 42_contrato.pdf
│   └── .manifest.json
└── logs/                    ← Logs de operaciones (opcional)
```

## DriveCacheService API

```csharp
public class DriveCacheService : IDisposable
{
    private SqliteConnection _db;

    // Inicializacion
    public void Initialize();  // Crear DB + tablas si no existen

    // Carpetas
    public List<DriveFolderDb> GetCachedFolders(int? parentId);
    public void CacheFolders(int? parentId, List<DriveFolderDb> folders);
    public void InvalidateFolder(int folderId);

    // Archivos
    public List<DriveFileDb> GetCachedFiles(int folderId);
    public void CacheFiles(int folderId, List<DriveFileDb> files);
    public void InvalidateFile(int fileId);

    // Thumbnails
    public string? GetCachedThumbnailPath(int fileId);
    public void SaveThumbnail(int fileId, byte[] jpegData);

    // Mantenimiento
    public long GetCacheSize();
    public void Cleanup(long maxSizeBytes);
    public void ClearAll();

    // Metadata
    public string? GetMeta(string key);
    public void SetMeta(string key, string value);
}
```

## Config en appsettings.json (nueva seccion)

```json
"DriveCache": {
    "Enabled": true,
    "MaxSizeMB": 200,
    "ThumbnailQuality": 80,
    "OpenFileTTLDays": 7,
    "CleanupIntervalHours": 24
}
```
