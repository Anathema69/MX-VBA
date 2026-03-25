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
    v_version       VARCHAR := '2.3.0';
    v_created_by    VARCHAR := 'Zuri Dev';
    v_file_size_mb  NUMERIC := 55.0;
    v_is_mandatory  BOOLEAN := false;
    v_min_version   VARCHAR := '2.2.0';

    v_release_notes TEXT := 'Version 2.3.0 - IMA Drive: Mejoras CAD + Ventana unica

IMA DRIVE - EXTENSIONES Y MAPEO:
- Soporte completo para 13 extensiones CAD/CNC: .ipt, .iam, .sldprt, .sldasm, .mcam, .mcx-5/7/9, .igs, .dwg, .dxf, .step, .stp
- Iconos y colores diferenciados: Piezas(morado), Ensambles(teal), CNC(naranja)
- Nombres legibles: Pieza Inventor, Ensamble SolidWorks, Programa Mastercam
- MIME types correctos para todos los formatos CAD

IMA DRIVE - FILTROS CAD:
- Sub-filtros en sidebar: Ensambles, Piezas, Planos, Modelos 3D, CNC
- Iconos PNG dedicados por subtipo
- Conteos dinamicos y auto-ocultamiento si no hay archivos CAD

IMA DRIVE - ENSAMBLES:
- Al abrir un ensamble (.iam/.sldasm) se descargan automaticamente todas las piezas de la carpeta
- Overlay de progreso visible con barra y conteo de archivos
- Inventor/SolidWorks encuentra las piezas referenciadas correctamente
- Resuelve inconsistencias al abrir ensambles de meses distintos

IMA DRIVE - ARCHIVOS:
- Filtro de basura: archivos ~$, .db, .lck, .tmp no se suben al Drive
- Fallback "Abrir con..." si no hay programa asociado o esta roto
- Soporte para thumbnails CAD via Windows Shell (requiere software instalado)

PLATAFORMA:
- Ventana unica: solo 1 ventana en el taskbar al abrir cualquier modulo
- Al cerrar modulo se regresa automaticamente al menu de modulos';

    v_download_url TEXT;
    v_changelog JSONB;
BEGIN
    v_download_url := 'https://github.com/Anathema69/MX-VBA/releases/download/v'
                      || v_version || '/SistemaGestionProyectos-v' || v_version || '-Setup.exe';

    v_changelog := '{
        "Added": [
            "IMA Drive: sub-filtros CAD (Ensambles, Piezas, Planos, Modelos 3D, CNC)",
            "IMA Drive: descarga automatica de contexto al abrir ensambles (.iam/.sldasm)",
            "IMA Drive: overlay de progreso con barra y conteo para descarga de contexto",
            "IMA Drive: thumbnails CAD via Windows Shell (IShellItemImageFactory)",
            "IMA Drive: filtro de basura en upload (~$, .db, .lck, .tmp, .bak)",
            "IMA Drive: fallback OpenAs_RunDLL si programa asociado no existe o esta roto",
            "Plataforma: ventana unica en taskbar (Hide/Show MainMenu)"
        ],
        "Improved": [
            "IMA Drive: 13 extensiones CAD/CNC mapeadas con iconos, colores y MIME types",
            "IMA Drive: colores diferenciados por subtipo (piezas morado, ensambles teal, CNC naranja)",
            "IMA Drive: nombres legibles (Pieza Inventor, Ensamble SolidWorks, Programa Mastercam)",
            "IMA Drive: iconos PNG en sub-filtros CAD (gear, ruler, wrench)"
        ],
        "Fixed": [
            "Fix: archivos .ipt/.iam/.sldprt/.sldasm mostraban icono generico",
            "Fix: archivos .mcam/.mcx-7/.mcx-5 no estaban en filtro CAD",
            "Fix: Process.Start no detectaba asociaciones de archivo rotas",
            "Fix: ensambles mezclaban piezas de diferentes carpetas al abrir secuencialmente"
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
