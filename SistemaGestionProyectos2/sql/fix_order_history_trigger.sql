-- ============================================================
-- FIX: Mejorar trigger record_order_history
-- ============================================================
-- ANTES: Solo rastreaba f_orderstat, f_po, order_percentage
-- AHORA: Rastrea todos los campos editables importantes
--
-- Campos YA rastreados por OTROS triggers (NO duplicar):
--   f_salesman → update_commission_on_vendor_change
--   f_commission_rate → sync_commission_rate_from_order
-- ============================================================

CREATE OR REPLACE FUNCTION record_order_history()
RETURNS TRIGGER AS $$
DECLARE
    v_user_id INTEGER;
    v_skip_status_change BOOLEAN := FALSE;
BEGIN
    v_user_id := COALESCE(NEW.updated_by, NEW.created_by, 1);

    IF TG_OP = 'INSERT' THEN
        INSERT INTO order_history (
            order_id, user_id, action, change_description, changed_at
        ) VALUES (
            NEW.f_order,
            v_user_id,
            'CREATE',
            'Orden creada: ' || COALESCE(NEW.f_po, 'Sin número'),
            CURRENT_TIMESTAMP
        );

    ELSIF TG_OP = 'UPDATE' THEN

        -- Estado de orden (con dedup para evitar duplicados de triggers automáticos)
        IF OLD.f_orderstat IS DISTINCT FROM NEW.f_orderstat THEN
            SELECT EXISTS(
                SELECT 1 FROM order_history
                WHERE order_id = NEW.f_order
                AND field_name = 'f_orderstat'
                AND old_value = OLD.f_orderstat::TEXT
                AND new_value = NEW.f_orderstat::TEXT
                AND action IN ('AUTO_STATUS_UPDATE', 'STATUS_CHANGE')
                AND changed_at >= NOW() - INTERVAL '2 seconds'
            ) INTO v_skip_status_change;

            IF NOT v_skip_status_change THEN
                INSERT INTO order_history (
                    order_id, user_id, action, field_name, old_value, new_value
                ) VALUES (
                    NEW.f_order, v_user_id, 'UPDATE', 'f_orderstat',
                    OLD.f_orderstat::TEXT, NEW.f_orderstat::TEXT
                );
            END IF;
        END IF;

        -- Número de PO
        IF OLD.f_po IS DISTINCT FROM NEW.f_po THEN
            INSERT INTO order_history (
                order_id, user_id, action, field_name, old_value, new_value
            ) VALUES (
                NEW.f_order, v_user_id, 'UPDATE', 'f_po', OLD.f_po, NEW.f_po
            );
        END IF;

        -- Porcentaje de facturación (con dedup)
        IF OLD.order_percentage IS DISTINCT FROM NEW.order_percentage THEN
            IF NOT EXISTS(
                SELECT 1 FROM order_history
                WHERE order_id = NEW.f_order
                AND field_name = 'order_percentage'
                AND old_value = COALESCE(OLD.order_percentage, 0)::TEXT
                AND new_value = NEW.order_percentage::TEXT
                AND changed_at >= NOW() - INTERVAL '2 seconds'
            ) THEN
                INSERT INTO order_history (
                    order_id, user_id, action, field_name, old_value, new_value
                ) VALUES (
                    NEW.f_order, v_user_id, 'UPDATE', 'order_percentage',
                    COALESCE(OLD.order_percentage, 0)::TEXT, NEW.order_percentage::TEXT
                );
            END IF;
        END IF;

        -- ============================================================
        -- CAMPOS NUEVOS (agregados 2026-02-09)
        -- ============================================================

        -- Cotización
        IF OLD.f_quote IS DISTINCT FROM NEW.f_quote THEN
            INSERT INTO order_history (
                order_id, user_id, action, field_name, old_value, new_value
            ) VALUES (
                NEW.f_order, v_user_id, 'UPDATE', 'f_quote',
                OLD.f_quote, NEW.f_quote
            );
        END IF;

        -- Descripción
        IF OLD.f_description IS DISTINCT FROM NEW.f_description THEN
            INSERT INTO order_history (
                order_id, user_id, action, field_name, old_value, new_value
            ) VALUES (
                NEW.f_order, v_user_id, 'UPDATE', 'f_description',
                LEFT(OLD.f_description, 200), LEFT(NEW.f_description, 200)
            );
        END IF;

        -- Cliente
        IF OLD.f_client IS DISTINCT FROM NEW.f_client THEN
            INSERT INTO order_history (
                order_id, user_id, action, field_name, old_value, new_value,
                change_description
            ) VALUES (
                NEW.f_order, v_user_id, 'UPDATE', 'f_client',
                OLD.f_client::TEXT, NEW.f_client::TEXT,
                'Cambio de cliente'
            );
        END IF;

        -- Contacto
        IF OLD.f_contact IS DISTINCT FROM NEW.f_contact THEN
            INSERT INTO order_history (
                order_id, user_id, action, field_name, old_value, new_value
            ) VALUES (
                NEW.f_order, v_user_id, 'UPDATE', 'f_contact',
                OLD.f_contact::TEXT, NEW.f_contact::TEXT
            );
        END IF;

        -- Subtotal de venta
        IF OLD.f_salesubtotal IS DISTINCT FROM NEW.f_salesubtotal THEN
            INSERT INTO order_history (
                order_id, user_id, action, field_name, old_value, new_value
            ) VALUES (
                NEW.f_order, v_user_id, 'UPDATE', 'f_salesubtotal',
                COALESCE(OLD.f_salesubtotal, 0)::TEXT, COALESCE(NEW.f_salesubtotal, 0)::TEXT
            );
        END IF;

        -- Total de venta
        IF OLD.f_saletotal IS DISTINCT FROM NEW.f_saletotal THEN
            INSERT INTO order_history (
                order_id, user_id, action, field_name, old_value, new_value
            ) VALUES (
                NEW.f_order, v_user_id, 'UPDATE', 'f_saletotal',
                COALESCE(OLD.f_saletotal, 0)::TEXT, COALESCE(NEW.f_saletotal, 0)::TEXT
            );
        END IF;

        -- Fecha estimada de entrega
        IF OLD.f_estdelivery IS DISTINCT FROM NEW.f_estdelivery THEN
            INSERT INTO order_history (
                order_id, user_id, action, field_name, old_value, new_value
            ) VALUES (
                NEW.f_order, v_user_id, 'UPDATE', 'f_estdelivery',
                OLD.f_estdelivery::TEXT, NEW.f_estdelivery::TEXT
            );
        END IF;

        -- Avance del trabajo (progress_percentage)
        IF OLD.progress_percentage IS DISTINCT FROM NEW.progress_percentage THEN
            INSERT INTO order_history (
                order_id, user_id, action, field_name, old_value, new_value
            ) VALUES (
                NEW.f_order, v_user_id, 'UPDATE', 'progress_percentage',
                COALESCE(OLD.progress_percentage, 0)::TEXT,
                COALESCE(NEW.progress_percentage, 0)::TEXT
            );
        END IF;

    END IF;

    RETURN NULL;
END;
$$ LANGUAGE plpgsql;

-- No necesita recrear el trigger, solo se reemplaza la función
-- El trigger record_order_history_trigger ya existe en t_order


-- ============================================================
-- VERIFICAR: Ejecutar después para confirmar
-- ============================================================
SELECT prosrc FROM pg_proc WHERE proname = 'record_order_history';
