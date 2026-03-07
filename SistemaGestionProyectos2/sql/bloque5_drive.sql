-- ============================================
-- BLOQUE 5: Modulo ARCHIVOS (Drive IMA)
-- Ejecutar en Supabase SQL Editor
-- ============================================

-- Carpetas (estructura de arbol auto-referencial)
CREATE TABLE drive_folders (
    id SERIAL PRIMARY KEY,
    parent_id INTEGER REFERENCES drive_folders(id) ON DELETE CASCADE,
    name VARCHAR(255) NOT NULL,
    linked_order_id INTEGER REFERENCES t_order(f_order) ON DELETE SET NULL,
    created_by INTEGER REFERENCES users(id),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(parent_id, name)
);

-- Archivos (metadatos en BD, blob en Cloudflare R2)
CREATE TABLE drive_files (
    id SERIAL PRIMARY KEY,
    folder_id INTEGER NOT NULL REFERENCES drive_folders(id) ON DELETE CASCADE,
    file_name VARCHAR(255) NOT NULL,
    storage_path TEXT NOT NULL,
    file_size BIGINT,
    content_type VARCHAR(100),
    uploaded_by INTEGER REFERENCES users(id),
    uploaded_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(folder_id, file_name)
);

-- Indexes
CREATE INDEX idx_drive_folders_parent ON drive_folders(parent_id);
CREATE INDEX idx_drive_folders_order ON drive_folders(linked_order_id) WHERE linked_order_id IS NOT NULL;
CREATE INDEX idx_drive_files_folder ON drive_files(folder_id);

-- Trigger para updated_at en carpetas
CREATE OR REPLACE FUNCTION fn_drive_folders_updated_at()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_drive_folders_updated_at
    BEFORE UPDATE ON drive_folders
    FOR EACH ROW
    EXECUTE FUNCTION fn_drive_folders_updated_at();

-- Carpeta raiz inicial
INSERT INTO drive_folders (parent_id, name, created_by)
VALUES (NULL, 'IMA MECATRONICA', NULL);

-- Funcion: obtener breadcrumb (ruta desde raiz hasta carpeta)
CREATE OR REPLACE FUNCTION get_folder_breadcrumb(p_folder_id INTEGER)
RETURNS TABLE(id INTEGER, name VARCHAR, parent_id INTEGER, depth INTEGER) AS $$
WITH RECURSIVE breadcrumb AS (
    SELECT df.id, df.name, df.parent_id, 0 AS depth
    FROM drive_folders df
    WHERE df.id = p_folder_id

    UNION ALL

    SELECT df.id, df.name, df.parent_id, b.depth + 1
    FROM drive_folders df
    JOIN breadcrumb b ON df.id = b.parent_id
)
SELECT b.id, b.name, b.parent_id, b.depth
FROM breadcrumb b
ORDER BY b.depth DESC;
$$ LANGUAGE sql STABLE;

-- Funcion: contar elementos hijos (carpetas + archivos) de una carpeta
CREATE OR REPLACE FUNCTION get_folder_child_count(p_folder_id INTEGER)
RETURNS INTEGER AS $$
    SELECT (
        (SELECT COUNT(*) FROM drive_folders WHERE parent_id = p_folder_id) +
        (SELECT COUNT(*) FROM drive_files WHERE folder_id = p_folder_id)
    )::INTEGER;
$$ LANGUAGE sql STABLE;
