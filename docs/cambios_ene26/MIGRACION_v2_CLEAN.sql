-- ============================================================
-- MIGRACIÓN v2.0 - SCRIPT LIMPIO (Ejecutar en partes)
-- ============================================================
-- INSTRUCCIONES: Ejecuta cada PARTE por separado
-- Espera que termine sin errores antes de pasar a la siguiente
-- ============================================================


-- ============================================================
-- PARTE 1: ROLES (Ejecutar primero)
-- ============================================================

-- 1.1 Verificar estado actual
SELECT role, COUNT(*) as cantidad FROM users GROUP BY role;

-- 1.2 Eliminar constraint actual
ALTER TABLE users DROP CONSTRAINT IF EXISTS users_role_check;

-- 1.3 Migrar roles existentes
UPDATE users SET role = 'administracion' WHERE role = 'admin';
UPDATE users SET role = 'coordinacion' WHERE role = 'coordinator';
UPDATE users SET role = 'ventas' WHERE role = 'salesperson';

-- 1.4 Crear nuevo constraint
ALTER TABLE users ADD CONSTRAINT users_role_check
CHECK (role IN ('direccion', 'administracion', 'proyectos', 'coordinacion', 'ventas'));

-- 1.5 Verificar migración
SELECT role, COUNT(*) as cantidad FROM users GROUP BY role;


-- ============================================================
-- PARTE 2: COLUMNAS EN T_ORDER (Ejecutar segundo)
-- ============================================================

-- 2.1 Agregar columnas nuevas
ALTER TABLE t_order ADD COLUMN IF NOT EXISTS gasto_operativo NUMERIC(15,2) DEFAULT 0;
ALTER TABLE t_order ADD COLUMN IF NOT EXISTS gasto_indirecto NUMERIC(15,2) DEFAULT 0;
ALTER TABLE t_order ADD COLUMN IF NOT EXISTS dias_estimados INTEGER DEFAULT NULL;

-- 2.2 Verificar
SELECT column_name, data_type, column_default
FROM information_schema.columns
WHERE table_name = 't_order'
AND column_name IN ('gasto_operativo', 'gasto_indirecto', 'dias_estimados');


-- ============================================================
-- PARTE 3: TABLA SYSTEM_CONFIG (Ejecutar tercero)
-- ============================================================

-- 3.1 Crear tabla
CREATE TABLE IF NOT EXISTS system_config (
    id SERIAL PRIMARY KEY,
    config_key VARCHAR(100) NOT NULL UNIQUE,
    config_value TEXT NOT NULL,
    config_type VARCHAR(20) DEFAULT 'string',
    description TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_by INTEGER
);

-- 3.2 Insertar configuración del semáforo
INSERT INTO system_config (config_key, config_value, config_type, description) VALUES
('semaforo_multiplicador_amarillo', '1.1', 'number', 'Multiplicador: (nomina + gasto_fijo) * este_valor'),
('semaforo_adicional_verde', '100000', 'number', 'Monto adicional sobre umbral amarillo para verde')
ON CONFLICT (config_key) DO NOTHING;

-- 3.3 Verificar
SELECT * FROM system_config;


-- ============================================================
-- PARTE 4: FUNCIÓN SEMÁFORO (Ejecutar cuarto)
-- ============================================================

-- Lógica del semáforo:
-- ROJO:     ventas = 0
-- AMARILLO: ventas >= (nomina + gasto_fijo) * 1.1
-- VERDE:    ventas >= (nomina + gasto_fijo) * 1.1 + 100000

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
    -- Obtener configuración (con defaults si no existe)
    SELECT COALESCE(
        (SELECT config_value::NUMERIC FROM system_config WHERE config_key = 'semaforo_multiplicador_amarillo'),
        1.1
    ) INTO v_multiplicador;

    SELECT COALESCE(
        (SELECT config_value::NUMERIC FROM system_config WHERE config_key = 'semaforo_adicional_verde'),
        100000
    ) INTO v_adicional_verde;

    -- Calcular umbrales
    -- Umbral amarillo = (nomina + gasto_fijo) * 1.1
    v_umbral_amarillo := (COALESCE(p_nomina, 0) + COALESCE(p_gasto_fijo, 0)) * v_multiplicador;

    -- Umbral verde = umbral_amarillo + 100,000
    v_umbral_verde := v_umbral_amarillo + v_adicional_verde;

    -- Determinar color según ventas
    IF COALESCE(p_ventas, 0) = 0 THEN
        RETURN 'ROJO';  -- Ventas = 0, rojo fuerte
    ELSIF p_ventas >= v_umbral_verde THEN
        RETURN 'VERDE';  -- Rebasa umbral verde
    ELSIF p_ventas >= v_umbral_amarillo THEN
        RETURN 'AMARILLO';  -- Rebasa umbral amarillo pero no verde
    ELSE
        RETURN 'ROJO';  -- No alcanza umbral amarillo
    END IF;
END;
$$ LANGUAGE plpgsql STABLE;

-- 4.2 Probar la función
-- Ejemplo: nomina=100,000 + gasto_fijo=50,000 = 150,000
-- Umbral amarillo = 150,000 * 1.1 = 165,000
-- Umbral verde = 165,000 + 100,000 = 265,000

