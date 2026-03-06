-- =====================================================
-- Bloque 3: Portal Ventas con subida de archivos
-- Tabla order_files + bucket order-files
-- =====================================================

-- 1. Tabla para registrar archivos subidos por orden/comision
CREATE TABLE IF NOT EXISTS order_files (
    id SERIAL PRIMARY KEY,
    f_order INTEGER NOT NULL REFERENCES t_order(f_order) ON DELETE CASCADE,
    file_name VARCHAR(255) NOT NULL,
    storage_path TEXT NOT NULL,
    file_size BIGINT,
    content_type VARCHAR(100),
    uploaded_by INTEGER REFERENCES users(id),
    vendor_id INTEGER REFERENCES t_vendor(f_vendor),
    commission_id INTEGER REFERENCES t_vendor_commission_payment(id),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- 2. Indexes
CREATE INDEX IF NOT EXISTS idx_order_files_order ON order_files(f_order);
CREATE INDEX IF NOT EXISTS idx_order_files_commission ON order_files(commission_id);
CREATE INDEX IF NOT EXISTS idx_order_files_vendor ON order_files(vendor_id);

-- 3. Crear bucket de storage (ejecutar en Supabase Dashboard > Storage)
-- INSERT INTO storage.buckets (id, name, public) VALUES ('order-files', 'order-files', false);

-- 4. RLS policies para el bucket (ejecutar en Supabase Dashboard > SQL Editor)
-- Nota: Estas policies se aplican via la API de Supabase Storage.
-- Como usamos service_role key desde la app de escritorio, RLS no aplica.
-- Si en el futuro se usa anon key, descomentar:

-- CREATE POLICY "Authenticated users can upload" ON storage.objects
--   FOR INSERT WITH CHECK (bucket_id = 'order-files');

-- CREATE POLICY "Authenticated users can read" ON storage.objects
--   FOR SELECT USING (bucket_id = 'order-files');

-- CREATE POLICY "Authenticated users can delete own files" ON storage.objects
--   FOR DELETE USING (bucket_id = 'order-files');
