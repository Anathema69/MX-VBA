-- ============================================================
-- SCRIPT DE MIGRACIÓN: Extensión v2.0 - IMA Mecatrónica
-- Fecha: Enero 2026
-- ============================================================
--
-- INSTRUCCIONES:
--   1. Ejecutar en ambiente de PRUEBA primero
--   2. Hacer BACKUP de producción antes de ejecutar
--   3. Ejecutar secciones en orden (tienen dependencias)
--   4. Verificar cada sección antes de continuar
--
-- ============================================================

-- ============================================================
-- FASE 0: BACKUP Y VERIFICACIÓN PREVIA
-- ============================================================

-- Verificar estado actual de roles
SELECT role, COUNT(*) as cantidad
FROM users
GROUP BY role
ORDER BY role;

-- Verificar constraint actual
SELECT conname, pg_get_constraintdef(oid)
FROM pg_constraint
WHERE conname = 'users_role_check';


-- ============================================================
-- FASE 1: SISTEMA DE ROLES
-- ============================================================
-- Cambio de:
--   admin → administracion
--   coordinator → coordinacion
--   salesperson → ventas
-- Nuevos: direccion, proyectos
-- ============================================================

-- 1.1 Eliminar constraint actual
ALTER TABLE users DROP CONSTRAINT IF EXISTS users_role_check;

-- 1.2 Migrar datos existentes
UPDATE users SET role = 'administracion' WHERE role = 'admin';
UPDATE users SET role = 'coordinacion' WHERE role = 'coordinator';
UPDATE users SET role = 'ventas' WHERE role = 'salesperson';

-- 1.3 Crear nuevo constraint con todos los roles
ALTER TABLE users ADD CONSTRAINT users_role_check
CHECK (role IN ('direccion', 'administracion', 'proyectos', 'coordinacion', 'ventas'));

-- 1.4 Verificar migración
SELECT role, COUNT(*) as cantidad
FROM users
GROUP BY role
ORDER BY role;

-- 1.5 Actualizar función get_vendors() para usar nuevo rol
CREATE OR REPLACE FUNCTION public.get_vendors()
RETURNS TABLE(id integer, full_name character varying, username character varying)
LANGUAGE plpgsql
AS $function$
BEGIN
    RETURN QUERY
    SELECT
        u.id,
        u.full_name,
        u.username
    FROM users u
    WHERE u.role = 'ventas'  -- Cambiado de 'salesperson' a 'ventas'
        AND u.is_active = true
    ORDER BY u.full_name;
END;
$function$;

COMMENT ON FUNCTION get_vendors IS 'Obtiene lista de vendedores activos (rol: ventas)';


-- ============================================================
-- FASE 2: TABLA DE CONFIGURACIÓN DEL SISTEMA
-- ============================================================
-- Para umbrales del semáforo y otras configuraciones editables
-- ============================================================

CREATE TABLE IF NOT EXISTS system_config (
    id SERIAL PRIMARY KEY,
    config_key VARCHAR(100) NOT NULL UNIQUE,
    config_value TEXT NOT NULL,
    config_type VARCHAR(20) DEFAULT 'string' CHECK (config_type IN ('string', 'number', 'boolean', 'json')),
    description TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_by INTEGER REFERENCES users(id)
);

CREATE INDEX idx_system_config_key ON system_config(config_key);

COMMENT ON TABLE system_config IS 'Configuración del sistema editable desde BD';

-- Insertar configuraciones del semáforo
INSERT INTO system_config (config_key, config_value, config_type, description) VALUES
('semaforo_multiplicador_amarillo', '1.1', 'number', 'Multiplicador para umbral amarillo: (nomina + gasto_fijo) * este_valor'),
('semaforo_adicional_verde', '100000', 'number', 'Monto adicional sobre umbral amarillo para alcanzar verde'),
('semaforo_enabled', 'true', 'boolean', 'Habilitar/deshabilitar semáforo en Balance')
ON CONFLICT (config_key) DO NOTHING;

-- Función helper para obtener config
CREATE OR REPLACE FUNCTION get_system_config(p_key VARCHAR)
RETURNS TEXT AS $$
BEGIN
    RETURN (SELECT config_value FROM system_config WHERE config_key = p_key);
END;
$$ LANGUAGE plpgsql STABLE;

