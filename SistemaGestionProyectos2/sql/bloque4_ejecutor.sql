-- ============================================================
-- BLOQUE 4: Columna "Ejecutor" en Ordenes
-- ============================================================
-- Tabla relacional many-to-many entre ordenes y empleados de nomina
-- Permite asignar multiples ejecutores a cada orden
-- ============================================================

-- Tabla relacional orden-ejecutor
CREATE TABLE IF NOT EXISTS order_ejecutores (
    id SERIAL PRIMARY KEY,
    f_order INTEGER NOT NULL REFERENCES t_order(f_order) ON DELETE CASCADE,
    payroll_id INTEGER NOT NULL REFERENCES t_payroll(f_payroll) ON DELETE CASCADE,
    assigned_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    assigned_by INTEGER REFERENCES users(id),
    UNIQUE(f_order, payroll_id)
);

-- Indexes para consultas rapidas
CREATE INDEX IF NOT EXISTS idx_order_ejecutores_order ON order_ejecutores(f_order);
CREATE INDEX IF NOT EXISTS idx_order_ejecutores_payroll ON order_ejecutores(payroll_id);

-- Vista helper: nombres concatenados por orden
CREATE OR REPLACE VIEW v_order_ejecutores AS
SELECT
    oe.f_order,
    STRING_AGG(p.f_employee, ', ' ORDER BY p.f_employee) as ejecutores_nombre,
    ARRAY_AGG(oe.payroll_id) as ejecutores_ids
FROM order_ejecutores oe
JOIN t_payroll p ON oe.payroll_id = p.f_payroll
GROUP BY oe.f_order;

-- Habilitar RLS (Row Level Security) - lectura publica, escritura controlada por app
ALTER TABLE order_ejecutores ENABLE ROW LEVEL SECURITY;

CREATE POLICY "order_ejecutores_select" ON order_ejecutores
    FOR SELECT USING (true);

CREATE POLICY "order_ejecutores_insert" ON order_ejecutores
    FOR INSERT WITH CHECK (true);

CREATE POLICY "order_ejecutores_delete" ON order_ejecutores
    FOR DELETE USING (true);

-- ============================================================
-- VERIFICACION
-- ============================================================
SELECT 'order_ejecutores' as tabla, count(*) as registros FROM order_ejecutores;
SELECT * FROM v_order_ejecutores LIMIT 5;
