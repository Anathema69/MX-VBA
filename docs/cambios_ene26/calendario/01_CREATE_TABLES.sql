-- ============================================
-- Módulo Calendario de Personal
-- Script 01: Creación de Tablas Principales
-- Fecha: 2026-01-25
-- ============================================

-- ============================================
-- 1. Tabla de Asistencia Diaria
-- ============================================
CREATE TABLE IF NOT EXISTS t_attendance (
    id SERIAL PRIMARY KEY,
    employee_id INTEGER NOT NULL REFERENCES t_payroll(f_payroll) ON DELETE CASCADE,
    attendance_date DATE NOT NULL,
    status VARCHAR(20) NOT NULL CHECK (status IN ('ASISTENCIA', 'RETARDO', 'FALTA', 'VACACIONES', 'FERIADO', 'DESCANSO')),
    check_in_time TIME,                    -- Hora de entrada
    check_out_time TIME,                   -- Hora de salida
    late_minutes INTEGER DEFAULT 0,        -- Minutos de retardo
    notes TEXT,                            -- Observaciones
    is_justified BOOLEAN DEFAULT FALSE,    -- Falta/Retardo justificado
    justification TEXT,                    -- Motivo de justificación
    created_by INTEGER,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_by INTEGER,
    updated_at TIMESTAMP,
    UNIQUE(employee_id, attendance_date)   -- Un registro por empleado por día
);

-- Índices para consultas frecuentes
CREATE INDEX IF NOT EXISTS idx_attendance_date ON t_attendance(attendance_date);
CREATE INDEX IF NOT EXISTS idx_attendance_employee ON t_attendance(employee_id);
CREATE INDEX IF NOT EXISTS idx_attendance_status ON t_attendance(status);
-- Nota: No se usa índice funcional con DATE_TRUNC porque no es IMMUTABLE en PostgreSQL
-- Las consultas por mes usarán idx_attendance_date con filtros de rango

COMMENT ON TABLE t_attendance IS 'Registro diario de asistencia del personal';
COMMENT ON COLUMN t_attendance.status IS 'Estado: ASISTENCIA, RETARDO, FALTA, VACACIONES, FERIADO, DESCANSO';
COMMENT ON COLUMN t_attendance.late_minutes IS 'Minutos de retardo (solo aplica si status=RETARDO)';
COMMENT ON COLUMN t_attendance.is_justified IS 'Indica si una falta o retardo está justificado';

-- ============================================
-- 2. Tabla de Vacaciones (Períodos)
-- ============================================
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

CREATE INDEX IF NOT EXISTS idx_vacation_dates ON t_vacation(start_date, end_date);
CREATE INDEX IF NOT EXISTS idx_vacation_employee ON t_vacation(employee_id);
CREATE INDEX IF NOT EXISTS idx_vacation_status ON t_vacation(status);

COMMENT ON TABLE t_vacation IS 'Períodos de vacaciones del personal';
COMMENT ON COLUMN t_vacation.total_days IS 'Días totales de vacaciones (calculado automáticamente)';

-- ============================================
-- 3. Verificar creación
-- ============================================
SELECT
    table_name,
    (SELECT COUNT(*) FROM information_schema.columns WHERE table_name = t.table_name) as columns
FROM information_schema.tables t
WHERE table_name IN ('t_attendance', 't_vacation')
ORDER BY table_name;
