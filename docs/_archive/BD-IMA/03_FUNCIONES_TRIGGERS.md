# Funciones y Triggers - IMA Mecatrónica

**Fecha de extracción:** 27 de Enero de 2026
**Total de funciones:** 43
**Total de triggers:** 28 *(neto: se eliminó 1, se agregó 1)*

---

## Resumen Ejecutivo

| Categoría | Cantidad | Descripción |
|-----------|----------|-------------|
| Funciones Trigger | 26 | Funciones que retornan `trigger` |
| Funciones Invocables | 17 | Funciones llamadas directamente |
| Triggers Activos | 28 | Todos habilitados (ENABLED) |

---

## Índice

1. [Lista de Funciones](#lista-de-funciones)
2. [Lista de Triggers](#lista-de-triggers)
3. [Triggers por Tabla](#triggers-por-tabla)
4. [Funciones Críticas - Core del Negocio](#funciones-críticas---core-del-negocio)
5. [Funciones de Auditoría](#funciones-de-auditoría)
6. [Funciones de Calendario](#funciones-de-calendario)
7. [Código Fuente de Funciones](#código-fuente-de-funciones)

---

## Lista de Funciones

### Funciones Invocables (19)

| Función | Argumentos | Retorna | Descripción |
|---------|------------|---------|-------------|
| `can_delete_order` | `p_order_id integer` | TABLE(can_delete, reason, invoice_count, expense_count, commission_count) | Verifica si una orden puede eliminarse |
| `create_vendor_commission_if_needed` | `p_order_id integer` | void | Crea comisión para vendedor si aplica |
| `create_vendor_user` | username, email, password, fullname, role, isactive | void | Crea usuario vendedor |
| `delete_order_with_audit` | `p_order_id, p_deleted_by, p_reason` | TABLE(success, message, deleted_order_id) | Elimina orden con auditoría |
| `f_balance_anual_horizontal` | `p_año integer` | TABLE (12 meses x 3 conceptos) | Balance anual pivoteado |
| `f_balance_completo_horizontal` | `p_año integer` | TABLE (sección, concepto, 12 meses) | Balance completo horizontal |
| `generate_holidays_for_year` | `target_year integer` | integer | Genera feriados recurrentes para un año |
| `get_attendance_for_date` | `check_date date` | TABLE (18 columnas de asistencia) | Estado de asistencia por fecha |
| `get_month_calendar` | `target_year, target_month` | TABLE (calendario con estadísticas) | Calendario mensual con stats |
| `get_monthly_payroll_total` | `year_param, month_param` | numeric | Total de nómina mensual |
| `get_overtime_audit_history` | `p_year, p_month` | TABLE (historial de cambios) | Historial de horas extras |
| `get_payroll_at_date` | `target_date date` | TABLE (nómina efectiva) | Nómina vigente en una fecha |
| `get_vendors` | (ninguno) | TABLE(id, full_name, username) | Lista de vendedores activos |
| `is_workday` | `check_date date` | boolean | Verifica si es día laboral |
| `set_current_user_id` | `p_user_id integer` | void | Establece usuario actual en sesión |
| `update_user_password` | `user_id, new_password` | void | Actualiza contraseña de usuario |
| `upsert_overtime_hours` | `p_year, p_month, p_amount, p_notes, p_user_id` | TABLE(success, message, overtime_id) | Inserta/actualiza horas extras |

### Funciones Trigger (26) *(neto: -1 eliminada, +1 nueva)*

| Función | Descripción |
|---------|-------------|
| `audit_attendance_changes` | Registra cambios en asistencias |
| `audit_overtime_hours` | Registra cambios en horas extras |
| `audit_vacation_changes` | Registra cambios en vacaciones |
| `auto_pay_zero_credit_expense` | Auto-paga gastos de proveedores sin crédito |
| `calculate_invoice_due_date` | Calcula fecha de vencimiento de factura |
| `calculate_scheduled_date` | Calcula fecha programada de gasto |
| `create_commission_on_order_creation` | Crea comisión al crear orden |
| `fn_log_vendor_removal` | Registra cuando se elimina vendedor de orden |
| `record_order_history` | Registra historial de cambios en órdenes |
| `set_created_by` | Establece campo created_by |
| `set_latest_version` | Marca versión como la más reciente |
| `set_order_audit_fields` | Establece campos de auditoría en orden (INSERT) |
| `sync_commission_rate` | Sincroniza tasa de comisión |
| `sync_commission_rate_from_order` | Sincroniza comisión desde orden |
| `track_fixed_expense_changes` | Registra cambios en gastos fijos |
| `track_payroll_changes` | Registra cambios en nómina |
| `update_commission_on_order_status_change` | Actualiza comisión al cambiar estado |
| `update_commission_on_vendor_change` | Actualiza comisión al cambiar vendedor |
| `update_invoice_status` | Actualiza estado de factura |
| `update_order_audit_fields` | Actualiza campos de auditoría en orden (UPDATE) |
| `update_order_on_invoice_change` | Actualiza orden al cambiar factura |
| `update_order_status_from_invoices` | Actualiza estado de orden desde facturas |
| `update_order_status_on_invoice` | Actualiza estado de orden por factura |
| `recalcular_gasto_operativo` | Recalcula t_order.gasto_operativo = SUM(monto) + SUM(commission_amount) *(Mod. 2026-02-09)* |
| `recalcular_gasto_operativo_por_comision` | Recalcula gasto_operativo cuando cambia commission_amount *(Nueva 2026-02-09)* |
| `update_updated_at_column` | Actualiza timestamp updated_at |

---

## Lista de Triggers

| Trigger | Tabla | Momento | Eventos | Función |
|---------|-------|---------|---------|---------|
| `trigger_set_latest_version` | app_versions | BEFORE | INSERT, UPDATE | set_latest_version |
| `trg_attendance_audit` | t_attendance | AFTER | INSERT, DELETE, UPDATE | audit_attendance_changes |
| `update_client_updated_at` | t_client | BEFORE | UPDATE | update_updated_at_column |
| `update_contact_updated_at` | t_contact | BEFORE | UPDATE | update_updated_at_column |
| `trigger_expense_scheduled_date` | t_expense | BEFORE | INSERT, UPDATE | calculate_scheduled_date |
| `update_expense_updated_at` | t_expense | BEFORE | UPDATE | update_updated_at_column |
| `z_expense_auto_pay_zero_credit` | t_expense | BEFORE | INSERT, UPDATE | auto_pay_zero_credit_expense |
| `fixed_expense_history_trigger` | t_fixed_expenses | AFTER | INSERT, DELETE, UPDATE | track_fixed_expense_changes |
| `trigger_calculate_due_date` | t_invoice | BEFORE | INSERT, UPDATE | calculate_invoice_due_date |
| `trigger_update_invoice_status` | t_invoice | BEFORE | INSERT, UPDATE | update_invoice_status |
| `trigger_update_order_status_unified` | t_invoice | AFTER | INSERT, DELETE, UPDATE | update_order_status_from_invoices |
| `update_invoice_updated_at` | t_invoice | BEFORE | UPDATE | update_updated_at_column |
| `record_order_history_trigger` | t_order | AFTER | INSERT, DELETE, UPDATE | record_order_history |
| `set_order_audit_fields_trigger` | t_order | BEFORE | INSERT, UPDATE | set_order_audit_fields |
| `trigger_create_commission_on_order` | t_order | AFTER | INSERT | create_commission_on_order_creation |
| `trigger_sync_commission_from_order` | t_order | AFTER | UPDATE | sync_commission_rate_from_order |
| `trigger_update_commission_on_status_change` | t_order | AFTER | UPDATE | update_commission_on_order_status_change |
| `trigger_update_commission_on_vendor_change` | t_order | BEFORE | UPDATE | update_commission_on_vendor_change |
| `update_order_updated_at` | t_order | BEFORE | UPDATE | update_updated_at_column |
| `trigger_overtime_audit` | t_overtime_hours | AFTER | INSERT, DELETE, UPDATE | audit_overtime_hours |
| `payroll_history_trigger` | t_payroll | AFTER | INSERT, UPDATE | track_payroll_changes |
| `update_supplier_updated_at` | t_supplier | BEFORE | UPDATE | update_updated_at_column |
| `trg_vacation_audit` | t_vacation | AFTER | INSERT, DELETE, UPDATE | audit_vacation_changes |
| `update_vendor_updated_at` | t_vendor | BEFORE | UPDATE | update_updated_at_column |
| `trg_before_commission_delete` | t_vendor_commission_payment | BEFORE | DELETE | fn_log_vendor_removal |
| `trg_recalcular_gasto_operativo` | order_gastos_operativos | AFTER | INSERT, DELETE, UPDATE | recalcular_gasto_operativo |
| `trg_recalcular_gasto_op_por_comision` | t_vendor_commission_payment | AFTER | INSERT, DELETE, UPDATE | recalcular_gasto_operativo_por_comision |
| `trigger_sync_commission_rate` | t_vendor_commission_payment | AFTER | UPDATE | sync_commission_rate |

---

## Triggers por Tabla

### order_gastos_operativos (1 trigger) - GASTOS OPERATIVOS *(Mod. 2026-02-09)*

| Trigger | Momento | Evento | Función | Propósito |
|---------|---------|--------|---------|-----------|
| `trg_recalcular_gasto_operativo` | AFTER | INSERT, UPDATE, DELETE | recalcular_gasto_operativo | Recalcula t_order.gasto_operativo = SUM(monto) + SUM(commission_amount) |

### t_order (7 triggers) - CORE DEL NEGOCIO

| Trigger | Momento | Evento | Función | Propósito |
|---------|---------|--------|---------|-----------|
| `set_order_audit_fields_trigger` | BEFORE | INSERT, UPDATE | set_order_audit_fields | Campos de auditoría |
| `update_order_updated_at` | BEFORE | UPDATE | update_updated_at_column | Timestamp automático |
| `trigger_update_commission_on_vendor_change` | BEFORE | UPDATE | update_commission_on_vendor_change | Actualiza comisión si cambia vendedor |
| `record_order_history_trigger` | AFTER | INSERT, DELETE, UPDATE | record_order_history | Historial de cambios |
| `trigger_create_commission_on_order` | AFTER | INSERT | create_commission_on_order_creation | Crea comisión automáticamente |
| `trigger_sync_commission_from_order` | AFTER | UPDATE | sync_commission_rate_from_order | Sincroniza tasa de comisión |
| `trigger_update_commission_on_status_change` | AFTER | UPDATE | update_commission_on_order_status_change | Actualiza estado de comisión |

### t_invoice (4 triggers)

| Trigger | Momento | Evento | Función |
|---------|---------|--------|---------|
| `trigger_calculate_due_date` | BEFORE | INSERT, UPDATE | calculate_invoice_due_date |
| `trigger_update_invoice_status` | BEFORE | INSERT, UPDATE | update_invoice_status |
| `update_invoice_updated_at` | BEFORE | UPDATE | update_updated_at_column |
| `trigger_update_order_status_unified` | AFTER | INSERT, DELETE, UPDATE | update_order_status_from_invoices |

### t_expense (3 triggers)

| Trigger | Momento | Evento | Función |
|---------|---------|--------|---------|
| `trigger_expense_scheduled_date` | BEFORE | INSERT, UPDATE | calculate_scheduled_date |
| `update_expense_updated_at` | BEFORE | UPDATE | update_updated_at_column |
| `z_expense_auto_pay_zero_credit` | BEFORE | INSERT, UPDATE | auto_pay_zero_credit_expense |

### t_vendor_commission_payment (3 triggers)

| Trigger | Momento | Evento | Función |
|---------|---------|--------|---------|
| `trg_before_commission_delete` | BEFORE | DELETE | fn_log_vendor_removal |
| `trigger_sync_commission_rate` | AFTER | UPDATE | sync_commission_rate |
| `trg_recalcular_gasto_op_por_comision` | AFTER | INSERT, UPDATE, DELETE | recalcular_gasto_operativo_por_comision *(Nuevo 2026-02-09)* |

### Tablas con trigger updated_at

| Tabla | Trigger |
|-------|---------|
| t_client | update_client_updated_at |
| t_contact | update_contact_updated_at |
| t_expense | update_expense_updated_at |
| t_invoice | update_invoice_updated_at |
| t_order | update_order_updated_at |
| t_supplier | update_supplier_updated_at |
| t_vendor | update_vendor_updated_at |

### Tablas de Auditoría

| Tabla | Trigger | Función |
|-------|---------|---------|
| t_attendance | trg_attendance_audit | audit_attendance_changes |
| t_vacation | trg_vacation_audit | audit_vacation_changes |
| t_overtime_hours | trigger_overtime_audit | audit_overtime_hours |
| t_payroll | payroll_history_trigger | track_payroll_changes |
| t_fixed_expenses | fixed_expense_history_trigger | track_fixed_expense_changes |

---

## Funciones Críticas - Core del Negocio

### delete_order_with_audit()

**Propósito:** Elimina una orden verificando dependencias y guardando auditoría completa.

**Parámetros:**
- `p_order_id INTEGER` - ID de la orden a eliminar
- `p_deleted_by INTEGER` - ID del usuario que elimina
- `p_reason TEXT` - Motivo de eliminación (default: 'Orden creada por error')

**Retorna:** TABLE(success BOOLEAN, message TEXT, deleted_order_id INTEGER)

**Validaciones:**
1. Verifica que la orden existe
2. Verifica que no tenga facturas asociadas
3. Verifica que no tenga gastos asociados
4. Verifica que no tenga comisiones asociadas

**Comportamiento:**
- Si pasa todas las validaciones, guarda snapshot en `t_order_deleted`
- Elimina la orden (order_history se elimina por CASCADE)
- Retorna mensaje de éxito con el número de PO

---

### can_delete_order()

**Propósito:** Verifica si una orden puede ser eliminada (validación previa).

**Parámetros:**
- `p_order_id INTEGER` - ID de la orden a verificar

**Retorna:** TABLE(can_delete BOOLEAN, reason TEXT, invoice_count INTEGER, expense_count INTEGER, commission_count INTEGER)

**Uso típico:**
```sql
SELECT * FROM can_delete_order(123);
-- Retorna: TRUE, 'Orden puede ser eliminada', 0, 0, 0
-- O: FALSE, 'Orden tiene dependencias: 2 facturas, 1 gastos, 1 comisiones', 2, 1, 1
```

---

### fn_log_vendor_removal()

**Propósito:** Trigger que registra en historial cuando se elimina un pago de comisión (vendedor removido de orden).

**Tabla afectada:** t_vendor_commission_payment
**Evento:** BEFORE DELETE
**Registra en:** t_commission_rate_history

**Campos especiales:**
- `is_vendor_removal = TRUE` - Indica que fue remoción de vendedor
- `new_rate = 0, new_amount = 0` - Valores en cero por eliminación

---

## Funciones de Auditoría

### audit_attendance_changes()

**Propósito:** Registra todos los cambios en la tabla de asistencias.

**Eventos capturados:** INSERT, UPDATE, DELETE

**Registra en:** t_attendance_audit

**Campos capturados:**
- status (estado de asistencia)
- check_in_time, check_out_time
- late_minutes (minutos de retardo)
- notes, is_justified

**Optimización:** Solo registra UPDATE si hubo cambios reales (usa IS DISTINCT FROM).

---

### audit_vacation_changes()

**Propósito:** Registra cambios en registros de vacaciones.

**Eventos capturados:** INSERT, UPDATE, DELETE

**Registra en:** t_vacation_audit

**Campos capturados:**
- start_date, end_date
- status
- notes

---

### track_payroll_changes() / track_fixed_expense_changes()

**Propósito:** Registran cambios históricos en nómina y gastos fijos para cálculos retroactivos de balance.

---

## Funciones de Calendario

### generate_holidays_for_year()

**Propósito:** Genera instancias de feriados recurrentes para un año específico.

**Parámetros:**
- `target_year INTEGER` - Año para generar feriados

**Retorna:** INTEGER (cantidad de feriados insertados)

**Comportamiento:**
- Lee feriados marcados como `is_recurring = TRUE`
- Crea instancias específicas para el año indicado
- Usa `ON CONFLICT DO NOTHING` para evitar duplicados

---

### is_workday()

**Propósito:** Verifica si una fecha es día laboral.

**Parámetros:**
- `check_date DATE` - Fecha a verificar

**Retorna:** BOOLEAN

**Lógica:**
1. Si es feriado obligatorio → FALSE
2. Si el día de la semana está configurado como no laboral → FALSE
3. De lo contrario → TRUE

---

### get_attendance_for_date()

**Propósito:** Obtiene el estado de asistencia de TODOS los empleados para una fecha.

**Retorna 18 columnas:**
- Datos del empleado (id, nombre, título, código, iniciales)
- Datos de asistencia (id, status, check_in, check_out, late_minutes)
- Estado de vacaciones (on_vacation, vacation_start, vacation_end)
- Estado de feriado (is_holiday, holiday_name)
- Día laboral (is_workday)

---

### get_month_calendar()

**Propósito:** Genera calendario del mes con estadísticas de asistencia por día.

**Retorna por cada día:**
- calendar_date, day_of_week, day_name
- is_workday, is_holiday, holiday_name
- total_employees, asistencias, retardos, faltas, vacaciones, sin_registro

---

## Código Fuente de Funciones

### delete_order_with_audit

```sql
CREATE OR REPLACE FUNCTION public.delete_order_with_audit(
    p_order_id integer,
    p_deleted_by integer,
    p_reason text DEFAULT 'Orden creada por error'::text
)
RETURNS TABLE(success boolean, message text, deleted_order_id integer)
LANGUAGE plpgsql
AS $function$
DECLARE
    v_order RECORD;
    v_invoice_count INTEGER;
    v_expense_count INTEGER;
    v_commission_count INTEGER;
BEGIN
    -- Verificar que la orden existe
    SELECT * INTO v_order FROM t_order WHERE f_order = p_order_id;

    IF NOT FOUND THEN
        RETURN QUERY SELECT FALSE, 'Orden no encontrada', NULL::INTEGER;
        RETURN;
    END IF;

    -- Verificar que no tenga facturas
    SELECT COUNT(*) INTO v_invoice_count FROM t_invoice WHERE f_order = p_order_id;
    IF v_invoice_count > 0 THEN
        RETURN QUERY SELECT FALSE,
            'No se puede eliminar: La orden tiene ' || v_invoice_count ||
            ' factura(s) asociada(s). Use CANCELAR en su lugar.',
            NULL::INTEGER;
        RETURN;
    END IF;

    -- Verificar que no tenga gastos
    SELECT COUNT(*) INTO v_expense_count FROM t_expense WHERE f_order = p_order_id;
    IF v_expense_count > 0 THEN
        RETURN QUERY SELECT FALSE,
            'No se puede eliminar: La orden tiene ' || v_expense_count ||
            ' gasto(s) asociado(s). Use CANCELAR en su lugar.',
            NULL::INTEGER;
        RETURN;
    END IF;

    -- Verificar que no tenga comisiones
    SELECT COUNT(*) INTO v_commission_count
    FROM t_vendor_commission_payment WHERE f_order = p_order_id;
    IF v_commission_count > 0 THEN
        RETURN QUERY SELECT FALSE,
            'No se puede eliminar: La orden tiene ' || v_commission_count ||
            ' pago(s) de comisión asociado(s). Use CANCELAR en su lugar.',
            NULL::INTEGER;
        RETURN;
    END IF;

    -- Guardar en tabla de auditoría antes de eliminar
    INSERT INTO t_order_deleted (
        original_order_id, f_po, f_quote, f_client, f_contact, f_salesman,
        f_podate, f_estdelivery, f_description, f_salesubtotal, f_saletotal,
        f_orderstat, f_expense, progress_percentage, order_percentage,
        f_commission_rate, deleted_by, deletion_reason, full_order_snapshot
    ) VALUES (
        v_order.f_order, v_order.f_po, v_order.f_quote, v_order.f_client,
        v_order.f_contact, v_order.f_salesman, v_order.f_podate, v_order.f_estdelivery,
        v_order.f_description, v_order.f_salesubtotal, v_order.f_saletotal,
        v_order.f_orderstat, v_order.f_expense, v_order.progress_percentage,
        v_order.order_percentage, v_order.f_commission_rate, p_deleted_by,
        p_reason, to_jsonb(v_order)
    );

    -- Eliminar la orden (order_history se elimina por CASCADE)
    DELETE FROM t_order WHERE f_order = p_order_id;

    RETURN QUERY SELECT TRUE,
        'Orden ' || v_order.f_po || ' eliminada exitosamente',
        p_order_id;
END;
$function$;
```

---

### can_delete_order

```sql
CREATE OR REPLACE FUNCTION public.can_delete_order(p_order_id integer)
RETURNS TABLE(can_delete boolean, reason text, invoice_count integer,
              expense_count integer, commission_count integer)
LANGUAGE plpgsql
AS $function$
DECLARE
    v_invoices INTEGER;
    v_expenses INTEGER;
    v_commissions INTEGER;
BEGIN
    SELECT COUNT(*) INTO v_invoices FROM t_invoice WHERE f_order = p_order_id;
    SELECT COUNT(*) INTO v_expenses FROM t_expense WHERE f_order = p_order_id;
    SELECT COUNT(*) INTO v_commissions FROM t_vendor_commission_payment WHERE f_order = p_order_id;

    IF v_invoices > 0 OR v_expenses > 0 OR v_commissions > 0 THEN
        RETURN QUERY SELECT FALSE,
            'Orden tiene dependencias: ' || v_invoices || ' facturas, ' ||
            v_expenses || ' gastos, ' || v_commissions || ' comisiones',
            v_invoices, v_expenses, v_commissions;
    ELSE
        RETURN QUERY SELECT TRUE, 'Orden puede ser eliminada',
            v_invoices, v_expenses, v_commissions;
    END IF;
END;
$function$;
```

---

### fn_log_vendor_removal

```sql
CREATE OR REPLACE FUNCTION public.fn_log_vendor_removal()
RETURNS trigger
LANGUAGE plpgsql
AS $function$
BEGIN
    INSERT INTO t_commission_rate_history (
        order_id, vendor_id, commission_payment_id,
        old_rate, old_amount, new_rate, new_amount,
        order_subtotal, order_number, vendor_name,
        changed_by, changed_by_name, changed_at,
        change_reason, is_vendor_removal
    )
    SELECT
        OLD.f_order,
        OLD.f_vendor,
        OLD.id,
        OLD.commission_rate,
        OLD.commission_amount,
        0, -- new_rate = 0 (sin vendedor)
        0, -- new_amount = 0
        o.f_salesubtotal,
        o.f_po,
        v.f_vendorname,
        COALESCE(OLD.updated_by, OLD.created_by, 1),
        COALESCE(u.full_name, 'Sistema'),
        NOW(),
        'Vendedor removido de la orden',
        TRUE
    FROM t_order o
    LEFT JOIN t_vendor v ON OLD.f_vendor = v.f_vendor
    LEFT JOIN users u ON COALESCE(OLD.updated_by, OLD.created_by) = u.id
    WHERE o.f_order = OLD.f_order;

    RETURN OLD;
END;
$function$;
```

---

### update_order_status_from_invoices

**Propósito:** Actualiza automáticamente el estado de una orden basándose en el estado de sus facturas.

**Trigger asociado:** `trigger_update_order_status_unified`
**Tabla:** t_invoice
**Eventos:** AFTER INSERT, DELETE, UPDATE

**Lógica de transiciones de estado:**

| Estado | ID | Condición |
|--------|:--:|-----------|
| COMPLETADA | 4 | Todas las facturas pagadas (`f_invoicestat = 4`) y ≥99% facturado |
| CERRADA | 3 | Todas las facturas con fecha de recepción, todas pendientes/pagadas, y ≥99% facturado |
| LIBERADA | 2 | ≥99% del total de la orden facturado |
| EN_PROCESO | 1 | Hay facturas pero la orden estaba en CREADA |

**Campos actualizados en t_order:**
- `f_orderstat` - Estado de la orden
- `order_percentage` - Porcentaje de facturación (calculado automáticamente)
- `progress_percentage` - Se establece a 100% cuando pasa a CERRADA (3) o COMPLETADA (4)
- `invoiced` - TRUE si hay facturas
- `last_invoice_date` - Fecha de última factura
- `updated_at` - Timestamp de actualización

**Importante:** Solo actualiza si el nuevo estado es MAYOR que el actual (nunca retrocede).

```sql
CREATE OR REPLACE FUNCTION update_order_status_from_invoices()
RETURNS TRIGGER AS $$
DECLARE
    v_order_id INTEGER;
    v_order_total NUMERIC(18,2);
    v_invoiced_total NUMERIC(18,2);
    v_percentage NUMERIC(5,2);
    v_current_status INTEGER;
    v_new_status INTEGER;
    v_invoice_count INTEGER;
    v_paid_count INTEGER;
    v_pending_count INTEGER;
    v_created_count INTEGER;
    v_has_reception_all BOOLEAN;
    v_should_update BOOLEAN := FALSE;
BEGIN
    -- Obtener el order_id de la factura (nueva o antigua)
    v_order_id := COALESCE(NEW.f_order, OLD.f_order);

    IF v_order_id IS NULL THEN
        RETURN COALESCE(NEW, OLD);
    END IF;

    -- Obtener información de la orden
    SELECT f_saletotal, f_orderstat
    INTO v_order_total, v_current_status
    FROM t_order
    WHERE f_order = v_order_id;

    -- Si no hay orden o total es 0, salir
    IF v_order_total IS NULL OR v_order_total = 0 THEN
        RETURN COALESCE(NEW, OLD);
    END IF;

    -- Calcular totales y conteos de facturas
    SELECT
        COALESCE(SUM(f_total), 0),
        COUNT(*),
        COUNT(CASE WHEN f_invoicestat = 1 THEN 1 END),
        COUNT(CASE WHEN f_invoicestat = 2 THEN 1 END),
        COUNT(CASE WHEN f_invoicestat = 4 THEN 1 END),
        COUNT(*) = COUNT(f_receptiondate)
    INTO
        v_invoiced_total,
        v_invoice_count,
        v_created_count,
        v_pending_count,
        v_paid_count,
        v_has_reception_all
    FROM t_invoice
    WHERE f_order = v_order_id;

    -- Calcular porcentaje facturado
    v_percentage := ROUND((v_invoiced_total / v_order_total) * 100, 2);

    -- Determinar el nuevo estado basado en las condiciones
    -- Primero verificar si TODAS están pagadas (estado más alto)
    IF v_paid_count = v_invoice_count AND v_percentage >= 99 THEN
        v_new_status := 4; -- COMPLETADA
        v_should_update := TRUE;
    -- Luego verificar si todas tienen recepción (pendientes o pagadas) Y 100% facturado
    ELSIF v_has_reception_all AND (v_pending_count + v_paid_count) = v_invoice_count AND v_percentage >= 99 THEN
        v_new_status := 3; -- CERRADA
        v_should_update := TRUE;
    -- Finalmente verificar si hay 100% facturado
    ELSIF v_percentage >= 99 THEN
        v_new_status := 2; -- LIBERADA
        v_should_update := TRUE;
    -- Si hay facturas pero no cumple ninguna condición anterior
    ELSIF v_invoice_count > 0 AND v_current_status = 0 THEN
        v_new_status := 1; -- EN_PROCESO
        v_should_update := TRUE;
    ELSE
        v_new_status := v_current_status;
    END IF;

    -- Solo actualizar si hay cambio y el nuevo estado es mayor o igual
    IF v_should_update AND v_new_status != v_current_status AND v_new_status > v_current_status THEN
        UPDATE t_order
        SET
            f_orderstat = v_new_status,
            order_percentage = ROUND(v_percentage),
            -- Actualizar progress_percentage a 100 cuando pasa a CERRADA o COMPLETADA
            progress_percentage = CASE
                WHEN v_new_status >= 3 THEN 100
                ELSE progress_percentage
            END,
            invoiced = CASE WHEN v_invoiced_total > 0 THEN TRUE ELSE FALSE END,
            last_invoice_date = CASE WHEN v_invoiced_total > 0 THEN CURRENT_DATE ELSE last_invoice_date END,
            updated_at = NOW()
        WHERE f_order = v_order_id;
    ELSE
        -- Aún así actualizar el porcentaje de facturación
        UPDATE t_order
        SET
            order_percentage = ROUND(v_percentage),
            invoiced = CASE WHEN v_invoiced_total > 0 THEN TRUE ELSE FALSE END,
            last_invoice_date = CASE WHEN v_invoiced_total > 0 THEN CURRENT_DATE ELSE last_invoice_date END,
            updated_at = NOW()
        WHERE f_order = v_order_id;
    END IF;

    RETURN COALESCE(NEW, OLD);
END;
$$ LANGUAGE plpgsql;
```

**Última actualización:** 27 de Enero de 2026 (v2.0.2)

---

### audit_attendance_changes

```sql
CREATE OR REPLACE FUNCTION public.audit_attendance_changes()
RETURNS trigger
LANGUAGE plpgsql
AS $function$
BEGIN
    IF TG_OP = 'INSERT' THEN
        INSERT INTO t_attendance_audit (
            attendance_id, employee_id, attendance_date, action,
            new_status, new_check_in_time, new_check_out_time,
            new_late_minutes, new_notes, new_is_justified,
            changed_by, changed_at
        ) VALUES (
            NEW.id, NEW.employee_id, NEW.attendance_date, 'INSERT',
            NEW.status, NEW.check_in_time, NEW.check_out_time,
            NEW.late_minutes, NEW.notes, NEW.is_justified,
            NEW.created_by, CURRENT_TIMESTAMP
        );
        RETURN NEW;

    ELSIF TG_OP = 'UPDATE' THEN
        -- Solo registrar si hubo cambios reales
        IF OLD.status IS DISTINCT FROM NEW.status
           OR OLD.check_in_time IS DISTINCT FROM NEW.check_in_time
           OR OLD.check_out_time IS DISTINCT FROM NEW.check_out_time
           OR OLD.late_minutes IS DISTINCT FROM NEW.late_minutes
           OR OLD.notes IS DISTINCT FROM NEW.notes
           OR OLD.is_justified IS DISTINCT FROM NEW.is_justified
        THEN
            INSERT INTO t_attendance_audit (
                attendance_id, employee_id, attendance_date, action,
                old_status, old_check_in_time, old_check_out_time,
                old_late_minutes, old_notes, old_is_justified,
                new_status, new_check_in_time, new_check_out_time,
                new_late_minutes, new_notes, new_is_justified,
                changed_by, changed_at
            ) VALUES (
                NEW.id, NEW.employee_id, NEW.attendance_date, 'UPDATE',
                OLD.status, OLD.check_in_time, OLD.check_out_time,
                OLD.late_minutes, OLD.notes, OLD.is_justified,
                NEW.status, NEW.check_in_time, NEW.check_out_time,
                NEW.late_minutes, NEW.notes, NEW.is_justified,
                NEW.updated_by, CURRENT_TIMESTAMP
            );
        END IF;
        RETURN NEW;

    ELSIF TG_OP = 'DELETE' THEN
        INSERT INTO t_attendance_audit (
            attendance_id, employee_id, attendance_date, action,
            old_status, old_check_in_time, old_check_out_time,
            old_late_minutes, old_notes, old_is_justified,
            changed_by, changed_at
        ) VALUES (
            OLD.id, OLD.employee_id, OLD.attendance_date, 'DELETE',
            OLD.status, OLD.check_in_time, OLD.check_out_time,
            OLD.late_minutes, OLD.notes, OLD.is_justified,
            OLD.updated_by, CURRENT_TIMESTAMP
        );
        RETURN OLD;
    END IF;

    RETURN NULL;
END;
$function$;
```

---

### generate_holidays_for_year

```sql
CREATE OR REPLACE FUNCTION public.generate_holidays_for_year(target_year integer)
RETURNS integer
LANGUAGE plpgsql
AS $function$
DECLARE
    inserted_count INTEGER := 0;
    holiday_rec RECORD;
BEGIN
    FOR holiday_rec IN
        SELECT name, description, is_mandatory, recurring_month, recurring_day
        FROM t_holiday
        WHERE is_recurring = TRUE
        AND recurring_month IS NOT NULL
        AND recurring_day IS NOT NULL
    LOOP
        INSERT INTO t_holiday (
            holiday_date, name, description, is_mandatory,
            is_recurring, recurring_month, recurring_day, year
        )
        VALUES (
            MAKE_DATE(target_year, holiday_rec.recurring_month, holiday_rec.recurring_day),
            holiday_rec.name,
            holiday_rec.description,
            holiday_rec.is_mandatory,
            FALSE,  -- No es recurrente, es instancia específica
            holiday_rec.recurring_month,
            holiday_rec.recurring_day,
            target_year
        )
        ON CONFLICT (holiday_date) DO NOTHING;

        inserted_count := inserted_count + 1;
    END LOOP;

    RETURN inserted_count;
END;
$function$;
```

---

### is_workday

```sql
CREATE OR REPLACE FUNCTION public.is_workday(check_date date)
RETURNS boolean
LANGUAGE plpgsql
AS $function$
DECLARE
    day_config RECORD;
    is_holiday BOOLEAN;
BEGIN
    -- Verificar si es feriado
    SELECT EXISTS(
        SELECT 1 FROM t_holiday
        WHERE holiday_date = check_date AND is_mandatory = TRUE
    ) INTO is_holiday;

    IF is_holiday THEN
        RETURN FALSE;
    END IF;

    -- Verificar configuración del día de la semana
    SELECT is_workday INTO day_config.is_workday
    FROM t_workday_config
    WHERE day_of_week = EXTRACT(DOW FROM check_date);

    RETURN COALESCE(day_config.is_workday, TRUE);
END;
$function$;
```

---

### update_updated_at_column

```sql
CREATE OR REPLACE FUNCTION public.update_updated_at_column()
RETURNS trigger
LANGUAGE plpgsql
AS $function$
BEGIN
    NEW.updated_at = CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$function$;
```

---

## Funciones de Gastos Operativos *(Actualizado 2026-02-09)*

### recalcular_gasto_operativo()

**Propósito:** Trigger que recalcula automáticamente `t_order.gasto_operativo` como la suma de gastos base + comisión del vendedor.

**Tabla afectada:** order_gastos_operativos
**Evento:** AFTER INSERT, UPDATE, DELETE
**Actualiza:** t_order.gasto_operativo

**Fórmula:** `SUM(order_gastos_operativos.monto) + SUM(t_vendor_commission_payment.commission_amount)`

```sql
CREATE OR REPLACE FUNCTION recalcular_gasto_operativo()
RETURNS TRIGGER AS $$
DECLARE
    v_order_id integer;
BEGIN
    v_order_id := COALESCE(NEW.f_order, OLD.f_order);
    UPDATE t_order
    SET gasto_operativo = (
        SELECT COALESCE(SUM(monto), 0)
        FROM order_gastos_operativos
        WHERE f_order = v_order_id
    ) + COALESCE((
        SELECT SUM(commission_amount)
        FROM t_vendor_commission_payment
        WHERE f_order = v_order_id
    ), 0)
    WHERE f_order = v_order_id;
    RETURN COALESCE(NEW, OLD);
END;
$$ LANGUAGE plpgsql;
```

---

### recalcular_gasto_operativo_por_comision() *(Nueva 2026-02-09)*

**Propósito:** Trigger que recalcula `t_order.gasto_operativo` cuando cambia `commission_amount` en pagos de comisión.

**Tabla afectada:** t_vendor_commission_payment
**Evento:** AFTER INSERT, UPDATE, DELETE
**Actualiza:** t_order.gasto_operativo

**Fórmula:** Misma que `recalcular_gasto_operativo()`

```sql
CREATE OR REPLACE FUNCTION recalcular_gasto_operativo_por_comision()
RETURNS TRIGGER AS $$
DECLARE
    v_order_id integer;
BEGIN
    v_order_id := COALESCE(NEW.f_order, OLD.f_order);
    UPDATE t_order
    SET gasto_operativo = (
        SELECT COALESCE(SUM(monto), 0)
        FROM order_gastos_operativos
        WHERE f_order = v_order_id
    ) + COALESCE((
        SELECT SUM(commission_amount)
        FROM t_vendor_commission_payment
        WHERE f_order = v_order_id
    ), 0)
    WHERE f_order = v_order_id;
    RETURN COALESCE(NEW, OLD);
END;
$$ LANGUAGE plpgsql;
```

### Funciones eliminadas (2026-02-09)

- `sync_commission_rate_to_gastos()` — Ya no se propaga commission_rate a gastos individuales
- Trigger `trg_sync_commission_rate` en t_order — Eliminado

---

## Diagrama de Flujo de Triggers en t_order

```
                    ┌─────────────────────────────────────────┐
                    │              INSERT en t_order           │
                    └─────────────────────────────────────────┘
                                        │
                    ┌───────────────────┼───────────────────┐
                    ▼                   ▼                   ▼
            [BEFORE INSERT]      [AFTER INSERT]      [AFTER INSERT]
    set_order_audit_fields    record_order_history   create_commission
    (created_by, updated_at)  (INSERT en history)   (si tiene vendedor)


                    ┌─────────────────────────────────────────┐
                    │              UPDATE en t_order           │
                    └─────────────────────────────────────────┘
                                        │
        ┌───────────────────────────────┼───────────────────────────────┐
        ▼                               ▼                               ▼
  [BEFORE UPDATE]                 [BEFORE UPDATE]                 [BEFORE UPDATE]
  set_order_audit_fields     update_commission_on_vendor    update_updated_at_column
                                  (si cambia vendedor)
        │                               │                               │
        └───────────────────────────────┼───────────────────────────────┘
                                        │
        ┌───────────────────────────────┼───────────────────────────────┐
        ▼                               ▼                               ▼
  [AFTER UPDATE]           [AFTER UPDATE]           [AFTER UPDATE]
  record_order_history  sync_commission_from_order  update_commission
  (cambios en history)    (sincroniza tasa)        _on_status
                                                   (si cambia estado)
```

```
              ┌───────────────────────────────────────────────────────┐
              │      INSERT/UPDATE/DELETE en order_gastos_operativos   │
              └───────────────────────────────────────────────────────┘
                                          │
                                          ▼
                                  [AFTER I/U/D]
                          recalcular_gasto_operativo
                   (SUM(monto) + SUM(commission_amount) → t_order)

              ┌───────────────────────────────────────────────────────┐
              │    INSERT/UPDATE/DELETE en t_vendor_commission_payment  │
              └───────────────────────────────────────────────────────┘
                                          │
                                          ▼
                                  [AFTER I/U/D]
                   recalcular_gasto_operativo_por_comision
                   (SUM(monto) + SUM(commission_amount) → t_order)
```

---

## Notas Importantes

1. **Orden de ejecución:** Los triggers BEFORE se ejecutan antes de la operación y pueden modificar NEW. Los triggers AFTER se ejecutan después y se usan para efectos secundarios.

2. **Prefijo `z_`:** El trigger `z_expense_auto_pay_zero_credit` usa prefijo `z_` para ejecutarse último alfabéticamente.

3. **CASCADE:** Al eliminar una orden, `order_history` se elimina automáticamente por CASCADE en la foreign key.

4. **Auditoría completa:** El sistema mantiene historial completo de cambios en órdenes, nómina, gastos fijos, asistencias y vacaciones.

5. **Comisiones automáticas:** Al crear una orden con vendedor, se crea automáticamente el registro de comisión.

6. **Gastos operativos (actualizado 2026-02-09):** `t_order.gasto_operativo` se calcula automáticamente por trigger: `SUM(order_gastos_operativos.monto) + SUM(t_vendor_commission_payment.commission_amount)`. Dos triggers independientes disparan el recálculo: uno en gastos y otro en comisiones.

7. **Propagación de comisión:** Al cambiar `f_commission_rate` en `t_order`, el trigger `sync_commission_rate_from_order` actualiza `t_vendor_commission_payment`, lo que a su vez dispara `trg_recalcular_gasto_op_por_comision` para recalcular el gasto operativo.

**Última actualización:** 09 de Febrero de 2026 (corrección fórmula gastos operativos)
