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

    v_release_notes TEXT := 'Version 2.3.1 - Sincronizacion de carpetas + UI mejorada

SINCRONIZACION DE CARPETAS (nuevo):
- Arrastrar una carpeta desde Windows al Drive sincroniza todo el arbol
- Crea subcarpetas automaticamente (estructura recursiva)
- Detecta archivos duplicados: opcion de Sobrescribir u Omitir
- Creacion de carpetas en paralelo por nivel (10 simultaneas)
- Upload paralelo de archivos (5 simultaneos)
- Boton Cancelar visible durante toda la operacion
- Overlay de progreso con barra, porcentaje y conteo
- Analisis instantaneo via carga bulk de BD (2 queries vs N)

ELIMINACION MEJORADA:
- Eliminar carpeta: overlay de progreso mientras procesa
- Eliminar archivo/bulk: overlay con conteo progresivo
- Eliminacion de carpetas grandes optimizada (2 queries vs N recursivas)
- Dialogo de confirmacion custom (icono rojo, boton Eliminar)

UI MEJORADA:
- Overlay de progreso rediseñado: fondo dim + card blanca + icono nube + barra redondeada
- Dialogo de sync: pills de colores + boton Sobrescribir naranja + Solo nuevos azul
- Confirm dialog: custom con icono (warning amarillo / destructive rojo)
- MessageBox nativo reemplazado por dialogo integrado al diseño

DIAGNOSTICO:
- Boton Diagnosticar (dev): compara R2 vs BD, detecta huerfanos, ofrece limpieza
- Paginacion en GetAllFilesFlat para >1000 archivos

LIMPIEZA:
- Purgar R2, Benchmark y Test Drive eliminados (obsoletos, ~600 lineas removidas)';

    v_download_url TEXT;
    v_changelog JSONB;
BEGIN
    v_download_url := 'https://github.com/Anathema69/MX-VBA/releases/download/v'
                      || v_version || '/SistemaGestionProyectos-v' || v_version || '-Setup.exe';

    v_changelog := '{
        "Added": [
            "Drive: sincronizacion de carpetas completas via drag-drop",
            "Drive: creacion recursiva de subcarpetas con deteccion de duplicados",
            "Drive: overlay de progreso con barra, porcentaje y boton Cancelar",
            "Drive: dialogo de sync con pills de colores y opciones Sobrescribir/Omitir",
            "Drive: boton Diagnosticar para verificar integridad R2 vs BD",
            "Drive: eliminacion con overlay de progreso (carpetas y archivos bulk)"
        ],
        "Improved": [
            "Drive: eliminacion de carpetas grandes optimizada (2 queries vs N recursivas)",
            "Drive: creacion de carpetas en paralelo por nivel (10 simultaneas)",
            "Drive: overlay rediseñado con fondo dim, card blanca e icono",
            "Drive: Confirm dialog custom reemplaza MessageBox nativo",
            "Drive: paginacion en GetAllFilesFlat para manejar >1000 archivos"
        ],
        "Fixed": [
            "Fix: arrastrar carpeta desde Windows fallaba silenciosamente",
            "Fix: archivos duplicados en upload causaban error 23505 silencioso",
            "Fix: cancelar sincronizacion colgaba la app (deadlock Dispatcher.Invoke)",
            "Fix: eliminar carpeta con cientos de archivos no respondia (N+1 queries)",
            "Fix: boton Cancelar/OK en Confirm dialog no funcionaba con WindowStyle.None"
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
