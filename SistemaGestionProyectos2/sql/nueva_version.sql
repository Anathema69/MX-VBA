-- ============================================================
-- SCRIPT: Insertar nueva versión en app_versions
-- ============================================================
-- INSTRUCCIONES:
-- 1. Modificar las variables al inicio con la información de la nueva versión
-- 2. Ejecutar en Supabase SQL Editor
-- 3. Subir el instalador al bucket ANTES de ejecutar este script
-- ============================================================

-- ============================================================
-- PASO 1: CONFIGURAR DATOS DE LA NUEVA VERSIÓN
-- ============================================================
-- Modifica estos valores según la nueva versión:

DO $$
DECLARE
    -- === MODIFICAR ESTOS VALORES ===
    v_version VARCHAR := '1.0.9';
    v_release_notes TEXT := 'Versión 1.0.9 - Correcciones de UI

CORRECCIONES:
- Ventana de Ingresos Pendientes ya no cubre la barra de tareas
- Ordenamiento de órdenes de más antiguo a más reciente
- Filtro por defecto en CREADA al abrir Gestión de Órdenes
- Mensaje "Sin registros para: {filtro}" cuando no hay datos

MEJORAS:
- Mejor experiencia de usuario en navegación de órdenes';

    v_created_by VARCHAR := 'Zuri Dev';
    v_file_size_mb NUMERIC := 50.00;
    v_is_mandatory BOOLEAN := false;
    v_min_version VARCHAR := NULL;  -- Versión mínima requerida para actualizar (NULL = cualquiera)

    -- === NO MODIFICAR ABAJO DE ESTA LÍNEA ===
    v_download_url TEXT;
    v_changelog JSONB;
BEGIN
    -- Construir URL del instalador (patrón estándar)
    v_download_url := 'https://wjozxqldvypdtfmkamud.supabase.co/storage/v1/object/public/app-installers/releases/v'
                      || v_version || '/SistemaGestionProyectos-v' || v_version || '-Setup.exe';

    -- Changelog vacío por defecto
    v_changelog := '{"Added": [], "Fixed": [], "Improved": []}'::jsonb;

    -- ============================================================
    -- PASO 2: Marcar versión anterior como NO latest
    -- ============================================================
    UPDATE app_versions
    SET is_latest = false
    WHERE is_latest = true;

    RAISE NOTICE 'Versiones anteriores marcadas como is_latest = false';

    -- ============================================================
    -- PASO 3: Insertar nueva versión
    -- ============================================================
    INSERT INTO app_versions (
        version,
        release_date,
        is_latest,
        is_mandatory,
        download_url,
        file_size_mb,
        release_notes,
        min_version,
        created_by,
        is_active,
        downloads_count,
        changelog
    ) VALUES (
        v_version,
        NOW(),
        true,  -- Esta es la nueva versión latest
        v_is_mandatory,
        v_download_url,
        v_file_size_mb,
        v_release_notes,
        v_min_version,
        v_created_by,
        true,
        0,
        v_changelog
    );

    RAISE NOTICE 'Nueva versión % insertada correctamente', v_version;
    RAISE NOTICE 'URL: %', v_download_url;
END $$;

-- ============================================================
-- VERIFICACIÓN: Mostrar versiones actuales
-- ============================================================
SELECT
    id,
    version,
    is_latest,
    is_active,
    release_date,
    downloads_count
FROM app_versions
ORDER BY id DESC
LIMIT 5;
