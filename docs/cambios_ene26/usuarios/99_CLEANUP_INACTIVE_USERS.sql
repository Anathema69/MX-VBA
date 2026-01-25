-- ============================================
-- LIMPIEZA DE USUARIOS INACTIVOS
-- Fecha: 25/01/2026
-- ============================================
-- IMPORTANTE: Ejecutar primero el DIAGNÓSTICO (Paso 1)
-- para ver qué registros serían afectados antes de eliminar.
-- ============================================

-- ============================================
-- PASO 1: DIAGNÓSTICO - Ver usuarios inactivos y sus dependencias
-- ============================================

-- 1.1 Listar usuarios inactivos
SELECT
    id,
    username,
    full_name,
    email,
    role,
    created_at,
    last_login
FROM users
WHERE is_active = FALSE
ORDER BY created_at DESC;

-- 1.2 Verificar dependencias en tablas de auditoría
SELECT 'audit_log' as tabla, COUNT(*) as registros
FROM audit_log WHERE user_id IN (SELECT id FROM users WHERE is_active = FALSE)
UNION ALL
SELECT 'invoice_audit', COUNT(*)
FROM invoice_audit WHERE user_id IN (SELECT id FROM users WHERE is_active = FALSE)
UNION ALL
SELECT 'order_history', COUNT(*)
FROM order_history WHERE user_id IN (SELECT id FROM users WHERE is_active = FALSE);

-- 1.3 Verificar dependencias en tablas principales (created_by, updated_by)
SELECT 't_vendor (f_user_id)' as tabla, COUNT(*) as registros
FROM t_vendor WHERE f_user_id IN (SELECT id FROM users WHERE is_active = FALSE)
UNION ALL
SELECT 't_client (created_by)', COUNT(*)
FROM t_client WHERE created_by IN (SELECT id FROM users WHERE is_active = FALSE)
UNION ALL
SELECT 't_client (updated_by)', COUNT(*)
FROM t_client WHERE updated_by IN (SELECT id FROM users WHERE is_active = FALSE)
UNION ALL
SELECT 't_order (created_by)', COUNT(*)
FROM t_order WHERE created_by IN (SELECT id FROM users WHERE is_active = FALSE)
UNION ALL
SELECT 't_order (updated_by)', COUNT(*)
FROM t_order WHERE updated_by IN (SELECT id FROM users WHERE is_active = FALSE)
UNION ALL
SELECT 't_invoice (created_by)', COUNT(*)
FROM t_invoice WHERE created_by IN (SELECT id FROM users WHERE is_active = FALSE)
UNION ALL
SELECT 't_expense (created_by)', COUNT(*)
FROM t_expense WHERE created_by IN (SELECT id FROM users WHERE is_active = FALSE)
UNION ALL
SELECT 't_balance_adjustments', COUNT(*)
FROM t_balance_adjustments WHERE created_by IN (SELECT id FROM users WHERE is_active = FALSE);

-- 1.4 Verificar tablas de asistencia/vacaciones
SELECT 't_attendance (created_by)' as tabla, COUNT(*) as registros
FROM t_attendance WHERE created_by IN (SELECT id FROM users WHERE is_active = FALSE)
UNION ALL
SELECT 't_attendance (updated_by)', COUNT(*)
FROM t_attendance WHERE updated_by IN (SELECT id FROM users WHERE is_active = FALSE)
UNION ALL
SELECT 't_vacation (created_by)', COUNT(*)
FROM t_vacation WHERE created_by IN (SELECT id FROM users WHERE is_active = FALSE)
UNION ALL
SELECT 't_vacation (approved_by)', COUNT(*)
FROM t_vacation WHERE approved_by IN (SELECT id FROM users WHERE is_active = FALSE)
UNION ALL
SELECT 't_attendance_audit (changed_by)', COUNT(*)
FROM t_attendance_audit WHERE changed_by IN (SELECT id FROM users WHERE is_active = FALSE);

-- ============================================
-- PASO 2: RESUMEN EJECUTIVO
-- ============================================
DO $$
DECLARE
    v_inactive_count INTEGER;
    v_audit_count INTEGER;
    v_data_count INTEGER;
BEGIN
    -- Contar usuarios inactivos
    SELECT COUNT(*) INTO v_inactive_count FROM users WHERE is_active = FALSE;

    -- Contar registros de auditoría relacionados
    SELECT
        COALESCE((SELECT COUNT(*) FROM audit_log WHERE user_id IN (SELECT id FROM users WHERE is_active = FALSE)), 0) +
        COALESCE((SELECT COUNT(*) FROM invoice_audit WHERE user_id IN (SELECT id FROM users WHERE is_active = FALSE)), 0) +
        COALESCE((SELECT COUNT(*) FROM order_history WHERE user_id IN (SELECT id FROM users WHERE is_active = FALSE)), 0)
    INTO v_audit_count;

    RAISE NOTICE '';
    RAISE NOTICE '==========================================';
    RAISE NOTICE 'RESUMEN DE DIAGNÓSTICO';
    RAISE NOTICE '==========================================';
    RAISE NOTICE 'Usuarios inactivos: %', v_inactive_count;
    RAISE NOTICE 'Registros de auditoría relacionados: %', v_audit_count;
    RAISE NOTICE '';

    IF v_audit_count > 0 THEN
        RAISE NOTICE '⚠️  ADVERTENCIA: Hay registros de auditoría vinculados.';
        RAISE NOTICE '    Si eliminas estos usuarios, los registros de auditoría';
        RAISE NOTICE '    perderán la referencia al usuario que hizo el cambio.';
        RAISE NOTICE '';
        RAISE NOTICE '    OPCIONES:';
        RAISE NOTICE '    1. Usar SET NULL en los FK antes de eliminar';
        RAISE NOTICE '    2. Mantener usuarios inactivos (recomendado para auditoría)';
        RAISE NOTICE '    3. Eliminar también los registros de auditoría (NO recomendado)';
    ELSE
        RAISE NOTICE '✓ No hay registros de auditoría vinculados.';
        RAISE NOTICE '  Es seguro eliminar los usuarios inactivos.';
    END IF;

    RAISE NOTICE '==========================================';
