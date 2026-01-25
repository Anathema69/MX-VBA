-- ============================================
-- Fix: Agregar campos de auditoria a order_gastos_indirectos
-- Fecha: 2026-01-25
-- ============================================

-- Agregar columnas updated_at y updated_by
ALTER TABLE order_gastos_indirectos
ADD COLUMN IF NOT EXISTS updated_at TIMESTAMP,
ADD COLUMN IF NOT EXISTS updated_by INTEGER;

-- Verificar cambios
SELECT column_name, data_type, is_nullable
FROM information_schema.columns
WHERE table_name = 'order_gastos_indirectos'
ORDER BY ordinal_position;
