-- ============================================================================
-- ACTUALIZAR FÓRMULA DE UTILIDAD EN BALANCE
-- Sistema de Gestión de Proyectos - IMA Mecatrónica
-- Fecha: 2026-01-27
-- ============================================================================
-- Este script actualiza la vista v_balance_completo para incluir los gastos
-- de órdenes (gasto_material, gasto_operativo, gasto_indirecto) en el cálculo
-- de la utilidad.
--
-- FÓRMULA NUEVA:
-- Utilidad = Ingresos_Esperados - (Nómina + Gastos_Fijos + Horas_Extra +
--            Gastos_Variables + Gasto_Operativo + Gasto_Indirecto)
--
-- NOTA: gasto_material ya está incluido en gastos_variables (viene de t_expense)
-- ============================================================================

-- PASO 0: Eliminar vistas existentes (en orden de dependencia)
-- ============================================================================
DROP VIEW IF EXISTS v_balance_completo CASCADE;
DROP VIEW IF EXISTS v_balance_gastos CASCADE;

-- PASO 1: Recrear v_balance_gastos con nuevas columnas
-- ============================================================================

CREATE VIEW v_balance_gastos AS
WITH rango_fechas AS (
    SELECT
        date_trunc('year', LEAST(
            COALESCE((SELECT min(f_hireddate) FROM t_payroll WHERE f_hireddate IS NOT NULL), CURRENT_DATE),
            COALESCE((SELECT min(effective_date) FROM t_fixed_expenses_history), CURRENT_DATE),
            COALESCE((SELECT min(f_paiddate) FROM t_expense WHERE f_paiddate IS NOT NULL), CURRENT_DATE),
            COALESCE((SELECT min(f_podate) FROM t_order WHERE f_podate IS NOT NULL), CURRENT_DATE)
        )) AS fecha_inicio,
        date_trunc('year', GREATEST(
            CURRENT_DATE,
            COALESCE((SELECT max(f_hireddate) FROM t_payroll), CURRENT_DATE),
            COALESCE((SELECT max(effective_date) FROM t_fixed_expenses_history), CURRENT_DATE)
        )) + INTERVAL '11 months' + INTERVAL '31 days' AS fecha_fin
),
meses AS (
    SELECT generate_series(
        date_trunc('month', (SELECT fecha_inicio FROM rango_fechas)),
        date_trunc('month', (SELECT fecha_fin FROM rango_fechas)),
        INTERVAL '1 month'
    )::DATE AS mes
),
nomina_mensual AS (
    SELECT
        m.mes,
        SUM(
            CASE
                WHEN p.is_active = true
                     AND (p.f_hireddate IS NULL OR p.f_hireddate <= (date_trunc('month', m.mes) + INTERVAL '1 month' - INTERVAL '1 day')::DATE)
                THEN COALESCE(
                    (SELECT ph.f_monthlypayroll
                     FROM t_payroll_history ph
                     WHERE ph.f_payroll = p.f_payroll
                       AND ph.effective_date <= m.mes
                     ORDER BY ph.effective_date DESC
                     LIMIT 1),
                    p.f_monthlypayroll
                )
                ELSE 0
            END
        ) AS total_nomina
    FROM meses m
    CROSS JOIN t_payroll p
    GROUP BY m.mes
),
horas_extra_mensual AS (
    SELECT
        make_date(year, month, 1) AS mes,
        amount AS total_horas_extra
    FROM t_overtime_hours
),
gastos_fijos_mensual AS (
    SELECT
        m.mes,
        SUM(
            CASE
                WHEN fe.is_active = true THEN
                    COALESCE(
                        (SELECT feh.monthly_amount
                         FROM t_fixed_expenses_history feh
                         WHERE feh.expense_id = fe.id
                           AND feh.effective_date <= m.mes
                           AND feh.change_type NOT IN ('DEACTIVATED', 'DELETED')
                         ORDER BY feh.effective_date DESC, feh.id DESC
                         LIMIT 1),
                        0
                    )
                ELSE 0
            END
        ) AS total_gastos_fijos
    FROM meses m
    CROSS JOIN t_fixed_expenses fe
    GROUP BY m.mes
),
gastos_variables_mensual AS (
    -- Gastos a proveedores pagados (incluye gasto_material de órdenes)
    SELECT
        m.mes,
        COALESCE(SUM(e.f_totalexpense), 0) AS total_gastos_variables
    FROM meses m
    LEFT JOIN t_expense e ON date_trunc('month', e.f_paiddate) = m.mes
                          AND e.f_status = 'PAGADO'
    GROUP BY m.mes
),
-- NUEVO: Gastos operativos e indirectos de órdenes por mes
gastos_ordenes_mensual AS (
    SELECT
        m.mes,
        COALESCE(SUM(o.gasto_operativo), 0) AS total_gasto_operativo,
        COALESCE(SUM(o.gasto_indirecto), 0) AS total_gasto_indirecto
    FROM meses m
    LEFT JOIN t_order o ON date_trunc('month', o.f_podate) = m.mes
                        AND o.f_podate IS NOT NULL
    GROUP BY m.mes
)
SELECT
    m.mes AS fecha,
    EXTRACT(YEAR FROM m.mes) AS año,
    EXTRACT(MONTH FROM m.mes) AS mes_numero,
    TO_CHAR(m.mes, 'TMMonth') AS mes_nombre,
    TO_CHAR(m.mes, 'Mon-YY') AS mes_corto,
    COALESCE(n.total_nomina, 0) AS nomina,
    COALESCE(he.total_horas_extra, 0) AS horas_extra,
    COALESCE(gf.total_gastos_fijos, 0) AS gastos_fijos,
    COALESCE(gv.total_gastos_variables, 0) AS gastos_variables,
    -- NUEVO: Gastos de órdenes
    COALESCE(go.total_gasto_operativo, 0) AS gasto_operativo,
    COALESCE(go.total_gasto_indirecto, 0) AS gasto_indirecto,
    -- Total gastos ahora incluye operativo e indirecto
    COALESCE(n.total_nomina, 0) +
    COALESCE(he.total_horas_extra, 0) +
    COALESCE(gf.total_gastos_fijos, 0) +
    COALESCE(gv.total_gastos_variables, 0) +
    COALESCE(go.total_gasto_operativo, 0) +
    COALESCE(go.total_gasto_indirecto, 0) AS total_gastos
