-- ============================================================
-- MIGRACIÓN: Agregar columna dias_estimados a t_order
-- Fecha: 26/01/2026
-- ============================================================
-- EJECUTAR SOLO SI 01_VERIFICAR_DIAS_ESTIMADOS.sql indica que NO existe
-- ============================================================

-- ============================================================
-- PASO 1: Agregar columna dias_estimados
-- ============================================================
ALTER TABLE t_order
ADD COLUMN IF NOT EXISTS dias_estimados INTEGER DEFAULT NULL;

COMMENT ON COLUMN t_order.dias_estimados IS
    'Días estimados para completar la orden. Usado para cálculo de utilidad aproximada.';

-- ============================================================
-- PASO 2: Opcional - Calcular días automáticamente para órdenes existentes
-- ============================================================
-- Basado en la diferencia entre fecha de entrega estimada y fecha de PO

/*
-- DESCOMENTAR SI SE DESEA CALCULAR AUTOMÁTICAMENTE:
UPDATE t_order
SET dias_estimados = f_estdelivery - f_podate
WHERE dias_estimados IS NULL
AND f_estdelivery IS NOT NULL
AND f_podate IS NOT NULL
AND f_estdelivery > f_podate
AND (f_estdelivery - f_podate) <= 365;  -- Máximo 1 año razonable
*/

-- ============================================================
-- PASO 3: Verificar resultado
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

-- ============================================================
-- PASO 4: Agregar a t_order_deleted para mantener consistencia
-- ============================================================
ALTER TABLE t_order_deleted
ADD COLUMN IF NOT EXISTS dias_estimados INTEGER;

-- ============================================================
-- VERIFICACIÓN FINAL
-- ============================================================
SELECT
    'Columna agregada correctamente' as resultado,
    COUNT(*) as ordenes_totales
FROM t_order;
