-- ============================================================================
-- TABLA DE AUDITORÍA PARA GASTOS A PROVEEDORES (t_expense)
-- Sistema de Gestión de Proyectos - IMA Mecatrónica
-- Fecha: 2026-01-27
-- ============================================================================
-- Esta tabla captura TODOS los cambios realizados a los gastos/facturas de
-- proveedores, permitiendo trazabilidad completa y reportes detallados.
-- ============================================================================

-- 1. CREAR TABLA DE AUDITORÍA
-- ============================================================================
CREATE TABLE IF NOT EXISTS t_expense_audit (
    -- Identificador único del registro de auditoría
    id SERIAL PRIMARY KEY,

    -- Referencia al gasto (puede ser NULL si el gasto fue eliminado)
    expense_id INTEGER,

    -- Tipo de acción realizada
    action VARCHAR(20) NOT NULL CHECK (action IN ('INSERT', 'UPDATE', 'DELETE', 'PAID', 'UNPAID')),

    -- =========================================================================
    -- VALORES ANTERIORES (antes del cambio) - Para UPDATE y DELETE
    -- =========================================================================
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

    -- =========================================================================
    -- VALORES NUEVOS (después del cambio) - Para INSERT y UPDATE
    -- =========================================================================
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

    -- =========================================================================
    -- INFORMACIÓN DEL CAMBIO (quién, cuándo, desde dónde)
    -- =========================================================================
    changed_by UUID,                              -- ID del usuario que hizo el cambio
    changed_by_username VARCHAR(50),              -- Username del usuario
    changed_by_fullname VARCHAR(100),             -- Nombre completo del usuario
    changed_by_role VARCHAR(50),                  -- Rol del usuario al momento del cambio
    changed_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),  -- Fecha y hora exacta (con timezone)

    -- =========================================================================
    -- INFORMACIÓN ADICIONAL PARA ANÁLISIS
    -- =========================================================================
    change_reason TEXT,                           -- Motivo del cambio (si se proporciona)
    ip_address VARCHAR(45),                       -- Dirección IP (IPv4 o IPv6)
    user_agent TEXT,                              -- Navegador/aplicación usada
    session_id VARCHAR(100),                      -- ID de sesión si aplica
    request_id VARCHAR(100),                      -- ID de request para correlación

    -- =========================================================================
    -- CAMPOS CALCULADOS PARA REPORTES
    -- =========================================================================
    amount_change NUMERIC(18,2),                  -- Diferencia en monto (new - old)
    days_until_due_old INTEGER,                   -- Días para vencer (antes)
    days_until_due_new INTEGER,                   -- Días para vencer (después)

    -- =========================================================================
    -- INFORMACIÓN DEL PROVEEDOR (para reportes sin JOINs)
    -- =========================================================================
    supplier_name VARCHAR(200),                   -- Nombre del proveedor al momento del cambio

    -- =========================================================================
    -- INFORMACIÓN DE LA ORDEN (para reportes sin JOINs)
    -- =========================================================================
    order_po VARCHAR(50),                         -- PO de la orden al momento del cambio

    -- =========================================================================
    -- METADATOS DEL SISTEMA
    -- =========================================================================
    app_version VARCHAR(20),                      -- Versión de la aplicación
    environment VARCHAR(20) DEFAULT 'production', -- Ambiente (production, staging, dev)
    created_at_audit TIMESTAMP WITH TIME ZONE DEFAULT NOW()  -- Timestamp de creación del registro de auditoría
);

-- ============================================================================
-- 2. ÍNDICES PARA CONSULTAS FRECUENTES
-- ============================================================================

-- Búsqueda por gasto específico
CREATE INDEX IF NOT EXISTS idx_expense_audit_expense_id
    ON t_expense_audit(expense_id);

-- Búsqueda por fecha (ordenado descendente para ver más recientes primero)
CREATE INDEX IF NOT EXISTS idx_expense_audit_changed_at
    ON t_expense_audit(changed_at DESC);

-- Filtrar por tipo de acción
CREATE INDEX IF NOT EXISTS idx_expense_audit_action
    ON t_expense_audit(action);

-- Búsqueda por usuario que hizo el cambio
CREATE INDEX IF NOT EXISTS idx_expense_audit_changed_by
    ON t_expense_audit(changed_by);

-- Búsqueda por proveedor
CREATE INDEX IF NOT EXISTS idx_expense_audit_supplier
    ON t_expense_audit(old_supplier_id, new_supplier_id);

