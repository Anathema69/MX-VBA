# Fase V3-C: Operaciones de Archivos

**Estado:** PENDIENTE
**Prioridad:** 2
**Archivos clave:** `DriveV2Window.xaml.cs`, `DriveService.cs`, SQL nuevo

---

## Checklist de implementacion

### C1: Mover archivos/carpetas
- [ ] Context menu: agregar "Mover a..." en menu de archivo Y carpeta
- [ ] Crear `FolderPickerDialog` — Window estilo IMA con TreeView
- [ ] TreeView lazy-load: cargar hijos de carpeta al expandir (no todo el arbol)
- [ ] Nodo raiz "IMA MECATRONICA" siempre visible y expandido
- [ ] Carpetas con icono + nombre + badge "VINCULADA" si aplica
- [ ] Carpeta actual deshabilitada (no puedes mover a donde ya estas)
- [ ] Boton "Mover aqui" en la parte inferior del dialog
- [ ] Para archivos: `UPDATE drive_files SET folder_id = @dest WHERE id = @id`
- [ ] Para carpetas: `UPDATE drive_folders SET parent_id = @dest WHERE id = @id`
- [ ] Validar UNIQUE constraint (no mover si ya existe archivo con mismo nombre en destino)
- [ ] Si existe duplicado: preguntar "Ya existe un archivo con ese nombre. Reemplazar?"
- [ ] SQL RPC: `validate_folder_move(folder_id, target_parent_id)` — no mover dentro de si mismo/descendiente
- [ ] Multi-select: mover multiples archivos seleccionados con un solo dialog
- [ ] Toast: "3 archivos movidos a 'Planos'" o "Carpeta 'Fotos' movida a 'OC-001'"
- [ ] Registrar en drive_activity: action='move', metadata={from_folder, to_folder}
- [ ] Reload carpeta actual despues de mover

### C2: Copiar archivos
- [ ] Context menu: "Copiar a..." (solo archivos, no carpetas)
- [ ] Reusar `FolderPickerDialog` de C1
- [ ] Copiar registro en drive_files: nuevo ID, nuevo folder_id, nuevo storage_path
- [ ] Copiar blob en R2: `S3Client.CopyObjectAsync(sourceBucket, sourceKey, destBucket, destKey)`
- [ ] Nuevo storage_path: `{destFolderId}/{timestamp}_{fileName}`
- [ ] Si nombre duplicado en destino: agregar " (copia)" antes de la extension
- [ ] Actualizar storage counter (+file_size)
- [ ] Toast: "contrato.pdf copiado a 'Planos'"
- [ ] Registrar en drive_activity: action='copy'

### C3: Drag-drop interno (entre carpetas)
- [ ] En cards/rows de carpeta: `AllowDrop = true`
- [ ] Handlers: `DragEnter`, `DragLeave`, `Drop`
- [ ] `DragEnter`: resaltar carpeta destino (borde azul, fondo azul claro)
- [ ] `DragLeave`: quitar resaltado
- [ ] `Drop`: ejecutar Move (misma logica que C1 pero sin dialog)
- [ ] Iniciar drag: `DragDrop.DoDragDrop(source, data, DragDropEffects.Move)`
- [ ] Data format: `DataFormats.Serializable` con lista de file/folder IDs
- [ ] Detectar tipo: si arrastra archivo → mover archivo; si arrastra carpeta → mover carpeta
- [ ] Validar: no drop en la misma carpeta, no carpeta dentro de si misma
- [ ] Visual feedback durante drag: cursor cambia a Move
- [ ] No confundir con drag-drop externo (subida de archivos desde escritorio — ya existe)

### C4: Cortar/Copiar/Pegar con teclado
- [ ] `Ctrl+X`: guardar archivos seleccionados en `_clipboard` con operacion `Cut`
- [ ] `Ctrl+C`: guardar archivos seleccionados en `_clipboard` con operacion `Copy`
- [ ] `Ctrl+V`: ejecutar Move (si Cut) o Copy (si Copy) a la carpeta actual
- [ ] Visual: archivos cortados se muestran con opacity 0.5
- [ ] Visual: badge "Copiado" en sidebar o status bar: "2 archivos listos para pegar"
- [ ] Limpiar clipboard despues de pegar exitoso (Cut) o mantener (Copy)
- [ ] Escape cancela el cortar/copiar pendiente
- [ ] Estado: `_clipboard: (List<DriveFileDb> files, ClipboardOp op)` donde `ClipboardOp { Cut, Copy }`

### C5: Duplicar archivo
- [ ] Context menu: "Duplicar" (solo archivos, no carpetas)
- [ ] Copiar en la MISMA carpeta
- [ ] Nombre: "archivo (copia).ext"; si ya existe: "archivo (copia 2).ext", etc.
- [ ] Misma logica interna que C2 pero sin dialog
- [ ] Toast: "contrato (copia).pdf creado"

---

## SQL

```sql
-- sql/drive_v3_operations.sql

-- Validar que una carpeta se puede mover a un destino
CREATE OR REPLACE FUNCTION validate_folder_move(
    p_folder_id INTEGER,
    p_target_id INTEGER
) RETURNS TABLE(can_move BOOLEAN, block_reason TEXT) AS $$
DECLARE
    v_is_descendant BOOLEAN;
    v_same_parent BOOLEAN;
BEGIN
    -- No mover a si mismo
    IF p_folder_id = p_target_id THEN
        RETURN QUERY SELECT FALSE, 'No se puede mover una carpeta dentro de si misma'::TEXT;
        RETURN;
    END IF;

    -- Verificar si target es descendiente de folder (crearia ciclo)
    WITH RECURSIVE descendants AS (
        SELECT id FROM drive_folders WHERE parent_id = p_folder_id
        UNION ALL
        SELECT df.id FROM drive_folders df
        JOIN descendants d ON df.parent_id = d.id
    )
    SELECT EXISTS(SELECT 1 FROM descendants WHERE id = p_target_id)
    INTO v_is_descendant;

    IF v_is_descendant THEN
        RETURN QUERY SELECT FALSE, 'No se puede mover una carpeta dentro de sus subcarpetas'::TEXT;
        RETURN;
    END IF;

    -- Verificar si ya esta en el destino (no-op)
    SELECT parent_id = p_target_id INTO v_same_parent
    FROM drive_folders WHERE id = p_folder_id;

    IF v_same_parent THEN
        RETURN QUERY SELECT FALSE, 'La carpeta ya esta en esta ubicacion'::TEXT;
        RETURN;
    END IF;

    RETURN QUERY SELECT TRUE, NULL::TEXT;
END;
$$ LANGUAGE plpgsql;
```

## FolderPickerDialog — Mockup

```
+------------------------------------------+
| Mover a...                          [X]  |
+------------------------------------------+
|                                          |
|  v IMA MECATRONICA                       |
|    v Ordenes 2025                        |
|      > OC-001 (Planos)     [VINCULADA]   |
|      > OC-002                            |
|    > Ordenes 2026                        |
|    > Administracion                      |
|    > Recursos Humanos                    |
|                                          |
+------------------------------------------+
| Destino: Ordenes 2025 / OC-001           |
| [Cancelar]              [Mover aqui]     |
+------------------------------------------+
```
