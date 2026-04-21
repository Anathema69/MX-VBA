# Vistas e Indices - IMA Mecatronica

**Fecha de extraccion:** 26 de Enero de 2026
**Ultima actualizacion:** 27 de Enero de 2026 - Agregados gastos de ordenes al balance

---

## Resumen

| Elemento | Cantidad |
|----------|----------|
| Vistas | 10 |
| Indices | 95 |

---

## Vistas de Base de Datos

### Lista de Vistas

| Vista | Descripcion |
|-------|-------------|
| `v_attendance_history` | Historial de cambios en asistencias con descripcion legible |
| `v_attendance_monthly_summary` | Resumen mensual de asistencias por empleado |
| `v_attendance_stats` | Estadisticas generales de asistencia del mes actual |
| `v_attendance_today` | Estado de asistencia del dia actual por empleado |
| `v_balance_completo` | Balance mensual completo (gastos + ingresos + utilidad) |
| `v_balance_gastos` | Desglose de gastos mensuales (nomina, fijos, variables) |
| `v_balance_ingresos` | Ingresos esperados y percibidos por mes |
| `v_income` | Detalle de ingresos por factura con fecha efectiva de pago |
| `v_order_gastos` | Ordenes con gastos calculados (material, operativo, indirecto) |
| `v_vacations_active` | Vacaciones activas, proximas y finalizadas |

---

### Definiciones de Vistas

#### v_attendance_history

**Proposito:** Historial de cambios en asistencias con descripcion legible

```sql
CREATE OR REPLACE VIEW v_attendance_history AS
 SELECT a.id,
    a.attendance_id,
    p.f_employee AS employee_name,
    a.attendance_date,
    a.action,
    a.old_status,
    a.new_status,
        CASE
            WHEN a.action::text = 'INSERT'::text THEN 'Registro creado: '::text || a.new_status::text
            WHEN a.action::text = 'UPDATE'::text THEN (('Cambio de '::text || COALESCE(a.old_status, 'N/A'::character varying)::text) || ' a '::text) || a.new_status::text
            WHEN a.action::text = 'DELETE'::text THEN 'Registro eliminado: '::text || a.old_status::text
            ELSE NULL::text
        END AS change_description,
    u.username AS changed_by_user,
    a.changed_at
   FROM t_attendance_audit a
     LEFT JOIN t_payroll p ON a.employee_id = p.f_payroll
     LEFT JOIN users u ON a.changed_by = u.id
  ORDER BY a.changed_at DESC
```

---

#### v_attendance_monthly_summary

**Proposito:** Resumen mensual de asistencias por empleado

```sql
CREATE OR REPLACE VIEW v_attendance_monthly_summary AS
 SELECT p.f_payroll AS employee_id,
    p.f_employee AS employee_name,
    p.f_title AS title,
    p.employee_code,
    date_trunc('month'::text, a.attendance_date::timestamp with time zone)::date AS month,
    to_char(date_trunc('month'::text, a.attendance_date::timestamp with time zone), 'YYYY-MM'::text) AS month_key,
    count(
        CASE
            WHEN a.status::text = 'ASISTENCIA'::text THEN 1
            ELSE NULL::integer
        END) AS asistencias,
    count(
        CASE
            WHEN a.status::text = 'RETARDO'::text THEN 1
            ELSE NULL::integer
        END) AS retardos,
    count(
        CASE
            WHEN a.status::text = 'FALTA'::text THEN 1
            ELSE NULL::integer
        END) AS faltas,
    count(
        CASE
            WHEN a.status::text = 'VACACIONES'::text THEN 1
            ELSE NULL::integer
        END) AS vacaciones,
    count(
        CASE
            WHEN a.status::text = 'FERIADO'::text THEN 1
            ELSE NULL::integer
        END) AS feriados,
    count(
        CASE
            WHEN a.status::text = 'DESCANSO'::text THEN 1
            ELSE NULL::integer
        END) AS descansos,
    sum(COALESCE(a.late_minutes, 0)) AS total_late_minutes,
    count(
        CASE
            WHEN a.is_justified = true AND (a.status::text = ANY (ARRAY['FALTA'::character varying, 'RETARDO'::character varying]::text[])) THEN 1
            ELSE NULL::integer
        END) AS justified_count
   FROM t_payroll p
     LEFT JOIN t_attendance a ON p.f_payroll = a.employee_id
  WHERE p.is_active = true
  GROUP BY p.f_payroll, p.f_employee, p.f_title, p.employee_code, (date_trunc('month'::text, a.attendance_date::timestamp with time zone))
```

---

#### v_attendance_stats

**Proposito:** Estadisticas generales de asistencia del mes actual

