-- ============================================
-- Configuración de Auto-Update en Supabase
-- Sistema de Gestión de Proyectos v1.0.1
-- Fecha: 14 de octubre de 2025
-- ============================================

-- Este script configura la infraestructura necesaria para
-- el sistema de actualizaciones automáticas.

-- ============================================
-- 1. CREAR TABLA DE VERSIONES
-- ============================================

CREATE TABLE IF NOT EXISTS app_versions (
    id SERIAL PRIMARY KEY,
    version VARCHAR(20) NOT NULL UNIQUE,  -- Ejemplo: "1.0.1", "1.0.2"
    release_date TIMESTAMP NOT NULL DEFAULT NOW(),
    is_latest BOOLEAN NOT NULL DEFAULT false,
    is_mandatory BOOLEAN NOT NULL DEFAULT false,  -- true = actualización forzada
    download_url TEXT NOT NULL,  -- URL del instalador en Supabase Storage
    file_size_mb DECIMAL(10, 2),  -- Tamaño del instalador en MB
    release_notes TEXT,  -- Notas de la versión (qué cambió)
    min_version VARCHAR(20),  -- Versión mínima requerida para actualizar
    created_by VARCHAR(100),
    is_active BOOLEAN NOT NULL DEFAULT true,
    downloads_count INTEGER DEFAULT 0,

    -- Metadatos adicionales
    changelog JSONB,  -- Changelog estructurado

    -- Índices para búsqueda rápida
    CONSTRAINT version_format CHECK (version ~ '^\d+\.\d+\.\d+$')
);

-- Índice para búsqueda rápida de la última versión
CREATE INDEX IF NOT EXISTS idx_app_versions_latest ON app_versions(is_latest, is_active) WHERE is_latest = true;
CREATE INDEX IF NOT EXISTS idx_app_versions_release_date ON app_versions(release_date DESC);

-- ============================================
-- 2. FUNCIÓN PARA MARCAR ÚLTIMA VERSIÓN
-- ============================================

-- Esta función asegura que solo una versión esté marcada como "latest"
CREATE OR REPLACE FUNCTION set_latest_version()
RETURNS TRIGGER AS $$
BEGIN
    -- Si la nueva versión es marcada como "latest"
    IF NEW.is_latest = true THEN
        -- Desmarcar todas las demás versiones
        UPDATE app_versions
        SET is_latest = false
        WHERE id != NEW.id AND is_latest = true;
    END IF;

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Trigger que ejecuta la función automáticamente
DROP TRIGGER IF EXISTS trigger_set_latest_version ON app_versions;
CREATE TRIGGER trigger_set_latest_version
    BEFORE INSERT OR UPDATE ON app_versions
    FOR EACH ROW
    EXECUTE FUNCTION set_latest_version();

-- ============================================
-- 3. INSERTAR VERSIÓN INICIAL (1.0.0)
-- ============================================

-- Esta es la versión que ya está distribuida (sin auto-update)
INSERT INTO app_versions (
    version,
    release_date,
    is_latest,
    is_mandatory,
    download_url,
    file_size_mb,
    release_notes,
    created_by,
    is_active
) VALUES (
    '1.0.0',
    '2025-10-14 00:00:00',
    false,  -- NO es la última (será reemplazada por 1.0.1)
    false,
    'N/A - Versión distribuida manualmente',
    50.0,
    'Versión inicial del sistema sin auto-update',
    'Zuri Dev',
    true
) ON CONFLICT (version) DO NOTHING;

-- ============================================
-- 4. INSERTAR VERSIÓN CON AUTO-UPDATE (1.0.1)
-- ============================================

-- IMPORTANTE: Primero sube el instalador a Supabase Storage, luego actualiza la URL aquí
INSERT INTO app_versions (
    version,
    release_date,
    is_latest,
    is_mandatory,
    download_url,
    file_size_mb,
    release_notes,
    created_by,
    is_active,
    changelog
) VALUES (
    '1.0.1',
    NOW(),
    true,  -- Esta ES la última versión
    false,  -- No es obligatoria (usuario puede postponer)
    'PENDIENTE - Actualizar después de subir a Storage',  -- ⚠️ ACTUALIZAR ESTA URL
    50.0,
    E'Sistema de actualizaciones automáticas implementado\n• Verificación de versión al iniciar\n• Descarga automática de actualizaciones\n• Notificaciones no invasivas\n• Opción de postponer actualización',
    'Zuri Dev',
    true,
    '{"added": ["Sistema de auto-update", "Verificación de versión al inicio"], "improved": ["Logs ahora en AppData", "Configuración de producción"], "fixed": []}'::jsonb
) ON CONFLICT (version) DO UPDATE SET
    is_latest = EXCLUDED.is_latest,
    download_url = EXCLUDED.download_url,
    release_notes = EXCLUDED.release_notes,
    changelog = EXCLUDED.changelog;

