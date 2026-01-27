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
    v_version       VARCHAR := '2.0.1';
    v_created_by    VARCHAR := 'Zuri Dev';
    v_file_size_mb  NUMERIC := 49.66;
    v_is_mandatory  BOOLEAN := true;   -- OBLIGATORIO
    v_min_version   VARCHAR := NULL;   -- NULL = cualquier versión puede actualizar

    -- ┌────────────────────────────────────────────────────────┐
    -- │                   RELEASE NOTES                        │
    -- └────────────────────────────────────────────────────────┘
    v_release_notes TEXT := 'Versión 2.0.1 - Balance con Gastos de Órdenes y Auditoría de Proveedores

MÓDULO BALANCE - Nueva Fórmula de Utilidad:
- Utilidad ahora incluye gastos de órdenes (operativo e indirecto)
- Fórmula: Ingresos - (Nómina + Horas Extra + Gastos Fijos +
  Gastos Variables + Gasto Operativo + Gasto Indirecto)
- 2 nuevas filas: Gasto Operativo y Gasto Indirecto
- Fila "Diferencia" con sombreado destacado
- Leyenda del semáforo de Ventas más visible (junto al título)
- Dots del semáforo más grandes y legibles

CUENTAS POR PAGAR - Auditoría Completa:
- Nueva tabla t_expense_audit para historial de cambios
- Trigger automático registra: INSERT, UPDATE, DELETE, PAID, UNPAID
- Vista v_expense_audit_report para consultas
- Captura usuario que crea (created_by) y modifica (updated_by)
- Columna FECHA PAGO visible en vista de gastos pagados
- Edición de fecha de pago en gastos ya pagados

GESTIÓN DE ÓRDENES Y FACTURAS:
- Correcciones en vista de proveedores pagados
- Ordenamiento mejorado por fecha
- Corrección de propiedad PaidDate

BASE DE DATOS:
- Scripts SQL para recrear vistas de balance
- Consultas de diagnóstico para gastos por orden
- Documentación actualizada de vistas e índices

ACTUALIZACIÓN OBLIGATORIA - Cambios críticos en cálculos de balance';

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
            "Gasto Operativo y Gasto Indirecto en Balance",
            "Tabla t_expense_audit para auditoría de gastos",
            "Vista v_expense_audit_report para reportes",
            "Columna FECHA PAGO en vista de proveedores pagados",
            "Edición de fecha de pago en gastos pagados",
            "Campos created_by y updated_by en t_expense",
            "Scripts SQL para actualizar vistas de balance"
        ],
        "Fixed": [
            "Corrección propiedad PaidDate en SupplierPendingView",
            "Correcciones en gestión de órdenes y facturas",
            "Ordenamiento en vista de proveedores pagados"
        ],
        "Improved": [
            "Fórmula de utilidad incluye gastos de órdenes",
            "Sombreado destacado en fila Diferencia",
            "Leyenda del semáforo más visible (junto al título)",
            "Dots del semáforo más grandes (10px)",
            "Documentación de vistas e índices actualizada"
        ],
        "Breaking": [
            "Requiere ejecutar actualizar_utilidad_balance.sql en BD"
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