```sql
CREATE OR REPLACE VIEW v_attendance_stats AS
 SELECT date_trunc('month'::text, CURRENT_DATE::timestamp with time zone)::date AS current_month,
    ( SELECT count(*) AS count
           FROM t_payroll
          WHERE t_payroll.is_active = true) AS total_employees,
    ( SELECT count(*) AS count
           FROM t_attendance
          WHERE date_trunc('month'::text, t_attendance.attendance_date::timestamp with time zone) = date_trunc('month'::text, CURRENT_DATE::timestamp with time zone) AND t_attendance.status::text = 'ASISTENCIA'::text) AS total_asistencias,
    ( SELECT count(*) AS count
           FROM t_attendance
          WHERE date_trunc('month'::text, t_attendance.attendance_date::timestamp with time zone) = date_trunc('month'::text, CURRENT_DATE::timestamp with time zone) AND t_attendance.status::text = 'RETARDO'::text) AS total_retardos,
    ( SELECT count(*) AS count
           FROM t_attendance
          WHERE date_trunc('month'::text, t_attendance.attendance_date::timestamp with time zone) = date_trunc('month'::text, CURRENT_DATE::timestamp with time zone) AND t_attendance.status::text = 'FALTA'::text) AS total_faltas,
    ( SELECT count(*) AS count
           FROM t_attendance
          WHERE date_trunc('month'::text, t_attendance.attendance_date::timestamp with time zone) = date_trunc('month'::text, CURRENT_DATE::timestamp with time zone) AND t_attendance.status::text = 'VACACIONES'::text) AS total_vacaciones,
    ( SELECT sum(t_attendance.late_minutes) AS sum
           FROM t_attendance
          WHERE date_trunc('month'::text, t_attendance.attendance_date::timestamp with time zone) = date_trunc('month'::text, CURRENT_DATE::timestamp with time zone)) AS total_late_minutes
```

---

#### v_attendance_today

**Proposito:** Estado de asistencia del dia actual por empleado

```sql
CREATE OR REPLACE VIEW v_attendance_today AS
 SELECT p.f_payroll AS employee_id,
    p.f_employee AS employee_name,
    p.f_title AS title,
    p.employee_code,
    "substring"(p.f_employee::text, 1, 1) || COALESCE("substring"(split_part(p.f_employee::text, ' '::text, 2), 1, 1), ''::text) AS initials,
    CURRENT_DATE AS today,
    a.id AS attendance_id,
    COALESCE(a.status, 'SIN_REGISTRO'::character varying) AS status,
    a.check_in_time,
    a.check_out_time,
    a.late_minutes,
    a.notes,
    a.is_justified,
        CASE
            WHEN v.id IS NOT NULL THEN true
            ELSE false
        END AS on_vacation,
    v.start_date AS vacation_start,
    v.end_date AS vacation_end,
        CASE
            WHEN h.id IS NOT NULL THEN true
            ELSE false
        END AS is_holiday,
    h.name AS holiday_name,
    wc.is_workday
   FROM t_payroll p
     LEFT JOIN t_attendance a ON p.f_payroll = a.employee_id AND a.attendance_date = CURRENT_DATE
     LEFT JOIN t_vacation v ON p.f_payroll = v.employee_id AND v.status::text = 'APROBADA'::text AND CURRENT_DATE >= v.start_date AND CURRENT_DATE <= v.end_date
     LEFT JOIN t_holiday h ON h.holiday_date = CURRENT_DATE AND h.is_mandatory = true
     LEFT JOIN t_workday_config wc ON wc.day_of_week::numeric = EXTRACT(dow FROM CURRENT_DATE)
  WHERE p.is_active = true
  ORDER BY p.f_employee
```

---

#### v_balance_completo

**Proposito:** Balance mensual completo (gastos + ingresos + utilidad)

**Columnas:**
| Columna | Descripcion |
|---------|-------------|
| `fecha` | Primer dia del mes |
| `año` | Año |
| `mes_numero` | Numero del mes (1-12) |
| `mes_nombre` | Nombre del mes |
| `mes_corto` | Formato corto (Ene-26) |
| `nomina` | Total nomina mensual |
| `horas_extra` | Total horas extra |
| `gastos_fijos` | Total gastos fijos |
| `gastos_variables` | Gastos a proveedores pagados |
| `gasto_operativo` | **Suma de gastos operativos de ordenes** *(Nuevo)* |
| `gasto_indirecto` | **Suma de gastos indirectos de ordenes** *(Nuevo)* |
| `total_gastos` | Suma de todos los gastos (incluye operativo e indirecto) |
| `ingresos_esperados` | Ingresos esperados por facturas |
| `ingresos_percibidos` | Ingresos cobrados |
| `diferencia` | ingresos_esperados - ingresos_percibidos |
| `ventas_totales` | Total ventas del mes |
| `utilidad_aproximada` | **ventas_totales - (gastos_fijos + gastos_variables + gasto_operativo + gasto_indirecto)** |

**Formula de Utilidad (actualizada 2026-01-27):**
```
utilidad_aproximada = ventas_totales - (gastos_fijos + gastos_variables + gasto_operativo + gasto_indirecto)
```

**Nota:** Se excluyen `nomina` y `horas_extra` del cálculo de utilidad. Estos campos se mantienen en la vista para visualización pero no afectan la utilidad.

