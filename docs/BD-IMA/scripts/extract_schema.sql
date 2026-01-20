-- ============================================================
-- SCRIPT: Extracción completa del esquema de BD
-- Proyecto: IMA Mecatrónica
-- Fecha: Enero 2026
-- ============================================================
-- INSTRUCCIONES:
--   Ejecutar cada sección por separado en Supabase SQL Editor
--   y copiar los resultados para documentar
-- ============================================================


-- ============================================================
-- SECCIÓN 1: LISTA DE TODAS LAS TABLAS
-- ============================================================
-- Ejecutar primero para tener el panorama general

SELECT
    table_name,
    (SELECT COUNT(*) FROM information_schema.columns c
     WHERE c.table_name = t.table_name AND c.table_schema = 'public') as num_columnas,
    pg_size_pretty(pg_total_relation_size(quote_ident(table_name))) as tamano,
    obj_description((quote_ident(table_name))::regclass, 'pg_class') as comentario
FROM information_schema.tables t
WHERE table_schema = 'public'
  AND table_type = 'BASE TABLE'
ORDER BY table_name;


-- ============================================================
-- SECCIÓN 2: DETALLE DE COLUMNAS POR TABLA
-- ============================================================
-- Muestra todas las columnas con sus tipos, nullability, defaults

SELECT
    c.table_name as tabla,
    c.column_name as columna,
    c.data_type as tipo,
    c.character_maximum_length as max_length,
    c.numeric_precision as precision,
    c.is_nullable as nullable,
    c.column_default as default_value,
    CASE
        WHEN pk.column_name IS NOT NULL THEN 'PK'
        ELSE ''
    END as es_pk
FROM information_schema.columns c
LEFT JOIN (
    SELECT ku.table_name, ku.column_name
    FROM information_schema.table_constraints tc
    JOIN information_schema.key_column_usage ku
        ON tc.constraint_name = ku.constraint_name
    WHERE tc.constraint_type = 'PRIMARY KEY'
) pk ON c.table_name = pk.table_name AND c.column_name = pk.column_name
WHERE c.table_schema = 'public'
ORDER BY c.table_name, c.ordinal_position;


-- ============================================================
-- SECCIÓN 3: FOREIGN KEYS (RELACIONES)
-- ============================================================
-- Muestra todas las relaciones entre tablas

SELECT
    tc.table_name as tabla_origen,
    kcu.column_name as columna_origen,
    ccu.table_name as tabla_destino,
    ccu.column_name as columna_destino,
    tc.constraint_name as nombre_fk,
    rc.delete_rule as on_delete,
    rc.update_rule as on_update
FROM information_schema.table_constraints tc
JOIN information_schema.key_column_usage kcu
    ON tc.constraint_name = kcu.constraint_name
JOIN information_schema.constraint_column_usage ccu
    ON ccu.constraint_name = tc.constraint_name
JOIN information_schema.referential_constraints rc
    ON rc.constraint_name = tc.constraint_name
WHERE tc.constraint_type = 'FOREIGN KEY'
  AND tc.table_schema = 'public'
ORDER BY tc.table_name, kcu.column_name;


-- ============================================================
-- SECCIÓN 4: ÍNDICES
-- ============================================================
-- Muestra todos los índices (importante para performance)

SELECT
    tablename as tabla,
    indexname as nombre_indice,
    indexdef as definicion
FROM pg_indexes
WHERE schemaname = 'public'
ORDER BY tablename, indexname;


-- ============================================================
-- SECCIÓN 5: TRIGGERS
-- ============================================================
-- Muestra todos los triggers activos

SELECT
    event_object_table as tabla,
    trigger_name as nombre_trigger,
    event_manipulation as evento,
    action_timing as momento,
    action_statement as accion
FROM information_schema.triggers
WHERE trigger_schema = 'public'
ORDER BY event_object_table, trigger_name;


-- ============================================================
-- SECCIÓN 6: FUNCIONES PERSONALIZADAS
-- ============================================================
-- Muestra funciones creadas (no las del sistema)

SELECT
    p.proname as nombre_funcion,
    pg_get_function_arguments(p.oid) as argumentos,
    pg_get_function_result(p.oid) as retorna,
    CASE p.provolatile
        WHEN 'i' THEN 'IMMUTABLE'
        WHEN 's' THEN 'STABLE'
        WHEN 'v' THEN 'VOLATILE'
    END as volatilidad,
    obj_description(p.oid, 'pg_proc') as comentario
FROM pg_proc p
JOIN pg_namespace n ON p.pronamespace = n.oid
WHERE n.nspname = 'public'
  AND p.prokind = 'f'  -- solo funciones, no procedimientos
ORDER BY p.proname;


-- ============================================================
-- SECCIÓN 7: CÓDIGO FUENTE DE FUNCIONES
-- ============================================================
-- Para ver el código de cada función

SELECT
    p.proname as nombre_funcion,
    pg_get_functiondef(p.oid) as codigo_fuente
FROM pg_proc p
JOIN pg_namespace n ON p.pronamespace = n.oid
WHERE n.nspname = 'public'
  AND p.prokind = 'f'
ORDER BY p.proname;


-- ============================================================
-- SECCIÓN 8: VISTAS
-- ============================================================
-- Muestra vistas de BD (si existen)

SELECT
    table_name as nombre_vista,
    view_definition as definicion
FROM information_schema.views
WHERE table_schema = 'public'
ORDER BY table_name;


-- ============================================================
-- SECCIÓN 9: ENUMS Y TIPOS PERSONALIZADOS
-- ============================================================
-- Muestra tipos enum si existen

SELECT
    t.typname as nombre_tipo,
    e.enumlabel as valor
FROM pg_type t
JOIN pg_enum e ON t.oid = e.enumtypid
JOIN pg_namespace n ON t.typnamespace = n.oid
WHERE n.nspname = 'public'
ORDER BY t.typname, e.enumsortorder;


-- ============================================================
-- SECCIÓN 10: CONSTRAINTS (CHECK, UNIQUE)
-- ============================================================
-- Restricciones adicionales

SELECT
    tc.table_name as tabla,
    tc.constraint_name as nombre,
    tc.constraint_type as tipo,
    cc.check_clause as condicion
FROM information_schema.table_constraints tc
LEFT JOIN information_schema.check_constraints cc
    ON tc.constraint_name = cc.constraint_name
WHERE tc.table_schema = 'public'
  AND tc.constraint_type IN ('CHECK', 'UNIQUE')
ORDER BY tc.table_name, tc.constraint_type;


-- ============================================================
-- SECCIÓN 11: CONTEO DE REGISTROS POR TABLA
-- ============================================================
-- Para tener idea del volumen de datos (ejecutar con cuidado en prod)

SELECT
    schemaname as esquema,
    relname as tabla,
    n_live_tup as registros_aprox
FROM pg_stat_user_tables
WHERE schemaname = 'public'
ORDER BY n_live_tup DESC;


-- ============================================================
-- SECCIÓN 12: SECUENCIAS (para IDs auto-increment)
-- ============================================================

SELECT
    sequence_name,
    start_value,
    increment,
    maximum_value
FROM information_schema.sequences
WHERE sequence_schema = 'public'
ORDER BY sequence_name;
