-- =============================================
-- B5E: Busqueda Scoped + Arbol de Seleccion
-- =============================================

-- RPC 1: search_in_folder
-- Busca carpetas y archivos dentro de una carpeta (y sus descendientes).
-- Si p_folder_id es NULL, busca en todo el Drive (global).
CREATE OR REPLACE FUNCTION search_in_folder(p_folder_id INT DEFAULT NULL, p_query TEXT DEFAULT '')
RETURNS TABLE (
    result_type TEXT,
    id INT,
    parent_id INT,
    folder_id INT,
    name TEXT,
    linked_order_id INT,
    file_size BIGINT,
    content_type TEXT,
    uploaded_by INT,
    uploaded_at TIMESTAMPTZ,
    storage_path TEXT,
    created_at TIMESTAMPTZ
) LANGUAGE plpgsql AS $$
BEGIN
    IF p_folder_id IS NULL THEN
        -- Global search (root)
        RETURN QUERY
        SELECT 'folder'::TEXT, f.id, f.parent_id, NULL::INT, f.name::TEXT, f.linked_order_id,
               NULL::BIGINT, NULL::TEXT, NULL::INT, NULL::TIMESTAMPTZ, NULL::TEXT, f.created_at
        FROM drive_folders f
        WHERE f.name ILIKE '%' || p_query || '%'
        ORDER BY f.name
        LIMIT 30;

        RETURN QUERY
        SELECT 'file'::TEXT, df.id, NULL::INT, df.folder_id, df.file_name::TEXT, NULL::INT,
               df.file_size, df.content_type::TEXT, df.uploaded_by, df.uploaded_at, df.storage_path::TEXT, NULL::TIMESTAMPTZ
        FROM drive_files df
        WHERE df.file_name ILIKE '%' || p_query || '%'
        ORDER BY df.uploaded_at DESC
        LIMIT 50;
    ELSE
        -- Scoped search: get all descendant folder IDs via recursive CTE
        RETURN QUERY
        WITH RECURSIVE descendants AS (
            SELECT p_folder_id AS did
            UNION ALL
            SELECT f.id FROM drive_folders f JOIN descendants d ON f.parent_id = d.did
        )
        SELECT 'folder'::TEXT, f.id, f.parent_id, NULL::INT, f.name::TEXT, f.linked_order_id,
               NULL::BIGINT, NULL::TEXT, NULL::INT, NULL::TIMESTAMPTZ, NULL::TEXT, f.created_at
        FROM drive_folders f
        WHERE f.id IN (SELECT did FROM descendants)
          AND f.id != p_folder_id  -- exclude the search root itself
          AND f.name ILIKE '%' || p_query || '%'
        ORDER BY f.name
        LIMIT 30;

        RETURN QUERY
        WITH RECURSIVE descendants AS (
            SELECT p_folder_id AS did
            UNION ALL
            SELECT f.id FROM drive_folders f JOIN descendants d ON f.parent_id = d.did
        )
        SELECT 'file'::TEXT, df.id, NULL::INT, df.folder_id, df.file_name::TEXT, NULL::INT,
               df.file_size, df.content_type::TEXT, df.uploaded_by, df.uploaded_at, df.storage_path::TEXT, NULL::TIMESTAMPTZ
        FROM drive_files df
        WHERE df.folder_id IN (SELECT did FROM descendants)
          AND df.file_name ILIKE '%' || p_query || '%'
        ORDER BY df.uploaded_at DESC
        LIMIT 50;
    END IF;
END;
$$;

-- RPC 2: validate_folder_link
-- Valida si una carpeta puede ser vinculada a una orden.
-- Reglas:
--   R2: Si un ANCESTRO tiene linked_order_id -> BLOQUEADO (estas dentro de carpeta vinculada)
--   R3: Si un DESCENDIENTE tiene linked_order_id -> BLOQUEADO (contiene carpetas vinculadas)
--   R5 soft: Si tiene subcarpetas sin vinculos -> WARNING con conteo
-- Retorna: can_link (bool), block_reason (text), warning_message (text)
CREATE OR REPLACE FUNCTION validate_folder_link(p_folder_id INT)
RETURNS TABLE (
    can_link BOOLEAN,
    block_reason TEXT,
    warning_message TEXT,
    descendant_folder_count INT,
    linked_descendant_count INT
) LANGUAGE plpgsql STABLE AS $$
DECLARE
    v_folder_parent_id INT;
    v_ancestor_linked_name TEXT;
    v_ancestor_linked_order INT;
    v_descendant_count INT;
    v_linked_descendants INT;
    v_linked_desc_names TEXT;