```sql
CREATE OR REPLACE VIEW v_balance_completo AS
SELECT
    COALESCE(g.fecha, i.fecha) AS fecha,
    COALESCE(g.año, i.año) AS año,
    COALESCE(g.mes_numero, i.mes_numero) AS mes_numero,
    COALESCE(g.mes_nombre, i.mes_nombre) AS mes_nombre,
    COALESCE(g.mes_corto, i.mes_corto) AS mes_corto,
    -- Gastos (se mantienen todos para visualización)
    COALESCE(g.nomina, 0) AS nomina,
    COALESCE(g.horas_extra, 0) AS horas_extra,
    COALESCE(g.gastos_fijos, 0) AS gastos_fijos,
    COALESCE(g.gastos_variables, 0) AS gastos_variables,
    COALESCE(g.gasto_operativo, 0) AS gasto_operativo,
    COALESCE(g.gasto_indirecto, 0) AS gasto_indirecto,
    COALESCE(g.total_gastos, 0) AS total_gastos,
    -- Ingresos
    COALESCE(i.ingresos_esperados, 0) AS ingresos_esperados,
    COALESCE(i.ingresos_percibidos, 0) AS ingresos_percibidos,
    COALESCE(i.ingresos_esperados, 0) - COALESCE(i.ingresos_percibidos, 0) AS diferencia,
    COALESCE(i.ventas_totales, 0) AS ventas_totales,
    -- UTILIDAD: ventas - (gastos_fijos + gastos_variables + gasto_operativo + gasto_indirecto)
    -- Se excluyen: nomina y horas_extra
    COALESCE(i.ventas_totales, 0) - (
        COALESCE(g.gastos_fijos, 0) +
        COALESCE(g.gastos_variables, 0) +
        COALESCE(g.gasto_operativo, 0) +
        COALESCE(g.gasto_indirecto, 0)
    ) AS utilidad_aproximada
FROM v_balance_gastos g
FULL JOIN v_balance_ingresos i ON g.fecha = i.fecha
WHERE COALESCE(g.año, i.año) IS NOT NULL
ORDER BY COALESCE(g.fecha, i.fecha);
```

---

#### v_balance_gastos

**Proposito:** Desglose de gastos mensuales (nomina, fijos, variables, operativos, indirectos)

**Columnas (actualizado 2026-01-27):**
| Columna | Descripcion |
|---------|-------------|
| `fecha` | Primer dia del mes |
| `año` | Año |
| `mes_numero` | Numero del mes (1-12) |
| `mes_nombre` | Nombre del mes |
| `mes_corto` | Formato corto |
| `nomina` | Total nomina mensual |
| `horas_extra` | Total horas extra |
| `gastos_fijos` | Total gastos fijos |
| `gastos_variables` | Gastos a proveedores pagados |
| `gasto_operativo` | **Suma de gasto_operativo de ordenes del mes** *(Nuevo)* |
| `gasto_indirecto` | **Suma de gasto_indirecto de ordenes del mes** *(Nuevo)* |
| `total_gastos` | Suma de todos los gastos |

