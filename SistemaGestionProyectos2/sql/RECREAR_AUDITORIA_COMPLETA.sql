-- ============================================================================
-- RECREAR AUDITORÍA COMPLETA DESDE CERO
-- Ejecutar este script COMPLETO en Supabase SQL Editor
-- ============================================================================

-- PASO 1: ELIMINAR TODO LO EXISTENTE
-- ============================================================================
DROP VIEW IF EXISTS v_expense_audit_report CASCADE;
DROP FUNCTION IF EXISTS fn_get_expense_history CASCADE;
DROP FUNCTION IF EXISTS fn_expense_audit_summary CASCADE;
DROP FUNCTION IF EXISTS fn_expense_audit_suspicious_activity CASCADE;
DROP TRIGGER IF EXISTS trg_expense_audit ON t_expense;
DROP FUNCTION IF EXISTS fn_expense_audit CASCADE;
DROP TABLE IF EXISTS t_expense_audit CASCADE;

-- PASO 2: AGREGAR COLUMNA updated_by A t_expense (si no existe)
-- ============================================================================
ALTER TABLE t_expense ADD COLUMN IF NOT EXISTS updated_by VARCHAR(100);

-- PASO 3: CREAR TABLA DE AUDITORÍA
-- ============================================================================
CREATE TABLE t_expense_audit (
    id SERIAL PRIMARY KEY,
    expense_id INTEGER,
    action VARCHAR(20) NOT NULL,

    -- Valores anteriores
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
    old_created_by TEXT,
    old_updated_by VARCHAR(100),

    -- Valores nuevos
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
    new_created_by TEXT,
    new_updated_by VARCHAR(100),

    -- Metadatos del cambio
    changed_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    amount_change NUMERIC(18,2),
    days_until_due_old INTEGER,
    days_until_due_new INTEGER,
    supplier_name VARCHAR(200),
    order_po VARCHAR(50),
    environment VARCHAR(20) DEFAULT 'production'
);

-- PASO 4: CREAR ÍNDICES
-- ============================================================================
CREATE INDEX idx_expense_audit_expense_id ON t_expense_audit(expense_id);
CREATE INDEX idx_expense_audit_changed_at ON t_expense_audit(changed_at DESC);
CREATE INDEX idx_expense_audit_action ON t_expense_audit(action);
CREATE INDEX idx_expense_audit_updated_by ON t_expense_audit(new_updated_by);

-- PASO 5: CREAR FUNCIÓN DEL TRIGGER
-- ============================================================================
CREATE OR REPLACE FUNCTION fn_expense_audit()
RETURNS TRIGGER AS $$
DECLARE
    v_action VARCHAR(20);
    v_amount_change NUMERIC(18,2);
    v_days_old INTEGER;
    v_days_new INTEGER;
    v_supplier_name VARCHAR(200);
    v_order_po VARCHAR(50);
BEGIN
    -- Determinar acción
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

    -- INSERT
    IF TG_OP = 'INSERT' THEN
        INSERT INTO t_expense_audit (
            expense_id, action,
            new_supplier_id, new_description, new_total_expense,
            new_expense_date, new_scheduled_date, new_status,
            new_paid_date, new_pay_method, new_order_id,
            new_expense_category, new_created_by, new_updated_by,
            amount_change, days_until_due_new, supplier_name, order_po
        ) VALUES (
            NEW.f_expense, v_action,
            NEW.f_supplier, NEW.f_description, NEW.f_totalexpense,
            NEW.f_expensedate, NEW.f_scheduleddate, NEW.f_status,
            NEW.f_paiddate, NEW.f_paymethod, NEW.f_order,
            NEW.expense_category, NEW.created_by::TEXT, NEW.updated_by,
            v_amount_change, v_days_new, v_supplier_name, v_order_po
        );
        RETURN NEW;

    -- UPDATE
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
            OLD.expense_category, OLD.created_by::TEXT, OLD.updated_by,
            NEW.f_supplier, NEW.f_description, NEW.f_totalexpense,
            NEW.f_expensedate, NEW.f_scheduleddate, NEW.f_status,
            NEW.f_paiddate, NEW.f_paymethod, NEW.f_order,
            NEW.expense_category, NEW.created_by::TEXT, NEW.updated_by,
            v_amount_change, v_days_old, v_days_new,
            v_supplier_name, v_order_po
        );
        RETURN NEW;

    -- DELETE
    ELSIF TG_OP = 'DELETE' THEN
        INSERT INTO t_expense_audit (
            expense_id, action,
            old_supplier_id, old_description, old_total_expense,
            old_expense_date, old_scheduled_date, old_status,
            old_paid_date, old_pay_method, old_order_id,
            old_expense_category, old_created_by, old_updated_by,
            amount_change, days_until_due_old, supplier_name, order_po
        ) VALUES (
            OLD.f_expense, v_action,
            OLD.f_supplier, OLD.f_description, OLD.f_totalexpense,
            OLD.f_expensedate, OLD.f_scheduleddate, OLD.f_status,
            OLD.f_paiddate, OLD.f_paymethod, OLD.f_order,
            OLD.expense_category, OLD.created_by::TEXT, OLD.updated_by,
            v_amount_change, v_days_old, v_supplier_name, v_order_po
        );
        RETURN OLD;
    END IF;

    RETURN NULL;
