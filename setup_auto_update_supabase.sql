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
    v_version       VARCHAR := '1.0.10';
    v_created_by    VARCHAR := 'Zuri Dev';
    v_file_size_mb  NUMERIC := 49.51;
    v_is_mandatory  BOOLEAN := false;
    v_min_version   VARCHAR := NULL;  -- NULL = cualquier versión puede actualizar

    -- ┌────────────────────────────────────────────────────────┐
    -- │                   RELEASE NOTES                        │
    -- └────────────────────────────────────────────────────────┘
    v_release_notes TEXT := 'Versión 1.0.10 - Correcciones UI/UX

BALANCE:
- HORAS EXTRA: Ceros centrados verticalmente en celdas
- TOTAL GASTOS: Color más claro (rosa suave)

PORTAL DE VENDEDORES - Edición de Comisiones:
- Editar tasa de comisión por orden específica
- Permitido en estados draft y pending (no en paid)
- Ícono de lápiz indica campo editable
- Doble clic para editar, Enter para guardar
- Auditoría: todos los cambios quedan registrados
- Sincronización automática con t_order
- UI optimista: respuesta inmediata

PORTAL DE PROVEEDORES - Nuevo Gasto:
- Campo Total: auto-selección al enfocar
- Punto decimal funciona correctamente
- Auto-formato a 2 decimales

PORTAL DE VENDEDORES - Nuevo Vendedor:
- Contraseña OBLIGATORIA para crear vendedor
- Indicación visual en rojo
- Validación doble de seguridad
- Toast notification en lugar de MessageBox';

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
            "Edición de tasa de comisión por orden",
            "Auditoría de cambios en t_commission_rate_history",
            "Ícono de lápiz en campos editables",
            "Auto-selección en campo Total de gastos"
        ],
        "Fixed": [
            "Ceros descentrados en HORAS EXTRA",
            "Punto decimal no funcionaba en Total",
            "Vendedores creados sin contraseña"
        ],
        "Improved": [
            "Color más claro en Total Gastos",
            "UI optimista en edición de comisiones",
            "Contraseña obligatoria con indicación visual",
            "Toast notifications en lugar de MessageBox"
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
