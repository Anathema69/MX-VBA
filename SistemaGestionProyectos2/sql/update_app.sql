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
    v_version       VARCHAR := '2.3.3';
    v_created_by    VARCHAR := 'Zuri Dev';
    v_file_size_mb  NUMERIC := 55.0;
    v_is_mandatory  BOOLEAN := false;
    v_min_version   VARCHAR := '2.2.0';

    v_release_notes TEXT := 'Version 2.3.3 - Fix drag-drop intermitente + Tests automatizados

FIX DRAG-DROP (critico):
- Drag-drop desde Windows fallaba intermitentemente al abrir Drive desde Ordenes
- Handlers registrados con handledEventsToo=true (siempre se disparan)
- FileWatcher ya no bloquea hilo UI durante drag (Dispatcher.InvokeAsync)
- Guard _isDragging previene reconstruccion visual durante operacion de arrastre
- Grid de contenido con Background=Transparent para hit-testing correcto
- Effects=Copy siempre seteado en DragEnter (no solo cuando hay folderId)

TESTS AUTOMATIZADOS:
- 9 tests DragDrop: auth, constructor, handlers, DragEnter/Over/Leave, guard, E2E
- Boton "Tests" en sidebar de Drive (usuario caaj)
- Boton "Test DragDrop" violeta en StressTestWindow
- Tests verifican fix via reflection (handledEventsToo) y eventos programaticos';

    v_download_url TEXT;
    v_changelog JSONB;
BEGIN
    v_download_url := 'https://github.com/Anathema69/MX-VBA/releases/download/v'
                      || v_version || '/SistemaGestionProyectos-v' || v_version || '-Setup.exe';

    v_changelog := '{
        "Added": [
            "Tests: 9 tests automatizados para drag-drop (auth, constructor, handlers, E2E)",
            "Tests: boton Tests en sidebar Drive para abrir StressTestWindow",
            "Tests: boton Test DragDrop en StressTestWindow con categoria violeta"
        ],
        "Improved": [
            "Drive: handlers drag registrados con handledEventsToo=true (robustez)",
            "Drive: FileWatcher usa Dispatcher.InvokeAsync en vez de Invoke (no bloquea OLE)",
            "Drive: Grid contenido con Background=Transparent (hit-testing correcto)",
            "Drive: Effects=Copy siempre seteado en DragEnter independiente de estado"
        ],
        "Fixed": [
            "Fix: drag-drop intermitente al abrir Drive desde Ordenes",
            "Fix: FileWatcher bloqueaba hilo UI durante drag con Dispatcher.Invoke",
            "Fix: RenderContent se ejecutaba durante drag activo (reconstruia visual tree)",
            "Fix: DragEnter no seteaba Effects cuando _currentFolderId era null temporalmente",
            "Fix: build-release.bat tenia VERSION hardcodeado en 2.1.1"
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
