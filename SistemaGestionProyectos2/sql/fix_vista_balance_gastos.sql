-- ============================================================
-- FIX: Vista v_balance_gastos
-- Fecha: 2025-12-23
-- ============================================================
-- CAMBIOS:
-- 1. Agregar "id DESC" en ORDER BY para obtener el registro más reciente
--    cuando hay múltiples con la misma effective_date
-- 2. Excluir registros con change_type IN ('DEACTIVATED', 'DELETED')
-- ============================================================

CREATE OR REPLACE VIEW v_balance_gastos AS
WITH rango_fechas AS (
    SELECT
        date_trunc('year'::text, LEAST(
            COALESCE((SELECT min(t_payroll.f_hireddate) FROM t_payroll WHERE t_payroll.f_hireddate IS NOT NULL), CURRENT_DATE),
            COALESCE((SELECT min(t_fixed_expenses_history.effective_date) FROM t_fixed_expenses_history), CURRENT_DATE),
            COALESCE((SELECT min(t_expense.f_paiddate) FROM t_expense WHERE t_expense.f_paiddate IS NOT NULL), CURRENT_DATE)
        )::timestamp with time zone) AS fecha_inicio,
        date_trunc('year'::text, GREATEST(
            CURRENT_DATE,
            COALESCE((SELECT max(t_payroll.f_hireddate) FROM t_payroll), CURRENT_DATE),
            COALESCE((SELECT max(t_fixed_expenses_history.effective_date) FROM t_fixed_expenses_history), CURRENT_DATE)
        )::timestamp with time zone) + '11 mons'::interval + '31 days'::interval AS fecha_fin
),
meses AS (
    SELECT generate_series(
        date_trunc('month'::text, (SELECT rango_fechas.fecha_inicio FROM rango_fechas)),
        date_trunc('month'::text, (SELECT rango_fechas.fecha_fin FROM rango_fechas)),
        '1 mon'::interval
    )::date AS mes
),
nomina_mensual AS (
    SELECT
        m.mes,
        sum(
            CASE
                WHEN p.is_active = true
                AND (p.f_hireddate IS NULL OR p.f_hireddate <= (date_trunc('month'::text, m.mes::timestamp with time zone) + '1 mon'::interval - '1 day'::interval)::date)
                THEN COALESCE((
                    SELECT ph.f_monthlypayroll
                    FROM t_payroll_history ph
                    WHERE ph.f_payroll = p.f_payroll AND ph.effective_date <= m.mes
                    ORDER BY ph.effective_date DESC
                    LIMIT 1
                ), p.f_monthlypayroll)
                ELSE 0::numeric
            END
        ) AS total_nomina
    FROM meses m
    CROSS JOIN t_payroll p
    GROUP BY m.mes
),
horas_extra_mensual AS (
    SELECT
        make_date(t_overtime_hours.year, t_overtime_hours.month, 1) AS mes,
        t_overtime_hours.amount AS total_horas_extra
    FROM t_overtime_hours
),
gastos_fijos_mensual AS (
    SELECT
        m.mes,
        sum(
            CASE
                WHEN fe.is_active = true THEN COALESCE((
                    SELECT feh.monthly_amount
                    FROM t_fixed_expenses_history feh
                    WHERE feh.expense_id = fe.id
                      AND feh.effective_date <= m.mes
                      AND feh.change_type NOT IN ('DEACTIVATED', 'DELETED')  -- ✅ FIX: Excluir desactivados/eliminados
                    ORDER BY feh.effective_date DESC, feh.id DESC  -- ✅ FIX: Agregar id DESC
                    LIMIT 1
                ), 0::numeric)
                ELSE 0::numeric
            END
        ) AS total_gastos_fijos
    FROM meses m
    CROSS JOIN t_fixed_expenses fe
    GROUP BY m.mes
),
gastos_variables_mensual AS (
    SELECT
        m.mes,
        COALESCE(sum(e.f_totalexpense), 0::numeric) AS total_gastos_variables
    FROM meses m
    LEFT JOIN t_expense e ON date_trunc('month'::text, e.f_paiddate::timestamp with time zone) = m.mes
        AND e.f_status::text = 'PAGADO'::text
    GROUP BY m.mes
)
SELECT
    m.mes AS fecha,
    EXTRACT(year FROM m.mes) AS "año",
    EXTRACT(month FROM m.mes) AS mes_numero,
    to_char(m.mes::timestamp with time zone, 'TMMonth'::text) AS mes_nombre,
    to_char(m.mes::timestamp with time zone, 'Mon-YY'::text) AS mes_corto,
    COALESCE(n.total_nomina, 0::numeric) AS nomina,
    COALESCE(he.total_horas_extra, 0::numeric) AS horas_extra,
    COALESCE(gf.total_gastos_fijos, 0::numeric) AS gastos_fijos,
    COALESCE(gv.total_gastos_variables, 0::numeric) AS gastos_variables,
    COALESCE(n.total_nomina, 0::numeric) + COALESCE(he.total_horas_extra, 0::numeric) + COALESCE(gf.total_gastos_fijos, 0::numeric) + COALESCE(gv.total_gastos_variables, 0::numeric) AS total_gastos
FROM meses m
LEFT JOIN nomina_mensual n ON n.mes = m.mes
LEFT JOIN horas_extra_mensual he ON he.mes = m.mes
LEFT JOIN gastos_fijos_mensual gf ON gf.mes = m.mes
LEFT JOIN gastos_variables_mensual gv ON gv.mes = m.mes
ORDER BY m.mes;

-- ============================================================
-- VERIFICACIÓN: Ejecutar después de crear la vista
-- ============================================================
-- SELECT fecha, año, mes_numero, gastos_fijos
-- FROM v_balance_gastos
-- WHERE año = 2025
-- ORDER BY mes_numero;