SELECT
    get_semaforo_color(0, 100000, 50000) as "ventas_0_ROJO",
    get_semaforo_color(100000, 100000, 50000) as "ventas_100k_ROJO",
    get_semaforo_color(165000, 100000, 50000) as "ventas_165k_AMARILLO",
    get_semaforo_color(200000, 100000, 50000) as "ventas_200k_AMARILLO",
    get_semaforo_color(265000, 100000, 50000) as "ventas_265k_VERDE",
    get_semaforo_color(300000, 100000, 50000) as "ventas_300k_VERDE";


-- ============================================================
-- PARTE 5: TABLA USER_PREFERENCES (Ejecutar quinto)
-- ============================================================

CREATE TABLE IF NOT EXISTS user_preferences (
    id SERIAL PRIMARY KEY,
    user_id INTEGER NOT NULL,
    preference_key VARCHAR(100) NOT NULL,
    preference_value TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(user_id, preference_key)
);

CREATE INDEX IF NOT EXISTS idx_user_preferences_user ON user_preferences(user_id);

-- Verificar
SELECT 'user_preferences creada' as resultado
WHERE EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'user_preferences');


-- ============================================================
-- PARTE 6: TABLA ATTENDANCE_RECORDS (Ejecutar sexto)
-- ============================================================

CREATE TABLE IF NOT EXISTS attendance_records (
    id SERIAL PRIMARY KEY,
    employee_id INTEGER NOT NULL,
    record_date DATE NOT NULL,
    record_type VARCHAR(20) NOT NULL CHECK (record_type IN ('ASISTENCIA', 'RETARDO', 'FALTA', 'VACACION', 'PERMISO', 'INCAPACIDAD')),
    check_in_time TIME,
    check_out_time TIME,
    notes TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    created_by INTEGER,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_by INTEGER,
    UNIQUE(employee_id, record_date)
);

CREATE INDEX IF NOT EXISTS idx_attendance_employee ON attendance_records(employee_id);
CREATE INDEX IF NOT EXISTS idx_attendance_date ON attendance_records(record_date);
CREATE INDEX IF NOT EXISTS idx_attendance_type ON attendance_records(record_type);

-- Verificar
SELECT 'attendance_records creada' as resultado
WHERE EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'attendance_records');


-- ============================================================
-- PARTE 7: TABLA ORDER_GASTOS_OPERATIVOS (Ejecutar séptimo)
-- ============================================================

CREATE TABLE IF NOT EXISTS order_gastos_operativos (
    id SERIAL PRIMARY KEY,
    f_order INTEGER NOT NULL,
    monto NUMERIC(15,2) NOT NULL DEFAULT 0,
    descripcion VARCHAR(255) NOT NULL,
    categoria VARCHAR(50),
    fecha_gasto DATE DEFAULT CURRENT_DATE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    created_by INTEGER,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_by INTEGER
);

CREATE INDEX IF NOT EXISTS idx_gastos_operativos_order ON order_gastos_operativos(f_order);

-- Verificar
SELECT 'order_gastos_operativos creada' as resultado
WHERE EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'order_gastos_operativos');


-- ============================================================
-- PARTE 8: FUNCIONES AUXILIARES (Ejecutar octavo)
-- ============================================================

-- Función para calcular gasto material de una orden
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

-- Función para trigger de updated_at
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Verificar
SELECT 'Funciones creadas' as resultado;


-- ============================================================
-- PARTE 9: VISTA V_ORDER_GASTOS (Ejecutar noveno)
-- ============================================================

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

-- Verificar
SELECT 'v_order_gastos creada' as resultado
WHERE EXISTS (SELECT 1 FROM information_schema.views WHERE table_name = 'v_order_gastos');


-- ============================================================
-- PARTE 10: VERIFICACIÓN FINAL (Ejecutar al final)
-- ============================================================

SELECT '=== VERIFICACIÓN MIGRACIÓN v2.0 ===' as seccion;

SELECT '1. Roles migrados:' as verificacion;
SELECT role, COUNT(*) as cantidad FROM users GROUP BY role ORDER BY role;

SELECT '2. Columnas en t_order:' as verificacion;
SELECT column_name, data_type
FROM information_schema.columns
WHERE table_name = 't_order'
AND column_name IN ('gasto_operativo', 'gasto_indirecto', 'dias_estimados');

SELECT '3. Tablas nuevas:' as verificacion;
SELECT table_name
FROM information_schema.tables
WHERE table_schema = 'public'
AND table_name IN ('system_config', 'user_preferences', 'attendance_records', 'order_gastos_operativos')
ORDER BY table_name;

SELECT '4. Configuración semáforo:' as verificacion;
SELECT config_key, config_value FROM system_config WHERE config_key LIKE 'semaforo%';

SELECT '5. Test semáforo (nomina=100k, gasto_fijo=50k):' as verificacion;
SELECT
    get_semaforo_color(0, 100000, 50000) as "0=ROJO",
    get_semaforo_color(165000, 100000, 50000) as "165k=AMARILLO",
    get_semaforo_color(265000, 100000, 50000) as "265k=VERDE";

SELECT '6. Vista v_order_gastos:' as verificacion;
SELECT COUNT(*) as ordenes_en_vista FROM v_order_gastos;

SELECT '=== MIGRACIÓN COMPLETADA ===' as resultado;