FROM meses m
LEFT JOIN nomina_mensual n ON n.mes = m.mes
LEFT JOIN horas_extra_mensual he ON he.mes = m.mes
LEFT JOIN gastos_fijos_mensual gf ON gf.mes = m.mes
LEFT JOIN gastos_variables_mensual gv ON gv.mes = m.mes
LEFT JOIN gastos_ordenes_mensual go ON go.mes = m.mes
ORDER BY m.mes;

-- PASO 2: Recrear v_balance_completo con nuevos campos
-- ============================================================================

CREATE VIEW v_balance_completo AS
SELECT
    COALESCE(g.fecha, i.fecha) AS fecha,
    COALESCE(g.año, i.año) AS año,
    COALESCE(g.mes_numero, i.mes_numero) AS mes_numero,
    COALESCE(g.mes_nombre, i.mes_nombre) AS mes_nombre,
    COALESCE(g.mes_corto, i.mes_corto) AS mes_corto,
    -- Gastos desglosados
    COALESCE(g.nomina, 0) AS nomina,
    COALESCE(g.horas_extra, 0) AS horas_extra,
    COALESCE(g.gastos_fijos, 0) AS gastos_fijos,
    COALESCE(g.gastos_variables, 0) AS gastos_variables,
    -- NUEVO: Gastos de órdenes expuestos
    COALESCE(g.gasto_operativo, 0) AS gasto_operativo,
    COALESCE(g.gasto_indirecto, 0) AS gasto_indirecto,
    -- Total (ya incluye operativo e indirecto)
    COALESCE(g.total_gastos, 0) AS total_gastos,
    -- Ingresos
    COALESCE(i.ingresos_esperados, 0) AS ingresos_esperados,
    COALESCE(i.ingresos_percibidos, 0) AS ingresos_percibidos,
    COALESCE(i.ingresos_esperados, 0) - COALESCE(i.ingresos_percibidos, 0) AS diferencia,
    COALESCE(i.ventas_totales, 0) AS ventas_totales,
    -- UTILIDAD: Ingresos esperados - Total gastos (ahora incluye gastos de órdenes)
    COALESCE(i.ingresos_esperados, 0) - COALESCE(g.total_gastos, 0) AS utilidad_aproximada
FROM v_balance_gastos g
FULL JOIN v_balance_ingresos i ON g.fecha = i.fecha
WHERE COALESCE(g.año, i.año) IS NOT NULL
ORDER BY COALESCE(g.fecha, i.fecha);

-- PASO 3: Permisos
-- ============================================================================
GRANT SELECT ON v_balance_gastos TO authenticated;
GRANT SELECT ON v_balance_completo TO authenticated;

-- PASO 4: Verificación
-- ============================================================================

SELECT 'VERIFICACIÓN DE COLUMNAS EN v_balance_gastos:' AS info;
SELECT column_name, data_type
FROM information_schema.columns
WHERE table_name = 'v_balance_gastos'
  AND table_schema = 'public'
ORDER BY ordinal_position;

SELECT '' AS separator;

SELECT 'VERIFICACIÓN DE COLUMNAS EN v_balance_completo:' AS info;
SELECT column_name, data_type
FROM information_schema.columns
WHERE table_name = 'v_balance_completo'
  AND table_schema = 'public'
ORDER BY ordinal_position;

SELECT '' AS separator;

SELECT 'DATOS DE PRUEBA (Año actual):' AS info;
SELECT
    mes_corto,
    nomina,
    gastos_fijos,
    gastos_variables,
    gasto_operativo,
    gasto_indirecto,
    total_gastos,
    ingresos_esperados,
    utilidad_aproximada
FROM v_balance_completo
WHERE año = EXTRACT(YEAR FROM CURRENT_DATE)
ORDER BY mes_numero;

-- ============================================================================
-- RESUMEN DE CAMBIOS
-- ============================================================================
-- 1. v_balance_gastos ahora incluye:
--    - gasto_operativo: suma de gasto_operativo de órdenes del mes
--    - gasto_indirecto: suma de gasto_indirecto de órdenes del mes
--    - total_gastos: ahora incluye estos nuevos gastos
--
-- 2. v_balance_completo expone los nuevos campos y mantiene la fórmula:
--    utilidad_aproximada = ingresos_esperados - total_gastos
--    (donde total_gastos ahora es más completo)
--
-- 3. La app NO requiere cambios porque usa las mismas columnas
-- ============================================================================