-- Búsqueda por rango de fechas + acción (para reportes)
CREATE INDEX IF NOT EXISTS idx_expense_audit_date_action
    ON t_expense_audit(changed_at, action);

-- Búsqueda por orden
CREATE INDEX IF NOT EXISTS idx_expense_audit_order
    ON t_expense_audit(old_order_id, new_order_id);

-- Índice compuesto para reportes de pagos
CREATE INDEX IF NOT EXISTS idx_expense_audit_payments
    ON t_expense_audit(action, changed_at DESC)
    WHERE action IN ('PAID', 'UNPAID');

-- ============================================================================
-- 3. FUNCIÓN DE TRIGGER PARA AUDITORÍA AUTOMÁTICA
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
        -- Detectar si fue un pago o despago
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
        SELECT f_suppliername INTO v_supplier_name
        FROM t_supplier
        WHERE f_supplier = NEW.f_supplier;
    ELSE
        SELECT f_suppliername INTO v_supplier_name
        FROM t_supplier
        WHERE f_supplier = OLD.f_supplier;
    END IF;

    -- Obtener PO de la orden
    IF TG_OP IN ('INSERT', 'UPDATE') AND NEW.f_order IS NOT NULL THEN
        SELECT f_po INTO v_order_po
        FROM t_order
        WHERE f_order = NEW.f_order;
    ELSIF TG_OP = 'DELETE' AND OLD.f_order IS NOT NULL THEN
        SELECT f_po INTO v_order_po
        FROM t_order
        WHERE f_order = OLD.f_order;
    END IF;

    -- Intentar obtener información del usuario actual (si está disponible en el contexto)
    BEGIN
        v_user_id := current_setting('app.current_user_id', true)::UUID;
        v_username := current_setting('app.current_username', true);
        v_fullname := current_setting('app.current_fullname', true);
        v_role := current_setting('app.current_role', true);
    EXCEPTION WHEN OTHERS THEN
        v_user_id := NULL;
        v_username := NULL;
        v_fullname := NULL;
        v_role := NULL;
    END;

    -- Insertar registro de auditoría
    IF TG_OP = 'INSERT' THEN
        INSERT INTO t_expense_audit (
            expense_id, action,
            new_supplier_id, new_description, new_total_expense,
            new_expense_date, new_scheduled_date, new_status,
            new_paid_date, new_pay_method, new_order_id,
            new_expense_category, new_created_by,
            changed_by, changed_by_username, changed_by_fullname, changed_by_role,
            amount_change, days_until_due_new,
            supplier_name, order_po
        ) VALUES (
            NEW.f_expense, v_action,
            NEW.f_supplier, NEW.f_description, NEW.f_totalexpense,
            NEW.f_expensedate, NEW.f_scheduleddate, NEW.f_status,
            NEW.f_paiddate, NEW.f_paymethod, NEW.f_order,
            NEW.expense_category, NEW.created_by,
            v_user_id, v_username, v_fullname, v_role,
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
            old_expense_category, old_created_by,
            -- Valores nuevos
            new_supplier_id, new_description, new_total_expense,
            new_expense_date, new_scheduled_date, new_status,
            new_paid_date, new_pay_method, new_order_id,
            new_expense_category, new_created_by,
            -- Información del cambio
            changed_by, changed_by_username, changed_by_fullname, changed_by_role,
            amount_change, days_until_due_old, days_until_due_new,
            supplier_name, order_po
        ) VALUES (
            NEW.f_expense, v_action,
            -- Valores anteriores
            OLD.f_supplier, OLD.f_description, OLD.f_totalexpense,
            OLD.f_expensedate, OLD.f_scheduleddate, OLD.f_status,
            OLD.f_paiddate, OLD.f_paymethod, OLD.f_order,
            OLD.expense_category, OLD.created_by,
            -- Valores nuevos
            NEW.f_supplier, NEW.f_description, NEW.f_totalexpense,
            NEW.f_expensedate, NEW.f_scheduleddate, NEW.f_status,
            NEW.f_paiddate, NEW.f_paymethod, NEW.f_order,
            NEW.expense_category, NEW.created_by,
            -- Información del cambio
            v_user_id, v_username, v_fullname, v_role,
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
            old_expense_category, old_created_by,
            changed_by, changed_by_username, changed_by_fullname, changed_by_role,
            amount_change, days_until_due_old,
            supplier_name, order_po
        ) VALUES (
            OLD.f_expense, v_action,
            OLD.f_supplier, OLD.f_description, OLD.f_totalexpense,
            OLD.f_expensedate, OLD.f_scheduleddate, OLD.f_status,
            OLD.f_paiddate, OLD.f_paymethod, OLD.f_order,
            OLD.expense_category, OLD.created_by,
            v_user_id, v_username, v_fullname, v_role,
            v_amount_change, v_days_old,
            v_supplier_name, v_order_po
        );
        RETURN OLD;
    END IF;

    RETURN NULL;
