-- ============================================
-- Módulo Calendario de Personal
-- Script 03: Sistema de Auditoría con Triggers
-- Fecha: 2026-01-25
-- ============================================

-- ============================================
-- 1. Tabla de Auditoría de Asistencias
-- ============================================
CREATE TABLE IF NOT EXISTS t_attendance_audit (
    id SERIAL PRIMARY KEY,
    attendance_id INTEGER NOT NULL,
    employee_id INTEGER NOT NULL,
    attendance_date DATE NOT NULL,
    action VARCHAR(10) NOT NULL CHECK (action IN ('INSERT', 'UPDATE', 'DELETE')),

    -- Valores anteriores (NULL para INSERT)
    old_status VARCHAR(20),
    old_check_in_time TIME,
    old_check_out_time TIME,
    old_late_minutes INTEGER,
    old_notes TEXT,
    old_is_justified BOOLEAN,

    -- Valores nuevos (NULL para DELETE)
    new_status VARCHAR(20),
    new_check_in_time TIME,
    new_check_out_time TIME,
    new_late_minutes INTEGER,
    new_notes TEXT,
    new_is_justified BOOLEAN,

    -- Metadatos de auditoría
    changed_by INTEGER,
    changed_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    ip_address INET,
    user_agent TEXT,
    change_reason TEXT
);

CREATE INDEX IF NOT EXISTS idx_audit_attendance_id ON t_attendance_audit(attendance_id);
CREATE INDEX IF NOT EXISTS idx_audit_employee_id ON t_attendance_audit(employee_id);
CREATE INDEX IF NOT EXISTS idx_audit_date ON t_attendance_audit(attendance_date);
CREATE INDEX IF NOT EXISTS idx_audit_action ON t_attendance_audit(action);
CREATE INDEX IF NOT EXISTS idx_audit_changed_at ON t_attendance_audit(changed_at);

COMMENT ON TABLE t_attendance_audit IS 'Historial de cambios en registros de asistencia';
COMMENT ON COLUMN t_attendance_audit.action IS 'Tipo de operación: INSERT, UPDATE, DELETE';
COMMENT ON COLUMN t_attendance_audit.changed_by IS 'ID del usuario que realizó el cambio';

-- ============================================
-- 2. Función Trigger para Auditoría de Asistencias
-- ============================================
CREATE OR REPLACE FUNCTION audit_attendance_changes()
RETURNS TRIGGER AS $$
BEGIN
    IF TG_OP = 'INSERT' THEN
        INSERT INTO t_attendance_audit (
            attendance_id, employee_id, attendance_date, action,
            new_status, new_check_in_time, new_check_out_time,
            new_late_minutes, new_notes, new_is_justified,
            changed_by, changed_at
        ) VALUES (
            NEW.id, NEW.employee_id, NEW.attendance_date, 'INSERT',
            NEW.status, NEW.check_in_time, NEW.check_out_time,
            NEW.late_minutes, NEW.notes, NEW.is_justified,
            NEW.created_by, CURRENT_TIMESTAMP
        );
        RETURN NEW;

    ELSIF TG_OP = 'UPDATE' THEN
        -- Solo registrar si hubo cambios reales
        IF OLD.status IS DISTINCT FROM NEW.status
           OR OLD.check_in_time IS DISTINCT FROM NEW.check_in_time
           OR OLD.check_out_time IS DISTINCT FROM NEW.check_out_time
           OR OLD.late_minutes IS DISTINCT FROM NEW.late_minutes
           OR OLD.notes IS DISTINCT FROM NEW.notes
           OR OLD.is_justified IS DISTINCT FROM NEW.is_justified
        THEN
            INSERT INTO t_attendance_audit (
                attendance_id, employee_id, attendance_date, action,
                old_status, old_check_in_time, old_check_out_time,
                old_late_minutes, old_notes, old_is_justified,
                new_status, new_check_in_time, new_check_out_time,
                new_late_minutes, new_notes, new_is_justified,
                changed_by, changed_at
            ) VALUES (
                NEW.id, NEW.employee_id, NEW.attendance_date, 'UPDATE',
                OLD.status, OLD.check_in_time, OLD.check_out_time,
                OLD.late_minutes, OLD.notes, OLD.is_justified,
                NEW.status, NEW.check_in_time, NEW.check_out_time,
                NEW.late_minutes, NEW.notes, NEW.is_justified,
                NEW.updated_by, CURRENT_TIMESTAMP
            );
        END IF;
        RETURN NEW;

    ELSIF TG_OP = 'DELETE' THEN
        INSERT INTO t_attendance_audit (
            attendance_id, employee_id, attendance_date, action,
            old_status, old_check_in_time, old_check_out_time,
            old_late_minutes, old_notes, old_is_justified,
            changed_by, changed_at
        ) VALUES (
            OLD.id, OLD.employee_id, OLD.attendance_date, 'DELETE',
            OLD.status, OLD.check_in_time, OLD.check_out_time,
            OLD.late_minutes, OLD.notes, OLD.is_justified,
            OLD.updated_by, CURRENT_TIMESTAMP
        );
        RETURN OLD;
    END IF;

    RETURN NULL;
