-- ============================================================
-- IMPLEMENTACIÓN: Eliminación de órdenes con auditoría
-- Fecha: 2025-12-23
-- ============================================================
-- Este script:
-- 1. Crea tabla t_order_deleted para auditoría de eliminaciones
-- 2. Crea función para eliminar orden con validaciones
-- 3. Registra snapshot completo antes de eliminar
-- ============================================================

-- ============================================================
-- PASO 1: Crear tabla de órdenes eliminadas (auditoría)
-- ============================================================
CREATE TABLE IF NOT EXISTS t_order_deleted (
    id SERIAL PRIMARY KEY,

    -- Datos originales de la orden
    original_order_id INTEGER NOT NULL,
    f_po VARCHAR(100),
    f_quote VARCHAR(100),
    f_client INTEGER,
    f_contact INTEGER,
    f_salesman INTEGER,
    f_podate DATE,
    f_estdelivery DATE,
    f_description TEXT,
    f_salesubtotal NUMERIC,
    f_saletotal NUMERIC,
    f_orderstat INTEGER,
    f_expense NUMERIC,
    progress_percentage INTEGER,
    order_percentage INTEGER,
    f_commission_rate NUMERIC,

    -- Datos de auditoría
    deleted_by INTEGER NOT NULL,
    deleted_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    deletion_reason TEXT,

    -- Snapshot completo en JSON por si se necesita más info
    full_order_snapshot JSONB
);

-- Índices para búsqueda rápida
CREATE INDEX IF NOT EXISTS idx_order_deleted_original_id ON t_order_deleted(original_order_id);
CREATE INDEX IF NOT EXISTS idx_order_deleted_po ON t_order_deleted(f_po);
CREATE INDEX IF NOT EXISTS idx_order_deleted_date ON t_order_deleted(deleted_at);

COMMENT ON TABLE t_order_deleted IS 'Auditoría de órdenes eliminadas permanentemente';

-- ============================================================
-- PASO 2: Función para eliminar orden con validaciones
-- ============================================================
CREATE OR REPLACE FUNCTION delete_order_with_audit(
    p_order_id INTEGER,
    p_deleted_by INTEGER,
    p_reason TEXT DEFAULT 'Orden creada por error'
)
RETURNS TABLE(
    success BOOLEAN,
    message TEXT,
    deleted_order_id INTEGER
) AS $$
DECLARE
    v_order RECORD;
    v_invoice_count INTEGER;
    v_expense_count INTEGER;
    v_commission_count INTEGER;
BEGIN
    -- Verificar que la orden existe
    SELECT * INTO v_order FROM t_order WHERE f_order = p_order_id;

    IF NOT FOUND THEN
        RETURN QUERY SELECT FALSE, 'Orden no encontrada', NULL::INTEGER;
        RETURN;
    END IF;

    -- Verificar que no tenga facturas
    SELECT COUNT(*) INTO v_invoice_count FROM t_invoice WHERE f_order = p_order_id;
    IF v_invoice_count > 0 THEN
        RETURN QUERY SELECT FALSE,
            'No se puede eliminar: La orden tiene ' || v_invoice_count || ' factura(s) asociada(s). Use CANCELAR en su lugar.',
            NULL::INTEGER;
        RETURN;
    END IF;

    -- Verificar que no tenga gastos
    SELECT COUNT(*) INTO v_expense_count FROM t_expense WHERE f_order = p_order_id;
    IF v_expense_count > 0 THEN
        RETURN QUERY SELECT FALSE,
            'No se puede eliminar: La orden tiene ' || v_expense_count || ' gasto(s) asociado(s). Use CANCELAR en su lugar.',
            NULL::INTEGER;
        RETURN;
    END IF;

    -- Verificar que no tenga comisiones pagadas
    SELECT COUNT(*) INTO v_commission_count FROM t_vendor_commission_payment WHERE f_order = p_order_id;
    IF v_commission_count > 0 THEN
        RETURN QUERY SELECT FALSE,
            'No se puede eliminar: La orden tiene ' || v_commission_count || ' pago(s) de comisión asociado(s). Use CANCELAR en su lugar.',
            NULL::INTEGER;
        RETURN;
    END IF;

    -- Guardar en tabla de auditoría antes de eliminar
    INSERT INTO t_order_deleted (
        original_order_id, f_po, f_quote, f_client, f_contact, f_salesman,
        f_podate, f_estdelivery, f_description, f_salesubtotal, f_saletotal,
        f_orderstat, f_expense, progress_percentage, order_percentage,
        f_commission_rate, deleted_by, deletion_reason, full_order_snapshot
    ) VALUES (
        v_order.f_order, v_order.f_po, v_order.f_quote, v_order.f_client,
        v_order.f_contact, v_order.f_salesman, v_order.f_podate, v_order.f_estdelivery,
        v_order.f_description, v_order.f_salesubtotal, v_order.f_saletotal,
        v_order.f_orderstat, v_order.f_expense, v_order.progress_percentage,
        v_order.order_percentage, v_order.f_commission_rate, p_deleted_by,
        p_reason, to_jsonb(v_order)
    );

    -- Eliminar la orden (order_history se elimina por CASCADE)
    DELETE FROM t_order WHERE f_order = p_order_id;

    RETURN QUERY SELECT TRUE,
        'Orden ' || v_order.f_po || ' eliminada exitosamente',
        p_order_id;
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION delete_order_with_audit IS 'Elimina una orden verificando dependencias y guardando auditoría';

-- ============================================================
-- PASO 3: Función simple para verificar si se puede eliminar
-- ============================================================
CREATE OR REPLACE FUNCTION can_delete_order(p_order_id INTEGER)
RETURNS TABLE(
    can_delete BOOLEAN,
    reason TEXT,
    invoice_count INTEGER,
    expense_count INTEGER,
    commission_count INTEGER
) AS $$
DECLARE
    v_invoices INTEGER;
    v_expenses INTEGER;
    v_commissions INTEGER;
BEGIN
    SELECT COUNT(*) INTO v_invoices FROM t_invoice WHERE f_order = p_order_id;
    SELECT COUNT(*) INTO v_expenses FROM t_expense WHERE f_order = p_order_id;
    SELECT COUNT(*) INTO v_commissions FROM t_vendor_commission_payment WHERE f_order = p_order_id;

    IF v_invoices > 0 OR v_expenses > 0 OR v_commissions > 0 THEN
        RETURN QUERY SELECT FALSE,
            'Orden tiene dependencias: ' || v_invoices || ' facturas, ' || v_expenses || ' gastos, ' || v_commissions || ' comisiones',
            v_invoices, v_expenses, v_commissions;
    ELSE
        RETURN QUERY SELECT TRUE, 'Orden puede ser eliminada', v_invoices, v_expenses, v_commissions;
    END IF;
END;
$$ LANGUAGE plpgsql;

-- ============================================================
-- VERIFICACIÓN: Probar que todo está creado correctamente
-- ============================================================
-- SELECT * FROM can_delete_order(1167);  -- Orden con factura (debería retornar FALSE)
-- SELECT * FROM can_delete_order(1168);  -- Orden sin dependencias (debería retornar TRUE)

-- Para eliminar una orden de prueba (CUIDADO):
-- SELECT * FROM delete_order_with_audit(ID_ORDEN, ID_USUARIO, 'Razón de eliminación');

-- Ver órdenes eliminadas:
-- SELECT * FROM t_order_deleted ORDER BY deleted_at DESC;
