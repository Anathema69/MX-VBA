-- ============================================
-- Fix: Cambiar fecha_gasto de DATE a TIMESTAMP
-- Fecha: 2026-01-25
-- ============================================

-- Cambiar el tipo de columna de DATE a TIMESTAMP
ALTER TABLE order_gastos_operativos
ALTER COLUMN fecha_gasto TYPE TIMESTAMP USING fecha_gasto::TIMESTAMP;

-- Cambiar el default a CURRENT_TIMESTAMP
ALTER TABLE order_gastos_operativos
ALTER COLUMN fecha_gasto SET DEFAULT CURRENT_TIMESTAMP;

-- Verificar el cambio
SELECT column_name, data_type, column_default
FROM information_schema.columns
WHERE table_name = 'order_gastos_operativos'
AND column_name IN ('fecha_gasto', 'created_at', 'updated_at');
