# Documentación de Vistas - Base de Datos IMA Mecatrónica
Generado: 2026-02-26 22:31:06
Vistas regulares: 11 | Vistas materializadas: 1

## Índice

### Vistas Regulares
1. [v_attendance_history](#v_attendance_history)
2. [v_attendance_monthly_summary](#v_attendance_monthly_summary)
3. [v_attendance_stats](#v_attendance_stats)
4. [v_attendance_today](#v_attendance_today)
5. [v_balance_completo](#v_balance_completo)
6. [v_balance_gastos](#v_balance_gastos)
7. [v_balance_ingresos](#v_balance_ingresos)
8. [v_expense_audit_report](#v_expense_audit_report)
9. [v_income](#v_income)
10. [v_order_gastos](#v_order_gastos)
11. [v_vacations_active](#v_vacations_active)

### Vistas Materializadas
1. [mv_balance_completo](#mv_balance_completo)

---

## v_attendance_history
> Vista legible del historial de cambios de asistencia
Tipo: Vista regular | Columnas: 10

### Columnas

| # | Columna | Tipo | Nullable |
|---|---------|------|----------|
| 1 | `id` | `int4` | NULL |
| 2 | `attendance_id` | `int4` | NULL |
| 3 | `employee_name` | `character varying` | NULL |
| 4 | `attendance_date` | `date` | NULL |
| 5 | `action` | `character varying` | NULL |
| 6 | `old_status` | `character varying` | NULL |
| 7 | `new_status` | `character varying` | NULL |
| 8 | `change_description` | `text` | NULL |
| 9 | `changed_by_user` | `character varying` | NULL |
| 10 | `changed_at` | `timestamp without time zone` | NULL |

### Definición SQL

```sql
SELECT a.id,
    a.attendance_id,
    p.f_employee AS employee_name,
    a.attendance_date,
    a.action,
    a.old_status,
    a.new_status,
        CASE
            WHEN ((a.action)::text = 'INSERT'::text) THEN ('Registro creado: '::text || (a.new_status)::text)
            WHEN ((a.action)::text = 'UPDATE'::text) THEN ((('Cambio de '::text || (COALESCE(a.old_status, 'N/A'::character varying))::text) || ' a '::text) || (a.new_status)::text)
            WHEN ((a.action)::text = 'DELETE'::text) THEN ('Registro eliminado: '::text || (a.old_status)::text)
            ELSE NULL::text
        END AS change_description,
    u.username AS changed_by_user,
    a.changed_at
   FROM ((t_attendance_audit a
     LEFT JOIN t_payroll p ON ((a.employee_id = p.f_payroll)))
     LEFT JOIN users u ON ((a.changed_by = u.id)))
  ORDER BY a.changed_at DESC;
```

### Tablas Referenciadas
- `t_attendance_audit`
- `t_payroll`
- `users`

---

## v_attendance_monthly_summary
> Resumen mensual de asistencia por empleado
Tipo: Vista regular | Columnas: 14

### Columnas

| # | Columna | Tipo | Nullable |
|---|---------|------|----------|
| 1 | `employee_id` | `int4` | NULL |
| 2 | `employee_name` | `character varying` | NULL |
| 3 | `title` | `character varying` | NULL |
| 4 | `employee_code` | `character varying` | NULL |
| 5 | `month` | `date` | NULL |
| 6 | `month_key` | `text` | NULL |
| 7 | `asistencias` | `int8` | NULL |
| 8 | `retardos` | `int8` | NULL |
| 9 | `faltas` | `int8` | NULL |
| 10 | `vacaciones` | `int8` | NULL |
| 11 | `feriados` | `int8` | NULL |
| 12 | `descansos` | `int8` | NULL |
| 13 | `total_late_minutes` | `int8` | NULL |
| 14 | `justified_count` | `int8` | NULL |

### Definición SQL

```sql
SELECT p.f_payroll AS employee_id,
    p.f_employee AS employee_name,
    p.f_title AS title,
    p.employee_code,
    (date_trunc('month'::text, (a.attendance_date)::timestamp with time zone))::date AS month,
    to_char(date_trunc('month'::text, (a.attendance_date)::timestamp with time zone), 'YYYY-MM'::text) AS month_key,
    count(
        CASE
            WHEN ((a.status)::text = 'ASISTENCIA'::text) THEN 1
            ELSE NULL::integer
        END) AS asistencias,
    count(
        CASE
            WHEN ((a.status)::text = 'RETARDO'::text) THEN 1
            ELSE NULL::integer
        END) AS retardos,
    count(
        CASE
            WHEN ((a.status)::text = 'FALTA'::text) THEN 1
            ELSE NULL::integer
        END) AS faltas,
    count(
        CASE
            WHEN ((a.status)::text = 'VACACIONES'::text) THEN 1
            ELSE NULL::integer
        END) AS vacaciones,
    count(
        CASE
            WHEN ((a.status)::text = 'FERIADO'::text) THEN 1
            ELSE NULL::integer
        END) AS feriados,
    count(
        CASE
            WHEN ((a.status)::text = 'DESCANSO'::text) THEN 1
            ELSE NULL::integer
        END) AS descansos,
    sum(COALESCE(a.late_minutes, 0)) AS total_late_minutes,
    count(
        CASE
            WHEN ((a.is_justified = true) AND ((a.status)::text = ANY ((ARRAY['FALTA'::character varying, 'RETARDO'::character varying])::text[]))) THEN 1
            ELSE NULL::integer
        END) AS justified_count
   FROM (t_payroll p
     LEFT JOIN t_attendance a ON ((p.f_payroll = a.employee_id)))
  WHERE (p.is_active = true)
  GROUP BY p.f_payroll, p.f_employee, p.f_title, p.employee_code, (date_trunc('month'::text, (a.attendance_date)::timestamp with time zone));
```

### Tablas Referenciadas
- `t_attendance`
- `t_payroll`

---

## v_attendance_stats
> Estadísticas generales de asistencia del mes actual
Tipo: Vista regular | Columnas: 7

### Columnas

| # | Columna | Tipo | Nullable |
|---|---------|------|----------|
| 1 | `current_month` | `date` | NULL |
| 2 | `total_employees` | `int8` | NULL |
| 3 | `total_asistencias` | `int8` | NULL |
| 4 | `total_retardos` | `int8` | NULL |
| 5 | `total_faltas` | `int8` | NULL |
| 6 | `total_vacaciones` | `int8` | NULL |
| 7 | `total_late_minutes` | `int8` | NULL |

### Definición SQL

```sql
SELECT (date_trunc('month'::text, (CURRENT_DATE)::timestamp with time zone))::date AS current_month,
    ( SELECT count(*) AS count
           FROM t_payroll
          WHERE (t_payroll.is_active = true)) AS total_employees,
    ( SELECT count(*) AS count
           FROM t_attendance
          WHERE ((date_trunc('month'::text, (t_attendance.attendance_date)::timestamp with time zone) = date_trunc('month'::text, (CURRENT_DATE)::timestamp with time zone)) AND ((t_attendance.status)::text = 'ASISTENCIA'::text))) AS total_asistencias,
    ( SELECT count(*) AS count
           FROM t_attendance
          WHERE ((date_trunc('month'::text, (t_attendance.attendance_date)::timestamp with time zone) = date_trunc('month'::text, (CURRENT_DATE)::timestamp with time zone)) AND ((t_attendance.status)::text = 'RETARDO'::text))) AS total_retardos,
    ( SELECT count(*) AS count
           FROM t_attendance
          WHERE ((date_trunc('month'::text, (t_attendance.attendance_date)::timestamp with time zone) = date_trunc('month'::text, (CURRENT_DATE)::timestamp with time zone)) AND ((t_attendance.status)::text = 'FALTA'::text))) AS total_faltas,
    ( SELECT count(*) AS count
           FROM t_attendance
          WHERE ((date_trunc('month'::text, (t_attendance.attendance_date)::timestamp with time zone) = date_trunc('month'::text, (CURRENT_DATE)::timestamp with time zone)) AND ((t_attendance.status)::text = 'VACACIONES'::text))) AS total_vacaciones,
    ( SELECT sum(t_attendance.late_minutes) AS sum
           FROM t_attendance
          WHERE (date_trunc('month'::text, (t_attendance.attendance_date)::timestamp with time zone) = date_trunc('month'::text, (CURRENT_DATE)::timestamp with time zone))) AS total_late_minutes;
```

### Tablas Referenciadas
- `t_attendance`
- `t_attendance.attendance_date)::timestamp`
- `t_attendance.status)::text`
- `t_payroll`
- `t_payroll.is_active`

---

## v_attendance_today
> Estado de asistencia de todos los empleados para el día actual
Tipo: Vista regular | Columnas: 19

### Columnas

| # | Columna | Tipo | Nullable |
|---|---------|------|----------|
| 1 | `employee_id` | `int4` | NULL |
| 2 | `employee_name` | `character varying` | NULL |
| 3 | `title` | `character varying` | NULL |
| 4 | `employee_code` | `character varying` | NULL |
| 5 | `initials` | `text` | NULL |
| 6 | `today` | `date` | NULL |
| 7 | `attendance_id` | `int4` | NULL |
| 8 | `status` | `character varying` | NULL |
| 9 | `check_in_time` | `time without time zone` | NULL |
| 10 | `check_out_time` | `time without time zone` | NULL |
| 11 | `late_minutes` | `int4` | NULL |
| 12 | `notes` | `text` | NULL |
| 13 | `is_justified` | `bool` | NULL |
| 14 | `on_vacation` | `bool` | NULL |
| 15 | `vacation_start` | `date` | NULL |
| 16 | `vacation_end` | `date` | NULL |
| 17 | `is_holiday` | `bool` | NULL |
| 18 | `holiday_name` | `character varying` | NULL |
| 19 | `is_workday` | `bool` | NULL |

### Definición SQL

```sql
SELECT p.f_payroll AS employee_id,
    p.f_employee AS employee_name,
    p.f_title AS title,
    p.employee_code,
    ("substring"((p.f_employee)::text, 1, 1) || COALESCE("substring"(split_part((p.f_employee)::text, ' '::text, 2), 1, 1), ''::text)) AS initials,
    CURRENT_DATE AS today,
    a.id AS attendance_id,
    COALESCE(a.status, 'SIN_REGISTRO'::character varying) AS status,
    a.check_in_time,
    a.check_out_time,
    a.late_minutes,
    a.notes,
    a.is_justified,
        CASE
            WHEN (v.id IS NOT NULL) THEN true
            ELSE false
        END AS on_vacation,
    v.start_date AS vacation_start,
    v.end_date AS vacation_end,
        CASE
            WHEN (h.id IS NOT NULL) THEN true
            ELSE false
        END AS is_holiday,
    h.name AS holiday_name,
    wc.is_workday
   FROM ((((t_payroll p
     LEFT JOIN t_attendance a ON (((p.f_payroll = a.employee_id) AND (a.attendance_date = CURRENT_DATE))))
     LEFT JOIN t_vacation v ON (((p.f_payroll = v.employee_id) AND ((v.status)::text = 'APROBADA'::text) AND ((CURRENT_DATE >= v.start_date) AND (CURRENT_DATE <= v.end_date)))))
     LEFT JOIN t_holiday h ON (((h.holiday_date = CURRENT_DATE) AND (h.is_mandatory = true))))
     LEFT JOIN t_workday_config wc ON (((wc.day_of_week)::numeric = EXTRACT(dow FROM CURRENT_DATE))))
  WHERE (p.is_active = true)
  ORDER BY p.f_employee;
```

### Tablas Referenciadas
- `t_attendance`
- `t_holiday`
- `t_payroll`
- `t_vacation`
- `t_workday_config`

---

## v_balance_completo
Tipo: Vista regular | Columnas: 17

### Columnas

| # | Columna | Tipo | Nullable |
|---|---------|------|----------|
| 1 | `fecha` | `date` | NULL |
| 2 | `año` | `numeric` | NULL |
| 3 | `mes_numero` | `numeric` | NULL |
| 4 | `mes_nombre` | `text` | NULL |
| 5 | `mes_corto` | `text` | NULL |
| 6 | `nomina` | `numeric` | NULL |
| 7 | `horas_extra` | `numeric` | NULL |
| 8 | `gastos_fijos` | `numeric` | NULL |
| 9 | `gastos_variables` | `numeric` | NULL |
| 10 | `gasto_operativo` | `numeric` | NULL |
| 11 | `gasto_indirecto` | `numeric` | NULL |
| 12 | `total_gastos` | `numeric` | NULL |
| 13 | `ingresos_esperados` | `numeric` | NULL |
| 14 | `ingresos_percibidos` | `numeric` | NULL |
| 15 | `diferencia` | `numeric` | NULL |
| 16 | `ventas_totales` | `numeric` | NULL |
| 17 | `utilidad_aproximada` | `numeric` | NULL |

### Definición SQL

```sql
SELECT COALESCE(g.fecha, i.fecha) AS fecha,
    COALESCE(g."año", i."año") AS "año",
    COALESCE(g.mes_numero, i.mes_numero) AS mes_numero,
    COALESCE(g.mes_nombre, i.mes_nombre) AS mes_nombre,
    COALESCE(g.mes_corto, i.mes_corto) AS mes_corto,
    COALESCE(g.nomina, (0)::numeric) AS nomina,
    COALESCE(g.horas_extra, (0)::numeric) AS horas_extra,
    COALESCE(g.gastos_fijos, (0)::numeric) AS gastos_fijos,
    COALESCE(g.gastos_variables, (0)::numeric) AS gastos_variables,
    COALESCE(g.gasto_operativo, (0)::numeric) AS gasto_operativo,
    COALESCE(g.gasto_indirecto, (0)::numeric) AS gasto_indirecto,
    COALESCE(g.total_gastos, (0)::numeric) AS total_gastos,
    COALESCE(i.ingresos_esperados, (0)::numeric) AS ingresos_esperados,
    COALESCE(i.ingresos_percibidos, (0)::numeric) AS ingresos_percibidos,
    (COALESCE(i.ingresos_esperados, (0)::numeric) - COALESCE(i.ingresos_percibidos, (0)::numeric)) AS diferencia,
    COALESCE(i.ventas_totales, (0)::numeric) AS ventas_totales,
    (COALESCE(i.ventas_totales, (0)::numeric) - (((COALESCE(g.gastos_fijos, (0)::numeric) + COALESCE(g.gastos_variables, (0)::numeric)) + COALESCE(g.gasto_operativo, (0)::numeric)) + COALESCE(g.gasto_indirecto, (0)::numeric))) AS utilidad_aproximada
   FROM (v_balance_gastos g
     FULL JOIN v_balance_ingresos i ON ((g.fecha = i.fecha)))
  WHERE (COALESCE(g."año", i."año") IS NOT NULL)
  ORDER BY COALESCE(g.fecha, i.fecha);
```

---

## v_balance_gastos
Tipo: Vista regular | Columnas: 12

### Columnas

| # | Columna | Tipo | Nullable |
|---|---------|------|----------|
| 1 | `fecha` | `date` | NULL |
| 2 | `año` | `numeric` | NULL |
| 3 | `mes_numero` | `numeric` | NULL |
| 4 | `mes_nombre` | `text` | NULL |
| 5 | `mes_corto` | `text` | NULL |
| 6 | `nomina` | `numeric` | NULL |
| 7 | `horas_extra` | `numeric` | NULL |
| 8 | `gastos_fijos` | `numeric` | NULL |
| 9 | `gastos_variables` | `numeric` | NULL |
| 10 | `gasto_operativo` | `numeric` | NULL |
| 11 | `gasto_indirecto` | `numeric` | NULL |
| 12 | `total_gastos` | `numeric` | NULL |

### Definición SQL

```sql
WITH rango_fechas AS (
         SELECT date_trunc('year'::text, (LEAST(COALESCE(( SELECT min(t_payroll.f_hireddate) AS min
                   FROM t_payroll
                  WHERE (t_payroll.f_hireddate IS NOT NULL)), CURRENT_DATE), COALESCE(( SELECT min(t_fixed_expenses_history.effective_date) AS min
                   FROM t_fixed_expenses_history), CURRENT_DATE), COALESCE(( SELECT min(t_expense.f_paiddate) AS min
                   FROM t_expense
                  WHERE (t_expense.f_paiddate IS NOT NULL)), CURRENT_DATE), COALESCE(( SELECT min(t_order.f_podate) AS min
                   FROM t_order
                  WHERE (t_order.f_podate IS NOT NULL)), CURRENT_DATE)))::timestamp with time zone) AS fecha_inicio,
            ((date_trunc('year'::text, (GREATEST(CURRENT_DATE, COALESCE(( SELECT max(t_payroll.f_hireddate) AS max
                   FROM t_payroll), CURRENT_DATE), COALESCE(( SELECT max(t_fixed_expenses_history.effective_date) AS max
                   FROM t_fixed_expenses_history), CURRENT_DATE)))::timestamp with time zone) + '11 mons'::interval) + '31 days'::interval) AS fecha_fin
        ), meses AS (
         SELECT (generate_series(date_trunc('month'::text, ( SELECT rango_fechas.fecha_inicio
                   FROM rango_fechas)), date_trunc('month'::text, ( SELECT rango_fechas.fecha_fin
                   FROM rango_fechas)), '1 mon'::interval))::date AS mes
        ), nomina_mensual AS (
         SELECT m_1.mes,
            sum(
                CASE
                    WHEN ((p.is_active = true) AND ((p.f_hireddate IS NULL) OR (p.f_hireddate <= (((date_trunc('month'::text, (m_1.mes)::timestamp with time zone) + '1 mon'::interval) - '1 day'::interval))::date))) THEN COALESCE(( SELECT ph.f_monthlypayroll
                       FROM t_payroll_history ph
                      WHERE ((ph.f_payroll = p.f_payroll) AND (ph.effective_date <= m_1.mes))
                      ORDER BY ph.effective_date DESC
                     LIMIT 1), p.f_monthlypayroll)
                    ELSE (0)::numeric
                END) AS total_nomina
           FROM (meses m_1
             CROSS JOIN t_payroll p)
          GROUP BY m_1.mes
        ), horas_extra_mensual AS (
         SELECT make_date(t_overtime_hours.year, t_overtime_hours.month, 1) AS mes,
            t_overtime_hours.amount AS total_horas_extra
           FROM t_overtime_hours
        ), gastos_fijos_mensual AS (
         SELECT m_1.mes,
            sum(
                CASE
                    WHEN (fe.is_active = true) THEN COALESCE(( SELECT feh.monthly_amount
                       FROM t_fixed_expenses_history feh
                      WHERE ((feh.expense_id = fe.id) AND (feh.effective_date <= m_1.mes) AND ((feh.change_type)::text <> ALL ((ARRAY['DEACTIVATED'::character varying, 'DELETED'::character varying])::text[])))
                      ORDER BY feh.effective_date DESC, feh.id DESC
                     LIMIT 1), (0)::numeric)
                    ELSE (0)::numeric
                END) AS total_gastos_fijos
           FROM (meses m_1
             CROSS JOIN t_fixed_expenses fe)
          GROUP BY m_1.mes
        ), gastos_variables_mensual AS (
         SELECT m_1.mes,
            COALESCE(sum(e.f_totalexpense), (0)::numeric) AS total_gastos_variables
           FROM (meses m_1
             LEFT JOIN t_expense e ON (((date_trunc('month'::text, (e.f_paiddate)::timestamp with time zone) = m_1.mes) AND ((e.f_status)::text = 'PAGADO'::text))))
          GROUP BY m_1.mes
        ), gastos_ordenes_mensual AS (
         SELECT m_1.mes,
            COALESCE(sum(o.gasto_operativo), (0)::numeric) AS total_gasto_operativo,
            COALESCE(sum(o.gasto_indirecto), (0)::numeric) AS total_gasto_indirecto
           FROM (meses m_1
             LEFT JOIN t_order o ON (((date_trunc('month'::text, (o.f_podate)::timestamp with time zone) = m_1.mes) AND (o.f_podate IS NOT NULL))))
          GROUP BY m_1.mes
        )
 SELECT m.mes AS fecha,
    EXTRACT(year FROM m.mes) AS "año",
    EXTRACT(month FROM m.mes) AS mes_numero,
    to_char((m.mes)::timestamp with time zone, 'TMMonth'::text) AS mes_nombre,
    to_char((m.mes)::timestamp with time zone, 'Mon-YY'::text) AS mes_corto,
    COALESCE(n.total_nomina, (0)::numeric) AS nomina,
    COALESCE(he.total_horas_extra, (0)::numeric) AS horas_extra,
    COALESCE(gf.total_gastos_fijos, (0)::numeric) AS gastos_fijos,
    COALESCE(gv.total_gastos_variables, (0)::numeric) AS gastos_variables,
    COALESCE(go.total_gasto_operativo, (0)::numeric) AS gasto_operativo,
    COALESCE(go.total_gasto_indirecto, (0)::numeric) AS gasto_indirecto,
    (((((COALESCE(n.total_nomina, (0)::numeric) + COALESCE(he.total_horas_extra, (0)::numeric)) + COALESCE(gf.total_gastos_fijos, (0)::numeric)) + COALESCE(gv.total_gastos_variables, (0)::numeric)) + COALESCE(go.total_gasto_operativo, (0)::numeric)) + COALESCE(go.total_gasto_indirecto, (0)::numeric)) AS total_gastos
   FROM (((((meses m
     LEFT JOIN nomina_mensual n ON ((n.mes = m.mes)))
     LEFT JOIN horas_extra_mensual he ON ((he.mes = m.mes)))
     LEFT JOIN gastos_fijos_mensual gf ON ((gf.mes = m.mes)))
     LEFT JOIN gastos_variables_mensual gv ON ((gv.mes = m.mes)))
     LEFT JOIN gastos_ordenes_mensual go ON ((go.mes = m.mes)))
  ORDER BY m.mes;
```

### Tablas Referenciadas
- `t_expense`
- `t_expense.f_paiddate`
- `t_fixed_expenses`
- `t_fixed_expenses_history`
- `t_order`
- `t_order.f_podate`
- `t_overtime_hours`
- `t_overtime_hours.amount`
- `t_overtime_hours.month`
- `t_payroll`
- `t_payroll.f_hireddate`
- `t_payroll_history`

---

## v_balance_ingresos
> Vista de balance de ingresos mensuales. 
- ingresos_esperados: Total de facturas por mes según fecha efectiva (puede cambiar si se paga tarde)
- ingresos_percibidos: Total de facturas realmente pagadas en el mes
- diferencia: Monto pendiente de cobro
- ventas_totales: Total de órdenes creadas en el mes
Tipo: Vista regular | Columnas: 9

### Columnas

| # | Columna | Tipo | Nullable |
|---|---------|------|----------|
| 1 | `fecha` | `date` | NULL |
| 2 | `año` | `numeric` | NULL |
| 3 | `mes_numero` | `numeric` | NULL |
| 4 | `mes_nombre` | `text` | NULL |
| 5 | `mes_corto` | `text` | NULL |
| 6 | `ingresos_esperados` | `numeric` | NULL |
| 7 | `ingresos_percibidos` | `numeric` | NULL |
| 8 | `diferencia` | `numeric` | NULL |
| 9 | `ventas_totales` | `numeric` | NULL |

### Definición SQL

```sql
WITH rango_fechas AS (
         SELECT LEAST(COALESCE(( SELECT min(t_invoice.f_invoicedate) AS min
                   FROM t_invoice
                  WHERE (t_invoice.f_invoicedate IS NOT NULL)), CURRENT_DATE), COALESCE(( SELECT min(t_payroll.f_hireddate) AS min
                   FROM t_payroll
                  WHERE (t_payroll.f_hireddate IS NOT NULL)), CURRENT_DATE), COALESCE(( SELECT min(t_expense.f_paiddate) AS min
                   FROM t_expense
                  WHERE (t_expense.f_paiddate IS NOT NULL)), CURRENT_DATE), COALESCE(( SELECT min(t_fixed_expenses_history.effective_date) AS min
                   FROM t_fixed_expenses_history
                  WHERE (t_fixed_expenses_history.effective_date IS NOT NULL)), CURRENT_DATE)) AS fecha_inicio,
            GREATEST(CURRENT_DATE, COALESCE(( SELECT max(t_invoice.due_date) AS max
                   FROM t_invoice
                  WHERE (t_invoice.due_date IS NOT NULL)), CURRENT_DATE), COALESCE(( SELECT max(t_invoice.f_invoicedate) AS max
                   FROM t_invoice
                  WHERE (t_invoice.f_invoicedate IS NOT NULL)), CURRENT_DATE)) AS fecha_fin
        ), meses AS (
         SELECT (generate_series(((date_trunc('month'::text, (( SELECT rango_fechas.fecha_inicio
                   FROM rango_fechas))::timestamp with time zone))::date)::timestamp with time zone, ((date_trunc('month'::text, (( SELECT rango_fechas.fecha_fin
                   FROM rango_fechas))::timestamp with time zone))::date)::timestamp with time zone, '1 mon'::interval))::date AS mes
        ), ingresos_esperados AS (
         SELECT (date_trunc('month'::text, (v_income.effective_payment_date)::timestamp with time zone))::date AS mes,
            sum(v_income.f_total) AS total
           FROM v_income
          WHERE (v_income.effective_payment_date IS NOT NULL)
          GROUP BY ((date_trunc('month'::text, (v_income.effective_payment_date)::timestamp with time zone))::date)
        ), ingresos_percibidos AS (
         SELECT (date_trunc('month'::text, (v_income.f_paymentdate)::timestamp with time zone))::date AS mes,
            sum(v_income.f_total) AS total
           FROM v_income
          WHERE (v_income.f_paymentdate IS NOT NULL)
          GROUP BY ((date_trunc('month'::text, (v_income.f_paymentdate)::timestamp with time zone))::date)
        ), ventas_totales AS (
         SELECT (date_trunc('month'::text, (t_order.f_podate)::timestamp with time zone))::date AS mes,
            sum(t_order.f_saletotal) AS total
           FROM t_order
          WHERE (t_order.f_podate IS NOT NULL)
          GROUP BY ((date_trunc('month'::text, (t_order.f_podate)::timestamp with time zone))::date)
        )
 SELECT m.mes AS fecha,
    EXTRACT(year FROM m.mes) AS "año",
    EXTRACT(month FROM m.mes) AS mes_numero,
    to_char((m.mes)::timestamp with time zone, 'TMMonth'::text) AS mes_nombre,
    to_char((m.mes)::timestamp with time zone, 'Mon-YY'::text) AS mes_corto,
    COALESCE(ie.total, (0)::numeric) AS ingresos_esperados,
    COALESCE(ip.total, (0)::numeric) AS ingresos_percibidos,
    (COALESCE(ie.total, (0)::numeric) - COALESCE(ip.total, (0)::numeric)) AS diferencia,
    COALESCE(vt.total, (0)::numeric) AS ventas_totales
   FROM (((meses m
     LEFT JOIN ingresos_esperados ie ON ((ie.mes = m.mes)))
     LEFT JOIN ingresos_percibidos ip ON ((ip.mes = m.mes)))
     LEFT JOIN ventas_totales vt ON ((vt.mes = m.mes)))
  ORDER BY m.mes;
```

### Tablas Referenciadas
- `t_expense`
- `t_expense.f_paiddate`
- `t_fixed_expenses_history`
- `t_fixed_expenses_history.effective_date`
- `t_invoice`
- `t_invoice.due_date`
- `t_invoice.f_invoicedate`
- `t_order`
- `t_order.f_podate`
- `t_order.f_podate)::timestamp`
- `t_payroll`
- `t_payroll.f_hireddate`

---

## v_expense_audit_report
Tipo: Vista regular | Columnas: 15

### Columnas

| # | Columna | Tipo | Nullable |
|---|---------|------|----------|
| 1 | `id` | `int4` | NULL |
| 2 | `expense_id` | `int4` | NULL |
| 3 | `action` | `character varying` | NULL |
| 4 | `accion` | `text` | NULL |
| 5 | `descripcion` | `text` | NULL |
| 6 | `proveedor` | `character varying` | NULL |
| 7 | `order_po` | `character varying` | NULL |
| 8 | `monto_anterior` | `numeric` | NULL |
| 9 | `monto_nuevo` | `numeric` | NULL |
| 10 | `diferencia` | `numeric` | NULL |
| 11 | `estado_anterior` | `character varying` | NULL |
| 12 | `estado_nuevo` | `character varying` | NULL |
| 13 | `changed_at` | `timestamptz` | NULL |
| 14 | `fecha_hora_mx` | `text` | NULL |
| 15 | `modificado_por` | `character varying` | NULL |

### Definición SQL

```sql
SELECT id,
    expense_id,
    action,
        CASE action
            WHEN 'INSERT'::text THEN 'Creado'::text
            WHEN 'UPDATE'::text THEN 'Modificado'::text
            WHEN 'DELETE'::text THEN 'Eliminado'::text
            WHEN 'PAID'::text THEN 'Pagado'::text
            WHEN 'UNPAID'::text THEN 'Pago revertido'::text
            ELSE NULL::text
        END AS accion,
    COALESCE(new_description, old_description) AS descripcion,
    supplier_name AS proveedor,
    order_po,
    old_total_expense AS monto_anterior,
    new_total_expense AS monto_nuevo,
    amount_change AS diferencia,
    old_status AS estado_anterior,
    new_status AS estado_nuevo,
    changed_at,
    to_char((changed_at AT TIME ZONE 'America/Mexico_City'::text), 'DD/MM/YYYY HH24:MI:SS'::text) AS fecha_hora_mx,
    COALESCE(new_updated_by, (new_created_by)::character varying, 'Sistema'::character varying) AS modificado_por
   FROM t_expense_audit ea
  ORDER BY changed_at DESC;
```

### Tablas Referenciadas
- `order_po`
- `t_expense_audit`

---

## v_income
> Vista dinámica de ingresos. Calcula fecha efectiva de pago (Expr2) según lógica de Access: 
- Si PAGADA → usa f_paymentdate
- Si vencida → usa fecha actual  
- Si pendiente → usa fecha de vencimiento
Tipo: Vista regular | Columnas: 13

### Columnas

| # | Columna | Tipo | Nullable |
|---|---------|------|----------|
| 1 | `f_folio` | `character varying` | NULL |
| 2 | `f_client` | `int4` | NULL |
| 3 | `client_name` | `character varying` | NULL |
| 4 | `f_total` | `numeric` | NULL |
| 5 | `f_receptiondate` | `date` | NULL |
| 6 | `due_date` | `date` | NULL |
| 7 | `f_invoicestat` | `int4` | NULL |
| 8 | `f_paymentdate` | `date` | NULL |
| 9 | `effective_payment_date` | `date` | NULL |
| 10 | `f_order` | `int4` | NULL |
| 11 | `f_po` | `character varying` | NULL |
| 12 | `f_invoice` | `int4` | NULL |
| 13 | `status_text` | `text` | NULL |

### Definición SQL

```sql
SELECT i.f_folio,
    c.f_client,
    c.f_name AS client_name,
    i.f_total,
    i.f_receptiondate,
    ((i.f_receptiondate + ('1 day'::interval * (COALESCE(c.f_credit, 0))::double precision)))::date AS due_date,
    i.f_invoicestat,
    i.f_paymentdate,
        CASE
            WHEN (i.f_invoicestat >= 3) THEN i.f_paymentdate
            WHEN (((i.f_receptiondate + ('1 day'::interval * (COALESCE(c.f_credit, 0))::double precision)))::date < CURRENT_DATE) THEN CURRENT_DATE
            ELSE ((i.f_receptiondate + ('1 day'::interval * (COALESCE(c.f_credit, 0))::double precision)))::date
        END AS effective_payment_date,
    o.f_order,
    o.f_po,
    i.f_invoice,
        CASE
            WHEN (i.f_invoicestat = 4) THEN 'PAGADA'::text
            WHEN (i.f_invoicestat = 3) THEN 'VENCIDA'::text
            WHEN (i.f_invoicestat = 2) THEN 'PENDIENTE'::text
            ELSE 'CREADA'::text
        END AS status_text
   FROM ((t_invoice i
     JOIN t_order o ON ((i.f_order = o.f_order)))
     JOIN t_client c ON ((o.f_client = c.f_client)))
  WHERE ((i.f_invoicestat IS NOT NULL) AND (i.f_invoicestat <> 0));
```

### Tablas Referenciadas
- `t_client`
- `t_invoice`
- `t_order`

---

## v_order_gastos
Tipo: Vista regular | Columnas: 25

### Columnas

| # | Columna | Tipo | Nullable |
|---|---------|------|----------|
| 1 | `f_order` | `int4` | NULL |
| 2 | `f_po` | `character varying` | NULL |
| 3 | `f_quote` | `character varying` | NULL |
| 4 | `f_podate` | `date` | NULL |
| 5 | `f_client` | `int4` | NULL |
| 6 | `f_contact` | `int4` | NULL |
| 7 | `f_description` | `character varying` | NULL |
| 8 | `f_salesman` | `int4` | NULL |
| 9 | `f_estdelivery` | `date` | NULL |
| 10 | `f_salesubtotal` | `numeric` | NULL |
| 11 | `f_saletotal` | `numeric` | NULL |
| 12 | `f_orderstat` | `int4` | NULL |
| 13 | `progress_percentage` | `int4` | NULL |
| 14 | `order_percentage` | `int4` | NULL |
| 15 | `f_commission_rate` | `numeric` | NULL |
| 16 | `created_by` | `int4` | NULL |
| 17 | `created_at` | `timestamp without time zone` | NULL |
| 18 | `updated_by` | `int4` | NULL |
| 19 | `updated_at` | `timestamp without time zone` | NULL |
| 20 | `gasto_operativo` | `numeric` | NULL |
| 21 | `gasto_indirecto` | `numeric` | NULL |
| 22 | `gasto_material` | `numeric` | NULL |
| 23 | `gasto_material_pendiente` | `numeric` | NULL |
| 24 | `total_gastos_proveedor` | `numeric` | NULL |
| 25 | `num_facturas_proveedor` | `int8` | NULL |

### Definición SQL

```sql
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
    COALESCE(o.gasto_operativo, (0)::numeric) AS gasto_operativo,
    COALESCE(o.gasto_indirecto, (0)::numeric) AS gasto_indirecto,
    COALESCE(g.gasto_material_pagado, (0)::numeric) AS gasto_material,
    COALESCE(g.gasto_material_pendiente, (0)::numeric) AS gasto_material_pendiente,
    COALESCE(g.total_gastos, (0)::numeric) AS total_gastos_proveedor,
    COALESCE(g.num_facturas, (0)::bigint) AS num_facturas_proveedor
   FROM (t_order o
     LEFT JOIN ( SELECT t_expense.f_order,
            sum(
                CASE
                    WHEN ((t_expense.f_status)::text = 'PAGADO'::text) THEN t_expense.f_totalexpense
                    ELSE (0)::numeric
                END) AS gasto_material_pagado,
            sum(
                CASE
                    WHEN ((t_expense.f_status)::text = 'PENDIENTE'::text) THEN t_expense.f_totalexpense
                    ELSE (0)::numeric
                END) AS gasto_material_pendiente,
            sum(t_expense.f_totalexpense) AS total_gastos,
            count(*) AS num_facturas
           FROM t_expense
          WHERE (t_expense.f_order IS NOT NULL)
          GROUP BY t_expense.f_order) g ON ((o.f_order = g.f_order)));
```

### Tablas Referenciadas
- `t_expense`
- `t_expense.f_order`
- `t_expense.f_status)::text`
- `t_expense.f_totalexpense`
- `t_order`

---

## v_vacations_active
> Vacaciones activas, próximas y recientes (últimos 30 días)
Tipo: Vista regular | Columnas: 13

### Columnas

| # | Columna | Tipo | Nullable |
|---|---------|------|----------|
| 1 | `id` | `int4` | NULL |
| 2 | `employee_id` | `int4` | NULL |
| 3 | `employee_name` | `character varying` | NULL |
| 4 | `title` | `character varying` | NULL |
| 5 | `start_date` | `date` | NULL |
| 6 | `end_date` | `date` | NULL |
| 7 | `total_days` | `int4` | NULL |
| 8 | `notes` | `text` | NULL |
| 9 | `status` | `character varying` | NULL |
| 10 | `approved_by_user` | `character varying` | NULL |
| 11 | `approved_at` | `timestamp without time zone` | NULL |
| 12 | `vacation_status` | `text` | NULL |
| 13 | `days_remaining` | `int4` | NULL |

### Definición SQL

```sql
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
            WHEN ((CURRENT_DATE >= v.start_date) AND (CURRENT_DATE <= v.end_date)) THEN 'EN_CURSO'::text
            WHEN (v.start_date > CURRENT_DATE) THEN 'PROXIMA'::text
            ELSE 'FINALIZADA'::text
        END AS vacation_status,
    (v.end_date - CURRENT_DATE) AS days_remaining
   FROM ((t_vacation v
     JOIN t_payroll p ON ((v.employee_id = p.f_payroll)))
     LEFT JOIN users u ON ((v.approved_by = u.id)))
  WHERE (((v.status)::text = 'APROBADA'::text) AND (v.end_date >= (CURRENT_DATE - '30 days'::interval)))
  ORDER BY v.start_date;
```

### Tablas Referenciadas
- `t_payroll`
- `t_vacation`
- `users`

---

# Vistas Materializadas

## mv_balance_completo
Tipo: Vista materializada | Columnas: 17

### Columnas

| # | Columna | Tipo | Nullable |
|---|---------|------|----------|
| 1 | `fecha` | `date` | NULL |
| 2 | `año` | `numeric` | NULL |
| 3 | `mes_numero` | `numeric` | NULL |
| 4 | `mes_nombre` | `text` | NULL |
| 5 | `mes_corto` | `text` | NULL |
| 6 | `nomina` | `numeric` | NULL |
| 7 | `horas_extra` | `numeric` | NULL |
| 8 | `gastos_fijos` | `numeric` | NULL |
| 9 | `gastos_variables` | `numeric` | NULL |
| 10 | `gasto_operativo` | `numeric` | NULL |
| 11 | `gasto_indirecto` | `numeric` | NULL |
| 12 | `total_gastos` | `numeric` | NULL |
| 13 | `ingresos_esperados` | `numeric` | NULL |
| 14 | `ingresos_percibidos` | `numeric` | NULL |
| 15 | `diferencia` | `numeric` | NULL |
| 16 | `ventas_totales` | `numeric` | NULL |
| 17 | `utilidad_aproximada` | `numeric` | NULL |

### Definición SQL

```sql
SELECT fecha,
    "año",
    mes_numero,
    mes_nombre,
    mes_corto,
    nomina,
    horas_extra,
    gastos_fijos,
    gastos_variables,
    gasto_operativo,
    gasto_indirecto,
    total_gastos,
    ingresos_esperados,
    ingresos_percibidos,
    diferencia,
    ventas_totales,
    utilidad_aproximada
   FROM v_balance_completo;
```

### Indexes
- `idx_mv_balance_pk`: `CREATE UNIQUE INDEX idx_mv_balance_pk ON public.mv_balance_completo USING btree ("año", mes_numero)`

---
