-- ============================================================
-- SCRIPT: Insertar nueva version en app_versions
-- ============================================================
-- INSTRUCCIONES:
--   1. Subir el instalador a GitHub Releases ANTES de ejecutar
--      Comando: gh release create v{VERSION} installer.exe --title "v{VERSION}" --notes "..."
--   2. Modificar SOLO la seccion "CONFIGURACION" abajo
--   3. Ejecutar en Supabase SQL Editor
--   4. Verificar con el SELECT final
-- ============================================================


-- ============================================================
-- CONFIGURACION DE LA NUEVA VERSION
-- ============================================================
-- >> MODIFICAR SOLO ESTOS VALORES <<

DO $$
DECLARE
    v_version       VARCHAR := '2.0.8';
    v_created_by    VARCHAR := 'Zuri Dev';
    v_file_size_mb  NUMERIC := 50.5;
    v_is_mandatory  BOOLEAN := true;
    v_min_version   VARCHAR := '2.0.7';

    v_release_notes TEXT := 'Version 2.0.8 - Portal Proveedores + Panel Vendedor + Correcciones

PORTAL PROVEEDORES (SupplierPendingDetailView):
- Fix: Seleccionar proveedor sin gastos pendientes ya no bloquea la fila nueva
- Fix: Orden seleccionada ya no desaparece visualmente al cambiar de campo
- Alineacion corregida en boton Nuevo Gasto

PANEL VENDEDOR (VendorDashboard_V2):
- Boton Liberar Orden ahora cambia el estado de la orden a LIBERADA (2) en la BD
- El cambio queda registrado automaticamente en order_history via trigger
- Comision pasa de draft a pending al liberar
- Boton Subir Factura: unico punto de upload, sin duplicados

MODULO ORDENES (OrdersManagementWindow):
- Boton Actualizar ahora invalida cache de catalogos (vendedores, clientes, estados)
- Fix: vendedor activado manualmente en BD ahora aparece al refrescar sin reiniciar

INFRAESTRUCTURA:
- Nuevo metodo BaseSupabaseService.InvalidateCatalogCaches() para invalidar caches de catalogos
- Logging diagnostico en flujos criticos de creacion de gastos y liberacion de ordenes

ACTUALIZACION OBLIGATORIA';

    v_download_url TEXT;
    v_changelog JSONB;
BEGIN
    v_download_url := 'https://github.com/Anathema69/MX-VBA/releases/download/v'
                      || v_version || '/SistemaGestionProyectos-v' || v_version || '-Setup.exe';

    v_changelog := '{
        "Fixed": [
            "Portal Proveedores: proveedor sin gastos pendientes bloqueaba la fila nueva",
            "Portal Proveedores: orden seleccionada desaparecia al cambiar de campo",
            "Modulo Ordenes: vendedor activado en BD no aparecia al refrescar (cache)",
            "Panel Vendedor: botones duplicados de subir factura"
        ],
        "Added": [
            "Panel Vendedor: Liberar Orden cambia estado de orden a LIBERADA en BD",
            "Panel Vendedor: cambio de estado registrado en order_history via trigger",
            "Infraestructura: InvalidateCatalogCaches() para refrescar catalogos"
        ],
        "Improved": [
            "Modulo Ordenes: boton Actualizar recarga catalogos completos desde BD",
            "Panel Vendedor: unico boton Subir Factura segun contexto (con/sin archivos)",
            "Portal Proveedores: alineacion visual del boton Nuevo Gasto"
        ]
    }'::jsonb;

    UPDATE app_versions SET is_latest = false WHERE is_latest = true;
    RAISE NOTICE 'Versiones anteriores marcadas como is_latest = false';

    INSERT INTO app_versions (
        version, release_date, is_latest, is_mandatory, download_url,
        file_size_mb, release_notes, min_version, created_by,
        is_active, downloads_count, changelog
    ) VALUES (
        v_version, NOW(), true, v_is_mandatory, v_download_url,
        v_file_size_mb, v_release_notes, v_min_version, v_created_by,
        true, 0, v_changelog
    );

    RAISE NOTICE 'Nueva version % insertada correctamente', v_version;
    RAISE NOTICE 'URL: %', v_download_url;
END $$;


-- ============================================================
-- VERIFICACION
-- ============================================================
SELECT id, version, is_latest, is_active, release_date::date as fecha,
       file_size_mb, downloads_count
FROM app_versions ORDER BY id DESC LIMIT 5;