END;
$$ LANGUAGE plpgsql SECURITY DEFINER;

-- PASO 6: CREAR TRIGGER
-- ============================================================================
CREATE TRIGGER trg_expense_audit
    AFTER INSERT OR UPDATE OR DELETE ON t_expense
    FOR EACH ROW
    EXECUTE FUNCTION fn_expense_audit();

-- PASO 7: CREAR VISTA DE REPORTES
-- ============================================================================
CREATE VIEW v_expense_audit_report AS
SELECT
    ea.id,
    ea.expense_id,
    ea.action,
    CASE ea.action
        WHEN 'INSERT' THEN 'Creado'
        WHEN 'UPDATE' THEN 'Modificado'
        WHEN 'DELETE' THEN 'Eliminado'
        WHEN 'PAID' THEN 'Pagado'
        WHEN 'UNPAID' THEN 'Pago revertido'
    END AS accion,
    COALESCE(ea.new_description, ea.old_description) AS descripcion,
    ea.supplier_name AS proveedor,
    ea.order_po,
    ea.old_total_expense AS monto_anterior,
    ea.new_total_expense AS monto_nuevo,
    ea.amount_change AS diferencia,
    ea.old_status AS estado_anterior,
    ea.new_status AS estado_nuevo,
    ea.changed_at,
    TO_CHAR(ea.changed_at AT TIME ZONE 'America/Mexico_City', 'DD/MM/YYYY HH24:MI:SS') AS fecha_hora_mx,
    COALESCE(ea.new_updated_by, ea.new_created_by, 'Sistema') AS modificado_por
FROM t_expense_audit ea
ORDER BY ea.changed_at DESC;

-- PASO 8: PERMISOS
-- ============================================================================
GRANT SELECT ON t_expense_audit TO authenticated;
GRANT SELECT ON v_expense_audit_report TO authenticated;

-- PASO 9: VERIFICACIÓN FINAL
-- ============================================================================
SELECT 'VERIFICACIÓN' AS titulo;

SELECT 'Tabla t_expense_audit' AS elemento,
       CASE WHEN EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 't_expense_audit')
            THEN '✅ OK' ELSE '❌ FALTA' END AS estado
UNION ALL
SELECT 'Columna new_updated_by',
       CASE WHEN EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 't_expense_audit' AND column_name = 'new_updated_by')
            THEN '✅ OK' ELSE '❌ FALTA' END
UNION ALL
SELECT 'Trigger trg_expense_audit',
       CASE WHEN EXISTS (SELECT 1 FROM information_schema.triggers WHERE trigger_name = 'trg_expense_audit')
            THEN '✅ OK' ELSE '❌ FALTA' END
UNION ALL
SELECT 'Vista v_expense_audit_report',
       CASE WHEN EXISTS (SELECT 1 FROM information_schema.views WHERE table_name = 'v_expense_audit_report')
            THEN '✅ OK' ELSE '❌ FALTA' END;
