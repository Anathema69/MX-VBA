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
    v_version       VARCHAR := '1.0.11';
    v_created_by    VARCHAR := 'Zuri Dev';
    v_file_size_mb  NUMERIC := 49.52;
    v_is_mandatory  BOOLEAN := false;
    v_min_version   VARCHAR := NULL;  -- NULL = cualquier versión puede actualizar

    -- ┌────────────────────────────────────────────────────────┐
    -- │                   RELEASE NOTES                        │
    -- └────────────────────────────────────────────────────────┘
    v_release_notes TEXT := 'Versión 1.0.11 - Nueva Vista Cuentas por Pagar

NUEVA VISTA - CUENTAS POR PAGAR (PORTAL PROVEEDORES):
- Vista pivoteada por proveedor con tarjetas
- Muestra total pendiente por cada proveedor
- Indicadores de estado: VENCIDO (rojo), POR VENCER (amarillo), AL CORRIENTE (verde)
- Click en proveedor muestra detalle de todos sus gastos pendientes
- Ordenamiento por mayor deuda de crédito
- Filtros por estado y búsqueda por nombre
- Tarjetas de resumen con totales

FIX - REMOCIÓN DE VENDEDOR DE ÓRDENES:
- Corregido error de FK constraint al remover vendedor
- Nuevo trigger registra la remoción en historial antes de eliminar
- Campo is_vendor_removal para identificar remociones
- FK cambiada a ON DELETE SET NULL para preservar auditoría

IMPORTANTE: Ejecutar script SQL fix_commission_history_fk.sql en la BD';

    -- ════════════════════════════════════════════════════════
    -- NO MODIFICAR DEBAJO DE ESTA LÍNEA
    -- ════════════════════════════════════════════════════════
    v_download_url TEXT;
    v_changelog JSONB;
BEGIN
    -- Construir URL del instalador (patrón estándar)
    v_download_url := 'https://wjozxqldvypdtfmkamud.supabase.co/storage/v1/object/public/app-installers/releases/v'
                      || v_version || '/SistemaGestionProyectos-v' || v_version || '-Setup.exe';

    -- Changelog estructurado (opcional, para futuras implementaciones)
    v_changelog := '{
        "Added": [
            "Nueva vista Cuentas por Pagar pivoteada por proveedor",
            "Tarjetas con indicadores de estado por proveedor",
            "Vista de detalle de gastos por proveedor",
            "Campo is_vendor_removal en historial de comisiones",
            "Trigger trg_before_commission_delete para auditoría"
        ],
        "Fixed": [
            "Error FK constraint al remover vendedor de orden",
            "Historial de comisiones ahora preserva auditoría de remociones"
        ],
        "Improved": [
            "Portal de Proveedores ahora muestra vista pivoteada",
            "Mejor experiencia de navegación en cuentas por pagar"
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
