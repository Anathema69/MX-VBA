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
    v_version       VARCHAR := '2.0.3';
    v_created_by    VARCHAR := 'Zuri Dev';
    v_file_size_mb  NUMERIC := 49.67;
    v_is_mandatory  BOOLEAN := true;   -- OBLIGATORIO
    v_min_version   VARCHAR := NULL;   -- NULL = cualquier versión puede actualizar

    -- ┌────────────────────────────────────────────────────────┐
    -- │                   RELEASE NOTES                        │
    -- └────────────────────────────────────────────────────────┘
    v_release_notes TEXT := 'Versión 2.0.3 - Triggers de Gastos Operativos, Comisión de Vendedor y Mejoras UI

MÓDULO ÓRDENES - Requerimientos 1-5:
- Filtros dinámicos por Año y Mes en listado de órdenes (basado en f_podate)
- Comisión de vendedor integrada en gastos operativos con preview en tiempo real
- Preview desglosado (Base + Comisión + Total) para nuevo gasto y edición inline
- Auto-commit de edición inline al presionar GUARDAR CAMBIOS
- Rol "proyectos" con mismos permisos que coordinación

ARQUITECTURA BD - GASTOS OPERATIVOS:
- Nueva columna f_commission_rate en order_gastos_operativos (snapshot del % comisión)
- Nuevo trigger trg_recalcular_gasto_operativo: calcula automáticamente t_order.gasto_operativo
- Nuevo trigger trg_sync_commission_rate: propaga cambios de comisión a gastos existentes
- Eliminado cálculo en C# (RecalcularGastoOperativo), reemplazado por trigger BD
- Monto almacena valor BASE, comisión se aplica en fórmula del trigger

ACCESO ADMINISTRACIÓN:
- Rol administración ahora ve las 3 columnas de gastos (operativo, indirecto, material)
- Acceso completo a edición de gastos en ventana de edición de orden

MEJORAS UI:
- Ventana edición redimensionable (800x680, CanResizeWithGrip)
- Removido botón de configuración (tuerca) del menú principal
- Removido botón de exportar de gestión de órdenes
- Coordinación/Proyectos: oculta columna vendedor y botón exportar
- Eliminado MessageBox de confirmación al cerrar sesión

PORTAL VENDEDORES:
- Descripción de orden visible en dashboard del vendedor
- Botón de cerrar sesión agregado

CORRECCIONES:
- Fix: doble-comisión al refrescar grilla (removidas propiedades calculadas)
- Fix: gastos no cargaban para perfil administración (3 condiciones restringidas)

DOCUMENTACIÓN:
- Documentación completa de triggers y funciones BD actualizadas
- Esquema BD actualizado con nuevas tablas y columnas

ACTUALIZACIÓN OBLIGATORIA - Cambios en arquitectura de gastos operativos';

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
            "Filtros dinámicos por Año y Mes en listado de órdenes",
            "Comisión de vendedor en gastos operativos con preview en tiempo real",
            "Trigger trg_recalcular_gasto_operativo para cálculo automático en BD",
            "Trigger trg_sync_commission_rate para propagación de cambios de comisión",
            "Columna f_commission_rate en order_gastos_operativos",
            "Acceso de administración a 3 columnas de gastos",
            "Rol proyectos con permisos de coordinación",
            "Descripción de orden en dashboard de vendedor",
            "Botón cerrar sesión en portal de vendedores"
        ],
        "Fixed": [
            "Doble-comisión al refrescar grilla de órdenes",
            "Gastos no cargaban para perfil administración",
            "Auto-commit de edición inline pendiente al guardar"
        ],
        "Improved": [
            "Ventana de edición redimensionable",
            "Documentación BD completa con triggers y funciones",
            "Arquitectura: cálculo de gastos movido de C# a trigger BD"
        ],
        "Removed": [
            "Botón de configuración (tuerca) del menú principal",
            "Botón de exportar de gestión de órdenes",
            "MessageBox de confirmación al cerrar sesión",
            "RecalcularGastoOperativo() de OrderService.cs"
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
