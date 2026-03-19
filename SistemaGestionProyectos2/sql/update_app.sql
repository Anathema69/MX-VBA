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
    v_version       VARCHAR := '2.1.0';
    v_created_by    VARCHAR := 'Zuri Dev';
    v_file_size_mb  NUMERIC := 54.0;
    v_is_mandatory  BOOLEAN := false;
    v_min_version   VARCHAR := '2.0.9';

    v_release_notes TEXT := 'Version 2.1.0 - Drive V3 Completo + Modo Produccion

=== DRIVE V3 - Fases A+B+C (Preview, Recientes, Operaciones) ===
- Preview de imagenes: overlay fullscreen con navegacion flechas, zoom, thumbnails async
- Cache de thumbnails en disco (%LOCALAPPDATA%/IMA-Drive/thumbs/)
- Recientes en contenido principal con toggle Mis archivos / Todos
- Actividad en sidebar (ultimos cambios por usuario)
- Operaciones: Cut (Ctrl+X), Copy (Ctrl+C), Paste (Ctrl+V) entre carpetas
- Descarga ZIP de multiples archivos seleccionados
- Drag & Drop de archivos desde el explorador

=== DRIVE V3 - Fases D+E (Open-in-Place + Simplificacion UI) ===
- FileWatcherService: Singleton con FileSystemWatcher + debounce 2s
- Doble-clic = descarga a local + abre con app nativa + auto-sync al guardar
- Sync badges en cards/rows (verde=abierto, azul=syncing, check=synced, rojo=error)
- SyncStatusBar inferior con boton Reintentar para errores
- Conflictos auto-resueltos (local siempre gana, sin dialogo)
- Panel de detalles ELIMINADO (320px recuperados): acciones via context menu
- Cache local visible en sidebar (tamano + boton Limpiar)
- Boton Volver reemplaza icono X para regresar al menu

=== DRIVE V3 - Fases F+G (Cache + Pulido UX) ===
- Atajos de teclado: F2 renombrar, Delete eliminar, F5 refrescar, Ctrl+N nueva carpeta, Ctrl+U subir, Ctrl+F buscar, Ctrl+A seleccionar todos, Backspace volver, Enter abrir
- Empty state mejorado con botones de accion (Subir archivos, Nueva carpeta)
- Animacion fade suave (150ms) al navegar entre carpetas
- Prefetch de carpetas nivel 1-2 al iniciar (navegacion instantanea)
- Limpieza automatica de thumbnails (>30 dias o >200MB via LRU)
- Cache label muestra tamano combinado (thumbnails + archivos abiertos)

=== DRIVE WORKFLOW TESTS ===
- 7 tests automatizados con archivos reales: FolderCRUD, FileCRUD, BulkUpload, OpenInPlace, ConflictAutoResolve, SubfolderTree, Setup
- Reporte copiable con tabla formateada (estado, nombre, tiempo, limite)
- Boton Test Drive en sidebar (solo usuario caaj)

=== SIDEBAR MEJORADO ===
- Filtrar por tipo ahora aparece antes de Actividad (acceso rapido)
- Recientes: empty state propio sin botones de crear/subir

=== SEGURIDAD Y PRODUCCION ===
- Certificado de firma incluido en el instalador (certutil importa a TrustedPublisher + Root)
- DevMode desactivado (sin auto-login, sin skip password, username vacio)
- Logging nivel Info (era Debug), retencion 30 dias
- Herramientas dev (Purgar R2, Benchmark, Test Drive) solo visibles para usuario caaj
- Proteccion contra loop de actualizacion (flag _updateCheckDone por sesion)';

    v_download_url TEXT;
    v_changelog JSONB;
BEGIN
    v_download_url := 'https://github.com/Anathema69/MX-VBA/releases/download/v'
                      || v_version || '/SistemaGestionProyectos-v' || v_version || '-Setup.exe';

    v_changelog := '{
        "Added": [
            "Drive V3-A: Preview imagenes fullscreen con zoom, flechas y thumbnails en disco",
            "Drive V3-B: Recientes en contenido principal con toggle Mis archivos/Todos",
            "Drive V3-B: Actividad reciente en sidebar",
            "Drive V3-C: Cut/Copy/Paste (Ctrl+X/C/V) entre carpetas",
            "Drive V3-C: Descarga ZIP de seleccion multiple + Drag & Drop",
            "Drive V3-D: Open-in-Place (doble-clic = abrir nativo + auto-sync al guardar)",
            "Drive V3-D: FileWatcherService con debounce 2s y manifest JSON",
            "Drive V3-D: Sync badges en cards/rows (abierto/syncing/synced/error)",
            "Drive V3-D: SyncStatusBar inferior con Reintentar",
            "Drive V3-G: Atajos teclado (F2, Delete, F5, Ctrl+N/U/F/A, Backspace, Enter)",
            "Drive V3-G: Empty state con botones Subir archivos y Nueva carpeta",
            "Drive V3-G: Animacion fade 150ms en transiciones de carpeta",
            "Drive V3-F: Prefetch carpetas nivel 1-2 al iniciar",
            "Drive V3-F: Limpieza automatica de thumbnails (LRU 200MB, 30 dias)",
            "Drive Workflow Tests: 7 tests automatizados con archivos reales",
            "Drive Workflow Tests: reporte copiable con tabla formateada",
            "Instalador: certificado de firma incluido (instalacion transparente)"
        ],
        "Improved": [
            "Drive V3-E: Panel detalles eliminado (320px recuperados, acciones via context menu)",
            "Drive V3-E: Conflictos auto-resueltos (local gana, sin dialogo)",
            "Drive V3-E: Boton Volver reemplaza icono X",
            "Drive V3-F: Cache label combinado (thumbs + open-in-place)",
            "Sidebar: Filtrar por tipo antes de Actividad (acceso rapido)",
            "Recientes: empty state contextual sin botones de crear",
            "Herramientas dev (Purgar/Benchmark/Test) solo para usuario caaj",
            "Logging: nivel Info con retencion 30 dias (modo produccion)"
        ],
        "Fixed": [
            "Fix: loop infinito de actualizacion (check unico por sesion con _updateCheckDone)",
            "Fix: DevMode desactivado en produccion (auto-login, skip password, username vacio)"
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