-- ============================================
-- 5. VERIFICACIÓN
-- ============================================

-- Ver todas las versiones
SELECT
    id,
    version,
    is_latest,
    is_mandatory,
    release_date,
    file_size_mb,
    substring(download_url, 1, 50) || '...' as download_url_preview,
    is_active,
    downloads_count
FROM app_versions
ORDER BY release_date DESC;

-- Ver solo la última versión activa
SELECT
    version,
    release_date,
    is_mandatory,
    file_size_mb,
    release_notes,
    download_url
FROM app_versions
WHERE is_latest = true AND is_active = true;

-- ============================================
-- 6. CONSULTAS ÚTILES
-- ============================================

-- Obtener última versión (query que usará la app)
-- SELECT * FROM app_versions WHERE is_latest = true AND is_active = true LIMIT 1;

-- Incrementar contador de descargas
-- UPDATE app_versions SET downloads_count = downloads_count + 1 WHERE version = '1.0.1';

-- Marcar versión como obligatoria
-- UPDATE app_versions SET is_mandatory = true WHERE version = '1.0.1';

-- Desactivar versión antigua
-- UPDATE app_versions SET is_active = false WHERE version = '1.0.0';

-- ============================================
-- INSTRUCCIONES PARA SUPABASE STORAGE
-- ============================================

-- ⚠️ IMPORTANTE: Después de ejecutar este script, configura Storage:
--
-- 1. Ve a Supabase Dashboard → Storage
-- 2. Crea un bucket llamado: "app-installers"
-- 3. Configuración del bucket:
--    • Public: YES (para que la app pueda descargar)
--    • File size limit: 100 MB
--    • Allowed MIME types: application/x-msdownload, application/octet-stream
--
-- 4. Sube el instalador:
--    • Nombre: SistemaGestionProyectos-v1.0.1-Setup.exe
--    • Ruta en bucket: /releases/v1.0.1/SistemaGestionProyectos-v1.0.1-Setup.exe
--
-- 5. Obtén la URL pública:
--    • Clic derecho en el archivo → "Get URL"
--    • Copia la URL (ejemplo: https://wjozxqldvypdtfmkamud.supabase.co/storage/v1/object/public/app-installers/releases/v1.0.1/...)
--
-- 6. Actualiza la tabla:
--    UPDATE app_versions
--    SET download_url = 'URL_DEL_PASO_5'
--    WHERE version = '1.0.1';

-- ============================================
-- POLÍTICA DE ACCESO PARA STORAGE (RLS)
-- ============================================

-- Si tienes Row Level Security (RLS) habilitado, necesitas crear políticas:

-- Permitir lectura pública del bucket app-installers
-- (Esto se hace desde el Dashboard de Supabase en Storage → Policies)
--
-- Policy Name: Public read access
-- Policy: SELECT
-- Target roles: anon, authenticated
-- Using expression: true

-- ============================================
-- TEMPLATE PARA FUTURAS VERSIONES
-- ============================================

-- Cuando saques una nueva versión (ejemplo 1.0.2):
--
-- INSERT INTO app_versions (
--     version,
--     is_latest,
--     is_mandatory,
--     download_url,
--     file_size_mb,
--     release_notes,
--     created_by,
--     changelog
-- ) VALUES (
--     '1.0.2',
--     true,
--     false,
--     'https://tu-url-en-storage.com/releases/v1.0.2/installer.exe',
--     50.5,
--     'Descripción de cambios',
--     'Zuri Dev',
--     '{"added": ["Nueva funcionalidad"], "improved": ["Mejora X"], "fixed": ["Bug Y"]}'::jsonb
-- );
