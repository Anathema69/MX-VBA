-- ============================================
-- Módulo Calendario de Personal
-- Script 04: Vistas para Reportes y Consultas
-- Fecha: 2026-01-25
-- ============================================

-- ============================================
-- 1. Vista: Resumen de Asistencia Mensual por Empleado
-- ============================================
CREATE OR REPLACE VIEW v_attendance_monthly_summary AS
SELECT
    p.f_payroll as employee_id,
    p.f_employee as employee_name,
    p.f_title as title,
    p.employee_code,
    DATE_TRUNC('month', a.attendance_date)::DATE as month,
    TO_CHAR(DATE_TRUNC('month', a.attendance_date), 'YYYY-MM') as month_key,
    COUNT(CASE WHEN a.status = 'ASISTENCIA' THEN 1 END) as asistencias,
    COUNT(CASE WHEN a.status = 'RETARDO' THEN 1 END) as retardos,
    COUNT(CASE WHEN a.status = 'FALTA' THEN 1 END) as faltas,
    COUNT(CASE WHEN a.status = 'VACACIONES' THEN 1 END) as vacaciones,
    COUNT(CASE WHEN a.status = 'FERIADO' THEN 1 END) as feriados,
    COUNT(CASE WHEN a.status = 'DESCANSO' THEN 1 END) as descansos,
    SUM(COALESCE(a.late_minutes, 0)) as total_late_minutes,
    COUNT(CASE WHEN a.is_justified = TRUE AND a.status IN ('FALTA', 'RETARDO') THEN 1 END) as justified_count
FROM t_payroll p
LEFT JOIN t_attendance a ON p.f_payroll = a.employee_id
WHERE p.is_active = TRUE
GROUP BY p.f_payroll, p.f_employee, p.f_title, p.employee_code, DATE_TRUNC('month', a.attendance_date);

COMMENT ON VIEW v_attendance_monthly_summary IS 'Resumen mensual de asistencia por empleado';

-- ============================================
-- 2. Vista: Estado de Asistencia del Día
-- ============================================
CREATE OR REPLACE VIEW v_attendance_today AS
SELECT
    p.f_payroll as employee_id,
    p.f_employee as employee_name,
    p.f_title as title,
    p.employee_code,
    SUBSTRING(p.f_employee, 1, 1) ||
        COALESCE(SUBSTRING(SPLIT_PART(p.f_employee, ' ', 2), 1, 1), '') as initials,
    CURRENT_DATE as today,
    a.id as attendance_id,
    COALESCE(a.status, 'SIN_REGISTRO') as status,
    a.check_in_time,
    a.check_out_time,
    a.late_minutes,
    a.notes,
    a.is_justified,
    -- Verificar si hay vacaciones activas
    CASE WHEN v.id IS NOT NULL THEN TRUE ELSE FALSE END as on_vacation,
    v.start_date as vacation_start,
    v.end_date as vacation_end,
    -- Verificar si es feriado
    CASE WHEN h.id IS NOT NULL THEN TRUE ELSE FALSE END as is_holiday,
    h.name as holiday_name,
    -- Verificar si es día laboral
    wc.is_workday
FROM t_payroll p
LEFT JOIN t_attendance a ON p.f_payroll = a.employee_id AND a.attendance_date = CURRENT_DATE
LEFT JOIN t_vacation v ON p.f_payroll = v.employee_id
    AND v.status = 'APROBADA'
    AND CURRENT_DATE BETWEEN v.start_date AND v.end_date
LEFT JOIN t_holiday h ON h.holiday_date = CURRENT_DATE AND h.is_mandatory = TRUE
LEFT JOIN t_workday_config wc ON wc.day_of_week = EXTRACT(DOW FROM CURRENT_DATE)
WHERE p.is_active = TRUE
ORDER BY p.f_employee;

COMMENT ON VIEW v_attendance_today IS 'Estado de asistencia de todos los empleados para el día actual';

