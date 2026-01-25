-- ============================================================
-- VISTA: v_utilidad_mensual
-- Fecha: 26/01/2026
-- ============================================================
-- Calcula la utilidad aproximada por mes considerando:
-- - Ventas del mes (por fecha de PO)
-- - Gasto de material de las órdenes
-- - Proporción de nómina y gastos fijos según días estimados
-- ============================================================

-- ============================================================
-- PREGUNTAS A RESPONDER ANTES DE EJECUTAR:
-- ============================================================
-- 1. ¿Qué fecha determina el mes de una orden?
--    OPCIÓN A: f_podate (fecha de orden de compra) <- ASUMIDO
--    OPCIÓN B: Fecha de factura cobrada
--    OPCIÓN C: Fecha de pago recibido
--
-- 2. ¿Qué hacer con órdenes sin dias_estimados?
--    OPCIÓN A: Excluirlas del cálculo
--    OPCIÓN B: Usar un valor por defecto (ej: 15 días)
--    OPCIÓN C: Usar la diferencia entre f_estdelivery y f_podate
-- ============================================================

-- ============================================================
-- OPCIÓN A: Vista usando f_podate como fecha del mes
-- ============================================================
CREATE OR REPLACE VIEW v_utilidad_mensual AS
WITH
-- Generar meses del rango de datos
meses AS (
    SELECT generate_series(
        date_trunc('month', (SELECT MIN(f_podate) FROM t_order WHERE f_podate IS NOT NULL)),
        date_trunc('month', CURRENT_DATE) + INTERVAL '11 months',
        '1 month'::interval
    )::date AS mes
),

-- Ventas y gastos por orden, agrupados por mes
ordenes_mes AS (
    SELECT
        date_trunc('month', o.f_podate)::date AS mes,
        COUNT(*) AS num_ordenes,
        SUM(COALESCE(o.f_saletotal, 0)) AS ventas_mes,
        SUM(COALESCE(g.gasto_material, 0)) AS gasto_material_mes,
        SUM(COALESCE(o.dias_estimados, 0)) AS dias_estimados_total,
        -- Contar órdenes con días estimados
        COUNT(CASE WHEN o.dias_estimados IS NOT NULL AND o.dias_estimados > 0 THEN 1 END) AS ordenes_con_dias
    FROM t_order o
    LEFT JOIN v_order_gastos g ON o.f_order = g.f_order
    WHERE o.f_podate IS NOT NULL
    GROUP BY date_trunc('month', o.f_podate)
),

-- Gastos fijos y nómina por mes (de v_balance_gastos)
gastos_mes AS (
    SELECT
        fecha AS mes,
        nomina,
        gastos_fijos,
        nomina + gastos_fijos AS base_gastos
    FROM v_balance_gastos
)

SELECT
    m.mes,
    EXTRACT(YEAR FROM m.mes) AS año,
    EXTRACT(MONTH FROM m.mes) AS mes_numero,
    to_char(m.mes, 'TMMonth') AS mes_nombre,

    -- Ventas
    COALESCE(o.ventas_mes, 0) AS ventas_totales,
    COALESCE(o.num_ordenes, 0) AS cantidad_ordenes,

    -- Gastos de material
    COALESCE(o.gasto_material_mes, 0) AS gasto_material,

    -- Días estimados
    COALESCE(o.dias_estimados_total, 0) AS dias_estimados_total,
    COALESCE(o.ordenes_con_dias, 0) AS ordenes_con_dias,

    -- Proporción del mes (días/30)
    CASE
        WHEN COALESCE(o.dias_estimados_total, 0) > 0
        THEN LEAST(o.dias_estimados_total::numeric / 30, 1)  -- Máximo 100%
        ELSE 0
    END AS proporcion_mes,

    -- Gastos fijos proporcionales
    COALESCE(g.base_gastos, 0) AS base_gastos,
    CASE
        WHEN COALESCE(o.dias_estimados_total, 0) > 0
        THEN COALESCE(g.base_gastos, 0) * LEAST(o.dias_estimados_total::numeric / 30, 1)
        ELSE 0
    END AS gastos_proporcionales,

    -- UTILIDAD APROXIMADA (fórmula final)
    COALESCE(o.ventas_mes, 0)
    - COALESCE(o.gasto_material_mes, 0)
    - (
        CASE
            WHEN COALESCE(o.dias_estimados_total, 0) > 0
            THEN COALESCE(g.base_gastos, 0) * LEAST(o.dias_estimados_total::numeric / 30, 1)
            ELSE 0
        END
    ) AS utilidad_aproximada

FROM meses m
LEFT JOIN ordenes_mes o ON o.mes = m.mes
LEFT JOIN gastos_mes g ON g.mes = m.mes
ORDER BY m.mes;

-- ============================================================
-- VERIFICACIÓN
-- ============================================================
/*
SELECT
    mes,
    año,
    mes_numero,
    ventas_totales,
    gasto_material,
    dias_estimados_total,
    proporcion_mes,
    base_gastos,
    gastos_proporcionales,
    utilidad_aproximada
FROM v_utilidad_mensual
WHERE año = 2026
ORDER BY mes_numero;
*/

-- ============================================================
-- NOTAS:
-- ============================================================
-- 1. Esta vista DEPENDE de:
--    - v_order_gastos (para gasto_material)
--    - v_balance_gastos (para nómina y gastos_fijos)
--    - Columna dias_estimados en t_order
--
-- 2. Si dias_estimados no está poblado:
--    - La proporción será 0
--    - Los gastos proporcionales serán 0
--    - La utilidad = ventas - gasto_material
--
-- 3. ALTERNATIVA: Si se quiere usar días reales:
--    - Cambiar o.dias_estimados por (o.f_estdelivery - o.f_podate)
--    - O usar el campo f_expense ya existente
-- ============================================================
