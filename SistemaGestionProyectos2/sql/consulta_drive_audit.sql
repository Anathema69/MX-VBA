-- ============================================
-- CONSULTA: Auditoria del modulo ARCHIVOS
-- Ejecutar en Supabase SQL Editor
-- ============================================

-- 1. Todas las acciones ordenadas cronologicamente
SELECT
    da.id,
    da.created_at AS fecha,
    da.action AS accion,
    da.target_type AS tipo,
    da.target_name AS nombre,
    da.old_value AS valor_anterior,
    da.new_value AS valor_nuevo,
    da.folder_id AS carpeta_padre,
    u.username AS usuario
FROM drive_audit da
LEFT JOIN users u ON da.user_id = u.id
ORDER BY da.created_at DESC;

-- 2. Resumen por tipo de accion
SELECT
    action AS accion,
    target_type AS tipo,
    COUNT(*) AS total
FROM drive_audit
GROUP BY action, target_type
ORDER BY total DESC;

-- 3. Actividad por usuario
SELECT
    u.username AS usuario,
    COUNT(*) AS total_acciones,
    COUNT(*) FILTER (WHERE da.action = 'CREATE') AS creaciones,
    COUNT(*) FILTER (WHERE da.action = 'UPLOAD') AS uploads,
    COUNT(*) FILTER (WHERE da.action = 'RENAME') AS renombres,
    COUNT(*) FILTER (WHERE da.action = 'DELETE') AS eliminaciones,
    COUNT(*) FILTER (WHERE da.action = 'LINK') AS vinculaciones,
    COUNT(*) FILTER (WHERE da.action = 'UNLINK') AS desvinculaciones
FROM drive_audit da
LEFT JOIN users u ON da.user_id = u.id
GROUP BY u.username
ORDER BY total_acciones DESC;

-- 4. Estado actual: carpetas con sus ordenes vinculadas
SELECT
    df.id,
    df.name AS carpeta,
    df.linked_order_id AS orden_vinculada,
    o.f_po AS numero_oc,
    df.created_at,
    u.username AS creada_por
FROM drive_folders df
LEFT JOIN t_order o ON df.linked_order_id = o.f_order
LEFT JOIN users u ON df.created_by = u.id
WHERE df.parent_id = (SELECT id FROM drive_folders WHERE parent_id IS NULL LIMIT 1)
ORDER BY df.name;

-- 5. Estado actual: archivos por carpeta
SELECT
    df.name AS carpeta,
    dfi.file_name AS archivo,
    dfi.file_size AS tamano_bytes,
    dfi.content_type AS tipo,
    dfi.uploaded_at AS fecha_subida,
    u.username AS subido_por
FROM drive_files dfi
JOIN drive_folders df ON dfi.folder_id = df.id
LEFT JOIN users u ON dfi.uploaded_by = u.id
ORDER BY df.name, dfi.uploaded_at DESC;