```sql
CREATE OR REPLACE VIEW v_balance_gastos AS
 WITH rango_fechas AS (
         SELECT date_trunc('year'::text, LEAST(
             COALESCE(( SELECT min(t_payroll.f_hireddate) AS min
                   FROM t_payroll
                  WHERE t_payroll.f_hireddate IS NOT NULL), CURRENT_DATE),
             COALESCE(( SELECT min(t_fixed_expenses_history.effective_date) AS min
                   FROM t_fixed_expenses_history), CURRENT_DATE),
             COALESCE(( SELECT min(t_expense.f_paiddate) AS min
                   FROM t_expense
                  WHERE t_expense.f_paiddate IS NOT NULL), CURRENT_DATE),
             COALESCE(( SELECT min(t_order.f_podate) AS min
                   FROM t_order
                  WHERE t_order.f_podate IS NOT NULL), CURRENT_DATE)
         )::timestamp with time zone) AS fecha_inicio,
            date_trunc('year'::text, GREATEST(CURRENT_DATE,
             COALESCE(( SELECT max(t_payroll.f_hireddate) AS max
                   FROM t_payroll), CURRENT_DATE),
             COALESCE(( SELECT max(t_fixed_expenses_history.effective_date) AS max
                   FROM t_fixed_expenses_history), CURRENT_DATE)
         )::timestamp with time zone) + '11 mons'::interval + '31 days'::interval AS fecha_fin
        ), meses AS (
         SELECT generate_series(date_trunc('month'::text, ( SELECT rango_fechas.fecha_inicio
                   FROM rango_fechas)), date_trunc('month'::text, ( SELECT rango_fechas.fecha_fin
                   FROM rango_fechas)), '1 mon'::interval)::date AS mes
        ), nomina_mensual AS (
         SELECT m_1.mes,
            sum(
                CASE
                    WHEN p.is_active = true AND (p.f_hireddate IS NULL OR p.f_hireddate <= (date_trunc('month'::text, m_1.mes::timestamp with time zone) + '1 mon'::interval - '1 day'::interval)::date) THEN COALESCE(( SELECT ph.f_monthlypayroll
                       FROM t_payroll_history ph
                      WHERE ph.f_payroll = p.f_payroll AND ph.effective_date <= m_1.mes
                      ORDER BY ph.effective_date DESC
                     LIMIT 1), p.f_monthlypayroll)
                    ELSE 0::numeric
                END) AS total_nomina
           FROM meses m_1
             CROSS JOIN t_payroll p
          GROUP BY m_1.mes
        ), horas_extra_mensual AS (
         SELECT make_date(t_overtime_hours.year, t_overtime_hours.month, 1) AS mes,
            t_overtime_hours.amount AS total_horas_extra
           FROM t_overtime_hours
        ), gastos_fijos_mensual AS (
         SELECT m_1.mes,
            sum(
                CASE
                    WHEN fe.is_active = true THEN COALESCE(( SELECT feh.monthly_amount
                       FROM t_fixed_expenses_history feh
                      WHERE feh.expense_id = fe.id AND feh.effective_date <= m_1.mes AND (feh.change_type::text <> ALL (ARRAY['DEACTIVATED'::character varying, 'DELETED'::character varying]::text[]))
                      ORDER BY feh.effective_date DESC, feh.id DESC
                     LIMIT 1), 0::numeric)
                    ELSE 0::numeric
                END) AS total_gastos_fijos
           FROM meses m_1
             CROSS JOIN t_fixed_expenses fe
          GROUP BY m_1.mes
        ), gastos_variables_mensual AS (
         -- TODOS los gastos a proveedores pagados (incluye los asociados a ordenes)
         SELECT m_1.mes,
            COALESCE(sum(e.f_totalexpense), 0::numeric) AS total_gastos_variables
           FROM meses m_1
             LEFT JOIN t_expense e ON date_trunc('month'::text, e.f_paiddate::timestamp with time zone) = m_1.mes
                AND e.f_status::text = 'PAGADO'::text
          GROUP BY m_1.mes
        ), gastos_ordenes_mensual AS (
         -- Gastos operativos e indirectos de ordenes (campos de t_order, NO de t_expense)
         SELECT m_1.mes,
            COALESCE(sum(o.gasto_operativo), 0::numeric) AS total_gasto_operativo,
            COALESCE(sum(o.gasto_indirecto), 0::numeric) AS total_gasto_indirecto
           FROM meses m_1
             LEFT JOIN t_order o ON date_trunc('month'::text, o.f_podate::timestamp with time zone) = m_1.mes
                AND o.f_podate IS NOT NULL
          GROUP BY m_1.mes
        )
 SELECT m.mes AS fecha,
    EXTRACT(year FROM m.mes) AS "año",
    EXTRACT(month FROM m.mes) AS mes_numero,
    to_char(m.mes::timestamp with time zone, 'TMMonth'::text) AS mes_nombre,
    to_char(m.mes::timestamp with time zone, 'Mon-YY'::text) AS mes_corto,
    COALESCE(n.total_nomina, 0::numeric) AS nomina,
    COALESCE(he.total_horas_extra, 0::numeric) AS horas_extra,
    COALESCE(gf.total_gastos_fijos, 0::numeric) AS gastos_fijos,
    COALESCE(gv.total_gastos_variables, 0::numeric) AS gastos_variables,
    COALESCE(go.total_gasto_operativo, 0::numeric) AS gasto_operativo,
    COALESCE(go.total_gasto_indirecto, 0::numeric) AS gasto_indirecto,
    COALESCE(n.total_nomina, 0::numeric) + COALESCE(he.total_horas_extra, 0::numeric) +
    COALESCE(gf.total_gastos_fijos, 0::numeric) + COALESCE(gv.total_gastos_variables, 0::numeric) +
    COALESCE(go.total_gasto_operativo, 0::numeric) + COALESCE(go.total_gasto_indirecto, 0::numeric) AS total_gastos
   FROM meses m
     LEFT JOIN nomina_mensual n ON n.mes = m.mes
     LEFT JOIN horas_extra_mensual he ON he.mes = m.mes
     LEFT JOIN gastos_fijos_mensual gf ON gf.mes = m.mes
     LEFT JOIN gastos_variables_mensual gv ON gv.mes = m.mes
     LEFT JOIN gastos_ordenes_mensual go ON go.mes = m.mes
  ORDER BY m.mes
```

---

#### v_balance_ingresos

**Proposito:** Ingresos esperados y percibidos por mes

