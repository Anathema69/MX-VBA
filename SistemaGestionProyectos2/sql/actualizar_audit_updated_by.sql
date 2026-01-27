-- ============================================================================
-- ACTUALIZACIÓN: Agregar campos updated_by a la auditoría
-- Sistema de Gestión de Proyectos - IMA Mecatrónica
-- Fecha: 2026-01-27
-- ============================================================================
-- Este script agrega los campos old_updated_by y new_updated_by a la tabla
-- de auditoría y actualiza el trigger para capturarlos.
-- ============================================================================

-- 1. AGREGAR COLUMNAS SI NO EXISTEN
-- ============================================================================

DO $$
BEGIN
    -- Agregar old_updated_by
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 't_expense_audit' AND column_name = 'old_updated_by'
    ) THEN
        ALTER TABLE t_expense_audit ADD COLUMN old_updated_by VARCHAR(100);
        RAISE NOTICE '✅ Columna old_updated_by agregada';
    ELSE
        RAISE NOTICE 'ℹ️ Columna old_updated_by ya existe';
    END IF;

    -- Agregar new_updated_by
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 't_expense_audit' AND column_name = 'new_updated_by'
    ) THEN
        ALTER TABLE t_expense_audit ADD COLUMN new_updated_by VARCHAR(100);
        RAISE NOTICE '✅ Columna new_updated_by agregada';
    ELSE
        RAISE NOTICE 'ℹ️ Columna new_updated_by ya existe';
    END IF;
END $$;

-- Crear índice para búsquedas por usuario que modificó
CREATE INDEX IF NOT EXISTS idx_expense_audit_updated_by
    ON t_expense_audit(new_updated_by);