-- Función helper para obtener config numérica
CREATE OR REPLACE FUNCTION get_system_config_numeric(p_key VARCHAR)
RETURNS NUMERIC AS $$
BEGIN
    RETURN (SELECT config_value::NUMERIC FROM system_config WHERE config_key = p_key);
END;
$$ LANGUAGE plpgsql STABLE;


-- ============================================================
-- FASE 3: COLUMNAS NUEVAS EN t_order
-- ============================================================

-- 3.1 Agregar columnas para gastos y días estimados
ALTER TABLE t_order
ADD COLUMN IF NOT EXISTS gasto_operativo NUMERIC(15,2) DEFAULT 0,
ADD COLUMN IF NOT EXISTS gasto_indirecto NUMERIC(15,2) DEFAULT 0,
ADD COLUMN IF NOT EXISTS dias_estimados INTEGER DEFAULT NULL;

-- 3.2 Agregar comentarios
COMMENT ON COLUMN t_order.gasto_operativo IS 'Gastos operativos totales (suma de detalle o valor directo)';
COMMENT ON COLUMN t_order.gasto_indirecto IS 'Gastos indirectos ingresados manualmente';
COMMENT ON COLUMN t_order.dias_estimados IS 'Días estimados para completar la orden';

-- 3.3 Verificar columnas agregadas
SELECT column_name, data_type, column_default
FROM information_schema.columns
WHERE table_name = 't_order'
AND column_name IN ('gasto_operativo', 'gasto_indirecto', 'dias_estimados');


-- ============================================================
-- FASE 4: TABLA DE PREFERENCIAS DE USUARIO
-- ============================================================
-- Para guardar filtros y configuraciones por usuario
-- ============================================================

CREATE TABLE IF NOT EXISTS user_preferences (
    id SERIAL PRIMARY KEY,
    user_id INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    preference_key VARCHAR(100) NOT NULL,
    preference_value TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(user_id, preference_key)
);

CREATE INDEX IF NOT EXISTS idx_user_preferences_user ON user_preferences(user_id);

COMMENT ON TABLE user_preferences IS 'Preferencias y filtros guardados por usuario (ej: filtro de órdenes)';

-- Trigger para updated_at
CREATE OR REPLACE FUNCTION update_user_preferences_updated_at()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trigger_user_preferences_updated_at ON user_preferences;
CREATE TRIGGER trigger_user_preferences_updated_at
    BEFORE UPDATE ON user_preferences
    FOR EACH ROW
    EXECUTE FUNCTION update_user_preferences_updated_at();


-- ============================================================
-- FASE 5: TABLA DE ASISTENCIAS (CALENDARIO RRHH)
-- ============================================================
-- Usa t_payroll como referencia de empleados
-- ============================================================

CREATE TABLE IF NOT EXISTS attendance_records (
    id SERIAL PRIMARY KEY,
    employee_id INTEGER NOT NULL REFERENCES t_payroll(f_payroll) ON DELETE CASCADE,
    record_date DATE NOT NULL,
    record_type VARCHAR(20) NOT NULL CHECK (record_type IN ('ASISTENCIA', 'RETARDO', 'FALTA', 'VACACION', 'PERMISO', 'INCAPACIDAD')),
    check_in_time TIME,
    check_out_time TIME,
    notes TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    created_by INTEGER REFERENCES users(id),
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_by INTEGER REFERENCES users(id),
    UNIQUE(employee_id, record_date)
);

CREATE INDEX IF NOT EXISTS idx_attendance_employee ON attendance_records(employee_id);
CREATE INDEX IF NOT EXISTS idx_attendance_date ON attendance_records(record_date);
CREATE INDEX IF NOT EXISTS idx_attendance_type ON attendance_records(record_type);
-- Nota: No se puede usar DATE_TRUNC en índice (no es IMMUTABLE)
-- Se usa índice por fecha que cubre consultas por mes

COMMENT ON TABLE attendance_records IS 'Registro de asistencias, retardos, faltas, vacaciones para calendario RRHH';

-- Trigger para updated_at
DROP TRIGGER IF EXISTS trigger_attendance_updated_at ON attendance_records;
CREATE TRIGGER trigger_attendance_updated_at
    BEFORE UPDATE ON attendance_records
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();


-- ============================================================
-- FASE 6: FUNCIONES PARA SEMÁFORO Y CÁLCULOS
-- ============================================================