-- ============================================
-- 3. Vista: Asistencia por Fecha Específica
-- ============================================
CREATE OR REPLACE FUNCTION get_attendance_for_date(check_date DATE)
RETURNS TABLE (
    employee_id INTEGER,
    employee_name VARCHAR,
    title VARCHAR,
    employee_code VARCHAR,
    initials VARCHAR,
    attendance_id INTEGER,
    status VARCHAR,
    check_in_time TIME,
    check_out_time TIME,
    late_minutes INTEGER,
    notes TEXT,
    is_justified BOOLEAN,
    on_vacation BOOLEAN,
    vacation_start DATE,
    vacation_end DATE,
    is_holiday BOOLEAN,
    holiday_name VARCHAR,
    is_workday BOOLEAN
) AS $$
BEGIN
    RETURN QUERY
    SELECT
        p.f_payroll,
        p.f_employee,
        p.f_title,
        p.employee_code,
        SUBSTRING(p.f_employee, 1, 1) ||
            COALESCE(SUBSTRING(SPLIT_PART(p.f_employee, ' ', 2), 1, 1), ''),
        a.id,
        COALESCE(a.status, 'SIN_REGISTRO'),
        a.check_in_time,
        a.check_out_time,
        a.late_minutes,
        a.notes,
        a.is_justified,
        CASE WHEN v.id IS NOT NULL THEN TRUE ELSE FALSE END,
        v.start_date,
        v.end_date,
        CASE WHEN h.id IS NOT NULL THEN TRUE ELSE FALSE END,
        h.name,
        wc.is_workday
    FROM t_payroll p
    LEFT JOIN t_attendance a ON p.f_payroll = a.employee_id AND a.attendance_date = check_date
    LEFT JOIN t_vacation v ON p.f_payroll = v.employee_id
        AND v.status = 'APROBADA'
        AND check_date BETWEEN v.start_date AND v.end_date
    LEFT JOIN t_holiday h ON h.holiday_date = check_date AND h.is_mandatory = TRUE
    LEFT JOIN t_workday_config wc ON wc.day_of_week = EXTRACT(DOW FROM check_date)
    WHERE p.is_active = TRUE
    ORDER BY p.f_employee;
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION get_attendance_for_date IS 'Obtiene el estado de asistencia de todos los empleados para una fecha específica';

-- ============================================
-- 4. Vista: Calendario del Mes con Estadísticas
-- ============================================
CREATE OR REPLACE FUNCTION get_month_calendar(target_year INTEGER, target_month INTEGER)
RETURNS TABLE (
    calendar_date DATE,
    day_of_week INTEGER,
    day_name VARCHAR,
    is_workday BOOLEAN,
    is_holiday BOOLEAN,
    holiday_name VARCHAR,
    total_employees INTEGER,
    asistencias INTEGER,
    retardos INTEGER,
    faltas INTEGER,
    vacaciones INTEGER,
    sin_registro INTEGER
) AS $$
DECLARE
    first_day DATE;
    last_day DATE;
BEGIN
    first_day := MAKE_DATE(target_year, target_month, 1);
    last_day := (first_day + INTERVAL '1 month' - INTERVAL '1 day')::DATE;

    RETURN QUERY
    SELECT
        d.date::DATE,
        EXTRACT(DOW FROM d.date)::INTEGER,
        TO_CHAR(d.date, 'Day')::VARCHAR,
        COALESCE(wc.is_workday, TRUE),
        CASE WHEN h.id IS NOT NULL THEN TRUE ELSE FALSE END,
        h.name::VARCHAR,
        (SELECT COUNT(*)::INTEGER FROM t_payroll WHERE is_active = TRUE),
        COUNT(CASE WHEN a.status = 'ASISTENCIA' THEN 1 END)::INTEGER,
        COUNT(CASE WHEN a.status = 'RETARDO' THEN 1 END)::INTEGER,
        COUNT(CASE WHEN a.status = 'FALTA' THEN 1 END)::INTEGER,
        COUNT(CASE WHEN a.status = 'VACACIONES' THEN 1 END)::INTEGER,
        ((SELECT COUNT(*) FROM t_payroll WHERE is_active = TRUE) - COUNT(a.id))::INTEGER
    FROM generate_series(first_day, last_day, '1 day'::INTERVAL) AS d(date)
    LEFT JOIN t_workday_config wc ON wc.day_of_week = EXTRACT(DOW FROM d.date)
    LEFT JOIN t_holiday h ON h.holiday_date = d.date AND h.is_mandatory = TRUE
    LEFT JOIN t_attendance a ON a.attendance_date = d.date
    GROUP BY d.date, wc.is_workday, h.id, h.name
    ORDER BY d.date;
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION get_month_calendar IS 'Genera el calendario del mes con estadísticas de asistencia';

