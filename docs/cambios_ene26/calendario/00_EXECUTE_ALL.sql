-- ============================================
-- Módulo Calendario de Personal
-- Script MASTER: Ejecutar todos los scripts en orden
-- Fecha: 2026-01-25
-- ============================================
--
-- INSTRUCCIONES:
-- Ejecutar los scripts en el siguiente orden:
--
-- 1. 01_CREATE_TABLES.sql    - Tablas principales (t_attendance, t_vacation)
-- 2. 02_CREATE_HOLIDAYS.sql  - Feriados y configuración de días laborales
-- 3. 03_CREATE_AUDIT.sql     - Sistema de auditoría con triggers
-- 4. 04_CREATE_VIEWS.sql     - Vistas para reportes
--
-- O ejecutar este archivo que incluye todo:
-- ============================================

BEGIN;

-- ============================================
-- PASO 1: Crear tablas principales
-- ============================================
\echo '>>> Paso 1: Creando tablas principales...'

CREATE TABLE IF NOT EXISTS t_attendance (
    id SERIAL PRIMARY KEY,
    employee_id INTEGER NOT NULL REFERENCES t_payroll(f_payroll) ON DELETE CASCADE,
    attendance_date DATE NOT NULL,
    status VARCHAR(20) NOT NULL CHECK (status IN ('ASISTENCIA', 'RETARDO', 'FALTA', 'VACACIONES', 'FERIADO', 'DESCANSO')),
    check_in_time TIME,
    check_out_time TIME,
    late_minutes INTEGER DEFAULT 0,
    notes TEXT,
    is_justified BOOLEAN DEFAULT FALSE,
    justification TEXT,
    created_by INTEGER,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_by INTEGER,
    updated_at TIMESTAMP,
    UNIQUE(employee_id, attendance_date)
);

CREATE TABLE IF NOT EXISTS t_vacation (
    id SERIAL PRIMARY KEY,
    employee_id INTEGER NOT NULL REFERENCES t_payroll(f_payroll) ON DELETE CASCADE,
    start_date DATE NOT NULL,
    end_date DATE NOT NULL,
    total_days INTEGER GENERATED ALWAYS AS (end_date - start_date + 1) STORED,
    notes TEXT,
    status VARCHAR(20) DEFAULT 'PENDIENTE' CHECK (status IN ('PENDIENTE', 'APROBADA', 'RECHAZADA', 'CANCELADA')),
    approved_by INTEGER,
    approved_at TIMESTAMP,
    rejection_reason TEXT,
    created_by INTEGER,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_by INTEGER,
    updated_at TIMESTAMP,
    CHECK (end_date >= start_date)
);

-- Índices
CREATE INDEX IF NOT EXISTS idx_attendance_date ON t_attendance(attendance_date);
CREATE INDEX IF NOT EXISTS idx_attendance_employee ON t_attendance(employee_id);
CREATE INDEX IF NOT EXISTS idx_attendance_status ON t_attendance(status);
CREATE INDEX IF NOT EXISTS idx_vacation_dates ON t_vacation(start_date, end_date);
CREATE INDEX IF NOT EXISTS idx_vacation_employee ON t_vacation(employee_id);

\echo '>>> Tablas principales creadas'

-- ============================================
-- PASO 2: Crear tabla de feriados
-- ============================================
\echo '>>> Paso 2: Creando tabla de feriados...'

CREATE TABLE IF NOT EXISTS t_holiday (
    id SERIAL PRIMARY KEY,
    holiday_date DATE NOT NULL UNIQUE,
    name VARCHAR(100) NOT NULL,
    description TEXT,
    is_mandatory BOOLEAN DEFAULT TRUE,
    is_recurring BOOLEAN DEFAULT TRUE,
    recurring_month INTEGER,
    recurring_day INTEGER,
    recurring_rule VARCHAR(50),
    year INTEGER,
    created_by INTEGER,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_by INTEGER,
    updated_at TIMESTAMP
);

CREATE TABLE IF NOT EXISTS t_workday_config (
    id SERIAL PRIMARY KEY,
    day_of_week INTEGER NOT NULL CHECK (day_of_week BETWEEN 0 AND 6),
    is_workday BOOLEAN DEFAULT TRUE,
    description VARCHAR(50),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP,
    UNIQUE(day_of_week)
);

-- Configuración por defecto
INSERT INTO t_workday_config (day_of_week, is_workday, description)
VALUES
    (0, FALSE, 'Domingo'),
    (1, TRUE, 'Lunes'),
    (2, TRUE, 'Martes'),
    (3, TRUE, 'Miércoles'),
    (4, TRUE, 'Jueves'),
    (5, TRUE, 'Viernes'),
    (6, FALSE, 'Sábado')
ON CONFLICT (day_of_week) DO NOTHING;

-- Feriados México 2026
INSERT INTO t_holiday (holiday_date, name, description, is_mandatory, is_recurring, recurring_month, recurring_day)
VALUES
    ('2026-01-01', 'Año Nuevo', 'Día de Año Nuevo', TRUE, TRUE, 1, 1),
    ('2026-02-05', 'Día de la Constitución', 'Aniversario de la Constitución Mexicana', TRUE, TRUE, 2, 5),
    ('2026-03-21', 'Natalicio de Benito Juárez', 'Aniversario del nacimiento de Benito Juárez', TRUE, TRUE, 3, 21),
    ('2026-05-01', 'Día del Trabajo', 'Día Internacional del Trabajo', TRUE, TRUE, 5, 1),
    ('2026-09-16', 'Día de la Independencia', 'Aniversario del Grito de Independencia', TRUE, TRUE, 9, 16),
    ('2026-11-20', 'Revolución Mexicana', 'Aniversario de la Revolución Mexicana', TRUE, TRUE, 11, 20),
    ('2026-12-25', 'Navidad', 'Día de Navidad', TRUE, TRUE, 12, 25)
