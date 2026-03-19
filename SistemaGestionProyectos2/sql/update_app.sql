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
    v_version       VARCHAR := '2.1.1';
    v_created_by    VARCHAR := 'Zuri Dev';
    v_file_size_mb  NUMERIC := 54.0;
    v_is_mandatory  BOOLEAN := false;
    v_min_version   VARCHAR := '2.1.0';

    v_release_notes TEXT := 'Version 2.1.1 - Open-in-Place mejorado + UX fixes

OPEN-IN-PLACE MEJORADO:
- Nombres de archivo limpios: archivos abiertos usan subcarpeta por ID en vez de prefijo numerico
  Antes: IMA-Drive/open/1420_Pieza.ipt → Ahora: IMA-Drive/open/1420/Pieza.ipt
- Deteccion de "Guardar como": al guardar con otro nombre o extension, el archivo nuevo se sube automaticamente a la misma carpeta de Drive
- Auto-refresh de UI: al sincronizar un archivo, la carpeta se refresca automaticamente sin necesidad de F5
- Migracion automatica de archivos existentes al nuevo formato de subcarpetas
- Debounce de 4s para "Save As" (apps como Inventor escriben lento)

UX FIXES:
- Busqueda sin resultados: muestra "Sin resultados" sin botones de crear/subir
- Empty state contextual: botones de accion solo aparecen en carpetas vacias (no en Recientes ni busqueda)';

    v_download_url TEXT;
    v_changelog JSONB;
BEGIN
    v_download_url := 'https://github.com/Anathema69/MX-VBA/releases/download/v'
                      || v_version || '/SistemaGestionProyectos-v' || v_version || '-Setup.exe';

    v_changelog := '{
        "Added": [
            "Open-in-Place: deteccion de Guardar como (nuevo archivo se sube automaticamente)",
            "Open-in-Place: auto-refresh de carpeta al sincronizar archivos"
        ],
        "Improved": [
            "Open-in-Place: nombres limpios con subcarpeta por ID (sin prefijo numerico)",
            "Open-in-Place: migracion automatica de archivos existentes al nuevo formato",
            "Open-in-Place: debounce 4s para Save As (apps CAD escriben lento)"
        ],
        "Fixed": [
            "Fix: busqueda sin resultados mostraba botones de crear/subir",
            "Fix: empty state contextual (solo botones en carpetas vacias, no en Recientes ni busqueda)"
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
