-- ============================================================
-- SCRIPT: Limpieza de archivos basura + carpeta fantasma en IMA Drive
-- ============================================================
-- INSTRUCCIONES:
--   1. Ejecutar PRIMERO las queries de diagnostico (seccion 1 y 2)
--   2. Revisar resultados antes de eliminar
--   3. Ejecutar la limpieza (seccion 3 y 4)
-- ============================================================


-- ============================================================
-- 1. DIAGNOSTICO: Archivos basura en drive_files
-- ============================================================
-- Archivos ~$ (lock files de SolidWorks/Office, 5-10 bytes)
-- Archivos .db (Thumbs.db de Windows)
-- Archivos .lck (lock files de CAD)

SELECT id, file_name, file_size, folder_id, uploaded_at,
       CASE
           WHEN file_name LIKE '~$%' THEN 'LOCK_FILE (SolidWorks/Office)'
           WHEN LOWER(file_name) LIKE '%.db' THEN 'THUMBS_DB (Windows)'
           WHEN LOWER(file_name) LIKE '%.lck' THEN 'LOCK_FILE (CAD)'
       END as tipo_basura
FROM drive_files
WHERE file_name LIKE '~$%'
   OR LOWER(file_name) LIKE '%.db'
   OR LOWER(file_name) LIKE '%.lck'
ORDER BY tipo_basura, file_name;


-- ============================================================
-- 2. DIAGNOSTICO: Carpeta fantasma "xTest"
-- ============================================================
-- Buscar la carpeta y su ubicacion en el arbol

-- 2a. Encontrar la carpeta
SELECT id, parent_id, name, linked_order_id, created_by, created_at
FROM drive_folders
WHERE LOWER(name) LIKE '%xtest%' OR LOWER(name) LIKE '%test%';

-- 2b. Ver su contenido (archivos y subcarpetas)
SELECT 'SUBCARPETA' as tipo, f.id, f.name as nombre, NULL::bigint as tamano, f.created_at
FROM drive_folders f
WHERE f.parent_id IN (SELECT id FROM drive_folders WHERE LOWER(name) LIKE '%xtest%')
UNION ALL
SELECT 'ARCHIVO' as tipo, df.id, df.file_name as nombre, df.file_size as tamano, df.uploaded_at
FROM drive_files df
WHERE df.folder_id IN (SELECT id FROM drive_folders WHERE LOWER(name) LIKE '%xtest%')
ORDER BY tipo, nombre;

-- 2c. Ver la ruta completa (padres hasta la raiz)
WITH RECURSIVE folder_path AS (
    SELECT id, parent_id, name, 1 as depth
    FROM drive_folders WHERE LOWER(name) LIKE '%xtest%'
    UNION ALL
    SELECT f.id, f.parent_id, f.name, fp.depth + 1
    FROM drive_folders f JOIN folder_path fp ON f.id = fp.parent_id
)
SELECT * FROM folder_path ORDER BY depth DESC;


-- ============================================================
-- 3. LIMPIEZA: Eliminar archivos basura
-- ============================================================
-- NOTA: Los blobs en R2 quedan huerfanos (costo negligible <1KB total)
-- El filtro de upload (MEJORA-2) previene que se vuelvan a subir

-- Primero contar cuantos se van a eliminar
SELECT COUNT(*) as total_basura,
       pg_size_pretty(SUM(file_size)) as tamano_total
FROM drive_files
WHERE file_name LIKE '~$%'
   OR LOWER(file_name) LIKE '%.db'
   OR LOWER(file_name) LIKE '%.lck';

-- EJECUTAR SOLO DESPUES DE VERIFICAR EL DIAGNOSTICO:
DELETE FROM drive_files
WHERE file_name LIKE '~$%'
   OR LOWER(file_name) LIKE '%.db'
   OR LOWER(file_name) LIKE '%.lck';


-- ============================================================
-- 4. LIMPIEZA: Eliminar carpeta fantasma "xTest"
-- ============================================================
-- IMPORTANTE: ON DELETE CASCADE eliminara subcarpetas y archivos automaticamente
-- Reemplaza {ID} con el id real de la carpeta (obtenido en seccion 2a)

-- EJECUTAR SOLO DESPUES DE VERIFICAR QUE ES LA CARPETA CORRECTA:
-- DELETE FROM drive_folders WHERE id = {ID};


-- ============================================================
-- 5. VERIFICACION POST-LIMPIEZA
-- ============================================================
-- Confirmar que no quedan archivos basura
SELECT COUNT(*) as basura_restante
FROM drive_files
WHERE file_name LIKE '~$%'
   OR LOWER(file_name) LIKE '%.db'
   OR LOWER(file_name) LIKE '%.lck';

-- Confirmar que la carpeta xTest fue eliminada
SELECT COUNT(*) as xtest_restante
FROM drive_folders
WHERE LOWER(name) LIKE '%xtest%';

-- Resumen final del estado de Drive
SELECT
    (SELECT COUNT(*) FROM drive_folders) as total_carpetas,
    (SELECT COUNT(*) FROM drive_files) as total_archivos,
    (SELECT pg_size_pretty(SUM(file_size)) FROM drive_files) as tamano_total;
