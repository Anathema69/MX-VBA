-- ============================================================================
-- CONSULTA: Detalle de gastos operativos e indirectos por orden
-- ============================================================================

-- Detalle de órdenes con gastos operativos en Enero 2026
SELECT
    o.f_order AS orden_id,
    o.f_po AS po,
    o.f_description AS descripcion,
    o.f_podate AS fecha_orden,
    o.f_saletotal AS venta_total,
    COALESCE(o.gasto_operativo, 0) AS gasto_operativo,
    COALESCE(o.gasto_indirecto, 0) AS gasto_indirecto,
    COALESCE(o.gasto_operativo, 0) + COALESCE(o.gasto_indirecto, 0) AS total_gastos_orden
FROM t_order o
WHERE o.f_podate >= '2026-01-01'
  AND o.f_podate < '2026-02-01'
  AND (COALESCE(o.gasto_operativo, 0) > 0 OR COALESCE(o.gasto_indirecto, 0) > 0)
ORDER BY o.f_podate, o.f_order;

-- Resumen por mes (para verificar totales)
SELECT
    TO_CHAR(o.f_podate, 'YYYY-MM') AS mes,
    COUNT(*) AS num_ordenes,
    SUM(COALESCE(o.gasto_operativo, 0)) AS total_gasto_operativo,
    SUM(COALESCE(o.gasto_indirecto, 0)) AS total_gasto_indirecto
FROM t_order o
WHERE o.f_podate >= '2026-01-01'
  AND o.f_podate < '2026-02-01'
  AND (COALESCE(o.gasto_operativo, 0) > 0 OR COALESCE(o.gasto_indirecto, 0) > 0)
GROUP BY TO_CHAR(o.f_podate, 'YYYY-MM')
ORDER BY mes;