```sql
CREATE OR REPLACE VIEW v_balance_ingresos AS
 WITH rango_fechas AS (
         SELECT LEAST(COALESCE(( SELECT min(t_invoice.f_invoicedate) AS min
                   FROM t_invoice
                  WHERE t_invoice.f_invoicedate IS NOT NULL), CURRENT_DATE), COALESCE(( SELECT min(t_payroll.f_hireddate) AS min
                   FROM t_payroll
                  WHERE t_payroll.f_hireddate IS NOT NULL), CURRENT_DATE), COALESCE(( SELECT min(t_expense.f_paiddate) AS min
                   FROM t_expense
                  WHERE t_expense.f_paiddate IS NOT NULL), CURRENT_DATE), COALESCE(( SELECT min(t_fixed_expenses_history.effective_date) AS min
                   FROM t_fixed_expenses_history
                  WHERE t_fixed_expenses_history.effective_date IS NOT NULL), CURRENT_DATE)) AS fecha_inicio,
            GREATEST(CURRENT_DATE, COALESCE(( SELECT max(t_invoice.due_date) AS max
                   FROM t_invoice
                  WHERE t_invoice.due_date IS NOT NULL), CURRENT_DATE), COALESCE(( SELECT max(t_invoice.f_invoicedate) AS max
                   FROM t_invoice
                  WHERE t_invoice.f_invoicedate IS NOT NULL), CURRENT_DATE)) AS fecha_fin
        ), meses AS (
         SELECT generate_series(date_trunc('month'::text, (( SELECT rango_fechas.fecha_inicio
                   FROM rango_fechas))::timestamp with time zone)::date::timestamp with time zone, date_trunc('month'::text, (( SELECT rango_fechas.fecha_fin
                   FROM rango_fechas))::timestamp with time zone)::date::timestamp with time zone, '1 mon'::interval)::date AS mes
        ), ingresos_esperados AS (
         SELECT date_trunc('month'::text, v_income.effective_payment_date::timestamp with time zone)::date AS mes,
            sum(v_income.f_total) AS total
           FROM v_income
          WHERE v_income.effective_payment_date IS NOT NULL
          GROUP BY (date_trunc('month'::text, v_income.effective_payment_date::timestamp with time zone)::date)
        ), ingresos_percibidos AS (
         SELECT date_trunc('month'::text, v_income.f_paymentdate::timestamp with time zone)::date AS mes,
            sum(v_income.f_total) AS total
           FROM v_income
          WHERE v_income.f_paymentdate IS NOT NULL
          GROUP BY (date_trunc('month'::text, v_income.f_paymentdate::timestamp with time zone)::date)
        ), ventas_totales AS (
         SELECT date_trunc('month'::text, t_order.f_podate::timestamp with time zone)::date AS mes,
            sum(t_order.f_saletotal) AS total
           FROM t_order
          WHERE t_order.f_podate IS NOT NULL
          GROUP BY (date_trunc('month'::text, t_order.f_podate::timestamp with time zone)::date)
        )
 SELECT m.mes AS fecha,
    EXTRACT(year FROM m.mes) AS "año",
    EXTRACT(month FROM m.mes) AS mes_numero,
    to_char(m.mes::timestamp with time zone, 'TMMonth'::text) AS mes_nombre,
    to_char(m.mes::timestamp with time zone, 'Mon-YY'::text) AS mes_corto,
    COALESCE(ie.total, 0::numeric) AS ingresos_esperados,
    COALESCE(ip.total, 0::numeric) AS ingresos_percibidos,
    COALESCE(ie.total, 0::numeric) - COALESCE(ip.total, 0::numeric) AS diferencia,
    COALESCE(vt.total, 0::numeric) AS ventas_totales
   FROM meses m
     LEFT JOIN ingresos_esperados ie ON ie.mes = m.mes
     LEFT JOIN ingresos_percibidos ip ON ip.mes = m.mes
     LEFT JOIN ventas_totales vt ON vt.mes = m.mes
  ORDER BY m.mes
```

---

#### v_income

**Proposito:** Detalle de ingresos por factura con fecha efectiva de pago

```sql
CREATE OR REPLACE VIEW v_income AS
 SELECT i.f_folio,
    c.f_client,
    c.f_name AS client_name,
    i.f_total,
    i.f_receptiondate,
    (i.f_receptiondate + '1 day'::interval * COALESCE(c.f_credit, 0)::double precision)::date AS due_date,
    i.f_invoicestat,
    i.f_paymentdate,
        CASE
            WHEN i.f_invoicestat >= 3 THEN i.f_paymentdate
            WHEN (i.f_receptiondate + '1 day'::interval * COALESCE(c.f_credit, 0)::double precision)::date < CURRENT_DATE THEN CURRENT_DATE
            ELSE (i.f_receptiondate + '1 day'::interval * COALESCE(c.f_credit, 0)::double precision)::date
        END AS effective_payment_date,
    o.f_order,
    o.f_po,
    i.f_invoice,
        CASE
            WHEN i.f_invoicestat = 4 THEN 'PAGADA'::text
            WHEN i.f_invoicestat = 3 THEN 'VENCIDA'::text
            WHEN i.f_invoicestat = 2 THEN 'PENDIENTE'::text
            ELSE 'CREADA'::text
        END AS status_text
   FROM t_invoice i
     JOIN t_order o ON i.f_order = o.f_order
     JOIN t_client c ON o.f_client = c.f_client
  WHERE i.f_invoicestat IS NOT NULL AND i.f_invoicestat <> 0
```

---

#### v_order_gastos

**Proposito:** Ordenes con gastos calculados (material, operativo, indirecto)

