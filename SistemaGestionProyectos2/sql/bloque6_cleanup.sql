-- ============================================
-- LIMPIEZA: Borrar todos los datos de inventario
-- Ejecutar en Supabase SQL Editor
-- ============================================

-- Desactivar solo triggers de usuario (no system triggers)
ALTER TABLE inventory_products DISABLE TRIGGER trg_audit_inventory_products;
ALTER TABLE inventory_products DISABLE TRIGGER trg_track_inventory_movement;
ALTER TABLE inventory_products DISABLE TRIGGER trg_inventory_products_updated;
ALTER TABLE inventory_categories DISABLE TRIGGER trg_audit_inventory_categories;
ALTER TABLE inventory_categories DISABLE TRIGGER trg_inventory_categories_updated;

-- Borrar en orden (hijos primero)
DELETE FROM inventory_audit;
DELETE FROM inventory_movements;
DELETE FROM inventory_products;
DELETE FROM inventory_categories;

-- Resetear secuencias
ALTER SEQUENCE inventory_audit_id_seq RESTART WITH 1;
ALTER SEQUENCE inventory_movements_id_seq RESTART WITH 1;
ALTER SEQUENCE inventory_products_id_seq RESTART WITH 1;
ALTER SEQUENCE inventory_categories_id_seq RESTART WITH 1;

-- Reactivar triggers
ALTER TABLE inventory_products ENABLE TRIGGER trg_audit_inventory_products;
ALTER TABLE inventory_products ENABLE TRIGGER trg_track_inventory_movement;
ALTER TABLE inventory_products ENABLE TRIGGER trg_inventory_products_updated;
ALTER TABLE inventory_categories ENABLE TRIGGER trg_audit_inventory_categories;
ALTER TABLE inventory_categories ENABLE TRIGGER trg_inventory_categories_updated;

-- Verificar
SELECT 'inventory_categories' AS tabla, COUNT(*) AS registros FROM inventory_categories
UNION ALL SELECT 'inventory_products', COUNT(*) FROM inventory_products
UNION ALL SELECT 'inventory_movements', COUNT(*) FROM inventory_movements
UNION ALL SELECT 'inventory_audit', COUNT(*) FROM inventory_audit;
