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
    v_version       VARCHAR := '2.0.4';
    v_created_by    VARCHAR := 'Zuri Dev';
    v_file_size_mb  NUMERIC := 49.67;
    v_is_mandatory  BOOLEAN := true;   -- OBLIGATORIO
    v_min_version   VARCHAR := NULL;   -- NULL = cualquier versión puede actualizar

    -- ┌────────────────────────────────────────────────────────┐
    -- │                   RELEASE NOTES                        │
    -- └────────────────────────────────────────────────────────┘
    v_release_notes TEXT := 'Versión 2.0.4 - Corrección fórmula gasto operativo

CORRECCIÓN DE FÓRMULA:
- Fórmula anterior: SUM(monto * (1 + commission_rate/100)) - comisión por gasto individual
- Fórmula nueva: SUM(monto) + SUM(commission_amount) - gastos base + comisión vendedor
- commission_amount proviene de t_vendor_commission_payment (ya existente)

CAMBIOS EN BD:
- Trigger recalcular_gasto_operativo() modificado con nueva fórmula
- Nuevo trigger trg_recalcular_gasto_op_por_comision en t_vendor_commission_payment
- Eliminado trigger trg_sync_commission_rate y función sync_commission_rate_to_gastos
- 21 órdenes recalculadas y verificadas OK

CAMBIOS EN APLICACIÓN:
- Removido CommissionRate de modelo de gastos operativos
- Simplificadas firmas de Add/UpdateGastoOperativo (sin parámetro commissionRate)
- Removido preview desglosado de comisión (Base + Comisión + Total)
- Header cambiado a Subtotal con línea informativa: comisión vendedor | gasto operativo
- Alineación corregida en lista de gastos (altura fija, botones centrados)

ACTUALIZACIÓN OBLIGATORIA - Corrección de cálculo de gastos operativos';

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
        "Fixed": [
            "Fórmula gasto operativo: SUM(monto) + SUM(commission_amount)",
            "Alineación vertical en lista de gastos operativos"
        ],
        "Added": [
            "Trigger trg_recalcular_gasto_op_por_comision en t_vendor_commission_payment",
            "Línea informativa de comisión vendedor en edición de orden"
        ],
        "Improved": [
            "Header Subtotal con info de comisión y gasto operativo total",
            "Firmas simplificadas de Add/UpdateGastoOperativo"
        ],
        "Removed": [
            "Trigger trg_sync_commission_rate y función sync_commission_rate_to_gastos",
            "Preview desglosado de comisión (Base + Comisión + Total)",
            "CommissionRate de modelo OrderGastoOperativoDb"
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
