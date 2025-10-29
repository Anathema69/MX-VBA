-- ============================================
-- Script Alternativo: Creación de Usuarios con pgcrypto
-- Sistema de Gestión de Proyectos v1.0.0
-- Fecha: 14 de octubre de 2025
-- Desarrollado por: Zuri Dev
-- ============================================

-- IMPORTANTE: Este script usa la extensión pgcrypto de PostgreSQL
-- para generar los hashes BCrypt directamente en la base de datos.
-- Esto elimina la necesidad de generar hashes manualmente.

-- ============================================
-- 0. HABILITAR EXTENSIÓN pgcrypto
-- ============================================

-- Habilitar la extensión (solo necesario ejecutar una vez)
CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- ============================================
-- 1. CREAR USUARIOS ADMINISTRADORES
-- ============================================

-- Admin 1: admin (Password: Admin2025!)
INSERT INTO users (username, email, password_hash, full_name, role, is_active, last_login)
VALUES (
    'adm1',
    'admin@sistema.com',
    crypt('adm_ima_1', gen_salt('bf', 11)),  -- BCrypt con cost factor 11
    'Administrador Principal',
    'admin',
    true,
    NULL
)
ON CONFLICT (username) DO NOTHING;

-- Admin 2: admin2 (Password: Admin2025!)
INSERT INTO users (username, email, password_hash, full_name, role, is_active, last_login)
VALUES (
    'adm2',
    'admin2@sistema.com',
    crypt('adm_ima_2', gen_salt('bf', 11)),
    'Administrador Secundario',
    'admin',
    true,
    NULL
)
ON CONFLICT (username) DO NOTHING;

-- ============================================
-- 2. CREAR USUARIOS COORDINADORES
-- ============================================

-- Coordinador 1: coordinador1 (Password: Coord2025!)
INSERT INTO users (username, email, password_hash, full_name, role, is_active, last_login)
VALUES (
    'cor1',
    'coordinador1@sistema.com',
    crypt('cor_ima_1', gen_salt('bf', 11)),
    'Coordinador de Proyectos 1',
    'coordinator',
    true,
    NULL
)
ON CONFLICT (username) DO NOTHING;

-- Coordinador 2: coordinador2 (Password: Coord2025!)
INSERT INTO users (username, email, password_hash, full_name, role, is_active, last_login)
VALUES (
    'cor2',
    'coordinador2@sistema.com',
    crypt('cor_ima_2', gen_salt('bf', 11)),
    'Coordinador de Proyectos 2',
    'coordinator',
    true,
    NULL
)
ON CONFLICT (username) DO NOTHING;

-- ============================================
-- 3. VERIFICACIÓN
-- ============================================

-- Verificar que los usuarios se crearon correctamente
SELECT
    id,
    username,
    email,
    full_name,
    role,
    is_active,
    created_at,
    last_login,
    substring(password_hash, 1, 20) || '...' as password_hash_preview
FROM users
WHERE username IN ('admin', 'admin2', 'coordinador1', 'coordinador2')
ORDER BY role, username;

-- Contar usuarios por rol
SELECT
    role,
    COUNT(*) as total_usuarios,
    COUNT(CASE WHEN is_active THEN 1 END) as usuarios_activos
FROM users
WHERE username IN ('admin', 'admin2', 'coordinador1', 'coordinador2')
GROUP BY role;

-- ============================================
-- CREDENCIALES DE ACCESO
-- ============================================

-- ADMINISTRADORES:
--   Usuario: admin       | Contraseña: Admin2025!
--   Usuario: admin2      | Contraseña: Admin2025!

-- COORDINADORES:
--   Usuario: coordinador1 | Contraseña: Coord2025!
--   Usuario: coordinador2 | Contraseña: Coord2025!

-- ============================================
-- VENTAJAS DE ESTE MÉTODO
-- ============================================

-- ✅ No requiere generar hashes manualmente
-- ✅ Los hashes se generan directamente en PostgreSQL
-- ✅ Compatible con BCrypt.Net usado en la aplicación
-- ✅ Cost factor 11 (mismo que la aplicación usa por defecto)
-- ✅ Cada hash es único gracias al salt aleatorio

-- ============================================
-- NOTAS TÉCNICAS
-- ============================================

-- • pgcrypto genera hashes BCrypt compatibles con BCrypt.Net
-- • gen_salt('bf', 11) usa algoritmo Blowfish con cost factor 11
-- • El cost factor 11 significa 2^11 = 2048 iteraciones
-- • Mayor cost factor = más seguro pero más lento (11 es óptimo)
-- • ON CONFLICT evita errores si los usuarios ya existen

-- ============================================
-- VERIFICACIÓN DE COMPATIBILIDAD
-- ============================================

-- Para verificar que el hash es compatible con tu aplicación C#,
-- puedes probar el login directamente en la aplicación usando:
--   Username: admin
--   Password: Admin2025!

-- Si el login funciona, significa que pgcrypto es 100% compatible
-- con BCrypt.Net de tu aplicación.

-- ============================================
-- SOLUCIÓN DE PROBLEMAS
-- ============================================

-- Si obtienes error "extension pgcrypto does not exist":
-- Ejecuta como superusuario (o pide al admin de Supabase):
-- CREATE EXTENSION pgcrypto;

-- Si los usuarios ya existen y quieres recrearlos:
-- DELETE FROM users WHERE username IN ('admin', 'admin2', 'coordinador1', 'coordinador2');
-- -- Luego vuelve a ejecutar los INSERT de arriba

-- Si quieres cambiar una contraseña después:
-- UPDATE users
-- SET password_hash = crypt('NuevaContraseña123!', gen_salt('bf', 11))
-- WHERE username = 'admin';
