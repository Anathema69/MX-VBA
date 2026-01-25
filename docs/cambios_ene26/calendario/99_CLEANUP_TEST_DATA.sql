-- ============================================
-- SCRIPT DE LIMPIEZA DE DATOS DE PRUEBA
-- Módulo: Calendario de Asistencia
-- Fecha: 25/01/2026
-- ============================================
-- ADVERTENCIA: Este script eliminará TODOS los registros
-- de asistencia, auditoría y vacaciones.
-- Solo ejecutar si los datos son de prueba.
-- ============================================

BEGIN;

-- 1. Mostrar conteo actual antes de eliminar
DO $$
DECLARE
    v_attendance_count INTEGER;
    v_audit_count INTEGER;
    v_vacation_count INTEGER;
BEGIN
    SELECT COUNT(*) INTO v_attendance_count FROM t_attendance;
    SELECT COUNT(*) INTO v_vacation_count FROM t_vacation;

    -- Verificar si existe la tabla de auditoría
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 't_attendance_audit') THEN
        EXECUTE 'SELECT COUNT(*) FROM t_attendance_audit' INTO v_audit_count;
    ELSE
        v_audit_count := 0;
    END IF;

    RAISE NOTICE '===========================================';
    RAISE NOTICE 'REGISTROS A ELIMINAR:';
    RAISE NOTICE '  - t_attendance: % registros', v_attendance_count;
    RAISE NOTICE '  - t_attendance_audit: % registros', v_audit_count;
    RAISE NOTICE '  - t_vacation: % registros', v_vacation_count;
    RAISE NOTICE '===========================================';
END $$;

-- 2. Eliminar registros de auditoría (primero por FK)
DELETE FROM t_attendance_audit;
RAISE NOTICE 'Tabla t_attendance_audit limpiada';

-- 3. Eliminar registros de asistencia
DELETE FROM t_attendance;
RAISE NOTICE 'Tabla t_attendance limpiada';

-- 4. Eliminar registros de vacaciones (opcional, descomentar si deseas)
-- DELETE FROM t_vacation;
-- RAISE NOTICE 'Tabla t_vacation limpiada';

-- 5. Resetear secuencias (IDs volverán a empezar desde 1)
ALTER SEQUENCE IF EXISTS t_attendance_id_seq RESTART WITH 1;
ALTER SEQUENCE IF EXISTS t_attendance_audit_id_seq RESTART WITH 1;
-- ALTER SEQUENCE IF EXISTS t_vacation_id_seq RESTART WITH 1;

-- 6. Mostrar conteo después de limpiar
DO $$
DECLARE
    v_attendance_count INTEGER;
    v_audit_count INTEGER;
    v_vacation_count INTEGER;
BEGIN
    SELECT COUNT(*) INTO v_attendance_count FROM t_attendance;
    SELECT COUNT(*) INTO v_vacation_count FROM t_vacation;

    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 't_attendance_audit') THEN
        EXECUTE 'SELECT COUNT(*) FROM t_attendance_audit' INTO v_audit_count;
    ELSE
        v_audit_count := 0;
    END IF;

    RAISE NOTICE '';
    RAISE NOTICE '===========================================';
    RAISE NOTICE 'LIMPIEZA COMPLETADA:';
    RAISE NOTICE '  - t_attendance: % registros', v_attendance_count;
    RAISE NOTICE '  - t_attendance_audit: % registros', v_audit_count;
    RAISE NOTICE '  - t_vacation: % registros', v_vacation_count;
    RAISE NOTICE '===========================================';
    RAISE NOTICE 'Secuencias reseteadas a 1';
    RAISE NOTICE 'Base de datos lista para datos reales';
    RAISE NOTICE '===========================================';
END $$;

COMMIT;

-- ============================================
-- SCRIPT ALTERNATIVO: Solo eliminar rango de fechas específico
-- Descomentar y ajustar fechas si solo quieres limpiar ciertos días
-- ============================================
/*
DELETE FROM t_attendance_audit
WHERE attendance_id IN (
    SELECT id FROM t_attendance
    WHERE attendance_date BETWEEN '2026-01-01' AND '2026-01-31'
);

DELETE FROM t_attendance
WHERE attendance_date BETWEEN '2026-01-01' AND '2026-01-31';
*/
