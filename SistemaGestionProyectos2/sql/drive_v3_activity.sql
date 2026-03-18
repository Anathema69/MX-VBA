-- ============================================
-- DRIVE V3-B: Actividad y Recientes
-- Ejecutar en Supabase SQL Editor
-- ============================================

CREATE TABLE IF NOT EXISTS drive_activity (
    id SERIAL PRIMARY KEY,
    user_id INTEGER REFERENCES users(id),
    action VARCHAR(20) NOT NULL,
    target_type VARCHAR(10) NOT NULL,
    target_id INTEGER NOT NULL,
    target_name VARCHAR(255),
    folder_id INTEGER REFERENCES drive_folders(id) ON DELETE SET NULL,
    metadata JSONB,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_drive_activity_user ON drive_activity(user_id, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_drive_activity_folder ON drive_activity(folder_id, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_drive_activity_recent ON drive_activity(created_at DESC);
