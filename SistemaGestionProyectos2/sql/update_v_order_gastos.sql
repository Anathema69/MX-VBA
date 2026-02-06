-- ============================================================
-- SCRIPT: Actualizar vista v_order_gastos para incluir f_commission_rate
-- ============================================================
-- INSTRUCCIONES:
--   1. Ejecutar en Supabase SQL Editor
--   2. Verificar que la vista incluya f_commission_rate
-- ============================================================

-- Verificar si la vista actual tiene f_commission_rate
SELECT column_name
FROM information_schema.columns
WHERE table_name = 'v_order_gastos'
  AND column_name = 'f_commission_rate';

-- Si el query anterior no devuelve resultados, ejecutar este CREATE OR REPLACE:

CREATE OR REPLACE VIEW v_order_gastos AS
SELECT o.f_order,
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
    o.f_commission_rate,  -- IMPORTANTE: Campo de comision del vendedor
    o.created_by,
    o.created_at,
    o.updated_by,
    o.updated_at,
    COALESCE(o.gasto_operativo, 0::numeric) AS gasto_operativo,
    COALESCE(o.gasto_indirecto, 0::numeric) AS gasto_indirecto,
    COALESCE(g.gasto_material_pagado, 0::numeric) AS gasto_material,
    COALESCE(g.gasto_material_pendiente, 0::numeric) AS gasto_material_pendiente,
    COALESCE(g.total_gastos, 0::numeric) AS total_gastos_proveedor,
    COALESCE(g.num_facturas, 0::bigint) AS num_facturas_proveedor
FROM t_order o
LEFT JOIN (
    SELECT t_expense.f_order,
        sum(CASE WHEN t_expense.f_status::text = 'PAGADO'::text THEN t_expense.f_totalexpense ELSE 0::numeric END) AS gasto_material_pagado,
        sum(CASE WHEN t_expense.f_status::text = 'PENDIENTE'::text THEN t_expense.f_totalexpense ELSE 0::numeric END) AS gasto_material_pendiente,
        sum(t_expense.f_totalexpense) AS total_gastos,
        count(*) AS num_facturas
    FROM t_expense
    WHERE t_expense.f_order IS NOT NULL
    GROUP BY t_expense.f_order
) g ON o.f_order = g.f_order;

-- Verificar que se haya agregado correctamente
SELECT column_name, data_type
FROM information_schema.columns
WHERE table_name = 'v_order_gastos'
ORDER BY ordinal_position;

-- ============================================================
-- ÍNDICE: Mejorar rendimiento de búsquedas por fecha (f_podate)
-- ============================================================
-- Este índice mejora las consultas que filtran por año y mes

-- Verificar si el índice ya existe
SELECT indexname FROM pg_indexes WHERE tablename = 't_order' AND indexname = 'idx_order_podate';

-- Crear índice si no existe
CREATE INDEX IF NOT EXISTS idx_order_podate ON t_order (f_podate);

-- Índice compuesto para filtros de estado + fecha (común en la app)
CREATE INDEX IF NOT EXISTS idx_order_status_podate ON t_order (f_orderstat, f_podate);

-- Verificar índices creados
SELECT indexname, indexdef
FROM pg_indexes
WHERE tablename = 't_order'
ORDER BY indexname;
