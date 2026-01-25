-- ============================================
-- FIX: Secuencia de t_attendance_audit desincronizada
-- Fecha: 25/01/2026
-- ============================================
-- Este error ocurre cuando:
-- 1. Se insertaron registros manualmente con IDs específicos
-- 2. Se restauró un backup
-- 3. Se ejecutó el script de limpieza pero quedaron registros
--
-- Error típico:
-- "duplicate key value violates unique constraint t_attendance_audit_pkey"
-- "Key (id)=(35) already exists"
-- ============================================

-- Paso 1: Ver el estado actual
DO $$
DECLARE
    v_max_id INTEGER;
    v_current_seq INTEGER;
BEGIN
    -- Obtener el máximo ID actual en la tabla
    SELECT COALESCE(MAX(id), 0) INTO v_max_id FROM t_attendance_audit;

    -- Obtener el valor actual de la secuencia
    SELECT last_value INTO v_current_seq FROM t_attendance_audit_id_seq;

    RAISE NOTICE '===========================================';
    RAISE NOTICE 'DIAGNÓSTICO DE SECUENCIA:';
    RAISE NOTICE '  - Máximo ID en tabla: %', v_max_id;
    RAISE NOTICE '  - Valor actual secuencia: %', v_current_seq;

    IF v_current_seq <= v_max_id THEN
        RAISE NOTICE '  - ESTADO: ❌ DESINCRONIZADA (secuencia <= max_id)';
    ELSE
        RAISE NOTICE '  - ESTADO: ✓ OK';
    END IF;
    RAISE NOTICE '===========================================';
END $$;

-- Paso 2: Corregir la secuencia
-- Establecer la secuencia al valor correcto (MAX(id) + 1)
SELECT setval('t_attendance_audit_id_seq', COALESCE((SELECT MAX(id) FROM t_attendance_audit), 0) + 1, false);

-- Paso 3: También corregir la secuencia de t_attendance por si acaso
SELECT setval('t_attendance_id_seq', COALESCE((SELECT MAX(id) FROM t_attendance), 0) + 1, false);

-- Paso 4: Verificar que quedó corregido
DO $$
DECLARE
    v_max_id INTEGER;
    v_new_seq INTEGER;
BEGIN
    SELECT COALESCE(MAX(id), 0) INTO v_max_id FROM t_attendance_audit;
    SELECT last_value INTO v_new_seq FROM t_attendance_audit_id_seq;

    RAISE NOTICE '';
    RAISE NOTICE '===========================================';
    RAISE NOTICE 'RESULTADO:';
    RAISE NOTICE '  - Máximo ID en tabla: %', v_max_id;
    RAISE NOTICE '  - Nuevo valor secuencia: %', v_new_seq;
    RAISE NOTICE '  - ESTADO: ✓ CORREGIDO';
    RAISE NOTICE '===========================================';
END $$;

-- ============================================
-- SCRIPT RÁPIDO (copiar y pegar en Supabase SQL Editor):
-- ============================================
-- SELECT setval('t_attendance_audit_id_seq', COALESCE((SELECT MAX(id) FROM t_attendance_audit), 0) + 1, false);
-- SELECT setval('t_attendance_id_seq', COALESCE((SELECT MAX(id) FROM t_attendance), 0) + 1, false);
-- ============================================
