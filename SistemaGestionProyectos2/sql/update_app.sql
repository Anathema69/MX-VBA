-- ============================================================
-- SCRIPT: Insertar nueva version en app_versions
-- ============================================================
-- INSTRUCCIONES:
--   1. Subir el instalador a Supabase Storage ANTES de ejecutar
--      Ruta: app-installers/releases/v{VERSION}/SistemaGestionProyectos-v{VERSION}-Setup.exe
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
    v_version       VARCHAR := '2.0.6';
    v_created_by    VARCHAR := 'Zuri Dev';
    v_file_size_mb  NUMERIC := 49.96;
    v_is_mandatory  BOOLEAN := true;
    v_min_version   VARCHAR := NULL;

    v_release_notes TEXT := 'Version 2.0.6 - Fase 4: Bloque 3 (Portal Ventas + Storage) + Bloque 4 (Ejecutor) + Bugfixes

PORTAL VENTAS CON SUBIDA DE ARCHIVOS (Bloque 3):
- Vendedor: subida multiple de archivos (jpg, png, pdf, doc, xls) por comision
- Galeria inline con thumbnails, collapsible por comision
- Preview fullscreen con navegacion entre archivos (flechas + teclado)
- Acciones CRUD: subir, ver, descargar, eliminar (draft/pending)
- Comisiones pagadas: solo visualizar y descargar (read-only)
- Boton "Solicitar Liberacion" (draft -> pending) con archivos adjuntos
- Admin: galeria inline en panel de comisiones con preview y descarga
- Infraestructura: Supabase Storage (bucket order-files) + tabla order_files
- StorageService reutilizable para Bloque 5 (carpetas por orden)

COLUMNA EJECUTOR (Bloque 4):
- Nueva columna "Ejecutor" en tabla de ordenes con selector multiple
- Tabla t_order_ejecutor para asignacion N:N
- Batch loading optimizado para nombres de ejecutores

CORRECCIONES:
- BUG-005: Correccion en facturacion de pagos a proveedores - el boton eliminar no aparecia para gastos ya pagados (afectaba proveedores con 0 dias de credito cuyo pago se registraba automaticamente). Status visual corregido, headers dinamicos por tab, auditoria de eliminacion
- BUG-006: Correccion en edicion de asistencia en calendario - no se podian modificar registros de asistencia una vez guardados. Reescrito el metodo de guardado, soporte para desmarcar registros y boton de actualizar agregado

ACTUALIZACION OBLIGATORIA - Portal de ventas con gestion de archivos';

    v_download_url TEXT;
    v_changelog JSONB;
BEGIN
    v_download_url := 'https://wjozxqldvypdtfmkamud.supabase.co/storage/v1/object/public/app-installers/releases/v'
                      || v_version || '/SistemaGestionProyectos-v' || v_version || '-Setup.exe';

    v_changelog := '{
        "Fixed": [
            "BUG-005: Portal Proveedores permite eliminar gastos pagados",
            "BUG-006: Calendario permite modificar asistencia registrada",
            "Desmarcar asistencia elimina registro con auditoria",
            "Boton vacaciones convertido a indicador visual sin conflicto"
        ],
        "Added": [
            "Portal Ventas: subida multiple de archivos por comision",
            "Galeria inline con thumbnails y preview fullscreen",
            "Navegacion entre archivos con flechas y teclado",
            "StorageService para Supabase Storage (bucket order-files)",
            "Tabla order_files para registro de archivos",
            "Boton Solicitar Liberacion (draft -> pending)",
            "Admin: galeria inline en panel de comisiones",
            "Columna Ejecutor con selector multiple en ordenes",
            "Tabla t_order_ejecutor para asignacion N:N"
        ],
        "Improved": [
            "Acciones CRUD contextuales: upload/delete en draft+pending, read-only en paid",
            "Toggle collapsible para archivos con hover interactivo y chevron visual",
            "Notificaciones toast en lugar de MessageBox en portal vendedor",
            "Diseño unificado con SupplierPendingView (colores, cards, status bar)"
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
