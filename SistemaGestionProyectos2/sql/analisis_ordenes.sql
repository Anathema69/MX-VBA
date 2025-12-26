-- ============================================================
-- ANÁLISIS: Estructura de órdenes y estados
-- Fecha: 2025-12-23
-- ============================================================

-- 1. Ver todos los estados de orden disponibles
SELECT * FROM t_orderstatus ORDER BY f_orderstatus;

-- 2. Ver estructura de la tabla de órdenes
SELECT column_name, data_type, is_nullable, column_default
FROM information_schema.columns
WHERE table_name = 't_order'
ORDER BY ordinal_position;

-- 3. Ver si existe tabla de historial de órdenes
SELECT table_name
FROM information_schema.tables
WHERE table_name LIKE '%order%history%'
   OR table_name LIKE '%order%log%'
   OR table_name LIKE '%order%audit%';

-- 4. Ver triggers en t_order
SELECT trigger_name, event_manipulation, action_statement
FROM information_schema.triggers
WHERE event_object_table = 't_order';

-- 5. Contar órdenes por estado
SELECT
    os.f_orderstatus as estado_id,
    os.f_status as estado_nombre,
    COUNT(o.f_order) as cantidad
FROM t_orderstatus os
LEFT JOIN t_order o ON o.f_ordstat = os.f_orderstatus
GROUP BY os.f_orderstatus, os.f_status
ORDER BY os.f_orderstatus;

-- 6. Ver si hay órdenes canceladas actualmente
SELECT f_order, f_po, f_ordstat, f_orderdate
FROM t_order
WHERE f_ordstat = 5  -- Asumiendo que 5 es CANCELADA
ORDER BY f_orderdate DESC
LIMIT 10;

-- 7. Ver foreign keys que dependen de t_order
SELECT
    tc.constraint_name,
    tc.table_name as tabla_dependiente,
    kcu.column_name as columna_fk,
    ccu.table_name as tabla_referenciada
FROM information_schema.table_constraints tc
JOIN information_schema.key_column_usage kcu
    ON tc.constraint_name = kcu.constraint_name
JOIN information_schema.constraint_column_usage ccu
    ON ccu.constraint_name = tc.constraint_name
WHERE tc.constraint_type = 'FOREIGN KEY'
  AND ccu.table_name = 't_order';
