-- ============================================================
-- QUERIES DE VERIFICACION: Integridad IMA Drive (BD + R2)
-- ============================================================
-- Ejecutar despues de operaciones de sync/overwrite para
-- verificar que no hay huerfanos ni inconsistencias.
-- ============================================================


-- ============================================================
-- 1. ARCHIVOS HUERFANOS EN BD (sin carpeta padre)
-- ============================================================
-- Archivos cuyo folder_id no existe en drive_folders
-- (no deberia pasar por CASCADE, pero por si acaso)

SELECT df.id, df.file_name, df.folder_id, df.storage_path
FROM drive_files df
LEFT JOIN drive_folders fo ON df.folder_id = fo.id
WHERE fo.id IS NULL;


-- ============================================================
-- 2. CARPETAS HUERFANAS (padre no existe, no es raiz)
-- ============================================================
-- Carpetas cuyo parent_id no es NULL pero el padre no existe

SELECT f.id, f.name, f.parent_id
FROM drive_folders f
LEFT JOIN drive_folders p ON f.parent_id = p.id
WHERE f.parent_id IS NOT NULL AND p.id IS NULL;


-- ============================================================
-- 3. ARCHIVOS DUPLICADOS EN MISMA CARPETA
-- ============================================================
-- No deberia existir por UNIQUE(folder_id, file_name)
-- pero verificamos por si hay constraint roto

SELECT folder_id, file_name, COUNT(*) as cantidad
FROM drive_files
GROUP BY folder_id, file_name
HAVING COUNT(*) > 1;


-- ============================================================
-- 4. ARCHIVOS CON STORAGE_PATH DUPLICADO
-- ============================================================
-- Dos registros apuntando al mismo blob en R2

SELECT storage_path, COUNT(*) as cantidad,
       ARRAY_AGG(id) as file_ids,
       ARRAY_AGG(file_name) as nombres
FROM drive_files
GROUP BY storage_path
HAVING COUNT(*) > 1;


-- ============================================================
-- 5. BLOBS POTENCIALMENTE HUERFANOS EN R2
-- ============================================================
-- No podemos consultar R2 desde SQL, pero podemos detectar
-- storage_paths que ya no deberian existir.
-- Compara esta lista con los objetos reales en R2.

-- Archivos eliminados recientemente (via drive_audit)
SELECT da.target_name, da.metadata, da.created_at
FROM drive_audit da
WHERE da.action = 'DELETE' AND da.target_type = 'FILE'
ORDER BY da.created_at DESC
LIMIT 20;


-- ============================================================
-- 6. RESUMEN DE INTEGRIDAD
-- ============================================================

SELECT
    (SELECT COUNT(*) FROM drive_folders) as total_carpetas,
    (SELECT COUNT(*) FROM drive_files) as total_archivos,
    (SELECT pg_size_pretty(COALESCE(SUM(file_size), 0)) FROM drive_files) as tamano_total,
    (SELECT COUNT(*) FROM drive_files df
     LEFT JOIN drive_folders fo ON df.folder_id = fo.id
     WHERE fo.id IS NULL) as archivos_huerfanos,
    (SELECT COUNT(*) FROM drive_folders f
     LEFT JOIN drive_folders p ON f.parent_id = p.id
     WHERE f.parent_id IS NOT NULL AND p.id IS NULL) as carpetas_huerfanas;


-- ============================================================
-- 7. VERIFICAR CARPETA ESPECIFICA POST-SYNC
-- ============================================================
-- Reemplaza {FOLDER_ID} con el ID de la carpeta que sincronizaste

-- SELECT f.id, f.name,
--        (SELECT COUNT(*) FROM drive_files WHERE folder_id = f.id) as archivos,
--        (SELECT COUNT(*) FROM drive_folders WHERE parent_id = f.id) as subcarpetas
-- FROM drive_folders f
-- WHERE f.id = {FOLDER_ID}
--    OR f.parent_id = {FOLDER_ID}
-- ORDER BY f.name;


-- ============================================================
-- 8. ACTIVIDAD RECIENTE (ultimas 20 acciones)
-- ============================================================

SELECT id, action, target_type, target_name,
       folder_id, metadata::text, created_at
FROM drive_activity
ORDER BY created_at DESC
LIMIT 20;