-- 6.1 Función del semáforo (usa configuración de BD)
CREATE OR REPLACE FUNCTION get_semaforo_color(
    p_ventas NUMERIC,
    p_nomina NUMERIC,
    p_gasto_fijo NUMERIC
) RETURNS TEXT AS $$
DECLARE
    v_multiplicador NUMERIC;
    v_adicional_verde NUMERIC;
    v_umbral_amarillo NUMERIC;
    v_umbral_verde NUMERIC;
BEGIN
    -- Obtener configuración de BD (con defaults)
    v_multiplicador := COALESCE(get_system_config_numeric('semaforo_multiplicador_amarillo'), 1.1);
    v_adicional_verde := COALESCE(get_system_config_numeric('semaforo_adicional_verde'), 100000);

    -- Calcular umbrales
    v_umbral_amarillo := (COALESCE(p_nomina, 0) + COALESCE(p_gasto_fijo, 0)) * v_multiplicador;
    v_umbral_verde := v_umbral_amarillo + v_adicional_verde;

    -- Determinar color
    IF COALESCE(p_ventas, 0) = 0 THEN
        RETURN 'ROJO';
    ELSIF p_ventas >= v_umbral_verde THEN
        RETURN 'VERDE';
    ELSIF p_ventas >= v_umbral_amarillo THEN
        RETURN 'AMARILLO';
    ELSE
        RETURN 'ROJO';
    END IF;
END;
$$ LANGUAGE plpgsql STABLE;

COMMENT ON FUNCTION get_semaforo_color IS 'Calcula color del semáforo según ventas vs (nómina+gastos_fijos). Umbrales configurables en system_config';


-- 6.2 Función para calcular gasto material de una orden
CREATE OR REPLACE FUNCTION get_gasto_material_orden(p_order_id INTEGER)
RETURNS NUMERIC AS $$
BEGIN
    RETURN COALESCE(
        (SELECT SUM(f_totalexpense)
         FROM t_expense
         WHERE f_order = p_order_id
         AND f_status = 'PAGADO'),
        0
    );
END;
$$ LANGUAGE plpgsql STABLE;

COMMENT ON FUNCTION get_gasto_material_orden IS 'Suma gastos pagados a proveedores (material) para una orden';


-- 6.3 Vista de órdenes con todos los gastos calculados
CREATE OR REPLACE VIEW v_order_gastos AS
SELECT
    o.f_order,
    o.f_po,
    o.f_description,
    o.f_salesubtotal,
    o.f_saletotal,
    o.dias_estimados,
    get_gasto_material_orden(o.f_order) AS gasto_material,
    COALESCE(o.gasto_operativo, 0) AS gasto_operativo,
    COALESCE(o.gasto_indirecto, 0) AS gasto_indirecto,
    (get_gasto_material_orden(o.f_order) + COALESCE(o.gasto_operativo, 0) + COALESCE(o.gasto_indirecto, 0)) AS gasto_total,
    o.f_orderstat,
    o.f_client,
    o.f_salesman,
    o.created_at
FROM t_order o;

COMMENT ON VIEW v_order_gastos IS 'Vista de órdenes con gastos calculados (material, operativo, indirecto)';


-- ============================================================
-- FASE 7: TABLA DETALLE GASTOS OPERATIVOS (PREPARACIÓN)
-- ============================================================
-- Estructura preparada, pendiente de definición final del cliente
-- ============================================================

CREATE TABLE IF NOT EXISTS order_gastos_operativos (
    id SERIAL PRIMARY KEY,
    f_order INTEGER NOT NULL REFERENCES t_order(f_order) ON DELETE CASCADE,
    monto NUMERIC(15,2) NOT NULL DEFAULT 0,
    descripcion VARCHAR(255) NOT NULL,
    categoria VARCHAR(50), -- Categoría opcional (ej: 'transporte', 'herramientas', etc.)
    fecha_gasto DATE DEFAULT CURRENT_DATE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    created_by INTEGER REFERENCES users(id),
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_by INTEGER REFERENCES users(id)
);

CREATE INDEX IF NOT EXISTS idx_gastos_operativos_order ON order_gastos_operativos(f_order);

COMMENT ON TABLE order_gastos_operativos IS 'Detalle de gastos operativos por orden (pendiente definición final)';

