-- ============================================
-- DRIVE V2 - Optimizacion de rendimiento
-- Ejecutar en Supabase SQL Editor
-- Fecha: 2026-03-10
-- ============================================

-- ============================================
-- PASO 1: Funcion RPC para stats de carpetas
-- Reemplaza N+1 queries (2 por carpeta) con 1 sola query
-- ============================================

CREATE OR REPLACE FUNCTION get_folder_stats(p_parent_id INTEGER)
RETURNS TABLE(
    folder_id INTEGER,
    file_count BIGINT,
    subfolder_count BIGINT,
    total_size BIGINT
) AS $$
    SELECT
        df.id AS folder_id,
        COALESCE(fi.cnt, 0) AS file_count,
        COALESCE(sf.cnt, 0) AS subfolder_count,
        COALESCE(fi.sz, 0) AS total_size
    FROM drive_folders df
    LEFT JOIN (
        SELECT folder_id, COUNT(*) AS cnt, COALESCE(SUM(file_size), 0) AS sz
        FROM drive_files
        GROUP BY folder_id
    ) fi ON fi.folder_id = df.id
    LEFT JOIN (
        SELECT parent_id, COUNT(*) AS cnt
        FROM drive_folders
        GROUP BY parent_id
    ) sf ON sf.parent_id = df.id
    WHERE df.parent_id = p_parent_id;
$$ LANGUAGE sql STABLE;

-- Verificar: SELECT * FROM get_folder_stats(1);
-- Donde 1 = id de la carpeta raiz (IMA MECATRONICA)

-- ============================================
-- PASO 2: Funcion RPC para breadcrumb optimizado
-- Reemplaza N queries secuenciales con 1 CTE recursivo
-- NOTA: Ya existe get_folder_breadcrumb en bloque5_drive.sql
-- pero no retorna todos los campos necesarios. Esta version
-- retorna tambien linked_order_id, created_by, created_at, updated_at
-- ============================================

CREATE OR REPLACE FUNCTION get_folder_breadcrumb_full(p_folder_id INTEGER)
RETURNS TABLE(
    id INTEGER,
    parent_id INTEGER,
    name VARCHAR,
    linked_order_id INTEGER,
    created_by INTEGER,
    created_at TIMESTAMP,
    updated_at TIMESTAMP,
    depth INTEGER
) AS $$
WITH RECURSIVE breadcrumb AS (
    SELECT
        df.id, df.parent_id, df.name, df.linked_order_id,
        df.created_by, df.created_at, df.updated_at,
        0 AS depth
    FROM drive_folders df
    WHERE df.id = p_folder_id

    UNION ALL

    SELECT
        df.id, df.parent_id, df.name, df.linked_order_id,
        df.created_by, df.created_at, df.updated_at,
        b.depth + 1
    FROM drive_folders df
    JOIN breadcrumb b ON df.id = b.parent_id
)
SELECT b.id, b.parent_id, b.name, b.linked_order_id,
       b.created_by, b.created_at, b.updated_at, b.depth
FROM breadcrumb b
ORDER BY b.depth DESC;
$$ LANGUAGE sql STABLE;

-- Verificar: SELECT * FROM get_folder_breadcrumb_full(5);
-- Retorna la ruta desde raiz hasta la carpeta 5, con todos los campos

-- ============================================
-- PASO 3: Funcion RPC para obtener ordenes por IDs (batch)
-- Reemplaza N queries individuales GetOrderById con 1 sola
-- ============================================

CREATE OR REPLACE FUNCTION get_orders_by_ids(p_order_ids INTEGER[])
RETURNS TABLE(
    f_order INTEGER,
    f_po VARCHAR,
    f_client INTEGER,
    f_description TEXT
) AS $$
    SELECT o.f_order, o.f_po, o.f_client, o.f_description
    FROM t_order o
    WHERE o.f_order = ANY(p_order_ids);
$$ LANGUAGE sql STABLE;

-- Verificar: SELECT * FROM get_orders_by_ids(ARRAY[1, 2, 3]);