END;
$$ LANGUAGE plpgsql SECURITY DEFINER;

-- ============================================================================
-- 4. CREAR TRIGGER
-- ============================================================================

-- Eliminar trigger si existe (para poder re-ejecutar el script)
DROP TRIGGER IF EXISTS trg_expense_audit ON t_expense;

-- Crear trigger que se ejecuta después de cada INSERT, UPDATE o DELETE
CREATE TRIGGER trg_expense_audit
    AFTER INSERT OR UPDATE OR DELETE ON t_expense
    FOR EACH ROW
    EXECUTE FUNCTION fn_expense_audit();

-- ============================================================================
-- 5. COMENTARIOS EN LA TABLA (para documentación)
-- ============================================================================

COMMENT ON TABLE t_expense_audit IS 'Tabla de auditoría para rastrear todos los cambios en gastos a proveedores (t_expense)';

COMMENT ON COLUMN t_expense_audit.action IS 'Tipo de acción: INSERT (nuevo), UPDATE (modificación), DELETE (eliminación), PAID (marcado como pagado), UNPAID (revertido a pendiente)';
COMMENT ON COLUMN t_expense_audit.amount_change IS 'Diferencia en el monto: positivo = aumento, negativo = disminución';
COMMENT ON COLUMN t_expense_audit.days_until_due_old IS 'Días hasta vencimiento antes del cambio (negativo = vencido)';
COMMENT ON COLUMN t_expense_audit.days_until_due_new IS 'Días hasta vencimiento después del cambio (negativo = vencido)';
COMMENT ON COLUMN t_expense_audit.supplier_name IS 'Nombre del proveedor al momento del cambio (desnormalizado para reportes)';
COMMENT ON COLUMN t_expense_audit.order_po IS 'PO de la orden al momento del cambio (desnormalizado para reportes)';

-- ============================================================================
-- 6. VISTA PARA REPORTES DE AUDITORÍA
-- ============================================================================

CREATE OR REPLACE VIEW v_expense_audit_report AS
SELECT
    ea.id,
    ea.expense_id,
    ea.action,
    -- Descripción legible de la acción
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
    CASE
        WHEN ea.amount_change > 0 THEN '+$' || TO_CHAR(ea.amount_change, 'FM999,999,999.00')
        WHEN ea.amount_change < 0 THEN '-$' || TO_CHAR(ABS(ea.amount_change), 'FM999,999,999.00')
        ELSE '$0.00'
    END AS amount_change_formatted,

    -- Estados
    ea.old_status,
    ea.new_status,

    -- Fechas de pago
    ea.old_paid_date,
    ea.new_paid_date,
    ea.old_pay_method,
    ea.new_pay_method,

    -- Información del cambio
    ea.changed_at,
    TO_CHAR(ea.changed_at AT TIME ZONE 'America/Mexico_City', 'DD/MM/YYYY HH24:MI:SS') AS changed_at_formatted,
    ea.changed_by,
    COALESCE(ea.changed_by_fullname, ea.changed_by_username, 'Sistema') AS changed_by_display,
    ea.changed_by_role,

    -- Campos para análisis
    ea.days_until_due_old,
    ea.days_until_due_new,
    ea.change_reason,
    ea.ip_address,

    -- Calcular qué campos cambiaron (para UPDATE)
    CASE WHEN ea.action = 'UPDATE' THEN
        ARRAY_REMOVE(ARRAY[
            CASE WHEN ea.old_description != ea.new_description THEN 'descripción' END,
            CASE WHEN ea.old_total_expense != ea.new_total_expense THEN 'monto' END,
            CASE WHEN ea.old_status != ea.new_status THEN 'estado' END,
            CASE WHEN ea.old_paid_date IS DISTINCT FROM ea.new_paid_date THEN 'fecha de pago' END,
            CASE WHEN ea.old_pay_method IS DISTINCT FROM ea.new_pay_method THEN 'método de pago' END,
            CASE WHEN ea.old_expense_date != ea.new_expense_date THEN 'fecha de gasto' END,
            CASE WHEN ea.old_scheduled_date IS DISTINCT FROM ea.new_scheduled_date THEN 'fecha programada' END,
            CASE WHEN ea.old_order_id IS DISTINCT FROM ea.new_order_id THEN 'orden' END,
            CASE WHEN ea.old_supplier_id != ea.new_supplier_id THEN 'proveedor' END
        ], NULL)
    END AS fields_changed