-- Trigger para actualizar gasto_operativo en t_order cuando cambia el detalle
CREATE OR REPLACE FUNCTION sync_order_gasto_operativo()
RETURNS TRIGGER AS $$
BEGIN
    -- Actualizar el total en t_order
    UPDATE t_order
    SET gasto_operativo = (
        SELECT COALESCE(SUM(monto), 0)
        FROM order_gastos_operativos
        WHERE f_order = COALESCE(NEW.f_order, OLD.f_order)
    ),
    updated_at = CURRENT_TIMESTAMP
    WHERE f_order = COALESCE(NEW.f_order, OLD.f_order);

    RETURN COALESCE(NEW, OLD);
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trigger_sync_gasto_operativo ON order_gastos_operativos;
CREATE TRIGGER trigger_sync_gasto_operativo
    AFTER INSERT OR UPDATE OR DELETE ON order_gastos_operativos
    FOR EACH ROW
    EXECUTE FUNCTION sync_order_gasto_operativo();


-- ============================================================
-- FASE 8: VERIFICACIÓN FINAL
-- ============================================================

-- Verificar nuevas tablas
SELECT table_name
FROM information_schema.tables
WHERE table_schema = 'public'
AND table_name IN ('system_config', 'user_preferences', 'attendance_records', 'order_gastos_operativos')
ORDER BY table_name;

-- Verificar nuevas columnas en t_order
SELECT column_name, data_type, column_default
FROM information_schema.columns
WHERE table_name = 't_order'
AND column_name IN ('gasto_operativo', 'gasto_indirecto', 'dias_estimados');

-- Verificar nuevas funciones
SELECT proname
FROM pg_proc
WHERE pronamespace = 'public'::regnamespace
AND proname IN ('get_semaforo_color', 'get_gasto_material_orden', 'get_system_config', 'get_system_config_numeric');

-- Verificar nueva vista
SELECT table_name
FROM information_schema.views
WHERE table_schema = 'public'
AND table_name = 'v_order_gastos';

-- Verificar roles migrados
SELECT role, COUNT(*), STRING_AGG(username, ', ') as usuarios
FROM users
GROUP BY role
ORDER BY role;

-- Test del semáforo
SELECT
    get_semaforo_color(0, 100000, 50000) as ventas_cero,
    get_semaforo_color(100000, 100000, 50000) as bajo_umbral,
    get_semaforo_color(170000, 100000, 50000) as en_amarillo,
    get_semaforo_color(300000, 100000, 50000) as en_verde;


-- ============================================================
-- FIN DEL SCRIPT DE MIGRACIÓN
-- ============================================================
--
-- Después de ejecutar este script:
-- 1. Verificar que la aplicación inicia correctamente
-- 2. Probar login con usuarios migrados
-- 3. Verificar permisos según nuevo rol
-- 4. Probar módulo de Balance (semáforo)
--
-- Si hay problemas, ver sección de ROLLBACK abajo
-- ============================================================


-- ============================================================
-- ROLLBACK (Solo en caso de emergencia)
-- ============================================================
--
-- -- Revertir roles
-- ALTER TABLE users DROP CONSTRAINT users_role_check;
-- UPDATE users SET role = 'admin' WHERE role IN ('direccion', 'administracion');
-- UPDATE users SET role = 'coordinator' WHERE role IN ('coordinacion', 'proyectos');
-- UPDATE users SET role = 'salesperson' WHERE role = 'ventas';
-- ALTER TABLE users ADD CONSTRAINT users_role_check
-- CHECK (role IN ('admin', 'coordinator', 'salesperson'));
--
-- -- Eliminar tablas nuevas
-- DROP TABLE IF EXISTS order_gastos_operativos CASCADE;
-- DROP TABLE IF EXISTS attendance_records CASCADE;
-- DROP TABLE IF EXISTS user_preferences CASCADE;
-- DROP TABLE IF EXISTS system_config CASCADE;
--
-- -- Eliminar columnas nuevas
-- ALTER TABLE t_order DROP COLUMN IF EXISTS gasto_operativo;
-- ALTER TABLE t_order DROP COLUMN IF EXISTS gasto_indirecto;
-- ALTER TABLE t_order DROP COLUMN IF EXISTS dias_estimados;
--
-- -- Eliminar funciones nuevas
-- DROP FUNCTION IF EXISTS get_semaforo_color;
-- DROP FUNCTION IF EXISTS get_gasto_material_orden;
-- DROP FUNCTION IF EXISTS get_system_config;
-- DROP FUNCTION IF EXISTS get_system_config_numeric;
--
-- -- Eliminar vista nueva
-- DROP VIEW IF EXISTS v_order_gastos;
-- ============================================================
