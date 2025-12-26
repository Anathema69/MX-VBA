-- ============================================================
-- ANÁLISIS PARTE 2: Historial y dependencias de órdenes
-- Fecha: 2025-12-23
-- ============================================================

-- 1. Ver estructura de order_history
SELECT column_name, data_type, is_nullable
FROM information_schema.columns
WHERE table_name = 'order_history'
ORDER BY ordinal_position;

-- 2. Ver definición de la función record_order_history
SELECT pg_get_functiondef(oid)
FROM pg_proc
WHERE proname = 'record_order_history';

-- 3. Ver foreign keys que APUNTAN a t_order (tablas dependientes)
SELECT
    tc.constraint_name,
    tc.table_name as tabla_dependiente,
    kcu.column_name as columna_fk,
    rc.delete_rule as regla_delete
FROM information_schema.table_constraints tc
JOIN information_schema.key_column_usage kcu
    ON tc.constraint_name = kcu.constraint_name
JOIN information_schema.referential_constraints rc
    ON tc.constraint_name = rc.constraint_name
JOIN information_schema.constraint_column_usage ccu
    ON ccu.constraint_name = tc.constraint_name
WHERE tc.constraint_type = 'FOREIGN KEY'
  AND ccu.table_name = 't_order';

-- 4. Ver un ejemplo de registro en order_history (si existe)
SELECT * FROM order_history ORDER BY created_at DESC LIMIT 5;

-- 5. Contar órdenes por estado actual
SELECT
    os.f_orderstatus as estado_id,
    os.f_name as estado_nombre,
    COUNT(o.f_order) as cantidad
FROM order_status os
LEFT JOIN t_order o ON o.f_ordstat = os.f_orderstatus
GROUP BY os.f_orderstatus, os.f_name
ORDER BY os.f_orderstatus;

-- 6. Ver si hay órdenes con facturas asociadas (para evaluar impacto de delete)
SELECT
    o.f_order,
    o.f_po,
    o.f_ordstat,
    COUNT(i.f_invoice) as num_facturas,
    COUNT(e.f_expense) as num_gastos
FROM t_order o
LEFT JOIN t_invoice i ON i.f_order = o.f_order
LEFT JOIN t_expense e ON e.f_order = o.f_order
GROUP BY o.f_order, o.f_po, o.f_ordstat
HAVING COUNT(i.f_invoice) > 0 OR COUNT(e.f_expense) > 0
ORDER BY o.f_order DESC
LIMIT 20;
