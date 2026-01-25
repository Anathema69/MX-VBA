-- ============================================
-- Setup completo para Gastos Indirectos
-- Fecha: 2026-01-25
-- Autor: Equipo IMA Mecatronica
-- ============================================
--
-- Este script implementa:
-- 1. Tabla de detalle para gastos indirectos manuales
-- 2. Columna suma en t_order
-- 3. Actualiza vista v_order_gastos
--
-- La app C# actualiza t_order.gasto_indirecto despues de cada CRUD
-- (mismo patron que gasto_operativo)
-- ============================================

-- ============================================
-- PASO 1: Crear tabla de detalle
-- ============================================
CREATE TABLE IF NOT EXISTS order_gastos_indirectos (
    id SERIAL PRIMARY KEY,
    f_order INTEGER NOT NULL REFERENCES t_order(f_order) ON DELETE CASCADE,
    monto NUMERIC(15,2) NOT NULL,
    descripcion VARCHAR(255) NOT NULL,
    fecha_gasto TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    created_by INTEGER
);

-- Indice para busquedas por orden
CREATE INDEX IF NOT EXISTS idx_gastos_indirectos_order
ON order_gastos_indirectos(f_order);

-- ============================================
-- PASO 2: Agregar columna en t_order
-- ============================================
ALTER TABLE t_order
ADD COLUMN IF NOT EXISTS gasto_indirecto NUMERIC(15,2) DEFAULT 0;

-- ============================================
-- PASO 3: Actualizar vista v_order_gastos
-- ============================================
-- Agrega gasto_indirecto manteniendo estructura existente

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
    -- Gastos de la orden
    COALESCE(o.gasto_operativo, 0) AS gasto_operativo,
    COALESCE(o.gasto_indirecto, 0) AS gasto_indirecto,
    -- Gastos de proveedores (t_expense)
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

-- ============================================
-- PASO 4: Verificar instalacion
-- ============================================
SELECT 'Tabla order_gastos_indirectos' AS componente,
       CASE WHEN EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'order_gastos_indirectos')
            THEN 'OK' ELSE 'FALTA' END AS estado
UNION ALL
SELECT 'Columna t_order.gasto_indirecto',
       CASE WHEN EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 't_order' AND column_name = 'gasto_indirecto')
            THEN 'OK' ELSE 'FALTA' END
UNION ALL
SELECT 'Vista v_order_gastos.gasto_indirecto',
       CASE WHEN EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'v_order_gastos' AND column_name = 'gasto_indirecto')
            THEN 'OK' ELSE 'FALTA' END;

-- ============================================
-- NOTAS PARA FORMULAS FUTURAS
-- ============================================
--
-- Si en el futuro se requiere una formula tipo:
--   gasto_indirecto = (A + B/C) + suma_manual
--
-- Modificar la vista asi:
--
-- CREATE OR REPLACE VIEW v_order_gastos AS
-- SELECT
--     ...
--     -- Gasto indirecto con formula
--     COALESCE(o.gasto_indirecto, 0) + (
--         COALESCE(A, 0) + COALESCE(B / NULLIF(C, 0), 0)
--     ) AS gasto_indirecto,
--     ...
--
-- Donde:
--   o.gasto_indirecto = suma de order_gastos_indirectos (manual)
--   (A + B/C) = formula calculada
--
-- La app C# no necesita cambios, solo lee el total de la vista.
-- ============================================
