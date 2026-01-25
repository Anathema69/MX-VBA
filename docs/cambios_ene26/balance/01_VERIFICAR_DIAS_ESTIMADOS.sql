-- ============================================================
-- VERIFICACIÓN: Columna dias_estimados en t_order
-- Fecha: 26/01/2026
-- ============================================================
-- EJECUTAR PRIMERO para determinar si la columna existe
-- ============================================================

-- ============================================================
-- PASO 1: Verificar si la columna existe
-- ============================================================
SELECT
    column_name,
    data_type,
    is_nullable,
    column_default
FROM information_schema.columns
WHERE table_schema = 'public'
AND table_name = 't_order'
AND column_name = 'dias_estimados';

-- Si devuelve 0 filas = NO EXISTE, ejecutar 02_ALTER_DIAS_ESTIMADOS.sql
-- Si devuelve 1 fila = EXISTE

-- ============================================================
-- PASO 2: Si existe, verificar datos actuales
-- ============================================================
SELECT
    'Órdenes totales' as metrica,
    COUNT(*) as valor
FROM t_order
UNION ALL
SELECT
    'Con días estimados',
    COUNT(*)
FROM t_order
WHERE dias_estimados IS NOT NULL AND dias_estimados > 0
UNION ALL
SELECT
    'Sin días estimados (NULL)',
    COUNT(*)
FROM t_order
WHERE dias_estimados IS NULL
UNION ALL
SELECT
    'Días estimados = 0',
    COUNT(*)
FROM t_order
WHERE dias_estimados = 0;

-- ============================================================
-- PASO 3: Ver distribución de días estimados (si existe)
-- ============================================================
SELECT
    dias_estimados,
    COUNT(*) as cantidad_ordenes
FROM t_order
WHERE dias_estimados IS NOT NULL
GROUP BY dias_estimados
ORDER BY dias_estimados;

-- ============================================================
-- PASO 4: Ver órdenes recientes para entender el patrón
-- ============================================================
SELECT
    f_order,
    f_po,
    f_podate,
    f_estdelivery,
    f_saletotal,
    dias_estimados,
    -- Calcular días entre PO y entrega estimada
    CASE
        WHEN f_estdelivery IS NOT NULL AND f_podate IS NOT NULL
        THEN f_estdelivery - f_podate
        ELSE NULL
    END as dias_calculados
FROM t_order
WHERE f_podate >= '2025-01-01'
ORDER BY f_podate DESC
LIMIT 20;
