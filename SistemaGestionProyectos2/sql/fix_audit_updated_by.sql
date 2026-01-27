-- ============================================================================
-- FIX: Agregar campos updated_by a la auditoría (versión corregida)
-- ============================================================================

-- 1. AGREGAR COLUMNAS
ALTER TABLE t_expense_audit ADD COLUMN IF NOT EXISTS old_updated_by VARCHAR(100);
ALTER TABLE t_expense_audit ADD COLUMN IF NOT EXISTS new_updated_by VARCHAR(100);

-- 2. ÍNDICE
CREATE INDEX IF NOT EXISTS idx_expense_audit_updated_by ON t_expense_audit(new_updated_by);

-- 3. ACTUALIZAR FUNCIÓN DE TRIGGER
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

    IF TG_OP = 'UPDATE' THEN
        v_amount_change := COALESCE(NEW.f_totalexpense, 0) - COALESCE(OLD.f_totalexpense, 0);
    ELSIF TG_OP = 'INSERT' THEN
        v_amount_change := NEW.f_totalexpense;
    ELSIF TG_OP = 'DELETE' THEN
        v_amount_change := -OLD.f_totalexpense;
    END IF;

    IF TG_OP IN ('UPDATE', 'DELETE') THEN
        v_days_old := COALESCE(OLD.f_scheduleddate, OLD.f_expensedate)::DATE - CURRENT_DATE;
    END IF;
    IF TG_OP IN ('INSERT', 'UPDATE') THEN
        v_days_new := COALESCE(NEW.f_scheduleddate, NEW.f_expensedate)::DATE - CURRENT_DATE;
    END IF;

    IF TG_OP IN ('INSERT', 'UPDATE') THEN
        SELECT f_suppliername INTO v_supplier_name FROM t_supplier WHERE f_supplier = NEW.f_supplier;
    ELSE
        SELECT f_suppliername INTO v_supplier_name FROM t_supplier WHERE f_supplier = OLD.f_supplier;
    END IF;

    IF TG_OP IN ('INSERT', 'UPDATE') AND NEW.f_order IS NOT NULL THEN
        SELECT f_po INTO v_order_po FROM t_order WHERE f_order = NEW.f_order;
    ELSIF TG_OP = 'DELETE' AND OLD.f_order IS NOT NULL THEN
        SELECT f_po INTO v_order_po FROM t_order WHERE f_order = OLD.f_order;
    END IF;

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
            NEW.expense_category, NEW.created_by::TEXT, NEW.updated_by,
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
            OLD.expense_category, OLD.created_by::TEXT, OLD.updated_by,
            NEW.f_supplier, NEW.f_description, NEW.f_totalexpense,
            NEW.f_expensedate, NEW.f_scheduleddate, NEW.f_status,
            NEW.f_paiddate, NEW.f_paymethod, NEW.f_order,
            NEW.expense_category, NEW.created_by::TEXT, NEW.updated_by,
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
            OLD.expense_category, OLD.created_by::TEXT, OLD.updated_by,
            v_amount_change, v_days_old,
            v_supplier_name, v_order_po
        );
        RETURN OLD;
    END IF;

    RETURN NULL;
END;
$$ LANGUAGE plpgsql SECURITY DEFINER;

-- 4. RECREAR TRIGGER
DROP TRIGGER IF EXISTS trg_expense_audit ON t_expense;
CREATE TRIGGER trg_expense_audit
    AFTER INSERT OR UPDATE OR DELETE ON t_expense
    FOR EACH ROW
    EXECUTE FUNCTION fn_expense_audit();

-- 5. ACTUALIZAR VISTA
CREATE OR REPLACE VIEW v_expense_audit_report AS
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
    CASE
        WHEN ea.action = 'INSERT' THEN ea.new_created_by
        ELSE COALESCE(ea.new_updated_by, ea.new_created_by, 'Sistema')
    END AS usuario_id,
    ea.new_updated_by AS modificado_por
FROM t_expense_audit ea
ORDER BY ea.changed_at DESC;

-- 6. VERIFICAR
SELECT 'Columnas en t_expense_audit:' AS info;
SELECT column_name FROM information_schema.columns
WHERE table_name = 't_expense_audit' AND column_name LIKE '%updated_by%';

SELECT 'Triggers en t_expense:' AS info;
SELECT trigger_name FROM information_schema.triggers WHERE event_object_table = 't_expense';
