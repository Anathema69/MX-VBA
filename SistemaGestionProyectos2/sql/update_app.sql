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
    v_version       VARCHAR := '2.0.2';
    v_created_by    VARCHAR := 'Zuri Dev';
    v_file_size_mb  NUMERIC := 49.66;
    v_is_mandatory  BOOLEAN := true;   -- OBLIGATORIO
    v_min_version   VARCHAR := NULL;   -- NULL = cualquier versión puede actualizar

    -- ┌────────────────────────────────────────────────────────┐
    -- │                   RELEASE NOTES                        │
    -- └────────────────────────────────────────────────────────┘
    v_release_notes TEXT := 'Versión 2.0.2 - Nueva Fórmula de Utilidad y Mejoras UX

MÓDULO BALANCE - Nueva Fórmula de Utilidad:
- NUEVA FÓRMULA: Utilidad = Ventas Totales - (Gastos Fijos + Gastos Variables + Gasto Operativo + Gasto Indirecto)
- Se EXCLUYEN Nómina y Horas Extra del cálculo de utilidad
- Nómina y Horas Extra se mantienen visibles para referencia
- Formato decimal mejorado: todos los valores muestran 2 decimales (#,##0.00)
- Incluye: valores mensuales, totales anuales, ventas, utilidad y KPIs

MENÚ PRINCIPAL:
- Cards de módulos con altura uniforme
- Eliminado texto extra debajo de iconos en BALANCE e INGRESOS PENDIENTES

GESTIÓN DE ÓRDENES Y FACTURACIÓN (commit bc8e589):
- Corregidas transiciones de estado de órdenes
- CheckAndUpdateOrderStatus ya no interfiere con trigger de BD
- Solo maneja transición a COMPLETADA respetando jerarquía
- Trigger BD maneja LIBERADA y CERRADA correctamente

MEJORAS EN FACTURACIÓN:
- Carga optimizada con ejecución paralela (Task.WhenAll)
- Navegación con Tab salta columnas no editables automáticamente
- Clic único para editar celdas (sin necesidad de F2)
- Mensaje de estados corregido

DOCUMENTACIÓN:
- Campos de porcentaje documentados:
  * ProgressPercentage = Avance del TRABAJO (manual)
  * OrderPercentage = Porcentaje de FACTURACIÓN (automático)
- SQL de v_balance_gastos y v_balance_completo sincronizados
- Fórmula de utilidad documentada en BD-IMA

ACTUALIZACIÓN OBLIGATORIA - Cambios en cálculo de utilidad';

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
            "Nueva fórmula de utilidad: Ventas - (Fijos + Variables + Operativo + Indirecto)",
            "Formato decimal C2 en todos los campos monetarios del Balance",
            "Carga paralela en módulo de facturación"
        ],
        "Fixed": [
            "Transiciones de estado de órdenes corregidas",
            "CheckAndUpdateOrderStatus no interfiere con trigger BD",
            "Navegación Tab salta columnas no editables",
            "Altura uniforme en cards del menú principal"
        ],
        "Improved": [
            "Clic único para editar celdas en facturación",
            "Documentación de campos ProgressPercentage vs OrderPercentage",
            "Sincronización de documentación BD con Supabase"
        ],
        "Changed": [
            "Utilidad excluye Nómina y Horas Extra del cálculo",
            "Base de utilidad cambia de Ingresos Esperados a Ventas Totales"
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