```sql
CREATE OR REPLACE VIEW v_order_gastos AS
 SELECT o.f_order,
    o.f_po,
    o.f_quote,
    o.f_podate,
    o.f_client,
    o.f_contact,
    o.f_description,
    o.f_salesman,
    o.f_estdelivery,
    o.f_salesubtotal,
    o.f_saletotal,
    o.f_orderstat,
    o.progress_percentage,
    o.order_percentage,
    o.f_commission_rate,
    o.created_by,
    o.created_at,
    o.updated_by,
    o.updated_at,
    COALESCE(o.gasto_operativo, 0::numeric) AS gasto_operativo,
    COALESCE(o.gasto_indirecto, 0::numeric) AS gasto_indirecto,
    COALESCE(g.gasto_material_pagado, 0::numeric) AS gasto_material,
    COALESCE(g.gasto_material_pendiente, 0::numeric) AS gasto_material_pendiente,
    COALESCE(g.total_gastos, 0::numeric) AS total_gastos_proveedor,
    COALESCE(g.num_facturas, 0::bigint) AS num_facturas_proveedor
   FROM t_order o
     LEFT JOIN ( SELECT t_expense.f_order,
            sum(
                CASE
                    WHEN t_expense.f_status::text = 'PAGADO'::text THEN t_expense.f_totalexpense
                    ELSE 0::numeric
                END) AS gasto_material_pagado,
            sum(
                CASE
                    WHEN t_expense.f_status::text = 'PENDIENTE'::text THEN t_expense.f_totalexpense
                    ELSE 0::numeric
                END) AS gasto_material_pendiente,
            sum(t_expense.f_totalexpense) AS total_gastos,
            count(*) AS num_facturas
           FROM t_expense
          WHERE t_expense.f_order IS NOT NULL
          GROUP BY t_expense.f_order) g ON o.f_order = g.f_order
```

---

#### v_vacations_active

**Proposito:** Vacaciones activas, proximas y finalizadas

```sql
CREATE OR REPLACE VIEW v_vacations_active AS
 SELECT v.id,
    v.employee_id,
    p.f_employee AS employee_name,
    p.f_title AS title,
    v.start_date,
    v.end_date,
    v.total_days,
    v.notes,
    v.status,
    u.username AS approved_by_user,
    v.approved_at,
        CASE
            WHEN CURRENT_DATE >= v.start_date AND CURRENT_DATE <= v.end_date THEN 'EN_CURSO'::text
            WHEN v.start_date > CURRENT_DATE THEN 'PROXIMA'::text
            ELSE 'FINALIZADA'::text
        END AS vacation_status,
    v.end_date - CURRENT_DATE AS days_remaining
   FROM t_vacation v
     JOIN t_payroll p ON v.employee_id = p.f_payroll
     LEFT JOIN users u ON v.approved_by = u.id
  WHERE v.status::text = 'APROBADA'::text AND v.end_date >= (CURRENT_DATE - '30 days'::interval)
  ORDER BY v.start_date
```

---

## Indices de Base de Datos

### Resumen por Tabla

| Tabla | Indices |
|-------|---------|
| app_versions | 4 |
| audit_log | 3 |
| invoice_audit | 1 |
| invoice_status | 1 |
| order_gastos_indirectos | 2 |
| order_gastos_operativos | 2 |
| order_history | 4 |
| order_status | 1 |
| t_attendance | 5 |
| t_attendance_audit | 6 |
| t_balance_adjustments | 2 |
| t_client | 2 |
| t_commission_rate_history | 4 |
| t_contact | 3 |
| t_expense | 5 |
| t_fixed_expenses | 1 |
| t_fixed_expenses_history | 1 |
| t_holiday | 4 |
| t_invoice | 5 |
| t_order | 4 |
| t_order_deleted | 4 |
| t_overtime_hours | 3 |
| t_overtime_hours_audit | 2 |
| t_payroll | 1 |
| t_payroll_history | 4 |
| t_payrollovertime | 1 |
| t_supplier | 1 |
| t_vacation | 4 |
| t_vacation_audit | 3 |
| t_vendor | 3 |
| t_vendor_commission_payment | 4 |
| t_workday_config | 2 |
| users | 3 |

---

### Detalle de Indices

#### app_versions

| Indice | Definicion |
|--------|------------|
| `app_versions_pkey` | `public.app_versions USING btree (id)` |
| `app_versions_version_key` | `public.app_versions USING btree (version)` |
| `idx_app_versions_latest` | `public.app_versions USING btree (is_latest, is_active) WHERE (is_latest = true)` |
| `idx_app_versions_release_date` | `public.app_versions USING btree (release_date DESC)` |

#### audit_log

| Indice | Definicion |
|--------|------------|
| `audit_log_pkey` | `public.audit_log USING btree (id)` |
| `idx_audit_table` | `public.audit_log USING btree (table_name)` |
| `idx_audit_user` | `public.audit_log USING btree (user_id)` |

#### invoice_audit

| Indice | Definicion |
|--------|------------|
| `invoice_audit_pkey` | `public.invoice_audit USING btree (id)` |

#### invoice_status

