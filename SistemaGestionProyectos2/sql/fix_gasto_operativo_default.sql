-- ============================================
-- Fix: Valor por defecto para gasto_operativo
-- Fecha: 2026-01-24
-- ============================================

-- 1. Verificar si la columna existe y agregar default
ALTER TABLE t_order
ALTER COLUMN gasto_operativo SET DEFAULT 0;

-- 2. Actualizar registros existentes que tengan NULL
UPDATE t_order
SET gasto_operativo = 0
WHERE gasto_operativo IS NULL;

-- 3. Verificar el resultado
SELECT
    column_name,
    column_default,
    is_nullable,
    data_type
FROM information_schema.columns
WHERE table_name = 't_order'
AND column_name = 'gasto_operativo';
