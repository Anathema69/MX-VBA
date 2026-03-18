# Fase V3-E: Open-in-Place (Edicion Nativa)

**Estado:** PENDIENTE
**Prioridad:** 4
**Archivos clave:** Nuevo `FileWatcherService.cs`, `DriveV2Window.xaml.cs`, `DriveService.cs`

> Esta es la fase mas transformadora. Convierte la experiencia de "descargar → editar → volver a subir" en "doble-clic → editar → guardar → listo".

---

## Checklist de implementacion

### E1: Directorio local gestionado
- [ ] Crear estructura: `%LOCALAPPDATA%/IMA-Drive/open/`
- [ ] Crear al iniciar la app si no existe: `Directory.CreateDirectory()`
- [ ] Convención de nombres: `{folderId}/{fileId}_{fileName}`
- [ ] Archivo manifest: `%LOCALAPPDATA%/IMA-Drive/open/.manifest.json`
- [ ] Modelo para manifest:
  ```csharp
  public class FileManifest
  {
      public List<WatchedFileEntry> Files { get; set; } = new();
  }
  public class WatchedFileEntry
  {
      public int FileId { get; set; }
      public string LocalPath { get; set; }      // relativo a /open/
      public DateTime RemoteUploadedAt { get; set; }
      public DateTime LocalModifiedAt { get; set; }
      public long Size { get; set; }
      public bool Watching { get; set; }
  }
  ```
- [ ] Cargar manifest al iniciar, guardar al modificar
- [ ] Limpieza al iniciar: eliminar archivos no accedidos en >7 dias
- [ ] Limpieza: eliminar entries del manifest cuyo archivo local no existe

### E2: "Abrir" (doble-clic) en vez de "Descargar"
- [ ] Cambiar handler de doble-clic en archivo:
  - Antes: `SaveFileDialog` → descargar
  - Ahora: descargar a directorio gestionado → `Process.Start()`
- [ ] Flujo completo:
  1. Verificar si existe en cache local (`manifest.json`)
  2. Si existe: comparar `RemoteUploadedAt` con `uploaded_at` del servidor
  3. Si es actual: abrir directamente
  4. Si esta desactualizado o no existe: descargar de R2
  5. Guardar/actualizar entry en manifest
  6. `Process.Start(new ProcessStartInfo(localPath) { UseShellExecute = true })`
  7. Iniciar FileWatcher para ese archivo
- [ ] Conservar "Descargar" en context menu (con SaveFileDialog, como antes)
- [ ] Loading: mostrar spinner en la card mientras descarga, antes de abrir
- [ ] Error: si la app del sistema no se encuentra, toast "No se encontro aplicacion para abrir .sldprt"

### E3: FileWatcherService
- [ ] Crear `Services/Drive/FileWatcherService.cs`
- [ ] Singleton (instanciado en SupabaseService o lazy static)
- [ ] `FileSystemWatcher` en `%LOCALAPPDATA%/IMA-Drive/open/`
  - `IncludeSubdirectories = true`
  - `NotifyFilter = LastWrite | Size`
  - `EnableRaisingEvents = true`
- [ ] `Changed` event handler:
  1. Identificar que archivo cambio (match con manifest)
  2. Debounce 2 segundos (usar `CancellationTokenSource` + `Task.Delay`)
  3. Si el archivo sigue cambiando (nuevo Changed event): reiniciar timer
  4. Verificar que el archivo no esta bloqueado:
     ```csharp
     private bool IsFileLocked(string path)
     {
         try { using var fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None); return false; }
         catch (IOException) { return true; }
     }
     ```
  5. Si bloqueado: retry 3 veces con 1s delay
  6. Verificar conflictos: comparar `uploaded_at` del servidor con `RemoteUploadedAt` del manifest
  7. Si no hay conflicto: upload a R2 (overwrite mismo `storage_path`)
  8. Actualizar `file_size` y `uploaded_at` en BD
  9. Actualizar manifest
  10. Emitir evento `FileAutoUploaded(fileName, "success")`
- [ ] Metodo `StartWatching(localPath, fileId, storagePath)`
- [ ] Metodo `StopWatching(localPath)`
- [ ] Metodo `StopAll()` — detener todo (al cerrar app)
- [ ] `IDisposable` — cleanup del FileSystemWatcher
- [ ] Thread safety: `SemaphoreSlim(1)` para uploads (uno a la vez)
- [ ] Evento publico: `event Action<string, string> FileAutoUploaded`

### E4: Indicadores visuales de sync
- [ ] Badge en card/row del archivo:
  - `_openedFiles: HashSet<int>` — archivos abiertos localmente
  - Sin abrir: sin badge (default)
  - Abierto: icono lapiz verde (16px) en esquina superior derecha de la card
  - Sincronizando: spinner (reemplaza lapiz)
  - Actualizado: checkmark verde (3s, luego vuelve a lapiz)
  - Error: icono rojo con tooltip
- [ ] En panel de detalle: TextBlock "Abierto localmente — cambios se guardan automaticamente"
- [ ] Suscribir a `FileWatcherService.FileAutoUploaded` para actualizar badges
- [ ] Al cerrar archivo externamente (detectar proceso terminado — complejo): quitar badge
  - Simplificacion: mantener badge mientras exista en manifest, quitar al limpiar