BEGIN
    -- R0: Prevent linking the root folder itself (structural, never linkable)
    SELECT parent_id INTO v_folder_parent_id FROM drive_folders WHERE id = p_folder_id;
    IF v_folder_parent_id IS NULL THEN
        RETURN QUERY SELECT FALSE,
            'La carpeta raiz no puede vincularse a una orden.'::TEXT,
            NULL::TEXT, 0, 0;
        RETURN;
    END IF;

    -- R2: Check ancestors (walk UP from parent)
    -- Skip root folders (parent_id IS NULL) — they are structural and should never block
    SELECT f.name, f.linked_order_id INTO v_ancestor_linked_name, v_ancestor_linked_order
    FROM (
        WITH RECURSIVE ancestors AS (
            SELECT parent_id FROM drive_folders WHERE id = p_folder_id
            UNION ALL
            SELECT f.parent_id FROM drive_folders f JOIN ancestors a ON f.id = a.parent_id
            WHERE a.parent_id IS NOT NULL
        )
        SELECT df.name, df.linked_order_id
        FROM ancestors a
        JOIN drive_folders df ON df.id = a.parent_id
        WHERE df.linked_order_id IS NOT NULL
          AND df.parent_id IS NOT NULL  -- exclude root from blocking
        LIMIT 1
    ) f;

    IF v_ancestor_linked_order IS NOT NULL THEN
        RETURN QUERY SELECT
            FALSE,
            format('Esta carpeta esta dentro de "%s" que ya esta vinculada a una orden.', v_ancestor_linked_name),
            NULL::TEXT, 0, 0;
        RETURN;
    END IF;

    -- R3 + R5: Check descendants (walk DOWN)
    WITH RECURSIVE descendants AS (
        SELECT id FROM drive_folders WHERE parent_id = p_folder_id
        UNION ALL
        SELECT f.id FROM drive_folders f JOIN descendants d ON f.parent_id = d.id
    )
    SELECT
        COUNT(*)::INT,
        COUNT(*) FILTER (WHERE df.linked_order_id IS NOT NULL)::INT,
        string_agg(df.name, ', ' ORDER BY df.name) FILTER (WHERE df.linked_order_id IS NOT NULL)
    INTO v_descendant_count, v_linked_descendants, v_linked_desc_names
    FROM descendants d
    JOIN drive_folders df ON df.id = d.id;

    -- R3: Block if descendants have links
    IF v_linked_descendants > 0 THEN
        RETURN QUERY SELECT
            FALSE,
            format('Contiene %s carpeta(s) ya vinculada(s): %s', v_linked_descendants, v_linked_desc_names),
            NULL::TEXT, v_descendant_count, v_linked_descendants;
        RETURN;
    END IF;

    -- R5 soft: Warning if has subcarpetas (but no links)
    IF v_descendant_count > 0 THEN
        RETURN QUERY SELECT
            TRUE,
            NULL::TEXT,
            format('Esta carpeta tiene %s subcarpeta(s) que quedaran bajo esta orden.', v_descendant_count),
            v_descendant_count, 0;
        RETURN;
    END IF;

    -- All clear
    RETURN QUERY SELECT TRUE, NULL::TEXT, NULL::TEXT, 0, 0;
END;
$$;

-- DATA FIX: Clear linked_order_id from root folder if accidentally set
-- Root folders (parent_id IS NULL) are structural and should never be linked to orders.
UPDATE drive_folders SET linked_order_id = NULL WHERE parent_id IS NULL AND linked_order_id IS NOT NULL;

-- RPC 3: get_folder_tree
-- Retorna todas las carpetas para construir el arbol de seleccion.
-- Dataset pequeno (~500 carpetas max), carga eager.
CREATE OR REPLACE FUNCTION get_folder_tree()
RETURNS TABLE (
    id INT,
    parent_id INT,
    name TEXT,
    linked_order_id INT
) LANGUAGE sql STABLE AS $$
    SELECT f.id, f.parent_id, f.name::TEXT, f.linked_order_id
    FROM drive_folders f
    ORDER BY f.parent_id NULLS FIRST, f.name;
$$;