FROM t_expense_audit ea
ORDER BY ea.changed_at DESC;

COMMENT ON VIEW v_expense_audit_report IS 'Vista formateada para reportes de auditoría de gastos';

-- ============================================================================
-- 7. FUNCIÓN PARA OBTENER HISTORIAL DE UN GASTO ESPECÍFICO
-- ============================================================================

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
        COALESCE(ea.changed_by_fullname, ea.changed_by_username, 'Sistema')::TEXT,
        ea.old_total_expense,
        ea.new_total_expense,
        ea.amount_change,
        ea.old_status,
        ea.new_status,
        CASE WHEN ea.action = 'UPDATE' THEN
            ARRAY_REMOVE(ARRAY[
                CASE WHEN ea.old_description != ea.new_description THEN 'descripción' END,
                CASE WHEN ea.old_total_expense != ea.new_total_expense THEN 'monto' END,
                CASE WHEN ea.old_status != ea.new_status THEN 'estado' END,
                CASE WHEN ea.old_paid_date IS DISTINCT FROM ea.new_paid_date THEN 'fecha de pago' END,
                CASE WHEN ea.old_pay_method IS DISTINCT FROM ea.new_pay_method THEN 'método de pago' END,
                CASE WHEN ea.old_expense_date != ea.new_expense_date THEN 'fecha de gasto' END,
                CASE WHEN ea.old_order_id IS DISTINCT FROM ea.new_order_id THEN 'orden' END
            ], NULL)
        END
    FROM t_expense_audit ea
    WHERE ea.expense_id = p_expense_id
    ORDER BY ea.changed_at DESC;
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION fn_get_expense_history IS 'Obtiene el historial completo de cambios de un gasto específico';

-- ============================================================================
-- 8. FUNCIÓN PARA REPORTE DE ACTIVIDAD POR PERÍODO
-- ============================================================================

CREATE OR REPLACE FUNCTION fn_expense_audit_summary(
    p_start_date DATE DEFAULT CURRENT_DATE - INTERVAL '30 days',
    p_end_date DATE DEFAULT CURRENT_DATE
)
RETURNS TABLE (
    action VARCHAR(20),
    action_description TEXT,
    total_count BIGINT,
    total_amount NUMERIC(18,2),
    unique_users BIGINT,
    unique_expenses BIGINT
) AS $$
BEGIN
    RETURN QUERY
    SELECT
        ea.action,
        CASE ea.action
            WHEN 'INSERT' THEN 'Gastos creados'
            WHEN 'UPDATE' THEN 'Gastos modificados'
            WHEN 'DELETE' THEN 'Gastos eliminados'
            WHEN 'PAID' THEN 'Gastos pagados'
            WHEN 'UNPAID' THEN 'Pagos revertidos'
        END::TEXT,
        COUNT(*)::BIGINT,
        COALESCE(SUM(ABS(ea.amount_change)), 0),
        COUNT(DISTINCT ea.changed_by)::BIGINT,
        COUNT(DISTINCT ea.expense_id)::BIGINT
    FROM t_expense_audit ea
    WHERE ea.changed_at::DATE BETWEEN p_start_date AND p_end_date
    GROUP BY ea.action
    ORDER BY COUNT(*) DESC;
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION fn_expense_audit_summary IS 'Resumen de actividad de auditoría por período';

-- ============================================================================
-- 9. FUNCIÓN PARA DETECTAR ACTIVIDAD SOSPECHOSA
-- ============================================================================

