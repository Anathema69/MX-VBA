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
    v_version       VARCHAR := '2.3.1';
    v_created_by    VARCHAR := 'Zuri Dev';
    v_file_size_mb  NUMERIC := 55.0;
    v_is_mandatory  BOOLEAN := false;
    v_min_version   VARCHAR := '2.2.0';

    v_release_notes TEXT := 'Version 2.3.1 - Sincronizacion de carpetas + Rediseno UX/UI

SINCRONIZACION DE CARPETAS (nuevo):
- Arrastrar carpeta desde Windows al Drive sincroniza todo el arbol recursivo
- Crea subcarpetas automaticamente con deteccion de duplicados
- Opcion de Sobrescribir u Omitir archivos existentes
- Creacion de carpetas en paralelo por nivel, upload paralelo 5 archivos
- Boton Cancelar visible durante toda la operacion
- Overlay de progreso con barra, porcentaje y conteo
- Analisis instantaneo via carga bulk de BD (2 queries vs N)

REDISENO UX/UI COMPLETO:
- Paleta consolidada: 8 tokens semanticos (Success, Warning, Danger, Info), Material colors eliminados
- Botones: hover/pressed states con ColorAnimation suave, nuevo GhostButton para acciones terciarias
- Confirm dialog: backdrop dim + scale animation + fade-in 200ms, botones con CornerRadius=8
- Toast rediseñado: estilo light pill con fondo blanco, border izquierdo color semantico, boton cerrar (X)
- Skeleton loading: ghost cards/rows con pulse animation reemplazan spinner central
- Staggered fade-in: 30ms delay por card, translateY 12->0 + opacity
- File/Folder cards: hover scale 1.015 con shadow elevation
- Sidebar chevron: animacion 90<->0 en 200ms
- Drag & drop overlay: fade-in + scale 0.9->1.0
- Image overlay: fade-in 250ms + scale, close con fade-out 200ms

ELIMINACION MEJORADA:
- Overlay de progreso en eliminar carpeta, archivo individual y bulk
- Eliminacion de carpetas grandes optimizada (2 queries vs N recursivas)
- Dialogo de confirmacion custom con icono contextual

DIAGNOSTICO:
- Boton Diagnosticar (dev): compara R2 vs BD, detecta huerfanos, ofrece limpieza

LIMPIEZA:
- Purgar R2, Benchmark y Test Drive eliminados (~600 lineas removidas)';

    v_download_url TEXT;
    v_changelog JSONB;
BEGIN
    v_download_url := 'https://github.com/Anathema69/MX-VBA/releases/download/v'
                      || v_version || '/SistemaGestionProyectos-v' || v_version || '-Setup.exe';

    v_changelog := '{
        "Added": [
            "Drive: sincronizacion de carpetas completas via drag-drop con arbol recursivo",
            "Drive: deteccion de duplicados con opciones Sobrescribir/Omitir",
            "Drive: overlay de progreso con barra, porcentaje y boton Cancelar",
            "Drive: skeleton loading con ghost cards/rows y pulse animation",
            "Drive: staggered fade-in (30ms delay/card) al renderizar contenido",
            "Drive: boton Diagnosticar para verificar integridad R2 vs BD",
            "Drive: eliminacion con overlay de progreso (carpetas y archivos bulk)",
            "UI: nuevo estilo GhostButton para acciones terciarias",
            "UI: 8 tokens semanticos de color (Success, Warning, Danger, Info + Bg)"
        ],
        "Improved": [
            "Drive: eliminacion de carpetas grandes optimizada (2 queries vs N recursivas)",
            "Drive: creacion de carpetas en paralelo por nivel (10 simultaneas)",
            "UI: paleta consolidada Tailwind, Material colors eliminados",
            "UI: botones con ColorAnimation hover/pressed suave (100ms in, 150ms out)",
            "UI: Confirm dialog con backdrop dim + scale/fade animation",
            "UI: Toast rediseñado estilo light pill (fondo blanco, border color, boton X)",
            "UI: hover scale 1.015 + shadow elevation en file/folder cards",
            "UI: sidebar chevron animado 200ms, drag-drop overlay animado",
            "UI: image overlay con fade-in 250ms + scale 0.9->1.0"
        ],
        "Fixed": [
            "Fix: arrastrar carpeta desde Windows fallaba silenciosamente",
            "Fix: archivos duplicados en upload causaban error 23505 silencioso",
            "Fix: cancelar sincronizacion colgaba la app (deadlock Dispatcher.Invoke)",
            "Fix: eliminar carpeta con cientos de archivos no respondia (N+1 queries)",
            "Fix: boton Cancel/OK en Confirm dialog no funcionaba con WindowStyle.None",
            "Fix: GetAllFilesFlat limitado a 1000 archivos (ahora paginado)"
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
