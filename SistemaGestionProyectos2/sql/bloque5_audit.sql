-- ============================================
-- BLOQUE 5: Auditoria del modulo ARCHIVOS
-- Ejecutar en Supabase SQL Editor
-- ============================================

-- Tabla de auditoria para todas las acciones del Drive
CREATE TABLE drive_audit (
    id SERIAL PRIMARY KEY,
    action VARCHAR(20) NOT NULL,         -- CREATE, RENAME, DELETE, LINK, UNLINK, UPLOAD
    target_type VARCHAR(10) NOT NULL,    -- FOLDER, FILE
    target_id INTEGER NOT NULL,
    target_name VARCHAR(255),
    folder_id INTEGER,                   -- carpeta donde ocurrio
    old_value TEXT,                       -- valor anterior (rename, unlink)
    new_value TEXT,                       -- valor nuevo (rename, link)
    user_id INTEGER REFERENCES users(id),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_drive_audit_date ON drive_audit(created_at DESC);
CREATE INDEX idx_drive_audit_user ON drive_audit(user_id);

-- Trigger: auditar INSERT en drive_folders
CREATE OR REPLACE FUNCTION fn_audit_drive_folder_insert()
RETURNS TRIGGER AS $$
BEGIN
    INSERT INTO drive_audit (action, target_type, target_id, target_name, folder_id, user_id)
    VALUES ('CREATE', 'FOLDER', NEW.id, NEW.name, NEW.parent_id, NEW.created_by);
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_audit_drive_folder_insert
    AFTER INSERT ON drive_folders
    FOR EACH ROW
    EXECUTE FUNCTION fn_audit_drive_folder_insert();

-- Trigger: auditar UPDATE en drive_folders (rename y link/unlink)
CREATE OR REPLACE FUNCTION fn_audit_drive_folder_update()
RETURNS TRIGGER AS $$
BEGIN
    -- Rename
    IF OLD.name IS DISTINCT FROM NEW.name THEN
        INSERT INTO drive_audit (action, target_type, target_id, target_name, folder_id, old_value, new_value, user_id)
        VALUES ('RENAME', 'FOLDER', NEW.id, NEW.name, NEW.parent_id, OLD.name, NEW.name, NEW.created_by);
    END IF;

    -- Link to order
    IF OLD.linked_order_id IS NULL AND NEW.linked_order_id IS NOT NULL THEN
        INSERT INTO drive_audit (action, target_type, target_id, target_name, folder_id, new_value, user_id)
        VALUES ('LINK', 'FOLDER', NEW.id, NEW.name, NEW.parent_id, NEW.linked_order_id::TEXT, NEW.created_by);
    END IF;

    -- Unlink from order
    IF OLD.linked_order_id IS NOT NULL AND NEW.linked_order_id IS NULL THEN
        INSERT INTO drive_audit (action, target_type, target_id, target_name, folder_id, old_value, user_id)
        VALUES ('UNLINK', 'FOLDER', NEW.id, NEW.name, NEW.parent_id, OLD.linked_order_id::TEXT, NEW.created_by);
    END IF;

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_audit_drive_folder_update
    AFTER UPDATE ON drive_folders
    FOR EACH ROW
    EXECUTE FUNCTION fn_audit_drive_folder_update();

-- Trigger: auditar DELETE en drive_folders
CREATE OR REPLACE FUNCTION fn_audit_drive_folder_delete()
RETURNS TRIGGER AS $$
BEGIN
    INSERT INTO drive_audit (action, target_type, target_id, target_name, folder_id, user_id)
    VALUES ('DELETE', 'FOLDER', OLD.id, OLD.name, OLD.parent_id, OLD.created_by);
    RETURN OLD;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_audit_drive_folder_delete
    BEFORE DELETE ON drive_folders
    FOR EACH ROW
    EXECUTE FUNCTION fn_audit_drive_folder_delete();

-- Trigger: auditar INSERT en drive_files (upload)
CREATE OR REPLACE FUNCTION fn_audit_drive_file_insert()
RETURNS TRIGGER AS $$
BEGIN
    INSERT INTO drive_audit (action, target_type, target_id, target_name, folder_id, new_value, user_id)
    VALUES ('UPLOAD', 'FILE', NEW.id, NEW.file_name, NEW.folder_id, NEW.storage_path, NEW.uploaded_by);
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_audit_drive_file_insert
    AFTER INSERT ON drive_files
    FOR EACH ROW
    EXECUTE FUNCTION fn_audit_drive_file_insert();

-- Trigger: auditar UPDATE en drive_files (rename)
CREATE OR REPLACE FUNCTION fn_audit_drive_file_update()
RETURNS TRIGGER AS $$
BEGIN
    IF OLD.file_name IS DISTINCT FROM NEW.file_name THEN
        INSERT INTO drive_audit (action, target_type, target_id, target_name, folder_id, old_value, new_value, user_id)
        VALUES ('RENAME', 'FILE', NEW.id, NEW.file_name, NEW.folder_id, OLD.file_name, NEW.file_name, NEW.uploaded_by);
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_audit_drive_file_update
    AFTER UPDATE ON drive_files
    FOR EACH ROW
    EXECUTE FUNCTION fn_audit_drive_file_update();

-- Trigger: auditar DELETE en drive_files
CREATE OR REPLACE FUNCTION fn_audit_drive_file_delete()
RETURNS TRIGGER AS $$
BEGIN
    INSERT INTO drive_audit (action, target_type, target_id, target_name, folder_id, old_value, user_id)
    VALUES ('DELETE', 'FILE', OLD.id, OLD.file_name, OLD.folder_id, OLD.storage_path, OLD.uploaded_by);
    RETURN OLD;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_audit_drive_file_delete
    BEFORE DELETE ON drive_files
    FOR EACH ROW
    EXECUTE FUNCTION fn_audit_drive_file_delete();

-- ============================================
-- FIX: Timestamps NULL en inserts desde Postgrest
-- Postgrest envia el campo como null explicito,
-- anulando el DEFAULT. Solucion: trigger BEFORE INSERT.
-- ============================================

CREATE OR REPLACE FUNCTION fn_drive_folders_set_timestamps()
RETURNS TRIGGER AS $$
BEGIN
    IF NEW.created_at IS NULL THEN
        NEW.created_at := CURRENT_TIMESTAMP;
    END IF;
    IF NEW.updated_at IS NULL THEN
        NEW.updated_at := CURRENT_TIMESTAMP;
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_drive_folders_set_timestamps
    BEFORE INSERT ON drive_folders
    FOR EACH ROW
    EXECUTE FUNCTION fn_drive_folders_set_timestamps();

CREATE OR REPLACE FUNCTION fn_drive_files_set_timestamps()
RETURNS TRIGGER AS $$
BEGIN
    IF NEW.uploaded_at IS NULL THEN
        NEW.uploaded_at := CURRENT_TIMESTAMP;
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_drive_files_set_timestamps
    BEFORE INSERT ON drive_files
    FOR EACH ROW
    EXECUTE FUNCTION fn_drive_files_set_timestamps();

-- Retroactivo: Actualizar timestamps NULL existentes
UPDATE drive_folders SET created_at = CURRENT_TIMESTAMP, updated_at = CURRENT_TIMESTAMP WHERE created_at IS NULL;
UPDATE drive_files SET uploaded_at = CURRENT_TIMESTAMP WHERE uploaded_at IS NULL;