-- 2. ACTUALIZAR FUNCIÓN DE TRIGGER
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
            NEW.expense_category, NEW.created_by::TEXT, NEW.updated_by,
            v_amount_change, v_days_new,
            v_supplier_name, v_order_po
        );
        RETURN NEW;

    ELSIF TG_OP = 'UPDATE' THEN
        INSERT INTO t_expense_audit (
            expense_id, action,
            -- Valores anteriores
            old_supplier_id, old_description, old_total_expense,
            old_expense_date, old_scheduled_date, old_status,
            old_paid_date, old_pay_method, old_order_id,
            old_expense_category, old_created_by, old_updated_by,
            -- Valores nuevos
            new_supplier_id, new_description, new_total_expense,
            new_expense_date, new_scheduled_date, new_status,
            new_paid_date, new_pay_method, new_order_id,
            new_expense_category, new_created_by, new_updated_by,
            -- Calculados
            amount_change, days_until_due_old, days_until_due_new,
            supplier_name, order_po
        ) VALUES (
            NEW.f_expense, v_action,
            -- Valores anteriores
            OLD.f_supplier, OLD.f_description, OLD.f_totalexpense,
            OLD.f_expensedate, OLD.f_scheduleddate, OLD.f_status,
            OLD.f_paiddate, OLD.f_paymethod, OLD.f_order,
            OLD.expense_category, OLD.created_by::TEXT, OLD.updated_by,
            -- Valores nuevos
            NEW.f_supplier, NEW.f_description, NEW.f_totalexpense,
            NEW.f_expensedate, NEW.f_scheduleddate, NEW.f_status,
            NEW.f_paiddate, NEW.f_paymethod, NEW.f_order,
            NEW.expense_category, NEW.created_by::TEXT, NEW.updated_by,
            -- Calculados
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

-- Recrear trigger (ya debería existir, pero por si acaso)
DROP TRIGGER IF EXISTS trg_expense_audit ON t_expense;
CREATE TRIGGER trg_expense_audit
    AFTER INSERT OR UPDATE OR DELETE ON t_expense
    FOR EACH ROW
    EXECUTE FUNCTION fn_expense_audit();

-- 3. ACTUALIZAR VISTA DE REPORTES
-- ============================================================================

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

    -- Información del gasto
    COALESCE(ea.new_description, ea.old_description) AS description,
    ea.supplier_name,
    ea.order_po,

    -- Montos
    ea.old_total_expense,
    ea.new_total_expense,
    ea.amount_change,

    -- Estados
    ea.old_status,
    ea.new_status,

    -- Fechas de pago
    ea.old_paid_date,
    ea.new_paid_date,

    -- Cuándo ocurrió el cambio
    ea.changed_at,
    TO_CHAR(ea.changed_at AT TIME ZONE 'America/Mexico_City', 'DD/MM/YYYY HH24:MI:SS') AS changed_at_mx,

    -- QUIÉN hizo el cambio (prioridad: updated_by > created_by)
    CASE
        WHEN ea.action = 'INSERT' THEN ea.new_created_by
        ELSE COALESCE(ea.new_updated_by, ea.new_created_by, 'Sistema')
    END AS changed_by_user_id,

    -- Obtener nombre del usuario que hizo el cambio
    CASE
        WHEN ea.action = 'INSERT' THEN
            (SELECT full_name FROM users WHERE id = ea.new_created_by::INTEGER)
        ELSE
            COALESCE(
                (SELECT full_name FROM users WHERE username = ea.new_updated_by),
                (SELECT full_name FROM users WHERE id = ea.new_created_by::INTEGER),
                'Sistema'
            )
    END AS changed_by_name,

    -- Días hasta vencimiento
    ea.days_until_due_old,
    ea.days_until_due_new,

    -- Detectar qué campos cambiaron (para UPDATE)
    CASE WHEN ea.action IN ('UPDATE', 'PAID', 'UNPAID') THEN
        ARRAY_REMOVE(ARRAY[
            CASE WHEN ea.old_description IS DISTINCT FROM ea.new_description THEN 'descripción' END,
            CASE WHEN ea.old_total_expense IS DISTINCT FROM ea.new_total_expense THEN 'monto' END,
            CASE WHEN ea.old_status IS DISTINCT FROM ea.new_status THEN 'estado' END,
            CASE WHEN ea.old_paid_date IS DISTINCT FROM ea.new_paid_date THEN 'fecha pago' END,
            CASE WHEN ea.old_pay_method IS DISTINCT FROM ea.new_pay_method THEN 'método pago' END,
            CASE WHEN ea.old_expense_date IS DISTINCT FROM ea.new_expense_date THEN 'fecha gasto' END,
            CASE WHEN ea.old_order_id IS DISTINCT FROM ea.new_order_id THEN 'orden' END,
            CASE WHEN ea.old_supplier_id IS DISTINCT FROM ea.new_supplier_id THEN 'proveedor' END
        ], NULL)
    END AS fields_changed

FROM t_expense_audit ea
ORDER BY ea.changed_at DESC;

COMMENT ON VIEW v_expense_audit_report IS 'Vista de reportes de auditoría con nombre del usuario que hizo el cambio';

-- 4. FUNCIÓN MEJORADA PARA HISTORIAL DE UN GASTO
-- ============================================================================

CREATE OR REPLACE FUNCTION fn_get_expense_history(p_expense_id INTEGER)
RETURNS TABLE (
    audit_id INTEGER,
    action VARCHAR(20),
    action_description TEXT,
    changed_at TIMESTAMP WITH TIME ZONE,
    changed_at_formatted TEXT,
    changed_by_id TEXT,
    changed_by_name TEXT,
    old_total NUMERIC(18,2),
    new_total NUMERIC(18,2),
    amount_change NUMERIC(18,2),
    old_status VARCHAR(20),
    new_status VARCHAR(20),
    fields_changed TEXT[]
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
        -- ID del usuario
        CASE
            WHEN ea.action = 'INSERT' THEN ea.new_created_by
            ELSE COALESCE(ea.new_updated_by, ea.new_created_by, 'Sistema')
        END::TEXT,
        -- Nombre del usuario
        CASE
            WHEN ea.action = 'INSERT' THEN
                (SELECT u.full_name FROM users u WHERE u.id = ea.new_created_by::INTEGER)
            ELSE
                COALESCE(
                    (SELECT u.full_name FROM users u WHERE u.username = ea.new_updated_by),
                    (SELECT u.full_name FROM users u WHERE u.id = ea.new_created_by::INTEGER),
                    'Sistema'
                )
        END::TEXT,
        ea.old_total_expense,
        ea.new_total_expense,
        ea.amount_change,
        ea.old_status,
        ea.new_status,
        CASE WHEN ea.action IN ('UPDATE', 'PAID', 'UNPAID') THEN
            ARRAY_REMOVE(ARRAY[
                CASE WHEN ea.old_description IS DISTINCT FROM ea.new_description THEN 'descripción' END,
                CASE WHEN ea.old_total_expense IS DISTINCT FROM ea.new_total_expense THEN 'monto' END,
                CASE WHEN ea.old_status IS DISTINCT FROM ea.new_status THEN 'estado' END,
                CASE WHEN ea.old_paid_date IS DISTINCT FROM ea.new_paid_date THEN 'fecha pago' END,
                CASE WHEN ea.old_order_id IS DISTINCT FROM ea.new_order_id THEN 'orden' END
            ], NULL)
        END
    FROM t_expense_audit ea
    WHERE ea.expense_id = p_expense_id
    ORDER BY ea.changed_at DESC;
END;
$$ LANGUAGE plpgsql;

-- ============================================================================
-- VERIFICACIÓN FINAL
-- ============================================================================

DO $$
DECLARE
    v_count INTEGER;
BEGIN
    -- Verificar columnas
    SELECT COUNT(*) INTO v_count
    FROM information_schema.columns
    WHERE table_name = 't_expense_audit'
      AND column_name IN ('old_updated_by', 'new_updated_by');

    RAISE NOTICE '';
    RAISE NOTICE '============================================================';
    RAISE NOTICE '✅ ACTUALIZACIÓN COMPLETADA';
    RAISE NOTICE '============================================================';
    RAISE NOTICE 'Columnas updated_by: % de 2 esperadas', v_count;
    RAISE NOTICE '';
    RAISE NOTICE 'Cambios realizados:';
    RAISE NOTICE '  1. Agregadas columnas old_updated_by y new_updated_by';
    RAISE NOTICE '  2. Trigger actualizado para capturar updated_by';
    RAISE NOTICE '  3. Vista v_expense_audit_report actualizada';
    RAISE NOTICE '  4. Función fn_get_expense_history mejorada';
    RAISE NOTICE '';
    RAISE NOTICE 'Ahora la auditoría capturará:';
    RAISE NOTICE '  - created_by: ID del usuario que CREÓ el gasto';
    RAISE NOTICE '  - updated_by: Username del usuario que MODIFICÓ';
    RAISE NOTICE '============================================================';
END $$;
