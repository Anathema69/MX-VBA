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
    v_version       VARCHAR := '2.0.9';
    v_created_by    VARCHAR := 'Zuri Dev';
    v_file_size_mb  NUMERIC := 51.0;
    v_is_mandatory  BOOLEAN := false;
    v_min_version   VARCHAR := '2.0.8';

    v_release_notes TEXT := 'Version 2.0.9 - Modulo Inventario (Mockup en pruebas)

NUEVO MODULO: INVENTARIO (fase de pruebas - datos de ejemplo)
- Pantalla principal con cards de categorias (nombre, color, productos, stock, alertas)
- KPI cards: total productos, por pedir, categorias activas
- Detalle de categoria con tabla de productos (DataGrid)
- Alertas de stock bajo: filas amber, icono warning, badge naranja
- Formulario nuevo/editar producto (10 campos con validacion visual)
- Formulario nueva categoria con color picker y preview en vivo
- Busqueda en tiempo real + filtro stock bajo + filtro ubicacion
- Toast notifications animadas (slide-in, auto-dismiss 3s)
- Confirmacion de eliminacion via overlay modal
- Boton INVENTARIO en MainMenu con badge EN PRUEBAS

UX/UI:
- Design system unificado con DriveV2 (azul #1D4ED8)
- ComboBox custom estilo SupplierPendingView (popup redondeado)
- Botones accion con fondo tintado (azul editar, rojo eliminar)
- Layout responsivo centrado para pantallas grandes (MaxWidth)
- Maximizar sin tapar barra de tareas

NOTA: Este modulo usa datos hardcoded para validacion con el cliente.
La implementacion con BD real sera en las fases 6B-6F.';

    v_download_url TEXT;
    v_changelog JSONB;
BEGIN
    v_download_url := 'https://github.com/Anathema69/MX-VBA/releases/download/v'
                      || v_version || '/SistemaGestionProyectos-v' || v_version || '-Setup.exe';

    v_changelog := '{
        "Added": [
            "Modulo Inventario: pantalla principal con cards de categorias",
            "Modulo Inventario: detalle de categoria con DataGrid de productos",
            "Modulo Inventario: formulario nuevo/editar producto (10 campos)",
            "Modulo Inventario: formulario nueva categoria con color picker",
            "Modulo Inventario: KPI cards (productos, por pedir, categorias)",
            "Modulo Inventario: toast notifications animadas",
            "Modulo Inventario: confirmacion eliminacion via overlay modal",
            "MainMenu: boton INVENTARIO con badge EN PRUEBAS"
        ],
        "Improved": [
            "UX: ComboBox custom en inventario (estilo SupplierPendingView)",
            "UX: botones accion DataGrid con fondo tintado (azul/rojo)",
            "UX: layout responsivo centrado para pantallas grandes",
            "UX: alertas stock bajo con filas amber y badges"
        ],
        "Fixed": []
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