| Indice | Definicion |
|--------|------------|
| `invoice_status_pkey` | `public.invoice_status USING btree (f_invoicestat)` |

#### order_gastos_indirectos

| Indice | Definicion |
|--------|------------|
| `idx_gastos_indirectos_order` | `public.order_gastos_indirectos USING btree (f_order)` |
| `order_gastos_indirectos_pkey` | `public.order_gastos_indirectos USING btree (id)` |

#### order_gastos_operativos

| Indice | Definicion |
|--------|------------|
| `idx_gastos_operativos_order` | `public.order_gastos_operativos USING btree (f_order)` |
| `order_gastos_operativos_pkey` | `public.order_gastos_operativos USING btree (id)` |

#### order_history

| Indice | Definicion |
|--------|------------|
| `idx_order_history_date` | `public.order_history USING btree (changed_at)` |
| `idx_order_history_order` | `public.order_history USING btree (order_id)` |
| `idx_order_history_user` | `public.order_history USING btree (user_id)` |
| `order_history_pkey` | `public.order_history USING btree (id)` |

#### order_status

| Indice | Definicion |
|--------|------------|
| `order_status_pkey` | `public.order_status USING btree (f_orderstatus)` |

#### t_attendance

| Indice | Definicion |
|--------|------------|
| `idx_attendance_date` | `public.t_attendance USING btree (attendance_date)` |
| `idx_attendance_employee` | `public.t_attendance USING btree (employee_id)` |
| `idx_attendance_status` | `public.t_attendance USING btree (status)` |
| `t_attendance_employee_id_attendance_date_key` | `public.t_attendance USING btree (employee_id, attendance_date)` |
| `t_attendance_pkey` | `public.t_attendance USING btree (id)` |

#### t_attendance_audit

| Indice | Definicion |
|--------|------------|
| `idx_audit_action` | `public.t_attendance_audit USING btree (action)` |
| `idx_audit_attendance_id` | `public.t_attendance_audit USING btree (attendance_id)` |
| `idx_audit_changed_at` | `public.t_attendance_audit USING btree (changed_at)` |
| `idx_audit_date` | `public.t_attendance_audit USING btree (attendance_date)` |
| `idx_audit_employee_id` | `public.t_attendance_audit USING btree (employee_id)` |
| `t_attendance_audit_pkey` | `public.t_attendance_audit USING btree (id)` |

#### t_balance_adjustments

| Indice | Definicion |
|--------|------------|
| `t_balance_adjustments_pkey` | `public.t_balance_adjustments USING btree (id)` |
| `t_balance_adjustments_year_month_adjustment_type_key` | `public.t_balance_adjustments USING btree (year, month, adjustment_type)` |

#### t_client

| Indice | Definicion |
|--------|------------|
| `idx_client_name` | `public.t_client USING btree (f_name)` |
| `t_client_pkey` | `public.t_client USING btree (f_client)` |

#### t_commission_rate_history

| Indice | Definicion |
|--------|------------|
| `idx_commission_history_date` | `public.t_commission_rate_history USING btree (changed_at)` |
| `idx_commission_history_order` | `public.t_commission_rate_history USING btree (order_id)` |
| `idx_commission_history_vendor` | `public.t_commission_rate_history USING btree (vendor_id)` |
| `t_commission_rate_history_pkey` | `public.t_commission_rate_history USING btree (id)` |

#### t_contact

| Indice | Definicion |
|--------|------------|
| `idx_contact_client` | `public.t_contact USING btree (f_client)` |
| `idx_contact_email` | `public.t_contact USING btree (f_email)` |
| `t_contact_pkey` | `public.t_contact USING btree (f_contact)` |

#### t_expense

| Indice | Definicion |
|--------|------------|
| `idx_expense_date` | `public.t_expense USING btree (f_expensedate)` |
| `idx_expense_order_date` | `public.t_expense USING btree (f_order, f_expensedate DESC) WHERE (f_order IS NOT NULL)` |
| `idx_expense_order_status` | `public.t_expense USING btree (f_order, f_status) WHERE (f_order IS NOT NULL)` |
| `idx_expense_supplier` | `public.t_expense USING btree (f_supplier)` |
| `t_expense_pkey` | `public.t_expense USING btree (f_expense)` |

#### t_fixed_expenses

| Indice | Definicion |
|--------|------------|
| `t_fixed_expenses_pkey` | `public.t_fixed_expenses USING btree (id)` |

#### t_fixed_expenses_history

| Indice | Definicion |
|--------|------------|
| `t_fixed_expenses_history_pkey` | `public.t_fixed_expenses_history USING btree (id)` |

#### t_holiday

| Indice | Definicion |
|--------|------------|
| `idx_holiday_date` | `public.t_holiday USING btree (holiday_date)` |
| `idx_holiday_year` | `public.t_holiday USING btree (EXTRACT(year FROM holiday_date))` |
| `t_holiday_holiday_date_key` | `public.t_holiday USING btree (holiday_date)` |
| `t_holiday_pkey` | `public.t_holiday USING btree (id)` |

#### t_invoice