CREATE OR REPLACE FUNCTION fn_expense_audit_suspicious_activity(
    p_hours INTEGER DEFAULT 24
)
RETURNS TABLE (
    alert_type TEXT,
    description TEXT,
    expense_id INTEGER,
    changed_by_display TEXT,
    changed_at TIMESTAMP WITH TIME ZONE,
    details JSONB
) AS $$
BEGIN
    RETURN QUERY

    -- Múltiples cambios al mismo gasto en poco tiempo
    SELECT
        'MULTIPLE_CHANGES'::TEXT,
        'Múltiples cambios al mismo gasto'::TEXT,
        ea.expense_id,
        COALESCE(ea.changed_by_fullname, ea.changed_by_username, 'Sistema')::TEXT,
        MAX(ea.changed_at),
        jsonb_build_object('count', COUNT(*), 'period_hours', p_hours)
    FROM t_expense_audit ea
    WHERE ea.changed_at > NOW() - (p_hours || ' hours')::INTERVAL
    GROUP BY ea.expense_id, ea.changed_by_fullname, ea.changed_by_username
    HAVING COUNT(*) > 5

    UNION ALL

    -- Cambios grandes en monto (más de 50% de cambio)
    SELECT
        'LARGE_AMOUNT_CHANGE'::TEXT,
        'Cambio significativo en monto'::TEXT,
        ea.expense_id,
        COALESCE(ea.changed_by_fullname, ea.changed_by_username, 'Sistema')::TEXT,
        ea.changed_at,
        jsonb_build_object(
            'old_amount', ea.old_total_expense,
            'new_amount', ea.new_total_expense,
            'change_percent', ROUND(ABS(ea.amount_change) / NULLIF(ea.old_total_expense, 0) * 100, 2)
        )
    FROM t_expense_audit ea
    WHERE ea.changed_at > NOW() - (p_hours || ' hours')::INTERVAL
      AND ea.action = 'UPDATE'
      AND ea.old_total_expense > 0
      AND ABS(ea.amount_change) / ea.old_total_expense > 0.5

    UNION ALL

    -- Eliminaciones de gastos
    SELECT
        'DELETION'::TEXT,
        'Gasto eliminado'::TEXT,
        ea.expense_id,
        COALESCE(ea.changed_by_fullname, ea.changed_by_username, 'Sistema')::TEXT,
        ea.changed_at,
        jsonb_build_object(
            'deleted_amount', ea.old_total_expense,
            'supplier', ea.supplier_name,
            'description', ea.old_description
        )
    FROM t_expense_audit ea
    WHERE ea.changed_at > NOW() - (p_hours || ' hours')::INTERVAL
      AND ea.action = 'DELETE'

    UNION ALL

    -- Pagos revertidos
    SELECT
        'PAYMENT_REVERSED'::TEXT,
        'Pago revertido a pendiente'::TEXT,
        ea.expense_id,
        COALESCE(ea.changed_by_fullname, ea.changed_by_username, 'Sistema')::TEXT,
        ea.changed_at,
        jsonb_build_object(
            'amount', ea.new_total_expense,
            'original_paid_date', ea.old_paid_date,
            'supplier', ea.supplier_name
        )
    FROM t_expense_audit ea
    WHERE ea.changed_at > NOW() - (p_hours || ' hours')::INTERVAL
      AND ea.action = 'UNPAID'

    ORDER BY changed_at DESC;
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION fn_expense_audit_suspicious_activity IS 'Detecta actividad potencialmente sospechosa en gastos';

-- ============================================================================
-- 10. GRANT PERMISOS (ajustar según necesidad)
-- ============================================================================

-- Permitir a usuarios autenticados leer la auditoría
GRANT SELECT ON t_expense_audit TO authenticated;
GRANT SELECT ON v_expense_audit_report TO authenticated;

-- Solo service_role puede insertar/modificar directamente (el trigger lo hace)
GRANT ALL ON t_expense_audit TO service_role;
GRANT USAGE, SELECT ON SEQUENCE t_expense_audit_id_seq TO service_role;

-- ============================================================================
-- FIN DEL SCRIPT
-- ============================================================================

-- Verificación: mostrar que todo se creó correctamente
DO $$
BEGIN
    RAISE NOTICE '✅ Tabla t_expense_audit creada correctamente';
    RAISE NOTICE '✅ Trigger trg_expense_audit creado en t_expense';
    RAISE NOTICE '✅ Vista v_expense_audit_report creada';
    RAISE NOTICE '✅ Funciones de reporte creadas:';
    RAISE NOTICE '   - fn_get_expense_history(expense_id)';
    RAISE NOTICE '   - fn_expense_audit_summary(start_date, end_date)';
    RAISE NOTICE '   - fn_expense_audit_suspicious_activity(hours)';
END $$;
