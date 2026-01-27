-- ============================================================================
-- SCRIPTS PARA EJECUTAR EN SUPABASE - EN ORDEN
-- Sistema de Gestión de Proyectos - IMA Mecatrónica
-- Fecha: 2026-01-27
-- ============================================================================
--
-- INSTRUCCIONES:
-- 1. Primero ejecuta: add_updated_by_to_expense.sql (agrega columna updated_by)
-- 2. Luego ejecuta: create_expense_audit.sql (crea tabla de auditoría)
--
-- O ejecuta este archivo completo que contiene todo en orden.
--
-- ============================================================================

-- ############################################################################
-- PARTE 1: AGREGAR COLUMNA updated_by A t_expense
-- ############################################################################

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

-- ############################################################################
-- PARTE 2: CREAR TABLA DE AUDITORÍA t_expense_audit
-- ############################################################################

-- 1. CREAR TABLA DE AUDITORÍA
CREATE TABLE IF NOT EXISTS t_expense_audit (
    id SERIAL PRIMARY KEY,
    expense_id INTEGER,
    action VARCHAR(20) NOT NULL CHECK (action IN ('INSERT', 'UPDATE', 'DELETE', 'PAID', 'UNPAID')),

    -- VALORES ANTERIORES
    old_supplier_id INTEGER,
    old_description TEXT,
    old_total_expense NUMERIC(18,2),
    old_expense_date DATE,
    old_scheduled_date DATE,
    old_status VARCHAR(20),
    old_paid_date DATE,
    old_pay_method VARCHAR(50),
    old_order_id INTEGER,
    old_expense_category VARCHAR(50),
    old_created_by VARCHAR(100),
    old_updated_by VARCHAR(100),

    -- VALORES NUEVOS
    new_supplier_id INTEGER,
    new_description TEXT,
    new_total_expense NUMERIC(18,2),
    new_expense_date DATE,
    new_scheduled_date DATE,
    new_status VARCHAR(20),
    new_paid_date DATE,
    new_pay_method VARCHAR(50),
    new_order_id INTEGER,
    new_expense_category VARCHAR(50),
    new_created_by VARCHAR(100),
    new_updated_by VARCHAR(100),

    -- INFORMACIÓN DEL CAMBIO
    changed_by UUID,
    changed_by_username VARCHAR(50),
    changed_by_fullname VARCHAR(100),
    changed_by_role VARCHAR(50),
    changed_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),

    -- INFORMACIÓN ADICIONAL
    change_reason TEXT,
    ip_address VARCHAR(45),
    user_agent TEXT,
    session_id VARCHAR(100),
    request_id VARCHAR(100),

    -- CAMPOS CALCULADOS
    amount_change NUMERIC(18,2),
    days_until_due_old INTEGER,
    days_until_due_new INTEGER,

    -- DATOS DESNORMALIZADOS
    supplier_name VARCHAR(200),
    order_po VARCHAR(50),

    -- METADATOS
    app_version VARCHAR(20),
    environment VARCHAR(20) DEFAULT 'production',
    created_at_audit TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

-- 2. ÍNDICES
CREATE INDEX IF NOT EXISTS idx_expense_audit_expense_id ON t_expense_audit(expense_id);
CREATE INDEX IF NOT EXISTS idx_expense_audit_changed_at ON t_expense_audit(changed_at DESC);
CREATE INDEX IF NOT EXISTS idx_expense_audit_action ON t_expense_audit(action);
CREATE INDEX IF NOT EXISTS idx_expense_audit_changed_by ON t_expense_audit(changed_by);
CREATE INDEX IF NOT EXISTS idx_expense_audit_supplier ON t_expense_audit(old_supplier_id, new_supplier_id);
CREATE INDEX IF NOT EXISTS idx_expense_audit_date_action ON t_expense_audit(changed_at, action);
CREATE INDEX IF NOT EXISTS idx_expense_audit_order ON t_expense_audit(old_order_id, new_order_id);
CREATE INDEX IF NOT EXISTS idx_expense_audit_payments ON t_expense_audit(action, changed_at DESC) WHERE action IN ('PAID', 'UNPAID');
CREATE INDEX IF NOT EXISTS idx_expense_audit_updated_by ON t_expense_audit(new_updated_by);