-- ============================================
-- 5. Vista: Vacaciones Activas y Próximas
-- ============================================
CREATE OR REPLACE VIEW v_vacations_active AS
SELECT
    v.id,
    v.employee_id,
    p.f_employee as employee_name,
    p.f_title as title,
    v.start_date,
    v.end_date,
    v.total_days,
    v.notes,
    v.status,
    u.username as approved_by_user,
    v.approved_at,
    CASE
        WHEN CURRENT_DATE BETWEEN v.start_date AND v.end_date THEN 'EN_CURSO'
        WHEN v.start_date > CURRENT_DATE THEN 'PROXIMA'
        ELSE 'FINALIZADA'
    END as vacation_status,
    v.end_date - CURRENT_DATE as days_remaining
FROM t_vacation v
JOIN t_payroll p ON v.employee_id = p.f_payroll
LEFT JOIN users u ON v.approved_by = u.id
WHERE v.status = 'APROBADA'
AND v.end_date >= CURRENT_DATE - INTERVAL '30 days'
ORDER BY v.start_date;

COMMENT ON VIEW v_vacations_active IS 'Vacaciones activas, próximas y recientes (últimos 30 días)';

-- ============================================
-- 6. Vista: Estadísticas Generales del Mes
-- ============================================
CREATE OR REPLACE VIEW v_attendance_stats AS
SELECT
    DATE_TRUNC('month', CURRENT_DATE)::DATE as current_month,
    (SELECT COUNT(*) FROM t_payroll WHERE is_active = TRUE) as total_employees,
    (SELECT COUNT(*) FROM t_attendance
     WHERE DATE_TRUNC('month', attendance_date) = DATE_TRUNC('month', CURRENT_DATE)
     AND status = 'ASISTENCIA') as total_asistencias,
    (SELECT COUNT(*) FROM t_attendance
     WHERE DATE_TRUNC('month', attendance_date) = DATE_TRUNC('month', CURRENT_DATE)
     AND status = 'RETARDO') as total_retardos,
    (SELECT COUNT(*) FROM t_attendance
     WHERE DATE_TRUNC('month', attendance_date) = DATE_TRUNC('month', CURRENT_DATE)
     AND status = 'FALTA') as total_faltas,
    (SELECT COUNT(*) FROM t_attendance
     WHERE DATE_TRUNC('month', attendance_date) = DATE_TRUNC('month', CURRENT_DATE)
     AND status = 'VACACIONES') as total_vacaciones,
    (SELECT SUM(late_minutes) FROM t_attendance
     WHERE DATE_TRUNC('month', attendance_date) = DATE_TRUNC('month', CURRENT_DATE)) as total_late_minutes;

COMMENT ON VIEW v_attendance_stats IS 'Estadísticas generales de asistencia del mes actual';

-- ============================================
-- 7. Verificar vistas creadas
-- ============================================
SELECT
    table_name as view_name,
    'VIEW' as type
FROM information_schema.views
WHERE table_schema = 'public'
AND table_name LIKE 'v_attendance%' OR table_name LIKE 'v_vacation%'
ORDER BY table_name;
