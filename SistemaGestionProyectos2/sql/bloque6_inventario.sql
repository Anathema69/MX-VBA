-- ============================================
-- BLOQUE 6: Modulo de Inventario
-- Ejecutar en Supabase SQL Editor
-- Fecha: 19-Mar-2026
-- ============================================

-- ============================================
-- 1. TABLAS PRINCIPALES
-- ============================================

-- Categorias de inventario
CREATE TABLE IF NOT EXISTS inventory_categories (
    id SERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    description TEXT,
    color VARCHAR(7) DEFAULT '#3498DB',
    icon VARCHAR(50),
    display_order INTEGER DEFAULT 0,
    is_active BOOLEAN DEFAULT TRUE,
    created_by INTEGER REFERENCES users(id),
    updated_by INTEGER REFERENCES users(id),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Productos de inventario
CREATE TABLE IF NOT EXISTS inventory_products (
    id SERIAL PRIMARY KEY,
    category_id INTEGER NOT NULL REFERENCES inventory_categories(id) ON DELETE CASCADE,
    code VARCHAR(50) UNIQUE,
    name VARCHAR(255) NOT NULL,
    description TEXT,
    stock_current NUMERIC(10,2) DEFAULT 0,
    stock_minimum NUMERIC(10,2) DEFAULT 0,
    unit VARCHAR(20) DEFAULT 'pza',
    unit_price NUMERIC(12,2) DEFAULT 0,
    location VARCHAR(100),
    supplier_id INTEGER REFERENCES t_supplier(f_supplier),
    notes TEXT,
    is_active BOOLEAN DEFAULT TRUE,
    created_by INTEGER REFERENCES users(id),
    updated_by INTEGER REFERENCES users(id),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Movimientos de inventario (entradas/salidas/ajustes)
CREATE TABLE IF NOT EXISTS inventory_movements (
    id SERIAL PRIMARY KEY,
    product_id INTEGER NOT NULL REFERENCES inventory_products(id) ON DELETE CASCADE,
    movement_type VARCHAR(20) NOT NULL CHECK (movement_type IN ('entrada', 'salida', 'ajuste')),
    quantity NUMERIC(10,2) NOT NULL,
    previous_stock NUMERIC(10,2),
    new_stock NUMERIC(10,2),
    reference_type VARCHAR(50),
    reference_id INTEGER,
    notes TEXT,
    created_by INTEGER REFERENCES users(id),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Auditoria de inventario (patron invoice_audit / audit_log)
CREATE TABLE IF NOT EXISTS inventory_audit (
    id SERIAL PRIMARY KEY,
    table_name VARCHAR(50) NOT NULL,
    record_id INTEGER NOT NULL,
    action VARCHAR(10) NOT NULL CHECK (action IN ('INSERT', 'UPDATE', 'DELETE')),
    old_values JSONB,
    new_values JSONB,
    user_id INTEGER REFERENCES users(id),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- ============================================
-- 2. INDEXES
-- ============================================

-- Categorias
CREATE INDEX IF NOT EXISTS idx_inv_categories_active
    ON inventory_categories(is_active, display_order);

-- Productos
CREATE INDEX IF NOT EXISTS idx_inv_products_category
    ON inventory_products(category_id)
    WHERE is_active = TRUE;

CREATE INDEX IF NOT EXISTS idx_inv_products_code
    ON inventory_products(code);

CREATE INDEX IF NOT EXISTS idx_inv_products_low_stock
    ON inventory_products(category_id)
    WHERE stock_current < stock_minimum AND is_active = TRUE;

CREATE INDEX IF NOT EXISTS idx_inv_products_location
    ON inventory_products(location)
    WHERE is_active = TRUE;

CREATE INDEX IF NOT EXISTS idx_inv_products_supplier
    ON inventory_products(supplier_id)
    WHERE supplier_id IS NOT NULL;

-- Movimientos
CREATE INDEX IF NOT EXISTS idx_inv_movements_product
    ON inventory_movements(product_id, created_at DESC);

CREATE INDEX IF NOT EXISTS idx_inv_movements_date
    ON inventory_movements(created_at DESC);

CREATE INDEX IF NOT EXISTS idx_inv_movements_type
    ON inventory_movements(movement_type, created_at DESC);

-- Auditoria
CREATE INDEX IF NOT EXISTS idx_inv_audit_table
    ON inventory_audit(table_name, record_id);

CREATE INDEX IF NOT EXISTS idx_inv_audit_date
    ON inventory_audit(created_at DESC);

-- ============================================
-- 3. VISTAS
-- ============================================

-- Productos con stock bajo (para alerta global)
CREATE OR REPLACE VIEW v_inventory_low_stock AS
SELECT
    p.id,
    p.code,
    p.name,
    p.stock_current,
    p.stock_minimum,
    p.unit,
    p.unit_price,
    p.location,
    (p.stock_minimum - p.stock_current) AS cantidad_por_pedir,
    c.id AS category_id,
    c.name AS category_name,
    c.color AS category_color,
    s.f_suppliername AS supplier_name,
    s.f_supplier AS supplier_id
FROM inventory_products p
JOIN inventory_categories c ON p.category_id = c.id
LEFT JOIN t_supplier s ON p.supplier_id = s.f_supplier
WHERE p.stock_current < p.stock_minimum
  AND p.is_active = TRUE
  AND c.is_active = TRUE
ORDER BY (p.stock_minimum - p.stock_current) DESC;

-- Resumen por categoria (para cards en pantalla principal)
CREATE OR REPLACE VIEW v_inventory_category_summary AS
SELECT
    c.id,
    c.name,
    c.description,
    c.color,
    c.icon,
    c.display_order,
    c.created_at,
    COUNT(p.id)::INTEGER AS total_products,
    COALESCE(SUM(p.stock_current), 0)::NUMERIC(12,2) AS total_stock,
    COUNT(CASE WHEN p.stock_current < p.stock_minimum THEN 1 END)::INTEGER AS low_stock_count,
    COALESCE(SUM(p.stock_current * p.unit_price), 0)::NUMERIC(14,2) AS total_value,
    CASE
        WHEN COUNT(p.id) = 0 THEN 100
        ELSE ROUND(
            (COUNT(p.id) - COUNT(CASE WHEN p.stock_current < p.stock_minimum THEN 1 END))::NUMERIC
            / COUNT(p.id) * 100
        )
    END::INTEGER AS health_percent
FROM inventory_categories c
LEFT JOIN inventory_products p ON p.category_id = c.id AND p.is_active = TRUE
WHERE c.is_active = TRUE
GROUP BY c.id, c.name, c.description, c.color, c.icon, c.display_order, c.created_at
ORDER BY c.display_order, c.name;

-- Detalle de movimientos (para historial)
CREATE OR REPLACE VIEW v_inventory_movement_detail AS
SELECT
    m.id,
    m.product_id,
    m.movement_type,
    m.quantity,
    m.previous_stock,
    m.new_stock,
    m.reference_type,
    m.reference_id,
    m.notes,
    m.created_at,
    m.created_by,
    p.code AS product_code,
    p.name AS product_name,
    p.unit AS product_unit,
    c.id AS category_id,
    c.name AS category_name,
    u.full_name AS created_by_name
FROM inventory_movements m
JOIN inventory_products p ON m.product_id = p.id
JOIN inventory_categories c ON p.category_id = c.id
LEFT JOIN users u ON m.created_by = u.id
ORDER BY m.created_at DESC;

-- ============================================
-- 4. FUNCIONES Y TRIGGERS
-- ============================================

-- 4A. Auto-actualizar updated_at en UPDATE
CREATE OR REPLACE FUNCTION fn_inventory_updated_at()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_inventory_categories_updated ON inventory_categories;
CREATE TRIGGER trg_inventory_categories_updated
    BEFORE UPDATE ON inventory_categories
    FOR EACH ROW
    EXECUTE FUNCTION fn_inventory_updated_at();

DROP TRIGGER IF EXISTS trg_inventory_products_updated ON inventory_products;
CREATE TRIGGER trg_inventory_products_updated
    BEFORE UPDATE ON inventory_products
    FOR EACH ROW
    EXECUTE FUNCTION fn_inventory_updated_at();

-- 4B. Auto-registrar movimientos al cambiar stock_current
CREATE OR REPLACE FUNCTION fn_track_inventory_movement()
RETURNS TRIGGER AS $$
DECLARE
    v_notes TEXT;
BEGIN
    IF OLD.stock_current IS DISTINCT FROM NEW.stock_current THEN
        -- Leer notas del contexto de sesion (si viene de fn_adjust_stock)
        v_notes := COALESCE(
            NULLIF(current_setting('inventory.movement_notes', TRUE), ''),
            'Ajuste directo de stock'
        );

        INSERT INTO inventory_movements (
            product_id, movement_type, quantity,
            previous_stock, new_stock, notes, created_by
        ) VALUES (
            NEW.id,
            CASE
                WHEN NEW.stock_current > OLD.stock_current THEN 'entrada'
                WHEN NEW.stock_current < OLD.stock_current THEN 'salida'
                ELSE 'ajuste'
            END,
            ABS(NEW.stock_current - OLD.stock_current),
            OLD.stock_current,
            NEW.stock_current,
            v_notes,
            COALESCE(NEW.updated_by, NEW.created_by)
        );
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_track_inventory_movement ON inventory_products;
CREATE TRIGGER trg_track_inventory_movement
    AFTER UPDATE OF stock_current ON inventory_products
    FOR EACH ROW
    EXECUTE FUNCTION fn_track_inventory_movement();

-- 4C. Auditoria completa (INSERT/UPDATE/DELETE) en categorias y productos
CREATE OR REPLACE FUNCTION fn_audit_inventory_changes()
RETURNS TRIGGER AS $$
BEGIN
    IF TG_OP = 'INSERT' THEN
        INSERT INTO inventory_audit (table_name, record_id, action, new_values, user_id)
        VALUES (TG_TABLE_NAME, NEW.id, 'INSERT', to_jsonb(NEW), NEW.created_by);
        RETURN NEW;

    ELSIF TG_OP = 'UPDATE' THEN
        -- Solo auditar si hubo cambios reales (excluir updated_at)
        IF to_jsonb(OLD) - 'updated_at' IS DISTINCT FROM to_jsonb(NEW) - 'updated_at' THEN
            INSERT INTO inventory_audit (table_name, record_id, action, old_values, new_values, user_id)
            VALUES (TG_TABLE_NAME, NEW.id, 'UPDATE', to_jsonb(OLD), to_jsonb(NEW),
                    COALESCE(NEW.updated_by, NEW.created_by));
        END IF;
        RETURN NEW;

    ELSIF TG_OP = 'DELETE' THEN
        INSERT INTO inventory_audit (table_name, record_id, action, old_values, user_id)
        VALUES (TG_TABLE_NAME, OLD.id, 'DELETE', to_jsonb(OLD),
                COALESCE(OLD.updated_by, OLD.created_by));
        RETURN OLD;
    END IF;

    RETURN NULL;
END;
$$ LANGUAGE plpgsql;

-- Triggers de auditoria
DROP TRIGGER IF EXISTS trg_audit_inventory_categories ON inventory_categories;
CREATE TRIGGER trg_audit_inventory_categories
    AFTER INSERT OR UPDATE OR DELETE ON inventory_categories
    FOR EACH ROW
    EXECUTE FUNCTION fn_audit_inventory_changes();

DROP TRIGGER IF EXISTS trg_audit_inventory_products ON inventory_products;
CREATE TRIGGER trg_audit_inventory_products
    AFTER INSERT OR UPDATE OR DELETE ON inventory_products
    FOR EACH ROW
    EXECUTE FUNCTION fn_audit_inventory_changes();

-- ============================================
-- 5. FUNCIONES RPC (llamadas desde C#)
-- ============================================

-- 5A. Stats globales para KPIs de la pantalla principal
CREATE OR REPLACE FUNCTION fn_get_inventory_stats()
RETURNS JSON AS $$
SELECT json_build_object(
    'total_products', (
        SELECT COUNT(*) FROM inventory_products WHERE is_active = TRUE
    ),
    'total_low_stock', (
        SELECT COUNT(*) FROM inventory_products
        WHERE stock_current < stock_minimum AND is_active = TRUE
    ),
    'total_categories', (
        SELECT COUNT(*) FROM inventory_categories WHERE is_active = TRUE
    ),
    'total_value', (
        SELECT COALESCE(SUM(stock_current * unit_price), 0)
        FROM inventory_products WHERE is_active = TRUE
    )
);
$$ LANGUAGE sql STABLE;

-- 5B. Ajuste seguro de stock con validacion (transaccional)
CREATE OR REPLACE FUNCTION fn_adjust_stock(
    p_product_id INTEGER,
    p_new_stock NUMERIC(10,2),
    p_user_id INTEGER,
    p_notes TEXT DEFAULT NULL
)
RETURNS JSON AS $$
DECLARE
    v_product inventory_products%ROWTYPE;
BEGIN
    -- Obtener producto actual
    SELECT * INTO v_product
    FROM inventory_products
    WHERE id = p_product_id AND is_active = TRUE;

    IF NOT FOUND THEN
        RETURN json_build_object('success', false, 'error', 'Producto no encontrado');
    END IF;

    -- Validar
    IF p_new_stock < 0 THEN
        RETURN json_build_object('success', false, 'error', 'El stock no puede ser negativo');
    END IF;

    IF p_new_stock = v_product.stock_current THEN
        RETURN json_build_object('success', true, 'message', 'Sin cambios');
    END IF;

    -- Pasar notas al trigger via variable de sesion (transaction-scoped)
    PERFORM set_config(
        'inventory.movement_notes',
        COALESCE(p_notes, 'Ajuste via sistema'),
        TRUE  -- is_local = transaction scope
    );

    -- Actualizar stock (el trigger fn_track_inventory_movement crea el movimiento)
    UPDATE inventory_products
    SET stock_current = p_new_stock, updated_by = p_user_id
    WHERE id = p_product_id;

    -- Limpiar variable de sesion
    PERFORM set_config('inventory.movement_notes', '', TRUE);

    RETURN json_build_object(
        'success', true,
        'previous_stock', v_product.stock_current,
        'new_stock', p_new_stock,
        'movement_type', CASE
            WHEN p_new_stock > v_product.stock_current THEN 'entrada'
            ELSE 'salida'
        END,
        'quantity', ABS(p_new_stock - v_product.stock_current)
    );
END;
$$ LANGUAGE plpgsql;

-- 5C. Ubicaciones distintas (para filtro dinamico en UI)
CREATE OR REPLACE FUNCTION fn_get_inventory_locations(p_category_id INTEGER DEFAULT NULL)
RETURNS SETOF VARCHAR AS $$
SELECT DISTINCT location
FROM inventory_products
WHERE is_active = TRUE
  AND location IS NOT NULL
  AND location != ''
  AND (p_category_id IS NULL OR category_id = p_category_id)
ORDER BY location;
$$ LANGUAGE sql STABLE;

-- ============================================
-- 6. DATOS SEMILLA (opcional, para testing)
-- ============================================

-- Descomentar para insertar datos de prueba:
/*
INSERT INTO inventory_categories (name, description, color, display_order, created_by) VALUES
    ('TORNILLERIA', 'Tornillos, tuercas y arandelas', '#3B82F6', 1, 1),
    ('CABLEADO', 'Cables electricos y de datos', '#10B981', 2, 1),
    ('CONECTORES', 'Conectores industriales y terminales', '#8B5CF6', 3, 1),
    ('HERRAMIENTAS', 'Herramientas manuales y electricas', '#F59E0B', 4, 1),
    ('SENSORES', 'Sensores de proximidad, temperatura', '#EC4899', 5, 1),
    ('MOTORES', 'Motores AC, DC y paso a paso', '#EF4444', 6, 1);

INSERT INTO inventory_products (category_id, code, name, stock_current, stock_minimum, unit, unit_price, location, created_by) VALUES
    (1, 'TOR-001', 'Tornillo M3x10',     150,  50, 'pza', 0.50, 'A-1', 1),
    (1, 'TOR-002', 'Tornillo M4x15',      20,  30, 'pza', 0.75, 'A-1', 1),
    (1, 'TOR-003', 'Tornillo M5x20',     200, 100, 'pza', 1.00, 'A-2', 1),
    (1, 'TOR-004', 'Tuerca M3',            45,  50, 'pza', 0.30, 'A-1', 1),
    (1, 'TOR-005', 'Arandela M3',         500, 100, 'pza', 0.10, 'A-3', 1),
    (1, 'TOR-006', 'Tornillo Allen M6',    80,  20, 'pza', 1.50, 'A-2', 1),
    (1, 'TOR-007', 'Perno M8x30',          10,  25, 'pza', 2.00, 'B-1', 1),
    (1, 'TOR-008', 'Rondana plana M4',    300,  50, 'pza', 0.15, 'A-1', 1);
*/

-- ============================================
-- VERIFICACION
-- ============================================
-- Ejecutar despues de crear todo:
-- SELECT * FROM v_inventory_category_summary;
-- SELECT * FROM v_inventory_low_stock;
-- SELECT fn_get_inventory_stats();
-- SELECT * FROM fn_get_inventory_locations();
