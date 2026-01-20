-- ============================================================
-- SCRIPT: Setup BD Staging - IMA Mecatrónica
-- Propósito: Crear estructura de BD para ambiente de pruebas
-- Fecha: Enero 2026
-- ============================================================
-- INSTRUCCIONES:
--   1. Crear proyecto nuevo en Supabase
--   2. Ir a SQL Editor
--   3. Ejecutar este script COMPLETO
--   4. Verificar que no haya errores
-- ============================================================

-- Habilitar extensiones necesarias
CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- ============================================================
-- PASO 1: TABLAS SIN DEPENDENCIAS (en orden)
-- ============================================================

-- Tabla de usuarios (base para todo)
CREATE TABLE IF NOT EXISTS users (
    id SERIAL PRIMARY KEY,
    username VARCHAR(100) NOT NULL UNIQUE,
    email VARCHAR(255) NOT NULL UNIQUE,
    password_hash VARCHAR(255) NOT NULL,
    full_name VARCHAR(255) NOT NULL,
    role VARCHAR(50) NOT NULL CHECK (role IN ('admin', 'coordinator', 'salesperson')),
    is_active BOOLEAN DEFAULT true,
    last_login TIMESTAMP,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Catálogo de estados de orden
CREATE TABLE IF NOT EXISTS order_status (
    f_orderstatus SERIAL PRIMARY KEY,
    f_name VARCHAR(100) NOT NULL,
    is_active BOOLEAN DEFAULT true,
    display_order INTEGER DEFAULT 0,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Catálogo de estados de factura
CREATE TABLE IF NOT EXISTS invoice_status (
    f_invoicestat SERIAL PRIMARY KEY,
    f_name VARCHAR(100) NOT NULL,
    is_active BOOLEAN DEFAULT true,
    display_order INTEGER DEFAULT 0,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Clientes
CREATE TABLE IF NOT EXISTS t_client (
    f_client SERIAL PRIMARY KEY,
    f_name VARCHAR(255) NOT NULL,
    f_address1 VARCHAR(255),
    f_address2 VARCHAR(255),
    f_credit INTEGER DEFAULT 0,
    tax_id VARCHAR(50),
    phone VARCHAR(50),
    email VARCHAR(255),
    is_active BOOLEAN DEFAULT true,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    created_by INTEGER REFERENCES users(id),
    updated_by INTEGER REFERENCES users(id)
);

-- Proveedores
CREATE TABLE IF NOT EXISTS t_supplier (
    f_supplier SERIAL PRIMARY KEY,
    f_suppliername VARCHAR(255) NOT NULL,
    f_supplieraddress VARCHAR(255),
    f_supplierphone VARCHAR(50),
    f_supplieremail VARCHAR(255),
    tax_id VARCHAR(50),
    payment_terms VARCHAR(100),
    is_active BOOLEAN DEFAULT true,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Vendedores
CREATE TABLE IF NOT EXISTS t_vendor (
    f_vendor SERIAL PRIMARY KEY,
    f_vendorname VARCHAR(255) NOT NULL,
    f_vendorphone VARCHAR(50),
    f_vendoremail VARCHAR(255),
    f_vendorrate NUMERIC(5,2) DEFAULT 0,
    is_active BOOLEAN DEFAULT true,
    user_id INTEGER REFERENCES users(id),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Contactos de clientes
CREATE TABLE IF NOT EXISTS t_contact (
    f_contact SERIAL PRIMARY KEY,
    f_client INTEGER REFERENCES t_client(f_client),
    f_contactname VARCHAR(255),
    f_email VARCHAR(255),
    f_phone VARCHAR(50),
    position VARCHAR(100),
    is_primary BOOLEAN DEFAULT false,
    is_active BOOLEAN DEFAULT true,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- ============================================================
-- PASO 2: TABLAS CORE CON DEPENDENCIAS
-- ============================================================

-- Órdenes (tabla principal)
CREATE TABLE IF NOT EXISTS t_order (
    f_order SERIAL PRIMARY KEY,
    f_client INTEGER REFERENCES t_client(f_client),
    f_contact INTEGER REFERENCES t_contact(f_contact),
    f_quote VARCHAR(100),
    f_po VARCHAR(100),
    f_podate DATE,
    f_estdelivery DATE,
    f_description TEXT,
    f_salesubtotal NUMERIC(15,2) DEFAULT 0,
    f_saletotal NUMERIC(15,2) DEFAULT 0,
    f_orderstat INTEGER REFERENCES order_status(f_orderstatus),
    f_expense NUMERIC(15,2) DEFAULT 0,
    actual_delivery DATE,
    profit_amount NUMERIC(15,2),
    progress_percentage INTEGER DEFAULT 0 CHECK (progress_percentage >= 0 AND progress_percentage <= 100),
    order_percentage INTEGER DEFAULT 0 CHECK (order_percentage >= 0 AND order_percentage <= 100),
    invoiced BOOLEAN DEFAULT false,
    last_invoice_date DATE,
    f_salesman INTEGER REFERENCES t_vendor(f_vendor),
    f_commission_rate NUMERIC(5,2),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    created_by INTEGER REFERENCES users(id),
    updated_by INTEGER REFERENCES users(id)
);

-- Facturas
CREATE TABLE IF NOT EXISTS t_invoice (
    f_invoice SERIAL PRIMARY KEY,
    f_order INTEGER REFERENCES t_order(f_order),
    f_folio VARCHAR(100),
    f_invoicedate DATE,
    f_receptiondate DATE,
    f_subtotal NUMERIC(15,2) DEFAULT 0,
    f_total NUMERIC(15,2) DEFAULT 0,
    f_downpayment VARCHAR(100),
    f_invoicestat INTEGER REFERENCES invoice_status(f_invoicestat),
    f_paymentdate DATE,
    f_paymentmethod VARCHAR(100),
    notes TEXT,
    due_date DATE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    created_by INTEGER REFERENCES users(id),
    updated_by INTEGER REFERENCES users(id)
);

-- Gastos/Cuentas por pagar
CREATE TABLE IF NOT EXISTS t_expense (
    f_expense SERIAL PRIMARY KEY,
    f_supplier INTEGER REFERENCES t_supplier(f_supplier),
    f_description VARCHAR(255),
    f_expensedate DATE,
    f_totalexpense NUMERIC(15,2) DEFAULT 0,
    f_status VARCHAR(50),
    f_paiddate DATE,
    f_paymethod VARCHAR(100),
    f_order INTEGER REFERENCES t_order(f_order),
    f_scheduleddate DATE,
    expense_category VARCHAR(100),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    created_by INTEGER REFERENCES users(id)
);

-- Comisiones de vendedores
CREATE TABLE IF NOT EXISTS t_vendor_commission_payment (
    id SERIAL PRIMARY KEY,
    f_order INTEGER NOT NULL REFERENCES t_order(f_order),
    f_vendor INTEGER NOT NULL REFERENCES t_vendor(f_vendor),
    commission_amount NUMERIC(15,2) NOT NULL,
    commission_rate NUMERIC(5,2) NOT NULL,
    payment_status VARCHAR(20) NOT NULL DEFAULT 'draft' CHECK (payment_status IN ('draft', 'pending', 'paid')),
    paid_date DATE,
    payment_reference VARCHAR(255),
    notes TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    created_by INTEGER REFERENCES users(id),
    updated_by INTEGER REFERENCES users(id)
);

-- ============================================================
-- PASO 3: TABLAS DE NÓMINA
-- ============================================================

CREATE TABLE IF NOT EXISTS t_payroll (
    f_payroll SERIAL PRIMARY KEY,
    f_employee VARCHAR(255),
    f_title VARCHAR(100),
    f_monthlypayroll NUMERIC(15,2) DEFAULT 0,
    f_department VARCHAR(100),
    f_hireddate DATE,
    f_email VARCHAR(255),
    f_phone VARCHAR(50),
    is_active BOOLEAN DEFAULT true,
    bank_account VARCHAR(50),
    bank_name VARCHAR(100),
    emergency_contact VARCHAR(255),
    emergency_phone VARCHAR(50),
    address TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    created_by INTEGER REFERENCES users(id),
    updated_by INTEGER REFERENCES users(id)
);

CREATE TABLE IF NOT EXISTS t_payroll_history (
    id SERIAL PRIMARY KEY,
    f_payroll INTEGER REFERENCES t_payroll(f_payroll),
    f_employee VARCHAR(255),
    f_title VARCHAR(100),
    f_monthlypayroll NUMERIC(15,2),
    f_department VARCHAR(100),
    f_hireddate DATE,
    f_email VARCHAR(255),
    f_phone VARCHAR(50),
    is_active BOOLEAN,
    bank_account VARCHAR(50),
    bank_name VARCHAR(100),
    emergency_contact VARCHAR(255),
    emergency_phone VARCHAR(50),
    address TEXT,
    effective_date DATE NOT NULL,
    change_type VARCHAR(50),
    change_summary TEXT,
    created_by INTEGER REFERENCES users(id),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS t_overtime_hours (
    id SERIAL PRIMARY KEY,
    year INTEGER NOT NULL,
    month INTEGER NOT NULL,
    amount NUMERIC(15,2) DEFAULT 0,
    notes TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    created_by INTEGER REFERENCES users(id),
    updated_by INTEGER REFERENCES users(id),
    UNIQUE(year, month)
);

CREATE TABLE IF NOT EXISTS t_overtime_hours_audit (
    id SERIAL PRIMARY KEY,
    overtime_id INTEGER NOT NULL REFERENCES t_overtime_hours(id),
    year INTEGER NOT NULL,
    month INTEGER NOT NULL,
    old_amount NUMERIC(15,2),
    new_amount NUMERIC(15,2),
    old_notes TEXT,
    new_notes TEXT,
    changed_by INTEGER REFERENCES users(id),
    changed_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS t_payrollovertime (
    f_payrollovertime SERIAL PRIMARY KEY,
    f_payroll INTEGER REFERENCES t_payroll(f_payroll),
    f_year INTEGER,
    f_month INTEGER,
    f_overtimehours NUMERIC(10,2) DEFAULT 0,
    f_overtimeamount NUMERIC(15,2) DEFAULT 0,
    notes TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    created_by INTEGER REFERENCES users(id)
);

-- ============================================================
-- PASO 4: TABLAS DE GASTOS FIJOS Y BALANCE
-- ============================================================

CREATE TABLE IF NOT EXISTS t_fixed_expenses (
    id SERIAL PRIMARY KEY,
    expense_type VARCHAR(100) NOT NULL,
    description VARCHAR(255),
    monthly_amount NUMERIC(15,2) DEFAULT 0,
    is_active BOOLEAN DEFAULT true,
    effective_date DATE DEFAULT CURRENT_DATE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    created_by INTEGER REFERENCES users(id)
);

CREATE TABLE IF NOT EXISTS t_fixed_expenses_history (
    id SERIAL PRIMARY KEY,
    expense_id INTEGER REFERENCES t_fixed_expenses(id),
    description VARCHAR(255),
    monthly_amount NUMERIC(15,2),
    effective_date DATE NOT NULL,
    change_type VARCHAR(50),
    change_summary TEXT,
    created_by INTEGER REFERENCES users(id),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS t_balance_adjustments (
    id SERIAL PRIMARY KEY,
    year INTEGER NOT NULL,
    month INTEGER NOT NULL,
    adjustment_type VARCHAR(100),
    original_amount NUMERIC(15,2),
    adjusted_amount NUMERIC(15,2),
    difference NUMERIC(15,2),
    reason TEXT,
    created_by INTEGER REFERENCES users(id),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(year, month, adjustment_type)
);

-- ============================================================
-- PASO 5: TABLAS DE AUDITORÍA E HISTORIAL
-- ============================================================

CREATE TABLE IF NOT EXISTS order_history (
    id SERIAL PRIMARY KEY,
    order_id INTEGER NOT NULL REFERENCES t_order(f_order) ON DELETE CASCADE,
    user_id INTEGER NOT NULL REFERENCES users(id),
    action VARCHAR(50) NOT NULL,
    field_name VARCHAR(100),
    old_value TEXT,
    new_value TEXT,
    change_description TEXT,
    ip_address VARCHAR(50),
    changed_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS t_order_deleted (
    id SERIAL PRIMARY KEY,
    original_order_id INTEGER NOT NULL,
    f_client INTEGER,
    f_contact INTEGER,
    f_quote VARCHAR(100),
    f_po VARCHAR(100),
    f_podate DATE,
    f_estdelivery DATE,
    f_description TEXT,
    f_salesubtotal NUMERIC(15,2),
    f_saletotal NUMERIC(15,2),
    f_orderstat INTEGER,
    f_expense NUMERIC(15,2),
    f_salesman INTEGER,
    f_commission_rate NUMERIC(5,2),
    created_at TIMESTAMP,
    deleted_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    deleted_by INTEGER NOT NULL REFERENCES users(id),
    deletion_reason TEXT
);

CREATE TABLE IF NOT EXISTS t_commission_rate_history (
    id SERIAL PRIMARY KEY,
    order_id INTEGER NOT NULL REFERENCES t_order(f_order),
    vendor_id INTEGER NOT NULL REFERENCES t_vendor(f_vendor),
    commission_payment_id INTEGER REFERENCES t_vendor_commission_payment(id) ON DELETE SET NULL,
    old_rate NUMERIC(5,2) NOT NULL,
    old_amount NUMERIC(15,2) NOT NULL,
    new_rate NUMERIC(5,2) NOT NULL,
    new_amount NUMERIC(15,2) NOT NULL,
    order_subtotal NUMERIC(15,2),
    order_number VARCHAR(100),
    vendor_name VARCHAR(255),
    changed_by INTEGER NOT NULL REFERENCES users(id),
    changed_by_name VARCHAR(255),
    changed_at TIMESTAMP DEFAULT now(),
    change_reason TEXT,
    ip_address VARCHAR(50),
    is_vendor_removal BOOLEAN DEFAULT false
);

CREATE TABLE IF NOT EXISTS audit_log (
    id SERIAL PRIMARY KEY,
    user_id INTEGER REFERENCES users(id),
    table_name VARCHAR(100),
    action VARCHAR(50),
    record_id INTEGER,
    old_values JSONB,
    new_values JSONB,
    ip_address VARCHAR(50),
    user_agent TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS invoice_audit (
    id SERIAL PRIMARY KEY,
    invoice_id INTEGER,
    action VARCHAR(50),
    old_values JSONB,
    new_values JSONB,
    user_id INTEGER REFERENCES users(id),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- ============================================================
-- PASO 6: TABLA DE VERSIONES DE APP
-- ============================================================

CREATE TABLE IF NOT EXISTS app_versions (
    id SERIAL PRIMARY KEY,
    version VARCHAR(20) NOT NULL UNIQUE CHECK (version ~ '^\d+\.\d+\.\d+$'),
    release_date TIMESTAMP NOT NULL DEFAULT now(),
    is_latest BOOLEAN NOT NULL DEFAULT false,
    is_mandatory BOOLEAN NOT NULL DEFAULT false,
    download_url TEXT NOT NULL,
    file_size_mb NUMERIC,
    release_notes TEXT,
    min_version VARCHAR(20),
    created_by VARCHAR(100),
    is_active BOOLEAN NOT NULL DEFAULT true,
    downloads_count INTEGER DEFAULT 0,
    changelog JSONB
);

-- ============================================================
-- PASO 7: ÍNDICES IMPORTANTES
-- ============================================================

CREATE INDEX IF NOT EXISTS idx_order_client ON t_order(f_client);
CREATE INDEX IF NOT EXISTS idx_order_status ON t_order(f_orderstat);
CREATE INDEX IF NOT EXISTS idx_order_salesman ON t_order(f_salesman);
CREATE INDEX IF NOT EXISTS idx_order_podate ON t_order(f_podate);
CREATE INDEX IF NOT EXISTS idx_invoice_order ON t_invoice(f_order);
CREATE INDEX IF NOT EXISTS idx_invoice_status ON t_invoice(f_invoicestat);
CREATE INDEX IF NOT EXISTS idx_expense_supplier ON t_expense(f_supplier);
CREATE INDEX IF NOT EXISTS idx_expense_order ON t_expense(f_order);
CREATE INDEX IF NOT EXISTS idx_expense_status ON t_expense(f_status);
CREATE INDEX IF NOT EXISTS idx_contact_client ON t_contact(f_client);
CREATE INDEX IF NOT EXISTS idx_commission_order ON t_vendor_commission_payment(f_order);
CREATE INDEX IF NOT EXISTS idx_commission_vendor ON t_vendor_commission_payment(f_vendor);
CREATE INDEX IF NOT EXISTS idx_order_history_order ON order_history(order_id);
CREATE INDEX IF NOT EXISTS idx_order_deleted_original ON t_order_deleted(original_order_id);

-- ============================================================
-- PASO 8: DATOS INICIALES (CATÁLOGOS)
-- ============================================================

-- Estados de orden
INSERT INTO order_status (f_name, display_order, is_active) VALUES
('EN PROCESO', 1, true),
('COTIZADA', 2, true),
('LIBERADA', 3, true),
('CERRADA', 4, true),
('COMPLETADA', 5, true),
('CANCELADA', 6, true)
ON CONFLICT DO NOTHING;

-- Estados de factura
INSERT INTO invoice_status (f_name, display_order, is_active) VALUES
('CREADA', 1, true),
('PENDIENTE', 2, true),
('VENCIDA', 3, true),
('PAGADA', 4, true),
('CANCELADA', 5, true)
ON CONFLICT DO NOTHING;

-- Usuario admin de prueba
INSERT INTO users (username, email, password_hash, full_name, role, is_active)
VALUES (
    'admin_test',
    'admin_test@ima.com',
    crypt('Test2026!', gen_salt('bf', 11)),
    'Admin de Pruebas',
    'admin',
    true
) ON CONFLICT (username) DO NOTHING;

-- Usuario coordinador de prueba
INSERT INTO users (username, email, password_hash, full_name, role, is_active)
VALUES (
    'coord_test',
    'coord_test@ima.com',
    crypt('Test2026!', gen_salt('bf', 11)),
    'Coordinador de Pruebas',
    'coordinator',
    true
) ON CONFLICT (username) DO NOTHING;

-- Usuario vendedor de prueba
INSERT INTO users (username, email, password_hash, full_name, role, is_active)
VALUES (
    'ventas_test',
    'ventas_test@ima.com',
    crypt('Test2026!', gen_salt('bf', 11)),
    'Vendedor de Pruebas',
    'salesperson',
    true
) ON CONFLICT (username) DO NOTHING;

-- Datos de prueba: Clientes
INSERT INTO t_client (f_name, f_credit, is_active) VALUES
('Cliente de Prueba 1', 30, true),
('Cliente de Prueba 2', 15, true),
('Cliente de Prueba 3', 45, true)
ON CONFLICT DO NOTHING;

-- Datos de prueba: Proveedores
INSERT INTO t_supplier (f_suppliername, is_active) VALUES
('Proveedor de Prueba 1', true),
('Proveedor de Prueba 2', true)
ON CONFLICT DO NOTHING;

-- Datos de prueba: Vendedor
INSERT INTO t_vendor (f_vendorname, f_vendorrate, is_active)
SELECT 'Vendedor de Pruebas', 5.00, true
WHERE NOT EXISTS (SELECT 1 FROM t_vendor WHERE f_vendorname = 'Vendedor de Pruebas');

-- ============================================================
-- PASO 9: FUNCIÓN AUXILIAR PARA UPDATED_AT
-- ============================================================

CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Triggers para updated_at en tablas principales
DROP TRIGGER IF EXISTS trigger_users_updated_at ON users;
CREATE TRIGGER trigger_users_updated_at
    BEFORE UPDATE ON users
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

DROP TRIGGER IF EXISTS trigger_order_updated_at ON t_order;
CREATE TRIGGER trigger_order_updated_at
    BEFORE UPDATE ON t_order
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

DROP TRIGGER IF EXISTS trigger_client_updated_at ON t_client;
CREATE TRIGGER trigger_client_updated_at
    BEFORE UPDATE ON t_client
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

DROP TRIGGER IF EXISTS trigger_invoice_updated_at ON t_invoice;
CREATE TRIGGER trigger_invoice_updated_at
    BEFORE UPDATE ON t_invoice
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

DROP TRIGGER IF EXISTS trigger_expense_updated_at ON t_expense;
CREATE TRIGGER trigger_expense_updated_at
    BEFORE UPDATE ON t_expense
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- ============================================================
-- PASO 10: VERIFICACIÓN FINAL
-- ============================================================

-- Verificar tablas creadas
SELECT table_name
FROM information_schema.tables
WHERE table_schema = 'public'
AND table_type = 'BASE TABLE'
ORDER BY table_name;

-- Verificar usuarios creados
SELECT id, username, full_name, role, is_active FROM users;

-- Verificar catálogos
SELECT 'order_status' as tabla, COUNT(*) as registros FROM order_status
UNION ALL
SELECT 'invoice_status', COUNT(*) FROM invoice_status
UNION ALL
SELECT 't_client', COUNT(*) FROM t_client
UNION ALL
SELECT 't_supplier', COUNT(*) FROM t_supplier
UNION ALL
SELECT 't_vendor', COUNT(*) FROM t_vendor;

-- ============================================================
-- FIN DEL SCRIPT
-- ============================================================
--
-- Credenciales de prueba:
--   admin_test / Test2026!
--   coord_test / Test2026!
--   ventas_test / Test2026!
--
-- Siguiente paso: Ejecutar MIGRACION_v2.sql para probar
-- los cambios de la extensión
-- ============================================================