END;
$$ LANGUAGE plpgsql;

-- ============================================
-- 3. Crear Trigger en t_attendance
-- ============================================
DROP TRIGGER IF EXISTS trg_attendance_audit ON t_attendance;
CREATE TRIGGER trg_attendance_audit
    AFTER INSERT OR UPDATE OR DELETE ON t_attendance
    FOR EACH ROW
    EXECUTE FUNCTION audit_attendance_changes();

-- ============================================
-- 4. Tabla de Auditoría de Vacaciones
-- ============================================
CREATE TABLE IF NOT EXISTS t_vacation_audit (
    id SERIAL PRIMARY KEY,
    vacation_id INTEGER NOT NULL,
    employee_id INTEGER NOT NULL,
    action VARCHAR(10) NOT NULL CHECK (action IN ('INSERT', 'UPDATE', 'DELETE')),

    -- Valores anteriores
    old_start_date DATE,
    old_end_date DATE,
    old_status VARCHAR(20),
    old_notes TEXT,

    -- Valores nuevos
    new_start_date DATE,
    new_end_date DATE,
    new_status VARCHAR(20),
    new_notes TEXT,

    -- Metadatos
    changed_by INTEGER,
    changed_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    change_reason TEXT
);

CREATE INDEX IF NOT EXISTS idx_vacation_audit_id ON t_vacation_audit(vacation_id);
CREATE INDEX IF NOT EXISTS idx_vacation_audit_employee ON t_vacation_audit(employee_id);

COMMENT ON TABLE t_vacation_audit IS 'Historial de cambios en registros de vacaciones';

-- ============================================
-- 5. Función Trigger para Auditoría de Vacaciones
-- ============================================
CREATE OR REPLACE FUNCTION audit_vacation_changes()
RETURNS TRIGGER AS $$
BEGIN
    IF TG_OP = 'INSERT' THEN
        INSERT INTO t_vacation_audit (
            vacation_id, employee_id, action,
            new_start_date, new_end_date, new_status, new_notes,
            changed_by
        ) VALUES (
            NEW.id, NEW.employee_id, 'INSERT',
            NEW.start_date, NEW.end_date, NEW.status, NEW.notes,
            NEW.created_by
        );
        RETURN NEW;

    ELSIF TG_OP = 'UPDATE' THEN
        INSERT INTO t_vacation_audit (
            vacation_id, employee_id, action,
            old_start_date, old_end_date, old_status, old_notes,
            new_start_date, new_end_date, new_status, new_notes,
            changed_by
        ) VALUES (
            NEW.id, NEW.employee_id, 'UPDATE',
            OLD.start_date, OLD.end_date, OLD.status, OLD.notes,
            NEW.start_date, NEW.end_date, NEW.status, NEW.notes,
            NEW.updated_by
        );
        RETURN NEW;

    ELSIF TG_OP = 'DELETE' THEN
        INSERT INTO t_vacation_audit (
            vacation_id, employee_id, action,
            old_start_date, old_end_date, old_status, old_notes,
            changed_by
        ) VALUES (
            OLD.id, OLD.employee_id, 'DELETE',
            OLD.start_date, OLD.end_date, OLD.status, OLD.notes,
            OLD.updated_by
        );
        RETURN OLD;
    END IF;

    RETURN NULL;
END;
$$ LANGUAGE plpgsql;

-- ============================================
-- 6. Crear Trigger en t_vacation
-- ============================================
DROP TRIGGER IF EXISTS trg_vacation_audit ON t_vacation;
CREATE TRIGGER trg_vacation_audit
    AFTER INSERT OR UPDATE OR DELETE ON t_vacation
    FOR EACH ROW
    EXECUTE FUNCTION audit_vacation_changes();

-- ============================================
-- 7. Vista para consultar historial de cambios
-- ============================================
CREATE OR REPLACE VIEW v_attendance_history AS
SELECT
    a.id,
    a.attendance_id,
    p.f_employee as employee_name,
    a.attendance_date,
    a.action,
    a.old_status,
    a.new_status,
    CASE
        WHEN a.action = 'INSERT' THEN 'Registro creado: ' || a.new_status
        WHEN a.action = 'UPDATE' THEN 'Cambio de ' || COALESCE(a.old_status, 'N/A') || ' a ' || a.new_status
        WHEN a.action = 'DELETE' THEN 'Registro eliminado: ' || a.old_status
    END as change_description,
    u.username as changed_by_user,
    a.changed_at
FROM t_attendance_audit a
LEFT JOIN t_payroll p ON a.employee_id = p.f_payroll
LEFT JOIN users u ON a.changed_by = u.id
ORDER BY a.changed_at DESC;

COMMENT ON VIEW v_attendance_history IS 'Vista legible del historial de cambios de asistencia';

-- ============================================
-- 8. Verificar creación
-- ============================================
SELECT
    'Tablas de auditoría creadas' as status,
    (SELECT COUNT(*) FROM information_schema.tables WHERE table_name LIKE 't_%_audit') as audit_tables,
    (SELECT COUNT(*) FROM information_schema.triggers WHERE trigger_name LIKE 'trg_%_audit') as triggers;