### E5: Manejo de conflictos
- [ ] Antes de upload: query `drive_files WHERE id = @id` para obtener `uploaded_at` actual
- [ ] Comparar con `RemoteUploadedAt` del manifest
- [ ] Si `uploaded_at` > `RemoteUploadedAt` → conflicto detectado
- [ ] Dialog de conflicto:
  - Titulo: "Conflicto de edicion"
  - Mensaje: "Este archivo fue modificado por [usuario] el [fecha] mientras lo tenias abierto."
  - Opciones (3 botones):
    - "Reemplazar con mi version" → upload overwrite
    - "Descargar version del servidor" → descargar nueva version, descartar cambios locales
    - "Guardar como copia" → upload con nombre modificado
  - Mostrar: fecha local vs fecha servidor
- [ ] "Guardar como copia": nombre = `archivo (conflicto {usuario} {fecha}).ext`
- [ ] Actualizar manifest despues de resolver

### E6: Barra de estado global
- [ ] StatusBar en la parte inferior de DriveV2Window
- [ ] Icono de nube: sync ok (checkmark), sync en progreso (flechas), error (X)
- [ ] Texto: "Todos los archivos sincronizados" / "Sincronizando contrato.pdf..." / "Error: contrato.pdf"
- [ ] Si hay error: boton "Reintentar" inline
- [ ] Al cerrar ventana: verificar si hay archivos pendientes de sync
- [ ] Si hay pendientes: dialog "Hay cambios sin sincronizar. Esperar o cerrar de todos modos?"
- [ ] Timer: si no hay actividad de sync en 30s, polling de manifest para verificar archivos modificados sin evento

---

## Diagrama de flujo detallado

```
ABRIR ARCHIVO (doble-clic)
=========================
                                    ┌──────────────┐
                                    │ Doble-clic   │
                                    │ en archivo   │
                                    └──────┬───────┘
                                           │
                                    ┌──────▼───────┐
                                    │ Existe en    │
                                    │ cache local? │
                                    └──┬───────┬───┘
                                 SI │       │ NO
                              ┌────▼───┐   │
                              │Version │   │
                              │actual? │   │
                              └─┬────┬─┘   │
                           SI │  NO│       │
                              │    │       │
                        ┌─────▼┐ ┌─▼───────▼──────┐
                        │Abrir │ │Descargar de R2  │
                        │local │ │a cache local    │
                        └──┬───┘ └──────┬──────────┘
                           │            │
                           └─────┬──────┘
                                 │
                          ┌──────▼───────┐
                          │Process.Start │
                          │(shell exec)  │
                          └──────┬───────┘
                                 │
                          ┌──────▼───────┐
                          │Start         │
                          │FileWatcher   │
                          └──────────────┘


AUTO-SYNC (FileWatcher)
=======================
                          ┌──────────────┐
                          │ File Changed │
                          │ event        │
                          └──────┬───────┘
                                 │
                          ┌──────▼───────┐
                          │ Debounce 2s  │
                          └──────┬───────┘
                                 │
                          ┌──────▼───────┐
                          │ File locked? │
                          └──┬───────┬───┘
                          SI │    NO │
                     ┌──────▼─┐     │
                     │Retry   │     │
                     │3x @ 1s │     │
                     └────────┘     │
                                    │
                          ┌─────────▼────┐
                          │ Conflicto?   │
                          │ (uploaded_at │
                          │  changed)    │
                          └──┬───────┬───┘
                          SI │    NO │
                    ┌───────▼─┐     │
                    │Dialog   │     │
                    │conflicto│     │
                    └─────────┘     │
                                    │
                          ┌─────────▼────┐
                          │ Upload a R2  │
                          │ (overwrite)  │
                          └──────┬───────┘
                                 │
                          ┌──────▼───────┐
                          │Update BD     │
                          │(size, date)  │
                          └──────┬───────┘
                                 │
                          ┌──────▼───────┐
                          │Toast:        │
                          │"Actualizado" │
                          └──────────────┘
```

---

## Archivos nuevos

| Archivo | Lineas est. | Descripcion |
|---------|-------------|-------------|
| `Services/Drive/FileWatcherService.cs` | ~250 | Monitoreo de cambios locales + auto-sync |
| `Models/DTOs/WatchedFile.cs` | ~30 | Modelo para manifest de archivos abiertos |
| `Models/DTOs/FileManifest.cs` | ~20 | Modelo para el manifest completo |

## Archivos a modificar

| Archivo | Cambio |
|---------|--------|
| `DriveV2Window.xaml.cs` | Doble-clic → Open-in-Place, badges sync, barra estado |
| `DriveV2Window.xaml` | StatusBar, badge overlays |
| `DriveService.cs` | Metodo para obtener `uploaded_at` para comparacion de conflictos |
| `SupabaseService.cs` | Registrar FileWatcherService |

## Consideraciones de testing

- Probar con archivos Word, Excel, PDF, imagenes
- Probar editar en Word → guardar → verificar que R2 se actualiza
- Probar cerrar Word sin guardar → verificar que no se sube nada
- Probar editar mientras otro usuario sube cambio → conflicto
- Probar con archivo grande (>50MB) → verificar timeout/progreso
- Probar cerrar la app con sync pendiente → dialog de advertencia
- Probar sin internet → error handling