-- 3. FUNCIÓN DE TRIGGER
CREATE OR REPLACE FUNCTION fn_expense_audit()
RETURNS TRIGGER AS $$
DECLARE
    v_action VARCHAR(20);
    v_amount_change NUMERIC(18,2);
    v_days_old INTEGER;
    v_days_new INTEGER;
    v_supplier_name VARCHAR(200);
    v_order_po VARCHAR(50);
    v_user_id UUID;
    v_username VARCHAR(50);
    v_fullname VARCHAR(100);
    v_role VARCHAR(50);
BEGIN
    -- Determinar el tipo de acción
    IF TG_OP = 'INSERT' THEN
        v_action := 'INSERT';
    ELSIF TG_OP = 'DELETE' THEN
        v_action := 'DELETE';
    ELSIF TG_OP = 'UPDATE' THEN
        IF OLD.f_status != 'PAGADO' AND NEW.f_status = 'PAGADO' THEN
            v_action := 'PAID';
        ELSIF OLD.f_status = 'PAGADO' AND NEW.f_status != 'PAGADO' THEN
            v_action := 'UNPAID';
        ELSE
            v_action := 'UPDATE';
        END IF;
    END IF;

    -- Calcular diferencia de monto
    IF TG_OP = 'UPDATE' THEN
        v_amount_change := COALESCE(NEW.f_totalexpense, 0) - COALESCE(OLD.f_totalexpense, 0);
    ELSIF TG_OP = 'INSERT' THEN
        v_amount_change := NEW.f_totalexpense;
    ELSIF TG_OP = 'DELETE' THEN
        v_amount_change := -OLD.f_totalexpense;
    END IF;

    -- Calcular días hasta vencimiento
    IF TG_OP IN ('UPDATE', 'DELETE') THEN
        v_days_old := COALESCE(OLD.f_scheduleddate, OLD.f_expensedate)::DATE - CURRENT_DATE;
    END IF;
    IF TG_OP IN ('INSERT', 'UPDATE') THEN
        v_days_new := COALESCE(NEW.f_scheduleddate, NEW.f_expensedate)::DATE - CURRENT_DATE;
    END IF;

    -- Obtener nombre del proveedor
    IF TG_OP IN ('INSERT', 'UPDATE') THEN
        SELECT f_suppliername INTO v_supplier_name FROM t_supplier WHERE f_supplier = NEW.f_supplier;
    ELSE
        SELECT f_suppliername INTO v_supplier_name FROM t_supplier WHERE f_supplier = OLD.f_supplier;
    END IF;

    -- Obtener PO de la orden
    IF TG_OP IN ('INSERT', 'UPDATE') AND NEW.f_order IS NOT NULL THEN
        SELECT f_po INTO v_order_po FROM t_order WHERE f_order = NEW.f_order;
    ELSIF TG_OP = 'DELETE' AND OLD.f_order IS NOT NULL THEN
        SELECT f_po INTO v_order_po FROM t_order WHERE f_order = OLD.f_order;
    END IF;

    -- Insertar registro de auditoría
    IF TG_OP = 'INSERT' THEN
        INSERT INTO t_expense_audit (
            expense_id, action,
            new_supplier_id, new_description, new_total_expense,
            new_expense_date, new_scheduled_date, new_status,
            new_paid_date, new_pay_method, new_order_id,
            new_expense_category, new_created_by, new_updated_by,
            amount_change, days_until_due_new,
            supplier_name, order_po
        ) VALUES (
            NEW.f_expense, v_action,
            NEW.f_supplier, NEW.f_description, NEW.f_totalexpense,
            NEW.f_expensedate, NEW.f_scheduleddate, NEW.f_status,
            NEW.f_paiddate, NEW.f_paymethod, NEW.f_order,
            NEW.expense_category, NEW.created_by, NEW.updated_by,
            v_amount_change, v_days_new,
            v_supplier_name, v_order_po
        );
        RETURN NEW;

    ELSIF TG_OP = 'UPDATE' THEN
        INSERT INTO t_expense_audit (
            expense_id, action,
            old_supplier_id, old_description, old_total_expense,
            old_expense_date, old_scheduled_date, old_status,
            old_paid_date, old_pay_method, old_order_id,
            old_expense_category, old_created_by, old_updated_by,
            new_supplier_id, new_description, new_total_expense,
            new_expense_date, new_scheduled_date, new_status,
            new_paid_date, new_pay_method, new_order_id,
            new_expense_category, new_created_by, new_updated_by,
            amount_change, days_until_due_old, days_until_due_new,
            supplier_name, order_po
        ) VALUES (
            NEW.f_expense, v_action,
            OLD.f_supplier, OLD.f_description, OLD.f_totalexpense,
            OLD.f_expensedate, OLD.f_scheduleddate, OLD.f_status,
            OLD.f_paiddate, OLD.f_paymethod, OLD.f_order,
            OLD.expense_category, OLD.created_by, OLD.updated_by,
            NEW.f_supplier, NEW.f_description, NEW.f_totalexpense,
            NEW.f_expensedate, NEW.f_scheduleddate, NEW.f_status,
            NEW.f_paiddate, NEW.f_paymethod, NEW.f_order,
            NEW.expense_category, NEW.created_by, NEW.updated_by,
            v_amount_change, v_days_old, v_days_new,
            v_supplier_name, v_order_po
        );
        RETURN NEW;

    ELSIF TG_OP = 'DELETE' THEN
        INSERT INTO t_expense_audit (
            expense_id, action,
            old_supplier_id, old_description, old_total_expense,
            old_expense_date, old_scheduled_date, old_status,
            old_paid_date, old_pay_method, old_order_id,
            old_expense_category, old_created_by, old_updated_by,
            amount_change, days_until_due_old,
            supplier_name, order_po
        ) VALUES (
            OLD.f_expense, v_action,
            OLD.f_supplier, OLD.f_description, OLD.f_totalexpense,
            OLD.f_expensedate, OLD.f_scheduleddate, OLD.f_status,
            OLD.f_paiddate, OLD.f_paymethod, OLD.f_order,
            OLD.expense_category, OLD.created_by, OLD.updated_by,
            v_amount_change, v_days_old,
            v_supplier_name, v_order_po
        );
        RETURN OLD;
    END IF;

    RETURN NULL;