| Indice | Definicion |
|--------|------------|
| `idx_invoice_folio` | `public.t_invoice USING btree (f_folio)` |
| `idx_invoice_order` | `public.t_invoice USING btree (f_order)` |
| `idx_invoice_order_folio` | `public.t_invoice USING btree (f_order, f_folio) WHERE (f_folio IS NOT NULL)` |
| `idx_invoice_status` | `public.t_invoice USING btree (f_invoicestat)` |
| `t_invoice_pkey` | `public.t_invoice USING btree (f_invoice)` |

#### t_order

| Indice | Definicion |
|--------|------------|
| `idx_order_client` | `public.t_order USING btree (f_client)` |
| `idx_order_po` | `public.t_order USING btree (f_po)` |
| `idx_order_status` | `public.t_order USING btree (f_orderstat)` |
| `t_order_pkey` | `public.t_order USING btree (f_order)` |

#### t_order_deleted

| Indice | Definicion |
|--------|------------|
| `idx_order_deleted_date` | `public.t_order_deleted USING btree (deleted_at)` |
| `idx_order_deleted_original_id` | `public.t_order_deleted USING btree (original_order_id)` |
| `idx_order_deleted_po` | `public.t_order_deleted USING btree (f_po)` |
| `t_order_deleted_pkey` | `public.t_order_deleted USING btree (id)` |

#### t_overtime_hours

| Indice | Definicion |
|--------|------------|
| `idx_overtime_year_month` | `public.t_overtime_hours USING btree (year, month)` |
| `t_overtime_hours_pkey` | `public.t_overtime_hours USING btree (id)` |
| `t_overtime_hours_year_month_key` | `public.t_overtime_hours USING btree (year, month)` |

#### t_overtime_hours_audit

| Indice | Definicion |
|--------|------------|
| `idx_overtime_audit_date` | `public.t_overtime_hours_audit USING btree (changed_at)` |
| `t_overtime_hours_audit_pkey` | `public.t_overtime_hours_audit USING btree (id)` |

#### t_payroll

| Indice | Definicion |
|--------|------------|
| `t_payroll_pkey` | `public.t_payroll USING btree (f_payroll)` |

#### t_payroll_history

| Indice | Definicion |
|--------|------------|
| `idx_payroll_history_active` | `public.t_payroll_history USING btree (f_payroll, is_active)` |
| `idx_payroll_history_date` | `public.t_payroll_history USING btree (effective_date)` |
| `idx_payroll_history_employee` | `public.t_payroll_history USING btree (f_payroll, effective_date DESC)` |
| `t_payroll_history_pkey` | `public.t_payroll_history USING btree (id)` |

#### t_payrollovertime

| Indice | Definicion |
|--------|------------|
| `t_payrollovertime_pkey` | `public.t_payrollovertime USING btree (f_payrollovertime)` |

#### t_supplier

| Indice | Definicion |
|--------|------------|
| `t_supplier_pkey` | `public.t_supplier USING btree (f_supplier)` |

#### t_vacation

| Indice | Definicion |
|--------|------------|
| `idx_vacation_dates` | `public.t_vacation USING btree (start_date, end_date)` |
| `idx_vacation_employee` | `public.t_vacation USING btree (employee_id)` |
| `idx_vacation_status` | `public.t_vacation USING btree (status)` |
| `t_vacation_pkey` | `public.t_vacation USING btree (id)` |

#### t_vacation_audit

| Indice | Definicion |
|--------|------------|
| `idx_vacation_audit_employee` | `public.t_vacation_audit USING btree (employee_id)` |
| `idx_vacation_audit_id` | `public.t_vacation_audit USING btree (vacation_id)` |
| `t_vacation_audit_pkey` | `public.t_vacation_audit USING btree (id)` |

#### t_vendor

| Indice | Definicion |
|--------|------------|
| `idx_vendor_name` | `public.t_vendor USING btree (f_vendorname)` |
| `idx_vendor_user` | `public.t_vendor USING btree (f_user_id)` |
| `t_vendor_pkey` | `public.t_vendor USING btree (f_vendor)` |

#### t_vendor_commission_payment

| Indice | Definicion |
|--------|------------|
| `idx_commission_payment_order` | `public.t_vendor_commission_payment USING btree (f_order)` |
| `idx_commission_payment_status` | `public.t_vendor_commission_payment USING btree (payment_status)` |
| `idx_commission_payment_vendor` | `public.t_vendor_commission_payment USING btree (f_vendor)` |
| `t_vendor_commission_payment_pkey` | `public.t_vendor_commission_payment USING btree (id)` |

#### t_workday_config

| Indice | Definicion |
|--------|------------|
| `t_workday_config_day_of_week_key` | `public.t_workday_config USING btree (day_of_week)` |
| `t_workday_config_pkey` | `public.t_workday_config USING btree (id)` |

#### users

| Indice | Definicion |
|--------|------------|
| `users_email_key` | `public.users USING btree (email)` |
| `users_pkey` | `public.users USING btree (id)` |
| `users_username_key` | `public.users USING btree (username)` |
