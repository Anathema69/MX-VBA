-- ============================================
-- Actualizar vista v_order_gastos para incluir gasto_operativo
-- Fecha: 2026-01-24
-- ============================================

-- Recrear la vista con el nuevo campo gasto_operativo
CREATE OR REPLACE VIEW v_order_gastos AS
SELECT
    o.f_order,
    o.f_po,
    o.f_quote,
    o.f_podate,
    o.f_client,
    o.f_contact,
    o.f_description,
    o.f_salesman,
    o.f_estdelivery,
    o.f_salesubtotal,
    o.f_saletotal,
    o.f_orderstat,
    o.progress_percentage,
    o.order_percentage,
    o.f_commission_rate,
    o.created_by,
    o.updated_by,
    o.created_at,
    o.updated_at,
    -- Campo gasto_operativo de t_order (suma de order_gastos_operativos)
    COALESCE(o.gasto_operativo, 0) AS gasto_operativo,
    -- Campos calculados de gastos a proveedores
    COALESCE(g.gasto_material, 0) AS gasto_material,
    COALESCE(g.gasto_material_pendiente, 0) AS gasto_material_pendiente,
    COALESCE(g.total_gastos_proveedor, 0) AS total_gastos_proveedor,
    COALESCE(g.num_facturas_proveedor, 0) AS num_facturas_proveedor
FROM t_order o
LEFT JOIN (
    SELECT
        e.f_order,
        SUM(CASE WHEN e.f_expense_status = 'PAGADO' THEN COALESCE(e.f_total, 0) ELSE 0 END) AS gasto_material,
        SUM(CASE WHEN e.f_expense_status = 'PENDIENTE' THEN COALESCE(e.f_total, 0) ELSE 0 END) AS gasto_material_pendiente,
        SUM(COALESCE(e.f_total, 0)) AS total_gastos_proveedor,
        COUNT(e.f_expense) AS num_facturas_proveedor
    FROM t_expense e
    WHERE e.f_order IS NOT NULL
    GROUP BY e.f_order
) g ON o.f_order = g.f_order;

-- Verificar resultado
SELECT
    f_order,
    f_po,
    gasto_operativo,
    gasto_material
FROM v_order_gastos
WHERE gasto_operativo > 0 OR gasto_material > 0
LIMIT 10;
