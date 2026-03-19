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
    v_version       VARCHAR := '2.2.0';
    v_created_by    VARCHAR := 'Zuri Dev';
    v_file_size_mb  NUMERIC := 55.0;
    v_is_mandatory  BOOLEAN := false;
    v_min_version   VARCHAR := '2.1.0';

    v_release_notes TEXT := 'Version 2.2.0 - Modulo de Inventario + IMA Drive en produccion

MODULO DE INVENTARIO (nuevo):
- Gestion completa de categorias y productos con stock
- Pantalla unificada sidebar+detalle (sin ventanas separadas)
- 8 categorias pre-configuradas: Tornilleria, Cableado, Conectores, Herramientas, Sensores, Motores, Neumatica, Electronica
- Creacion/edicion inline de categorias y productos (sin dialogs modales)
- Alertas de stock bajo con indicadores visuales en sidebar y tabla
- Filtros por ubicacion, stock bajo y busqueda por codigo/nombre
- Auditoria completa de cambios (INSERT/UPDATE/DELETE)
- Color auto-asignado por categoria desde paleta de 8 colores
- Atajos: Enter=guardar, Escape=cancelar en formularios inline

IMA DRIVE + INVENTARIO:
- Botones del menu principal cambiados de amarillo (EN PRUEBAS) a azul normal
- Badge "EN PRUEBAS" removido de ambos modulos

BASE DE DATOS:
- 4 nuevas tablas: inventory_categories, inventory_products, inventory_movements, inventory_audit
- 3 vistas: v_inventory_low_stock, v_inventory_category_summary, v_inventory_movement_detail
- 6 funciones RPC: fn_get_inventory_stats, fn_adjust_stock, fn_get_inventory_locations, etc.
- 11 indexes optimizados con partial indexes';

    v_download_url TEXT;
    v_changelog JSONB;
BEGIN
    v_download_url := 'https://github.com/Anathema69/MX-VBA/releases/download/v'
                      || v_version || '/SistemaGestionProyectos-v' || v_version || '-Setup.exe';

    v_changelog := '{
        "Added": [
            "Modulo de Inventario: gestion completa de categorias y productos",
            "Inventario: creacion/edicion inline sin dialogs modales",
            "Inventario: alertas de stock bajo con indicadores visuales",
            "Inventario: filtros por ubicacion, stock bajo y busqueda",
            "Inventario: auditoria completa de cambios en BD",
            "Inventario: atajos Enter=guardar, Escape=cancelar"
        ],
        "Improved": [
            "IMA Drive y Inventario: botones del menu en azul normal (produccion)",
            "Inventario: UI unificada sidebar+detalle en una sola ventana",
            "Inventario: color auto-asignado por categoria desde paleta de 8 colores"
        ],
        "Fixed": [
            "Fix: badge EN PRUEBAS removido de IMA Drive e Inventario"
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
