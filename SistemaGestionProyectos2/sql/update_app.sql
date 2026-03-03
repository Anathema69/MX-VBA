-- ============================================================
-- SCRIPT: Insertar nueva versión en app_versions
-- ============================================================
-- INSTRUCCIONES:
--   1. Subir el instalador a Supabase Storage ANTES de ejecutar
--      Ruta: app-installers/releases/v{VERSION}/SistemaGestionProyectos-v{VERSION}-Setup.exe
--   2. Modificar SOLO la sección "CONFIGURACIÓN" abajo
--   3. Ejecutar en Supabase SQL Editor
--   4. Verificar con el SELECT final
-- ============================================================


-- ============================================================
-- CONFIGURACIÓN DE LA NUEVA VERSIÓN
-- ============================================================
-- >> MODIFICAR SOLO ESTOS VALORES <<

DO $$
DECLARE
    -- ┌────────────────────────────────────────────────────────┐
    -- │                    DATOS BÁSICOS                       │
    -- └────────────────────────────────────────────────────────┘
    v_version       VARCHAR := '2.0.5';
    v_created_by    VARCHAR := 'Zuri Dev';
    v_file_size_mb  NUMERIC := 49.83;
    v_is_mandatory  BOOLEAN := true;   -- OBLIGATORIO
    v_min_version   VARCHAR := NULL;   -- NULL = cualquier versión puede actualizar

    -- ┌────────────────────────────────────────────────────────┐
    -- │                   RELEASE NOTES                        │
    -- └────────────────────────────────────────────────────────┘
    v_release_notes TEXT := 'Versión 2.0.5 - Fase 4 Bloque 1: Ajustes cosméticos + Soporte multi-monitor

AJUSTES COSMÉTICOS (Fase 3 pendientes):
- Centrado de valores en todas las tablas DataGrid (6 vistas corregidas)
- Filtro de Año en Manejo de Órdenes ahora abre con el año actual seleccionado
- Renombrado "Portal del Vendedor" a "Portal Ventas" en toda la aplicación
- Renombrado "Portal de Proveedores" a "Portal Proveedores" en toda la aplicación
- Columnas en Manejo de Órdenes ajustadas para mejor visibilidad, resize manual habilitado
- Botones de regreso estandarizados a "← Volver" en todas las ventanas

SOPORTE MULTI-MONITOR:
- Nuevo WindowHelper con Win32 API (MonitorFromWindow + GetMonitorInfo)
- Las ventanas ahora se adaptan al monitor donde se abren, no solo al primario
- Corregido bug donde ventanas hijas abrían en monitor equivocado
- Eliminado WindowState=Maximized nativo (causaba maximizar en monitor primario)
- 17 ventanas migradas al nuevo sistema de posicionamiento

CORRECCIONES:
- Fix: cerrar sesión ya no interrumpe screen share de Google Meet
- Fix: eliminado MessageBox al cerrar sesión como vendedor
- Fix: barra de tareas ya no queda cubierta en ninguna ventana
- Fix: PendingIncomesDetailView respeta barra de tareas correctamente

ACTUALIZACIÓN OBLIGATORIA - Mejoras de interfaz y soporte multi-monitor';

    -- ════════════════════════════════════════════════════════
    -- NO MODIFICAR DEBAJO DE ESTA LÍNEA
    -- ════════════════════════════════════════════════════════
    v_download_url TEXT;
    v_changelog JSONB;
BEGIN
    -- Construir URL del instalador (patrón estándar)
    v_download_url := 'https://wjozxqldvypdtfmkamud.supabase.co/storage/v1/object/public/app-installers/releases/v'
                      || v_version || '/SistemaGestionProyectos-v' || v_version || '-Setup.exe';

    -- Changelog estructurado
    v_changelog := '{
        "Fixed": [
            "Cerrar sesión ya no interrumpe screen share de Google Meet",
            "Eliminado MessageBox al cerrar sesión como vendedor",
            "Barra de tareas ya no queda cubierta en ninguna ventana",
            "Ventanas hijas ya no abren en monitor equivocado en setup multi-monitor"
        ],
        "Added": [
            "WindowHelper con Win32 API para detección correcta de monitor",
            "Hook SourceInitialized para re-posicionamiento con handle real",
            "Owner establecido en ventanas hijas para herencia de monitor",
            "CanUserResizeColumns en DataGrid de Órdenes"
        ],
        "Improved": [
            "Centrado de valores en 6 DataGrids (columnas numéricas, fechas, estados)",
            "Filtro de Año default al año actual en Manejo de Órdenes",
            "Anchos de columnas optimizados para mejor visibilidad",
            "Botones estandarizados: ← Volver (navegación), Cancelar (diálogos), Cerrar Sesión (logout)"
        ],
        "Changed": [
            "Portal del Vendedor renombrado a Portal Ventas",
            "Portal de Proveedores renombrado a Portal Proveedores",
            "17 ventanas migradas de SystemParameters.WorkArea a WindowHelper multi-monitor",
            "4 ventanas migradas de WindowState=Maximized a MaximizeWithTaskbar()"
        ]
    }'::jsonb;

    -- ════════════════════════════════════════════════════════
    -- PASO 1: Marcar versiones anteriores como NO latest
    -- ════════════════════════════════════════════════════════
    UPDATE app_versions
    SET is_latest = false
    WHERE is_latest = true;

    RAISE NOTICE '✓ Versiones anteriores marcadas como is_latest = false';

    -- ════════════════════════════════════════════════════════
    -- PASO 2: Insertar nueva versión
    -- ════════════════════════════════════════════════════════
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
        true,
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

    RAISE NOTICE '✓ Nueva versión % insertada correctamente', v_version;
    RAISE NOTICE '✓ URL: %', v_download_url;
    RAISE NOTICE '✓ Tamaño: % MB', v_file_size_mb;
END $$;


-- ============================================================
-- VERIFICACIÓN: Mostrar últimas versiones
-- ============================================================
SELECT
    id,
    version,
    is_latest,
    is_active,
    release_date::date as fecha,
    file_size_mb,
    downloads_count
FROM app_versions
ORDER BY id DESC
LIMIT 5;
