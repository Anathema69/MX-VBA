-- ============================================================
-- FIX: Corregir formula de gasto_operativo
-- ============================================================
-- FORMULA ANTERIOR: SUM(monto * (1 + f_commission_rate/100))
--   → Aplica comision individualmente a cada gasto (INCORRECTO)
--
-- FORMULA NUEVA:    SUM(monto) + commission_amount
--   → Suma gastos base + monto de comision del vendedor (CORRECTO)
--
-- EJECUTAR EN ORDEN: Paso 1, 2, 3, 4, 5
-- ============================================================


-- ============================================================
-- PASO 0: Verificar estado actual (antes de cambios)
-- ============================================================
SELECT o.f_order, o.f_po, o.gasto_operativo as actual,
       COALESCE(SUM(g.monto), 0) as suma_gastos,
       COALESCE(c.total_comision, 0) as comision_vendedor,
       COALESCE(SUM(g.monto), 0) + COALESCE(c.total_comision, 0) as nuevo_esperado
FROM t_order o
LEFT JOIN order_gastos_operativos g ON o.f_order = g.f_order
LEFT JOIN (
    SELECT f_order, SUM(commission_amount) as total_comision
    FROM t_vendor_commission_payment
    GROUP BY f_order
) c ON o.f_order = c.f_order
WHERE EXISTS (SELECT 1 FROM order_gastos_operativos WHERE f_order = o.f_order)
   OR c.total_comision > 0
GROUP BY o.f_order, o.f_po, o.gasto_operativo, c.total_comision
ORDER BY o.f_order;


-- ============================================================
-- PASO 1: Modificar funcion recalcular_gasto_operativo()
-- ============================================================
-- Trigger en order_gastos_operativos (AFTER INSERT/UPDATE/DELETE)
-- Nueva formula: SUM(monto) + SUM(commission_amount)

CREATE OR REPLACE FUNCTION recalcular_gasto_operativo()
RETURNS TRIGGER AS $$
DECLARE
    v_order_id integer;
BEGIN
    v_order_id := COALESCE(NEW.f_order, OLD.f_order);

    UPDATE t_order
    SET gasto_operativo = (
        SELECT COALESCE(SUM(monto), 0)
        FROM order_gastos_operativos
        WHERE f_order = v_order_id
    ) + COALESCE((
        SELECT SUM(commission_amount)
        FROM t_vendor_commission_payment
        WHERE f_order = v_order_id
    ), 0)
    WHERE f_order = v_order_id;

    RETURN COALESCE(NEW, OLD);
END;
$$ LANGUAGE plpgsql;

-- El trigger trg_recalcular_gasto_operativo ya existe, no necesita recrearse
-- Solo se reemplaza la funcion


-- ============================================================
-- PASO 2: Crear funcion y trigger en t_vendor_commission_payment
-- ============================================================
-- Cuando commission_amount cambia, recalcular gasto_operativo

CREATE OR REPLACE FUNCTION recalcular_gasto_operativo_por_comision()
RETURNS TRIGGER AS $$
DECLARE
    v_order_id integer;
BEGIN
    v_order_id := COALESCE(NEW.f_order, OLD.f_order);

    UPDATE t_order
    SET gasto_operativo = (
        SELECT COALESCE(SUM(monto), 0)
        FROM order_gastos_operativos
        WHERE f_order = v_order_id
    ) + COALESCE((
        SELECT SUM(commission_amount)
        FROM t_vendor_commission_payment
        WHERE f_order = v_order_id
    ), 0)
    WHERE f_order = v_order_id;

    RETURN COALESCE(NEW, OLD);
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_recalcular_gasto_op_por_comision
    AFTER INSERT OR UPDATE OR DELETE ON t_vendor_commission_payment
    FOR EACH ROW EXECUTE FUNCTION recalcular_gasto_operativo_por_comision();


-- ============================================================
-- PASO 3: Eliminar trigger que propaga rate a gastos individuales
-- ============================================================
-- Ya no necesario: la comision no se calcula por gasto individual

DROP TRIGGER IF EXISTS trg_sync_commission_rate ON t_order;
DROP FUNCTION IF EXISTS sync_commission_rate_to_gastos();


-- ============================================================
-- PASO 4: Recalcular gasto_operativo para TODAS las ordenes
-- ============================================================
-- Aplica la nueva formula a todos los registros existentes

UPDATE t_order o
SET gasto_operativo = COALESCE(gastos.suma, 0) + COALESCE(comision.total, 0)
FROM (
    SELECT f_order, SUM(monto) as suma
    FROM order_gastos_operativos
    GROUP BY f_order
) gastos
FULL OUTER JOIN (
    SELECT f_order, SUM(commission_amount) as total
    FROM t_vendor_commission_payment
    GROUP BY f_order
) comision ON gastos.f_order = comision.f_order
WHERE o.f_order = COALESCE(gastos.f_order, comision.f_order);


-- ============================================================
-- PASO 5: Verificar resultados
-- ============================================================
-- Comparar valores actualizados contra lo esperado

SELECT o.f_order, o.f_po, o.gasto_operativo as actual_nuevo,
       COALESCE(g.suma, 0) as suma_gastos,
       COALESCE(c.total, 0) as comision_vendedor,
       COALESCE(g.suma, 0) + COALESCE(c.total, 0) as esperado,
       CASE
           WHEN o.gasto_operativo = COALESCE(g.suma, 0) + COALESCE(c.total, 0)
           THEN 'OK' ELSE 'DIFERENCIA'
       END as estado
FROM t_order o
LEFT JOIN (
    SELECT f_order, SUM(monto) as suma
    FROM order_gastos_operativos
    GROUP BY f_order
) g ON o.f_order = g.f_order
LEFT JOIN (
    SELECT f_order, SUM(commission_amount) as total
    FROM t_vendor_commission_payment
    GROUP BY f_order
) c ON o.f_order = c.f_order
WHERE COALESCE(g.suma, 0) + COALESCE(c.total, 0) > 0
ORDER BY o.f_order;


-- ============================================================
-- OPCIONAL: Eliminar columna f_commission_rate de order_gastos_operativos
-- ============================================================
-- Descomentar si se decide eliminar (ya no se usa en el calculo)
-- ALTER TABLE order_gastos_operativos DROP COLUMN IF EXISTS f_commission_rate;
