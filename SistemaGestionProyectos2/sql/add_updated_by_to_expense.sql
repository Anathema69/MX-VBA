-- ============================================================================
-- AGREGAR COLUMNA updated_by A t_expense
-- Sistema de Gestión de Proyectos - IMA Mecatrónica
-- Fecha: 2026-01-27
-- ============================================================================
-- Esta columna permite rastrear quién realizó la última modificación a un gasto
-- ============================================================================

-- 1. Agregar columna updated_by si no existe
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 't_expense' AND column_name = 'updated_by'
    ) THEN
        ALTER TABLE t_expense ADD COLUMN updated_by VARCHAR(100);
        RAISE NOTICE '✅ Columna updated_by agregada a t_expense';
    ELSE
        RAISE NOTICE 'ℹ️ La columna updated_by ya existe en t_expense';
    END IF;
END $$;

-- 2. Agregar comentario a la columna
COMMENT ON COLUMN t_expense.updated_by IS 'Usuario que realizó la última modificación al gasto';

-- 3. Crear índice para búsquedas por usuario que modificó
CREATE INDEX IF NOT EXISTS idx_expense_updated_by ON t_expense(updated_by);

-- 4. Verificar que updated_at existe (debería existir)
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 't_expense' AND column_name = 'updated_at'
    ) THEN
        ALTER TABLE t_expense ADD COLUMN updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW();
        RAISE NOTICE '✅ Columna updated_at agregada a t_expense';
    ELSE
        RAISE NOTICE 'ℹ️ La columna updated_at ya existe en t_expense';
    END IF;
END $$;

-- 5. Crear trigger para actualizar updated_at automáticamente
CREATE OR REPLACE FUNCTION fn_expense_update_timestamp()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_expense_update_timestamp ON t_expense;
CREATE TRIGGER trg_expense_update_timestamp
    BEFORE UPDATE ON t_expense
    FOR EACH ROW
    EXECUTE FUNCTION fn_expense_update_timestamp();

RAISE NOTICE '✅ Trigger de updated_at creado/actualizado';

-- ============================================================================
-- FIN DEL SCRIPT
-- ============================================================================