END $$;

-- ============================================
-- PASO 3: ELIMINACIÓN (SOLO SI EL DIAGNÓSTICO LO PERMITE)
-- ============================================
-- ⚠️ DESCOMENTAR SOLO DESPUÉS DE REVISAR EL DIAGNÓSTICO

/*
-- Opción A: Eliminar usuarios inactivos SIN dependencias
-- (Solo funciona si no hay registros vinculados)
DELETE FROM users
WHERE is_active = FALSE
AND id NOT IN (SELECT DISTINCT user_id FROM audit_log WHERE user_id IS NOT NULL)
AND id NOT IN (SELECT DISTINCT user_id FROM invoice_audit WHERE user_id IS NOT NULL)
AND id NOT IN (SELECT DISTINCT user_id FROM order_history WHERE user_id IS NOT NULL)
AND id NOT IN (SELECT DISTINCT created_by FROM t_order WHERE created_by IS NOT NULL)
AND id NOT IN (SELECT DISTINCT updated_by FROM t_order WHERE updated_by IS NOT NULL)
AND id NOT IN (SELECT DISTINCT created_by FROM t_client WHERE created_by IS NOT NULL)
AND id NOT IN (SELECT DISTINCT created_by FROM t_invoice WHERE created_by IS NOT NULL)
AND id NOT IN (SELECT DISTINCT created_by FROM t_expense WHERE created_by IS NOT NULL);
*/

/*
-- Opción B: Actualizar FK a NULL y luego eliminar
-- (Para eliminar usuarios aunque tengan registros vinculados)
BEGIN;
    -- Actualizar referencias a NULL (auditoría)
    UPDATE audit_log SET user_id = NULL
    WHERE user_id IN (SELECT id FROM users WHERE is_active = FALSE);

    UPDATE invoice_audit SET user_id = NULL
    WHERE user_id IN (SELECT id FROM users WHERE is_active = FALSE);

    UPDATE order_history SET user_id = 1 -- Asignar a admin
    WHERE user_id IN (SELECT id FROM users WHERE is_active = FALSE);

    -- Ahora eliminar usuarios
    DELETE FROM users WHERE is_active = FALSE;

    -- Verificar
    SELECT 'Usuarios eliminados' as resultado, COUNT(*) as total
    FROM users WHERE is_active = FALSE;
COMMIT;
*/

/*
-- Opción C: Eliminar usuarios específicos por ID
-- (La más segura - eliminar uno por uno)
DELETE FROM users WHERE id = 123; -- Reemplazar con ID real
*/

-- ============================================
-- SCRIPT RÁPIDO: Solo ver usuarios inactivos
-- ============================================
-- SELECT id, username, full_name, role, created_at
-- FROM users WHERE is_active = FALSE;

-- ============================================
-- DIAGNÓSTICO RÁPIDO: Ver usuarios inactivos vinculados a vendedores
-- ============================================
/*
SELECT
    u.id as user_id,
    u.username,
    u.full_name,
    u.is_active,
    v.f_vendor as vendor_id,
    v.f_vendorname as vendor_name,
    v.f_active as vendor_active
FROM users u
INNER JOIN t_vendor v ON v.f_user_id = u.id
WHERE u.is_active = FALSE;
*/

-- ============================================
-- OPCIÓN SEGURA: Eliminar usuarios inactivos que NO son vendedores
-- ============================================
/*
DELETE FROM users
WHERE is_active = FALSE
AND id NOT IN (SELECT f_user_id FROM t_vendor WHERE f_user_id IS NOT NULL);
*/

-- ============================================
-- OPCIÓN: Desvincular vendedor antes de eliminar usuario
-- (Si el vendedor también está inactivo)
-- ============================================
/*
BEGIN;
    -- Primero, desactivar vendedores cuyos usuarios están inactivos
    UPDATE t_vendor SET f_active = FALSE
    WHERE f_user_id IN (SELECT id FROM users WHERE is_active = FALSE);

    -- Desvincular el usuario del vendedor (poner NULL)
    UPDATE t_vendor SET f_user_id = NULL
    WHERE f_user_id IN (SELECT id FROM users WHERE is_active = FALSE);

    -- Ahora eliminar usuarios inactivos
    DELETE FROM users WHERE is_active = FALSE;
COMMIT;
*/