ON CONFLICT (holiday_date) DO NOTHING;

\echo '>>> Feriados creados'

-- ============================================
-- PASO 3: Crear sistema de auditoría
-- ============================================
\echo '>>> Paso 3: Creando sistema de auditoría...'

CREATE TABLE IF NOT EXISTS t_attendance_audit (
    id SERIAL PRIMARY KEY,
    attendance_id INTEGER NOT NULL,
    employee_id INTEGER NOT NULL,
    attendance_date DATE NOT NULL,
    action VARCHAR(10) NOT NULL CHECK (action IN ('INSERT', 'UPDATE', 'DELETE')),
    old_status VARCHAR(20),
    old_check_in_time TIME,
    old_check_out_time TIME,
    old_late_minutes INTEGER,
    old_notes TEXT,
    old_is_justified BOOLEAN,
    new_status VARCHAR(20),
    new_check_in_time TIME,
    new_check_out_time TIME,
    new_late_minutes INTEGER,
    new_notes TEXT,
    new_is_justified BOOLEAN,
    changed_by INTEGER,
    changed_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    ip_address INET,
    user_agent TEXT,
    change_reason TEXT
);

CREATE INDEX IF NOT EXISTS idx_audit_attendance_id ON t_attendance_audit(attendance_id);
CREATE INDEX IF NOT EXISTS idx_audit_changed_at ON t_attendance_audit(changed_at);

-- Función de auditoría
CREATE OR REPLACE FUNCTION audit_attendance_changes()
RETURNS TRIGGER AS $$
BEGIN
    IF TG_OP = 'INSERT' THEN
        INSERT INTO t_attendance_audit (
            attendance_id, employee_id, attendance_date, action,
            new_status, new_check_in_time, new_check_out_time,
            new_late_minutes, new_notes, new_is_justified, changed_by
        ) VALUES (
            NEW.id, NEW.employee_id, NEW.attendance_date, 'INSERT',
            NEW.status, NEW.check_in_time, NEW.check_out_time,
            NEW.late_minutes, NEW.notes, NEW.is_justified, NEW.created_by
        );
        RETURN NEW;
    ELSIF TG_OP = 'UPDATE' THEN
        IF OLD.status IS DISTINCT FROM NEW.status
           OR OLD.late_minutes IS DISTINCT FROM NEW.late_minutes
           OR OLD.is_justified IS DISTINCT FROM NEW.is_justified
        THEN
            INSERT INTO t_attendance_audit (
                attendance_id, employee_id, attendance_date, action,
                old_status, old_late_minutes, old_is_justified,
                new_status, new_late_minutes, new_is_justified, changed_by
            ) VALUES (
                NEW.id, NEW.employee_id, NEW.attendance_date, 'UPDATE',
                OLD.status, OLD.late_minutes, OLD.is_justified,
                NEW.status, NEW.late_minutes, NEW.is_justified, NEW.updated_by
            );
        END IF;
        RETURN NEW;
    ELSIF TG_OP = 'DELETE' THEN
        INSERT INTO t_attendance_audit (
            attendance_id, employee_id, attendance_date, action,
            old_status, old_late_minutes, old_is_justified, changed_by
        ) VALUES (
            OLD.id, OLD.employee_id, OLD.attendance_date, 'DELETE',
            OLD.status, OLD.late_minutes, OLD.is_justified, OLD.updated_by
        );
        RETURN OLD;
    END IF;
    RETURN NULL;
END;
$$ LANGUAGE plpgsql;

-- Trigger
DROP TRIGGER IF EXISTS trg_attendance_audit ON t_attendance;
CREATE TRIGGER trg_attendance_audit
    AFTER INSERT OR UPDATE OR DELETE ON t_attendance
    FOR EACH ROW EXECUTE FUNCTION audit_attendance_changes();

\echo '>>> Sistema de auditoría creado'

-- ============================================
-- PASO 4: Función is_workday
-- ============================================
CREATE OR REPLACE FUNCTION is_workday(check_date DATE)
RETURNS BOOLEAN AS $$
DECLARE
    is_holiday BOOLEAN;
    day_is_workday BOOLEAN;
BEGIN
    SELECT EXISTS(SELECT 1 FROM t_holiday WHERE holiday_date = check_date AND is_mandatory = TRUE)
    INTO is_holiday;

    IF is_holiday THEN RETURN FALSE; END IF;

    SELECT is_workday INTO day_is_workday
    FROM t_workday_config
    WHERE day_of_week = EXTRACT(DOW FROM check_date);

    RETURN COALESCE(day_is_workday, TRUE);
END;
$$ LANGUAGE plpgsql;

\echo '>>> Funciones auxiliares creadas'

COMMIT;

\echo ''
\echo '============================================'
\echo '  MÓDULO CALENDARIO INSTALADO CORRECTAMENTE'
\echo '============================================'
\echo ''

-- Verificación final
SELECT
    'Tablas creadas' as item,
    COUNT(*) as count
FROM information_schema.tables
WHERE table_name IN ('t_attendance', 't_vacation', 't_holiday', 't_workday_config', 't_attendance_audit')
UNION ALL
SELECT
    'Feriados 2026' as item,
    COUNT(*) as count
FROM t_holiday
WHERE EXTRACT(YEAR FROM holiday_date) = 2026;
