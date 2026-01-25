-- =====================================================
-- GASTO MATERIAL - VISTA OPTIMIZADA
-- Ejecutar en Supabase SQL Editor (PRODUCCIÓN)
-- =====================================================

-- PASO 1: Crear índice para optimizar consultas de gastos por orden
-- Este índice acelera significativamente el GROUP BY y filtros
CREATE INDEX IF NOT EXISTS idx_expense_order_status
ON t_expense(f_order, f_status)
WHERE f_order IS NOT NULL;

-- Índice adicional para ordenar por fecha
CREATE INDEX IF NOT EXISTS idx_expense_order_date
ON t_expense(f_order, f_expensedate DESC)
WHERE f_order IS NOT NULL;

-- PASO 2: Crear vista que calcula gasto_material por orden
-- Esta vista se puede consultar como una tabla normal
 -- Eliminar vista existente
  
DROP VIEW IF EXISTS v_order_gastos;

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
      o.created_at,
      o.updated_by,
      o.updated_at,
      COALESCE(g.gasto_material_pagado, 0) AS gasto_material,
      COALESCE(g.gasto_material_pendiente, 0) AS gasto_material_pendiente,
      COALESCE(g.total_gastos, 0) AS total_gastos_proveedor,
      COALESCE(g.num_facturas, 0) AS num_facturas_proveedor
  FROM t_order o
  LEFT JOIN (
      SELECT
          f_order,
          SUM(CASE WHEN f_status = 'PAGADO' THEN f_totalexpense ELSE 0 END) AS gasto_material_pagado,
          SUM(CASE WHEN f_status = 'PENDIENTE' THEN f_totalexpense ELSE 0 END) AS gasto_material_pendiente,
          SUM(f_totalexpense) AS total_gastos,
          COUNT(*) AS num_facturas
      FROM t_expense
      WHERE f_order IS NOT NULL
      GROUP BY f_order
  ) g ON o.f_order = g.f_order;
  
  
-- PASO 3: Verificar que la vista funciona
-- Mostrar órdenes con gastos
SELECT
    f_order,
    f_po AS orden,
    f_saletotal AS venta,
    gasto_material,
    gasto_material_pendiente,
    f_saletotal - gasto_material AS utilidad_bruta,
    num_facturas_proveedor
FROM v_order_gastos
WHERE gasto_material > 0 OR gasto_material_pendiente > 0
ORDER BY f_podate DESC
LIMIT 10;

-- PASO 4: Test de rendimiento (debería ser < 100ms)
EXPLAIN ANALYZE
SELECT f_order, f_po, gasto_material
FROM v_order_gastos
WHERE f_orderstat IN (0, 1, 2, 3, 4)
ORDER BY f_podate DESC
LIMIT 100;

-- =====================================================
-- NOTAS:
-- - La vista v_order_gastos incluye TODOS los campos de t_order
--   más los campos calculados de gastos
-- - gasto_material = suma de gastos PAGADOS a proveedores
-- - gasto_material_pendiente = suma de gastos PENDIENTES
-- - El C# puede consultar esta vista en lugar de t_order
--   para obtener los gastos calculados automáticamente
-- =====================================================