END;
$$ LANGUAGE plpgsql SECURITY DEFINER;

-- 4. CREAR TRIGGER
DROP TRIGGER IF EXISTS trg_expense_audit ON t_expense;
CREATE TRIGGER trg_expense_audit
    AFTER INSERT OR UPDATE OR DELETE ON t_expense
    FOR EACH ROW
    EXECUTE FUNCTION fn_expense_audit();

-- 5. COMENTARIOS
COMMENT ON TABLE t_expense_audit IS 'Tabla de auditoría para rastrear todos los cambios en gastos a proveedores';
COMMENT ON COLUMN t_expense_audit.action IS 'Tipo de acción: INSERT, UPDATE, DELETE, PAID, UNPAID';
COMMENT ON COLUMN t_expense_audit.new_updated_by IS 'Usuario que realizó la modificación (capturado desde la app)';

-- 6. VISTA PARA REPORTES
CREATE OR REPLACE VIEW v_expense_audit_report AS
SELECT
    ea.id,
    ea.expense_id,
    ea.action,
    CASE ea.action
        WHEN 'INSERT' THEN 'Gasto creado'
        WHEN 'UPDATE' THEN 'Gasto modificado'
        WHEN 'DELETE' THEN 'Gasto eliminado'
        WHEN 'PAID' THEN 'Gasto pagado'
        WHEN 'UNPAID' THEN 'Pago revertido'
    END AS action_description,
    COALESCE(ea.new_description, ea.old_description) AS description,
    ea.supplier_name,
    ea.order_po,
    ea.old_total_expense,
    ea.new_total_expense,
    ea.amount_change,
    ea.old_status,
    ea.new_status,
    ea.old_paid_date,
    ea.new_paid_date,
    ea.changed_at,
    TO_CHAR(ea.changed_at AT TIME ZONE 'America/Mexico_City', 'DD/MM/YYYY HH24:MI:SS') AS changed_at_formatted,
    COALESCE(ea.new_updated_by, ea.new_created_by, 'Sistema') AS changed_by_display,
    ea.days_until_due_old,
    ea.days_until_due_new
