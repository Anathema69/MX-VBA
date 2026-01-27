-- ============================================================================
-- SCRIPT DE VERIFICACIÓN - Auditoría de Gastos
-- Sistema de Gestión de Proyectos - IMA Mecatrónica
-- Fecha: 2026-01-27
-- ============================================================================
-- Ejecuta este script para verificar que todo se instaló correctamente
-- ============================================================================

-- 1. VERIFICAR COLUMNA updated_by EN t_expense
-- ============================================================================
SELECT '1. COLUMNA updated_by EN t_expense' AS verificacion;

SELECT
    column_name,
    data_type,
    character_maximum_length,
    is_nullable
FROM information_schema.columns
WHERE table_name = 't_expense'
  AND column_name IN ('updated_by', 'updated_at', 'created_by', 'created_at')
ORDER BY column_name;

-- 2. VERIFICAR TABLA t_expense_audit EXISTE
-- ============================================================================
SELECT '2. TABLA t_expense_audit' AS verificacion;

SELECT
    table_name,
    (SELECT COUNT(*) FROM information_schema.columns WHERE table_name = 't_expense_audit') AS total_columnas
FROM information_schema.tables
WHERE table_name = 't_expense_audit';

-- 3. LISTAR COLUMNAS DE t_expense_audit
-- ============================================================================
SELECT '3. COLUMNAS DE t_expense_audit' AS verificacion;

SELECT
    column_name,
    data_type,
    COALESCE(character_maximum_length::text, numeric_precision::text, '') AS size
FROM information_schema.columns
WHERE table_name = 't_expense_audit'
ORDER BY ordinal_position;

-- 4. VERIFICAR TRIGGERS EN t_expense
-- ============================================================================
SELECT '4. TRIGGERS EN t_expense' AS verificacion;

SELECT
    trigger_name,
    event_manipulation,
    action_timing,
    action_statement
FROM information_schema.triggers
WHERE event_object_table = 't_expense'
ORDER BY trigger_name;

-- 5. VERIFICAR FUNCIONES CREADAS
-- ============================================================================
SELECT '5. FUNCIONES DE AUDITORÍA' AS verificacion;

SELECT
    routine_name,
    routine_type,
    data_type AS return_type
FROM information_schema.routines
WHERE routine_schema = 'public'
  AND routine_name IN (
    'fn_expense_audit',
    'fn_expense_update_timestamp',
    'fn_get_expense_history',
    'fn_expense_audit_summary',
    'fn_expense_audit_suspicious_activity'
  )
ORDER BY routine_name;

-- 6. VERIFICAR VISTA v_expense_audit_report
-- ============================================================================
SELECT '6. VISTA v_expense_audit_report' AS verificacion;

SELECT
    table_name AS view_name,
    (SELECT COUNT(*) FROM information_schema.columns WHERE table_name = 'v_expense_audit_report') AS total_columnas
FROM information_schema.views
WHERE table_name = 'v_expense_audit_report';

-- 7. VERIFICAR ÍNDICES EN t_expense_audit
-- ============================================================================
SELECT '7. ÍNDICES EN t_expense_audit' AS verificacion;

SELECT
    indexname,
    indexdef
FROM pg_indexes
WHERE tablename = 't_expense_audit'
ORDER BY indexname;

-- 8. CONTEO DE REGISTROS ACTUALES
-- ============================================================================
SELECT '8. CONTEO DE REGISTROS' AS verificacion;

SELECT
    't_expense' AS tabla,
    COUNT(*) AS registros
FROM t_expense
UNION ALL
SELECT
    't_expense_audit' AS tabla,
    COUNT(*) AS registros
FROM t_expense_audit;

-- 9. PRUEBA RÁPIDA: Ver si el trigger funciona (solo lectura)
-- ============================================================================
SELECT '9. ÚLTIMOS REGISTROS DE AUDITORÍA (si hay)' AS verificacion;

SELECT
    id,
    expense_id,
    action,
    COALESCE(new_updated_by, new_created_by, '-') AS usuario,
    supplier_name,
    new_total_expense,
    TO_CHAR(changed_at, 'DD/MM/YYYY HH24:MI:SS') AS fecha_cambio
FROM t_expense_audit
ORDER BY changed_at DESC
LIMIT 5;

-- 10. RESUMEN FINAL
-- ============================================================================
SELECT '10. RESUMEN DE VERIFICACIÓN' AS verificacion;

SELECT
    'Columna updated_by en t_expense' AS elemento,
    CASE WHEN EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 't_expense' AND column_name = 'updated_by'
    ) THEN '✅ OK' ELSE '❌ FALTA' END AS estado
UNION ALL
SELECT
    'Tabla t_expense_audit',
    CASE WHEN EXISTS (
        SELECT 1 FROM information_schema.tables
        WHERE table_name = 't_expense_audit'
    ) THEN '✅ OK' ELSE '❌ FALTA' END
UNION ALL
SELECT
    'Trigger trg_expense_audit',
    CASE WHEN EXISTS (
        SELECT 1 FROM information_schema.triggers
        WHERE trigger_name = 'trg_expense_audit'
    ) THEN '✅ OK' ELSE '❌ FALTA' END
UNION ALL
SELECT
    'Trigger trg_expense_update_timestamp',
    CASE WHEN EXISTS (
        SELECT 1 FROM information_schema.triggers
        WHERE trigger_name = 'trg_expense_update_timestamp'
    ) THEN '✅ OK' ELSE '❌ FALTA' END
UNION ALL
SELECT
    'Función fn_expense_audit',
    CASE WHEN EXISTS (
        SELECT 1 FROM information_schema.routines
        WHERE routine_name = 'fn_expense_audit'
    ) THEN '✅ OK' ELSE '❌ FALTA' END
UNION ALL
SELECT
    'Función fn_get_expense_history',
    CASE WHEN EXISTS (
        SELECT 1 FROM information_schema.routines
        WHERE routine_name = 'fn_get_expense_history'
    ) THEN '✅ OK' ELSE '❌ FALTA' END
UNION ALL
SELECT
    'Vista v_expense_audit_report',
    CASE WHEN EXISTS (
        SELECT 1 FROM information_schema.views
        WHERE table_name = 'v_expense_audit_report'
    ) THEN '✅ OK' ELSE '❌ FALTA' END;

-- ============================================================================
-- FIN DE VERIFICACIÓN
-- ============================================================================
