-- ============================================
-- DRIVE V3-C: Operaciones de Archivos
-- Ejecutar en Supabase SQL Editor
-- ============================================

-- Validar que una carpeta se puede mover a un destino
CREATE OR REPLACE FUNCTION validate_folder_move(
    p_folder_id INTEGER,
    p_target_id INTEGER
) RETURNS TABLE(can_move BOOLEAN, block_reason TEXT) AS $$
DECLARE
    v_is_descendant BOOLEAN;
    v_same_parent BOOLEAN;
BEGIN
    -- No mover a si mismo
    IF p_folder_id = p_target_id THEN
        RETURN QUERY SELECT FALSE, 'No se puede mover una carpeta dentro de si misma'::TEXT;
        RETURN;
    END IF;

    -- Verificar si target es descendiente de folder (crearia ciclo)
    WITH RECURSIVE descendants AS (
        SELECT id FROM drive_folders WHERE parent_id = p_folder_id
        UNION ALL
        SELECT df.id FROM drive_folders df
        JOIN descendants d ON df.parent_id = d.id
    )
    SELECT EXISTS(SELECT 1 FROM descendants WHERE id = p_target_id)
    INTO v_is_descendant;

    IF v_is_descendant THEN
        RETURN QUERY SELECT FALSE, 'No se puede mover una carpeta dentro de sus subcarpetas'::TEXT;
        RETURN;
    END IF;

    -- Verificar si ya esta en el destino (no-op)
    SELECT parent_id = p_target_id INTO v_same_parent
    FROM drive_folders WHERE id = p_folder_id;

    IF v_same_parent THEN
        RETURN QUERY SELECT FALSE, 'La carpeta ya esta en esta ubicacion'::TEXT;
        RETURN;
    END IF;

    RETURN QUERY SELECT TRUE, NULL::TEXT;
END;
$$ LANGUAGE plpgsql;