FROM t_expense_audit ea
ORDER BY ea.changed_at DESC;

-- 7. FUNCIÓN PARA HISTORIAL DE UN GASTO
CREATE OR REPLACE FUNCTION fn_get_expense_history(p_expense_id INTEGER)
RETURNS TABLE (
    audit_id INTEGER,
    action VARCHAR(20),
    action_description TEXT,
    changed_at TIMESTAMP WITH TIME ZONE,
    changed_at_formatted TEXT,
    changed_by_display TEXT,
    old_total NUMERIC(18,2),
    new_total NUMERIC(18,2),
    amount_change NUMERIC(18,2),
    old_status VARCHAR(20),
    new_status VARCHAR(20)
) AS $$
BEGIN
    RETURN QUERY
    SELECT
        ea.id,
        ea.action,
        CASE ea.action
            WHEN 'INSERT' THEN 'Gasto creado'
            WHEN 'UPDATE' THEN 'Gasto modificado'
            WHEN 'DELETE' THEN 'Gasto eliminado'
            WHEN 'PAID' THEN 'Gasto pagado'
            WHEN 'UNPAID' THEN 'Pago revertido'
        END::TEXT,
        ea.changed_at,
        TO_CHAR(ea.changed_at AT TIME ZONE 'America/Mexico_City', 'DD/MM/YYYY HH24:MI:SS'),
        COALESCE(ea.new_updated_by, ea.new_created_by, 'Sistema')::TEXT,
        ea.old_total_expense,
        ea.new_total_expense,
        ea.amount_change,
        ea.old_status,
        ea.new_status
    FROM t_expense_audit ea
    WHERE ea.expense_id = p_expense_id
    ORDER BY ea.changed_at DESC;
END;
$$ LANGUAGE plpgsql;

-- 8. PERMISOS
GRANT SELECT ON t_expense_audit TO authenticated;
GRANT SELECT ON v_expense_audit_report TO authenticated;
GRANT ALL ON t_expense_audit TO service_role;
GRANT USAGE, SELECT ON SEQUENCE t_expense_audit_id_seq TO service_role;

-- ############################################################################
-- VERIFICACIÓN FINAL
-- ############################################################################

DO $$
BEGIN
    RAISE NOTICE '';
    RAISE NOTICE '============================================================';
    RAISE NOTICE '✅ SCRIPT EJECUTADO CORRECTAMENTE';
    RAISE NOTICE '============================================================';
    RAISE NOTICE '';
    RAISE NOTICE 'Cambios realizados:';
    RAISE NOTICE '  1. Columna updated_by agregada a t_expense';
    RAISE NOTICE '  2. Tabla t_expense_audit creada';
    RAISE NOTICE '  3. Trigger trg_expense_audit activo en t_expense';
    RAISE NOTICE '  4. Vista v_expense_audit_report creada';
    RAISE NOTICE '  5. Función fn_get_expense_history creada';
    RAISE NOTICE '';
    RAISE NOTICE 'A partir de ahora, todos los cambios en t_expense serán';
    RAISE NOTICE 'registrados automáticamente en t_expense_audit.';
    RAISE NOTICE '============================================================';
END $$;
