# Funciones y Triggers - Base de Datos IMA Mecatrónica
Generado: 2026-02-26 22:36:15
Funciones: 49 | Triggers: 30

## Índice de Funciones

### Funciones RPC/Negocio (21)
1. [can_delete_order](#func-can_delete_order)
2. [create_vendor_commission_if_needed](#func-create_vendor_commission_if_needed)
3. [create_vendor_user](#func-create_vendor_user)
4. [delete_order_with_audit](#func-delete_order_with_audit)
5. [f_balance_anual_horizontal](#func-f_balance_anual_horizontal)
6. [f_balance_completo_horizontal](#func-f_balance_completo_horizontal)
7. [generate_holidays_for_year](#func-generate_holidays_for_year)
8. [get_attendance_for_date](#func-get_attendance_for_date)
9. [get_expense_statistics](#func-get_expense_statistics)
10. [get_expense_stats_by_status](#func-get_expense_stats_by_status)
11. [get_month_calendar](#func-get_month_calendar)
12. [get_monthly_payroll_total](#func-get_monthly_payroll_total)
13. [get_monthly_payroll_total](#func-get_monthly_payroll_total)
14. [get_overtime_audit_history](#func-get_overtime_audit_history)
15. [get_payroll_at_date](#func-get_payroll_at_date)
16. [get_vendors](#func-get_vendors)
17. [is_workday](#func-is_workday)
18. [refresh_balance_completo](#func-refresh_balance_completo)
19. [set_current_user_id](#func-set_current_user_id)
20. [update_user_password](#func-update_user_password)
21. [upsert_overtime_hours](#func-upsert_overtime_hours)

### Funciones de Trigger (24)
1. [audit_attendance_changes](#func-audit_attendance_changes)
2. [audit_overtime_hours](#func-audit_overtime_hours)
3. [audit_vacation_changes](#func-audit_vacation_changes)
4. [auto_pay_zero_credit_expense](#func-auto_pay_zero_credit_expense)
5. [calculate_invoice_due_date](#func-calculate_invoice_due_date)
6. [calculate_scheduled_date](#func-calculate_scheduled_date)
7. [create_commission_on_order_creation](#func-create_commission_on_order_creation)
8. [fn_expense_audit](#func-fn_expense_audit)
9. [fn_expense_update_timestamp](#func-fn_expense_update_timestamp)
10. [fn_log_vendor_removal](#func-fn_log_vendor_removal)
11. [recalcular_gasto_operativo](#func-recalcular_gasto_operativo)
12. [recalcular_gasto_operativo_por_comision](#func-recalcular_gasto_operativo_por_comision)
13. [record_order_history](#func-record_order_history)
14. [set_latest_version](#func-set_latest_version)
15. [set_order_audit_fields](#func-set_order_audit_fields)
16. [sync_commission_rate](#func-sync_commission_rate)
17. [sync_commission_rate_from_order](#func-sync_commission_rate_from_order)
18. [track_fixed_expense_changes](#func-track_fixed_expense_changes)
19. [track_payroll_changes](#func-track_payroll_changes)
20. [update_commission_on_order_status_change](#func-update_commission_on_order_status_change)
21. [update_commission_on_vendor_change](#func-update_commission_on_vendor_change)
22. [update_invoice_status](#func-update_invoice_status)
23. [update_order_status_from_invoices](#func-update_order_status_from_invoices)
24. [update_updated_at_column](#func-update_updated_at_column)

### Funciones Huérfanas - sin trigger activo (4)
1. [set_created_by](#func-set_created_by)
2. [update_order_audit_fields](#func-update_order_audit_fields)
3. [update_order_on_invoice_change](#func-update_order_on_invoice_change)
4. [update_order_status_on_invoice](#func-update_order_status_on_invoice)

---

# Funciones RPC / Negocio

## can_delete_order

- **Argumentos**: `p_order_id integer`
- **Retorna**: `TABLE(can_delete boolean, reason text, invoice_count integer, expense_count integer, commission_count integer)`
- **Lenguaje**: `plpgsql`
- **Volatilidad**: `VOLATILE`

### Código Fuente

```plpgsql
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
            'Orden tiene dependencias: ' || v_invoices || ' facturas, ' || v_expenses || ' gastos, ' || v_commissions || ' comisiones',
            v_invoices, v_expenses, v_commissions;
    ELSE
        RETURN QUERY SELECT TRUE, 'Orden puede ser eliminada', v_invoices, v_expenses, v_commissions;
    END IF;
END;
```

---

## create_vendor_commission_if_needed

- **Argumentos**: `p_order_id integer`
- **Retorna**: `void`
- **Lenguaje**: `plpgsql`
- **Volatilidad**: `VOLATILE`

### Código Fuente

```plpgsql
DECLARE
    v_order RECORD;
    v_commission_rate DECIMAL;
BEGIN
    -- Obtener información de la orden
    SELECT * INTO v_order
    FROM t_order
    WHERE f_order = p_order_id
    AND f_orderstat >= 3  -- COMPLETADA o superior
    AND f_salesman IS NOT NULL;
    
    -- Si encontramos la orden y no existe comisión
    IF FOUND AND NOT EXISTS (
        SELECT 1 FROM t_vendor_commission_payment 
        WHERE f_order = p_order_id AND f_vendor = v_order.f_salesman
    ) THEN
        -- Obtener tasa de comisión
        v_commission_rate := COALESCE(v_order.f_commission_rate, 0);
        
        -- Si la comisión es 0, buscar la del vendedor
        IF v_commission_rate = 0 THEN
            SELECT f_commission_rate INTO v_commission_rate
            FROM t_vendor
            WHERE f_vendor = v_order.f_salesman;
            
            v_commission_rate := COALESCE(v_commission_rate, 10);
        END IF;
        
        -- Insertar comisión
        INSERT INTO t_vendor_commission_payment (
            f_order,
            f_vendor,
            commission_rate,
            commission_amount,
            payment_status,
            created_by,
            created_at
        ) VALUES (
            p_order_id,
            v_order.f_salesman,
            v_commission_rate,
            (v_order.f_salesubtotal * v_commission_rate / 100),
            'pending',
            COALESCE(v_order.updated_by, v_order.created_by, 1),
            NOW()
        );
    END IF;
END;
```

---

## create_vendor_user

- **Argumentos**: `username character varying, email character varying, password character varying, fullname character varying, role character varying, isactive boolean`
- **Retorna**: `void`
- **Lenguaje**: `plpgsql`
- **Volatilidad**: `VOLATILE`

### Código Fuente

```plpgsql
BEGIN
    INSERT INTO users (username, email, password_hash, full_name, role, is_active)
    VALUES (
        username,
        email,
        extensions.crypt(password, extensions.gen_salt('bf')),
        fullname,
        role,
        isactive
    );
END;
```

---

## delete_order_with_audit
> Elimina una orden verificando dependencias y guardando auditoría

- **Argumentos**: `p_order_id integer, p_deleted_by integer, p_reason text DEFAULT 'Orden creada por error'::text`
- **Retorna**: `TABLE(success boolean, message text, deleted_order_id integer)`
- **Lenguaje**: `plpgsql`
- **Volatilidad**: `VOLATILE`

### Código Fuente

```plpgsql
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
            'No se puede eliminar: La orden tiene ' || v_invoice_count || ' factura(s) asociada(s). Use CANCELAR en su lugar.',
            NULL::INTEGER;
        RETURN;
    END IF;

    -- Verificar que no tenga gastos
    SELECT COUNT(*) INTO v_expense_count FROM t_expense WHERE f_order = p_order_id;
    IF v_expense_count > 0 THEN
        RETURN QUERY SELECT FALSE,
            'No se puede eliminar: La orden tiene ' || v_expense_count || ' gasto(s) asociado(s). Use CANCELAR en su lugar.',
            NULL::INTEGER;
        RETURN;
    END IF;

    -- Verificar que no tenga comisiones pagadas
    SELECT COUNT(*) INTO v_commission_count FROM t_vendor_commission_payment WHERE f_order = p_order_id;
    IF v_commission_count > 0 THEN
        RETURN QUERY SELECT FALSE,
            'No se puede eliminar: La orden tiene ' || v_commission_count || ' pago(s) de comisión asociado(s). Use CANCELAR en su lugar.',
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
```

---

## f_balance_anual_horizontal

- **Argumentos**: `"p_año" integer`
- **Retorna**: `TABLE("año" integer, nomina_enero numeric, nomina_febrero numeric, nomina_marzo numeric, nomina_abril numeric, nomina_mayo numeric, nomina_junio numeric, nomina_julio numeric, nomina_agosto numeric, nomina_septiembre numeric, nomina_octubre numeric, nomina_noviembre numeric, nomina_diciembre numeric, nomina_total numeric, horas_extra_total numeric, gastos_fijos_enero numeric, gastos_fijos_febrero numeric, gastos_fijos_marzo numeric, gastos_fijos_abril numeric, gastos_fijos_mayo numeric, gastos_fijos_junio numeric, gastos_fijos_julio numeric, gastos_fijos_agosto numeric, gastos_fijos_septiembre numeric, gastos_fijos_octubre numeric, gastos_fijos_noviembre numeric, gastos_fijos_diciembre numeric, gastos_fijos_total numeric, gastos_var_enero numeric, gastos_var_febrero numeric, gastos_var_marzo numeric, gastos_var_abril numeric, gastos_var_mayo numeric, gastos_var_junio numeric, gastos_var_julio numeric, gastos_var_agosto numeric, gastos_var_septiembre numeric, gastos_var_octubre numeric, gastos_var_noviembre numeric, gastos_var_diciembre numeric, gastos_var_total numeric)`
- **Lenguaje**: `plpgsql`
- **Volatilidad**: `VOLATILE`

### Código Fuente

```plpgsql
BEGIN
    RETURN QUERY
    SELECT 
        p_año,
        -- Nómina por mes
        MAX(CASE WHEN bg.mes_numero = 1 THEN bg.nomina ELSE 0 END),
        MAX(CASE WHEN bg.mes_numero = 2 THEN bg.nomina ELSE 0 END),
        MAX(CASE WHEN bg.mes_numero = 3 THEN bg.nomina ELSE 0 END),
        MAX(CASE WHEN bg.mes_numero = 4 THEN bg.nomina ELSE 0 END),
        MAX(CASE WHEN bg.mes_numero = 5 THEN bg.nomina ELSE 0 END),
        MAX(CASE WHEN bg.mes_numero = 6 THEN bg.nomina ELSE 0 END),
        MAX(CASE WHEN bg.mes_numero = 7 THEN bg.nomina ELSE 0 END),
        MAX(CASE WHEN bg.mes_numero = 8 THEN bg.nomina ELSE 0 END),
        MAX(CASE WHEN bg.mes_numero = 9 THEN bg.nomina ELSE 0 END),
        MAX(CASE WHEN bg.mes_numero = 10 THEN bg.nomina ELSE 0 END),
        MAX(CASE WHEN bg.mes_numero = 11 THEN bg.nomina ELSE 0 END),
        MAX(CASE WHEN bg.mes_numero = 12 THEN bg.nomina ELSE 0 END),
        SUM(bg.nomina),
        -- Horas Extra
        0::NUMERIC,
        -- Gastos Fijos por mes
        MAX(CASE WHEN bg.mes_numero = 1 THEN bg.gastos_fijos ELSE 0 END),
        MAX(CASE WHEN bg.mes_numero = 2 THEN bg.gastos_fijos ELSE 0 END),
        MAX(CASE WHEN bg.mes_numero = 3 THEN bg.gastos_fijos ELSE 0 END),
        MAX(CASE WHEN bg.mes_numero = 4 THEN bg.gastos_fijos ELSE 0 END),
        MAX(CASE WHEN bg.mes_numero = 5 THEN bg.gastos_fijos ELSE 0 END),
        MAX(CASE WHEN bg.mes_numero = 6 THEN bg.gastos_fijos ELSE 0 END),
        MAX(CASE WHEN bg.mes_numero = 7 THEN bg.gastos_fijos ELSE 0 END),
        MAX(CASE WHEN bg.mes_numero = 8 THEN bg.gastos_fijos ELSE 0 END),
        MAX(CASE WHEN bg.mes_numero = 9 THEN bg.gastos_fijos ELSE 0 END),
        MAX(CASE WHEN bg.mes_numero = 10 THEN bg.gastos_fijos ELSE 0 END),
        MAX(CASE WHEN bg.mes_numero = 11 THEN bg.gastos_fijos ELSE 0 END),
        MAX(CASE WHEN bg.mes_numero = 12 THEN bg.gastos_fijos ELSE 0 END),
        SUM(bg.gastos_fijos),
        -- Gastos Variables por mes
        MAX(CASE WHEN bg.mes_numero = 1 THEN bg.gastos_variables ELSE 0 END),
        MAX(CASE WHEN bg.mes_numero = 2 THEN bg.gastos_variables ELSE 0 END),
        MAX(CASE WHEN bg.mes_numero = 3 THEN bg.gastos_variables ELSE 0 END),
        MAX(CASE WHEN bg.mes_numero = 4 THEN bg.gastos_variables ELSE 0 END),
        MAX(CASE WHEN bg.mes_numero = 5 THEN bg.gastos_variables ELSE 0 END),
        MAX(CASE WHEN bg.mes_numero = 6 THEN bg.gastos_variables ELSE 0 END),
        MAX(CASE WHEN bg.mes_numero = 7 THEN bg.gastos_variables ELSE 0 END),
        MAX(CASE WHEN bg.mes_numero = 8 THEN bg.gastos_variables ELSE 0 END),
        MAX(CASE WHEN bg.mes_numero = 9 THEN bg.gastos_variables ELSE 0 END),
        MAX(CASE WHEN bg.mes_numero = 10 THEN bg.gastos_variables ELSE 0 END),
        MAX(CASE WHEN bg.mes_numero = 11 THEN bg.gastos_variables ELSE 0 END),
        MAX(CASE WHEN bg.mes_numero = 12 THEN bg.gastos_variables ELSE 0 END),
        SUM(bg.gastos_variables)
    FROM v_balance_gastos bg
    WHERE bg.año = p_año;
END;
```

---

## f_balance_completo_horizontal

- **Argumentos**: `"p_año" integer`
- **Retorna**: `TABLE(seccion text, concepto text, enero numeric, febrero numeric, marzo numeric, abril numeric, mayo numeric, junio numeric, julio numeric, agosto numeric, septiembre numeric, octubre numeric, noviembre numeric, diciembre numeric, total_anual numeric)`
- **Lenguaje**: `plpgsql`
- **Volatilidad**: `VOLATILE`

### Código Fuente

```plpgsql
BEGIN
    RETURN QUERY
    -- SECCIÓN GASTOS
    SELECT 
        'GASTOS'::TEXT,
        'Nómina'::TEXT,
        SUM(CASE WHEN mes_numero = 1 THEN nomina ELSE 0 END),
        SUM(CASE WHEN mes_numero = 2 THEN nomina ELSE 0 END),
        SUM(CASE WHEN mes_numero = 3 THEN nomina ELSE 0 END),
        SUM(CASE WHEN mes_numero = 4 THEN nomina ELSE 0 END),
        SUM(CASE WHEN mes_numero = 5 THEN nomina ELSE 0 END),
        SUM(CASE WHEN mes_numero = 6 THEN nomina ELSE 0 END),
        SUM(CASE WHEN mes_numero = 7 THEN nomina ELSE 0 END),
        SUM(CASE WHEN mes_numero = 8 THEN nomina ELSE 0 END),
        SUM(CASE WHEN mes_numero = 9 THEN nomina ELSE 0 END),
        SUM(CASE WHEN mes_numero = 10 THEN nomina ELSE 0 END),
        SUM(CASE WHEN mes_numero = 11 THEN nomina ELSE 0 END),
        SUM(CASE WHEN mes_numero = 12 THEN nomina ELSE 0 END),
        SUM(nomina)
    FROM v_balance_completo WHERE año = p_año
    
    UNION ALL
    
    SELECT 
        'GASTOS'::TEXT,
        'Horas Extra'::TEXT,
        SUM(CASE WHEN mes_numero = 1 THEN horas_extra ELSE 0 END),
        SUM(CASE WHEN mes_numero = 2 THEN horas_extra ELSE 0 END),
        SUM(CASE WHEN mes_numero = 3 THEN horas_extra ELSE 0 END),
        SUM(CASE WHEN mes_numero = 4 THEN horas_extra ELSE 0 END),
        SUM(CASE WHEN mes_numero = 5 THEN horas_extra ELSE 0 END),
        SUM(CASE WHEN mes_numero = 6 THEN horas_extra ELSE 0 END),
        SUM(CASE WHEN mes_numero = 7 THEN horas_extra ELSE 0 END),
        SUM(CASE WHEN mes_numero = 8 THEN horas_extra ELSE 0 END),
        SUM(CASE WHEN mes_numero = 9 THEN horas_extra ELSE 0 END),
        SUM(CASE WHEN mes_numero = 10 THEN horas_extra ELSE 0 END),
        SUM(CASE WHEN mes_numero = 11 THEN horas_extra ELSE 0 END),
        SUM(CASE WHEN mes_numero = 12 THEN horas_extra ELSE 0 END),
        SUM(horas_extra)
    FROM v_balance_completo WHERE año = p_año
    
    UNION ALL
    
    SELECT 
        'GASTOS'::TEXT,
        'Gastos Fijos'::TEXT,
        SUM(CASE WHEN mes_numero = 1 THEN gastos_fijos ELSE 0 END),
        SUM(CASE WHEN mes_numero = 2 THEN gastos_fijos ELSE 0 END),
        SUM(CASE WHEN mes_numero = 3 THEN gastos_fijos ELSE 0 END),
        SUM(CASE WHEN mes_numero = 4 THEN gastos_fijos ELSE 0 END),
        SUM(CASE WHEN mes_numero = 5 THEN gastos_fijos ELSE 0 END),
        SUM(CASE WHEN mes_numero = 6 THEN gastos_fijos ELSE 0 END),
        SUM(CASE WHEN mes_numero = 7 THEN gastos_fijos ELSE 0 END),
        SUM(CASE WHEN mes_numero = 8 THEN gastos_fijos ELSE 0 END),
        SUM(CASE WHEN mes_numero = 9 THEN gastos_fijos ELSE 0 END),
        SUM(CASE WHEN mes_numero = 10 THEN gastos_fijos ELSE 0 END),
        SUM(CASE WHEN mes_numero = 11 THEN gastos_fijos ELSE 0 END),
        SUM(CASE WHEN mes_numero = 12 THEN gastos_fijos ELSE 0 END),
        SUM(gastos_fijos)
    FROM v_balance_completo WHERE año = p_año
    
    UNION ALL
    
    SELECT 
        'GASTOS'::TEXT,
        'Gasto Variable'::TEXT,
        SUM(CASE WHEN mes_numero = 1 THEN gastos_variables ELSE 0 END),
        SUM(CASE WHEN mes_numero = 2 THEN gastos_variables ELSE 0 END),
        SUM(CASE WHEN mes_numero = 3 THEN gastos_variables ELSE 0 END),
        SUM(CASE WHEN mes_numero = 4 THEN gastos_variables ELSE 0 END),
        SUM(CASE WHEN mes_numero = 5 THEN gastos_variables ELSE 0 END),
        SUM(CASE WHEN mes_numero = 6 THEN gastos_variables ELSE 0 END),
        SUM(CASE WHEN mes_numero = 7 THEN gastos_variables ELSE 0 END),
        SUM(CASE WHEN mes_numero = 8 THEN gastos_variables ELSE 0 END),
        SUM(CASE WHEN mes_numero = 9 THEN gastos_variables ELSE 0 END),
        SUM(CASE WHEN mes_numero = 10 THEN gastos_variables ELSE 0 END),
        SUM(CASE WHEN mes_numero = 11 THEN gastos_variables ELSE 0 END),
        SUM(CASE WHEN mes_numero = 12 THEN gastos_variables ELSE 0 END),
        SUM(gastos_variables)
    FROM v_balance_completo WHERE año = p_año
    
    UNION ALL
    
    -- SECCIÓN INGRESOS
    SELECT 
        'INGRESOS'::TEXT,
        'Ingresos Esperados'::TEXT,
        SUM(CASE WHEN mes_numero = 1 THEN ingresos_esperados ELSE 0 END),
        SUM(CASE WHEN mes_numero = 2 THEN ingresos_esperados ELSE 0 END),
        SUM(CASE WHEN mes_numero = 3 THEN ingresos_esperados ELSE 0 END),
        SUM(CASE WHEN mes_numero = 4 THEN ingresos_esperados ELSE 0 END),
        SUM(CASE WHEN mes_numero = 5 THEN ingresos_esperados ELSE 0 END),
        SUM(CASE WHEN mes_numero = 6 THEN ingresos_esperados ELSE 0 END),
        SUM(CASE WHEN mes_numero = 7 THEN ingresos_esperados ELSE 0 END),
        SUM(CASE WHEN mes_numero = 8 THEN ingresos_esperados ELSE 0 END),
        SUM(CASE WHEN mes_numero = 9 THEN ingresos_esperados ELSE 0 END),
        SUM(CASE WHEN mes_numero = 10 THEN ingresos_esperados ELSE 0 END),
        SUM(CASE WHEN mes_numero = 11 THEN ingresos_esperados ELSE 0 END),
        SUM(CASE WHEN mes_numero = 12 THEN ingresos_esperados ELSE 0 END),
        SUM(ingresos_esperados)
    FROM v_balance_completo WHERE año = p_año
    
    UNION ALL
    
    SELECT 
        'INGRESOS'::TEXT,
        'Ingresos Percibidos'::TEXT,
        SUM(CASE WHEN mes_numero = 1 THEN ingresos_percibidos ELSE 0 END),
        SUM(CASE WHEN mes_numero = 2 THEN ingresos_percibidos ELSE 0 END),
        SUM(CASE WHEN mes_numero = 3 THEN ingresos_percibidos ELSE 0 END),
        SUM(CASE WHEN mes_numero = 4 THEN ingresos_percibidos ELSE 0 END),
        SUM(CASE WHEN mes_numero = 5 THEN ingresos_percibidos ELSE 0 END),
        SUM(CASE WHEN mes_numero = 6 THEN ingresos_percibidos ELSE 0 END),
        SUM(CASE WHEN mes_numero = 7 THEN ingresos_percibidos ELSE 0 END),
        SUM(CASE WHEN mes_numero = 8 THEN ingresos_percibidos ELSE 0 END),
        SUM(CASE WHEN mes_numero = 9 THEN ingresos_percibidos ELSE 0 END),
        SUM(CASE WHEN mes_numero = 10 THEN ingresos_percibidos ELSE 0 END),
        SUM(CASE WHEN mes_numero = 11 THEN ingresos_percibidos ELSE 0 END),
        SUM(CASE WHEN mes_numero = 12 THEN ingresos_percibidos ELSE 0 END),
        SUM(ingresos_percibidos)
    FROM v_balance_completo WHERE año = p_año
    
    UNION ALL
    
    SELECT 
        'INGRESOS'::TEXT,
        'Diferencia'::TEXT,
        SUM(CASE WHEN mes_numero = 1 THEN diferencia ELSE 0 END),
        SUM(CASE WHEN mes_numero = 2 THEN diferencia ELSE 0 END),
        SUM(CASE WHEN mes_numero = 3 THEN diferencia ELSE 0 END),
        SUM(CASE WHEN mes_numero = 4 THEN diferencia ELSE 0 END),
        SUM(CASE WHEN mes_numero = 5 THEN diferencia ELSE 0 END),
        SUM(CASE WHEN mes_numero = 6 THEN diferencia ELSE 0 END),
        SUM(CASE WHEN mes_numero = 7 THEN diferencia ELSE 0 END),
        SUM(CASE WHEN mes_numero = 8 THEN diferencia ELSE 0 END),
        SUM(CASE WHEN mes_numero = 9 THEN diferencia ELSE 0 END),
        SUM(CASE WHEN mes_numero = 10 THEN diferencia ELSE 0 END),
        SUM(CASE WHEN mes_numero = 11 THEN diferencia ELSE 0 END),
        SUM(CASE WHEN mes_numero = 12 THEN diferencia ELSE 0 END),
        SUM(diferencia)
    FROM v_balance_completo WHERE año = p_año
    
    UNION ALL
    
    -- UTILIDAD
    SELECT 
        'RESULTADO'::TEXT,
        'Utilidad Aproximada'::TEXT,
        SUM(CASE WHEN mes_numero = 1 THEN utilidad_aproximada ELSE 0 END),
        SUM(CASE WHEN mes_numero = 2 THEN utilidad_aproximada ELSE 0 END),
        SUM(CASE WHEN mes_numero = 3 THEN utilidad_aproximada ELSE 0 END),
        SUM(CASE WHEN mes_numero = 4 THEN utilidad_aproximada ELSE 0 END),
        SUM(CASE WHEN mes_numero = 5 THEN utilidad_aproximada ELSE 0 END),
        SUM(CASE WHEN mes_numero = 6 THEN utilidad_aproximada ELSE 0 END),
        SUM(CASE WHEN mes_numero = 7 THEN utilidad_aproximada ELSE 0 END),
        SUM(CASE WHEN mes_numero = 8 THEN utilidad_aproximada ELSE 0 END),
        SUM(CASE WHEN mes_numero = 9 THEN utilidad_aproximada ELSE 0 END),
        SUM(CASE WHEN mes_numero = 10 THEN utilidad_aproximada ELSE 0 END),
        SUM(CASE WHEN mes_numero = 11 THEN utilidad_aproximada ELSE 0 END),
        SUM(CASE WHEN mes_numero = 12 THEN utilidad_aproximada ELSE 0 END),
        SUM(utilidad_aproximada)
    FROM v_balance_completo WHERE año = p_año
    
    UNION ALL
    
    -- VENTAS TOTALES
    SELECT 
        'VENTAS'::TEXT,
        'Ventas Totales'::TEXT,
        SUM(CASE WHEN mes_numero = 1 THEN ventas_totales ELSE 0 END),
        SUM(CASE WHEN mes_numero = 2 THEN ventas_totales ELSE 0 END),
        SUM(CASE WHEN mes_numero = 3 THEN ventas_totales ELSE 0 END),
        SUM(CASE WHEN mes_numero = 4 THEN ventas_totales ELSE 0 END),
        SUM(CASE WHEN mes_numero = 5 THEN ventas_totales ELSE 0 END),
        SUM(CASE WHEN mes_numero = 6 THEN ventas_totales ELSE 0 END),
        SUM(CASE WHEN mes_numero = 7 THEN ventas_totales ELSE 0 END),
        SUM(CASE WHEN mes_numero = 8 THEN ventas_totales ELSE 0 END),
        SUM(CASE WHEN mes_numero = 9 THEN ventas_totales ELSE 0 END),
        SUM(CASE WHEN mes_numero = 10 THEN ventas_totales ELSE 0 END),
        SUM(CASE WHEN mes_numero = 11 THEN ventas_totales ELSE 0 END),
        SUM(CASE WHEN mes_numero = 12 THEN ventas_totales ELSE 0 END),
        SUM(ventas_totales)
    FROM v_balance_completo WHERE año = p_año;
END;
```

---

## generate_holidays_for_year
> Genera los feriados recurrentes para un año específico

- **Argumentos**: `target_year integer`
- **Retorna**: `integer`
- **Lenguaje**: `plpgsql`
- **Volatilidad**: `VOLATILE`

### Código Fuente

```plpgsql
DECLARE
    inserted_count INTEGER := 0;
    holiday_rec RECORD;
BEGIN
    -- Copiar feriados recurrentes al nuevo año
    FOR holiday_rec IN
        SELECT name, description, is_mandatory, recurring_month, recurring_day
        FROM t_holiday
        WHERE is_recurring = TRUE
        AND recurring_month IS NOT NULL
        AND recurring_day IS NOT NULL
    LOOP
        INSERT INTO t_holiday (holiday_date, name, description, is_mandatory, is_recurring, recurring_month, recurring_day, year)
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
```

---

## get_attendance_for_date
> Obtiene el estado de asistencia de todos los empleados para una fecha específica

- **Argumentos**: `check_date date`
- **Retorna**: `TABLE(employee_id integer, employee_name character varying, title character varying, employee_code character varying, initials character varying, attendance_id integer, status character varying, check_in_time time without time zone, check_out_time time without time zone, late_minutes integer, notes text, is_justified boolean, on_vacation boolean, vacation_start date, vacation_end date, is_holiday boolean, holiday_name character varying, is_workday boolean)`
- **Lenguaje**: `plpgsql`
- **Volatilidad**: `VOLATILE`

### Código Fuente

```plpgsql
BEGIN
    RETURN QUERY
    SELECT
        p.f_payroll,
        p.f_employee,
        p.f_title,
        p.employee_code,
        SUBSTRING(p.f_employee, 1, 1) ||
            COALESCE(SUBSTRING(SPLIT_PART(p.f_employee, ' ', 2), 1, 1), ''),
        a.id,
        COALESCE(a.status, 'SIN_REGISTRO'),
        a.check_in_time,
        a.check_out_time,
        a.late_minutes,
        a.notes,
        a.is_justified,
        CASE WHEN v.id IS NOT NULL THEN TRUE ELSE FALSE END,
        v.start_date,
        v.end_date,
        CASE WHEN h.id IS NOT NULL THEN TRUE ELSE FALSE END,
        h.name,
        wc.is_workday
    FROM t_payroll p
    LEFT JOIN t_attendance a ON p.f_payroll = a.employee_id AND a.attendance_date = check_date
    LEFT JOIN t_vacation v ON p.f_payroll = v.employee_id
        AND v.status = 'APROBADA'
        AND check_date BETWEEN v.start_date AND v.end_date
    LEFT JOIN t_holiday h ON h.holiday_date = check_date AND h.is_mandatory = TRUE
    LEFT JOIN t_workday_config wc ON wc.day_of_week = EXTRACT(DOW FROM check_date)
    WHERE p.is_active = TRUE
    ORDER BY p.f_employee;
END;
```

---

## get_expense_statistics

- **Argumentos**: `(ninguno)`
- **Retorna**: `json`
- **Lenguaje**: `sql`
- **Volatilidad**: `STABLE`

### Código Fuente

```sql
SELECT json_build_object(
        'TotalExpenses', COALESCE(SUM(f_totalexpense), 0),
        'PendingExpenses', COALESCE(SUM(CASE WHEN f_status = 'PENDIENTE' THEN f_totalexpense ELSE 0 END), 0),
        'PaidExpenses', COALESCE(SUM(CASE WHEN f_status = 'PAGADO' THEN f_totalexpense ELSE 0 END), 0),
        'OverdueExpenses', COALESCE(SUM(CASE WHEN f_status = 'PENDIENTE' AND f_scheduleddate < NOW() THEN f_totalexpense ELSE 0 END), 0),
        'ExpenseCount', COUNT(*),
        'AverageExpense', COALESCE(AVG(f_totalexpense), 0)
    )
    FROM t_expense;
```

---

## get_expense_stats_by_status

- **Argumentos**: `(ninguno)`
- **Retorna**: `TABLE(status text, total numeric)`
- **Lenguaje**: `sql`
- **Volatilidad**: `STABLE`

### Código Fuente

```sql
SELECT f_status, COALESCE(SUM(f_totalexpense), 0)
    FROM t_expense
    GROUP BY f_status;
```

---

## get_month_calendar
> Genera el calendario del mes con estadísticas de asistencia

- **Argumentos**: `target_year integer, target_month integer`
- **Retorna**: `TABLE(calendar_date date, day_of_week integer, day_name character varying, is_workday boolean, is_holiday boolean, holiday_name character varying, total_employees integer, asistencias integer, retardos integer, faltas integer, vacaciones integer, sin_registro integer)`
- **Lenguaje**: `plpgsql`
- **Volatilidad**: `VOLATILE`

### Código Fuente

```plpgsql
DECLARE
    first_day DATE;
    last_day DATE;
BEGIN
    first_day := MAKE_DATE(target_year, target_month, 1);
    last_day := (first_day + INTERVAL '1 month' - INTERVAL '1 day')::DATE;

    RETURN QUERY
    SELECT
        d.date::DATE,
        EXTRACT(DOW FROM d.date)::INTEGER,
        TO_CHAR(d.date, 'Day')::VARCHAR,
        COALESCE(wc.is_workday, TRUE),
        CASE WHEN h.id IS NOT NULL THEN TRUE ELSE FALSE END,
        h.name::VARCHAR,
        (SELECT COUNT(*)::INTEGER FROM t_payroll WHERE is_active = TRUE),
        COUNT(CASE WHEN a.status = 'ASISTENCIA' THEN 1 END)::INTEGER,
        COUNT(CASE WHEN a.status = 'RETARDO' THEN 1 END)::INTEGER,
        COUNT(CASE WHEN a.status = 'FALTA' THEN 1 END)::INTEGER,
        COUNT(CASE WHEN a.status = 'VACACIONES' THEN 1 END)::INTEGER,
        ((SELECT COUNT(*) FROM t_payroll WHERE is_active = TRUE) - COUNT(a.id))::INTEGER
    FROM generate_series(first_day, last_day, '1 day'::INTERVAL) AS d(date)
    LEFT JOIN t_workday_config wc ON wc.day_of_week = EXTRACT(DOW FROM d.date)
    LEFT JOIN t_holiday h ON h.holiday_date = d.date AND h.is_mandatory = TRUE
    LEFT JOIN t_attendance a ON a.attendance_date = d.date
    GROUP BY d.date, wc.is_workday, h.id, h.name
    ORDER BY d.date;
END;
```

---

## get_monthly_payroll_total

- **Argumentos**: `(ninguno)`
- **Retorna**: `numeric`
- **Lenguaje**: `sql`
- **Volatilidad**: `STABLE`

### Código Fuente

```sql
SELECT COALESCE(SUM(f_monthlypayroll), 0)
    FROM t_payroll
    WHERE is_active = true;
```

---

## get_monthly_payroll_total

- **Argumentos**: `year_param integer, month_param integer`
- **Retorna**: `numeric`
- **Lenguaje**: `plpgsql`
- **Volatilidad**: `VOLATILE`

### Código Fuente

```plpgsql
DECLARE
    target_date DATE;
    total NUMERIC;
BEGIN
    -- Último día del mes para asegurar que tomamos todos los cambios del mes
    target_date := (DATE_TRUNC('month', MAKE_DATE(year_param, month_param, 1)) + INTERVAL '1 month - 1 day')::DATE;
    
    SELECT COALESCE(SUM(f_monthlypayroll), 0)
    INTO total
    FROM get_payroll_at_date(target_date);
    
    RETURN total;
END;
```

---

## get_overtime_audit_history

- **Argumentos**: `p_year integer, p_month integer`
- **Retorna**: `TABLE(change_date timestamp without time zone, change_type character varying, old_amount numeric, new_amount numeric, changed_by_name character varying, notes text)`
- **Lenguaje**: `plpgsql`
- **Volatilidad**: `VOLATILE`

### Código Fuente

```plpgsql
BEGIN
    RETURN QUERY
    SELECT 
        a.changed_at,
        a.change_type,
        a.old_amount,
        a.new_amount,
        u.full_name,
        a.change_reason
    FROM t_overtime_hours_audit a
    LEFT JOIN users u ON a.changed_by = u.id
    WHERE a.year = p_year 
      AND a.month = p_month
    ORDER BY a.changed_at DESC;
END;
```

---

## get_payroll_at_date

- **Argumentos**: `target_date date`
- **Retorna**: `TABLE(f_payroll integer, f_employee character varying, f_title character varying, f_range character varying, f_condition character varying, f_monthlypayroll numeric, is_active boolean, effective_from date)`
- **Lenguaje**: `plpgsql`
- **Volatilidad**: `VOLATILE`

### Código Fuente

```plpgsql
BEGIN
    RETURN QUERY
    WITH latest_changes AS (
        SELECT DISTINCT ON (h.f_payroll)
            h.f_payroll,
            h.f_employee,
            h.f_title,
            h.f_range,
            h.f_condition,
            h.f_monthlypayroll,
            h.is_active,
            h.effective_date
        FROM t_payroll_history h
        WHERE h.effective_date <= target_date
        ORDER BY h.f_payroll, h.effective_date DESC
    )
    SELECT 
        lc.f_payroll,
        lc.f_employee,
        lc.f_title,
        lc.f_range,
        lc.f_condition,
        lc.f_monthlypayroll,
        lc.is_active,
        lc.effective_date
    FROM latest_changes lc
    WHERE lc.is_active = TRUE;
END;
```

---

## get_vendors
> Obtiene lista de vendedores activos para los combos

- **Argumentos**: `(ninguno)`
- **Retorna**: `TABLE(id integer, full_name character varying, username character varying)`
- **Lenguaje**: `plpgsql`
- **Volatilidad**: `VOLATILE`

### Código Fuente

```plpgsql
BEGIN
    RETURN QUERY
    SELECT 
        u.id,
        u.full_name,
        u.username
    FROM users u
    WHERE u.role = 'salesperson'
        AND u.is_active = true
    ORDER BY u.full_name;
END;
```

---

## is_workday
> Verifica si una fecha es día laboral (no feriado, no fin de semana)

- **Argumentos**: `check_date date`
- **Retorna**: `boolean`
- **Lenguaje**: `plpgsql`
- **Volatilidad**: `VOLATILE`

### Código Fuente

```plpgsql
DECLARE
    day_config RECORD;
    is_holiday BOOLEAN;
BEGIN
    -- Verificar si es feriado
    SELECT EXISTS(SELECT 1 FROM t_holiday WHERE holiday_date = check_date AND is_mandatory = TRUE)
    INTO is_holiday;

    IF is_holiday THEN
        RETURN FALSE;
    END IF;

    -- Verificar configuración del día de la semana
    SELECT is_workday INTO day_config.is_workday
    FROM t_workday_config
    WHERE day_of_week = EXTRACT(DOW FROM check_date);

    RETURN COALESCE(day_config.is_workday, TRUE);
END;
```

---

## refresh_balance_completo

- **Argumentos**: `(ninguno)`
- **Retorna**: `void`
- **Lenguaje**: `plpgsql`
- **Volatilidad**: `VOLATILE`
- **Security**: `SECURITY DEFINER`

### Código Fuente

```plpgsql
BEGIN
    REFRESH MATERIALIZED VIEW CONCURRENTLY mv_balance_completo;
END;
```

---

## set_current_user_id

- **Argumentos**: `p_user_id integer`
- **Retorna**: `void`
- **Lenguaje**: `plpgsql`
- **Volatilidad**: `VOLATILE`

### Código Fuente

```plpgsql
BEGIN
    PERFORM set_config('app.current_user_id', p_user_id::TEXT, false);
END;
```

---

## update_user_password

- **Argumentos**: `user_id integer, new_password character varying`
- **Retorna**: `void`
- **Lenguaje**: `plpgsql`
- **Volatilidad**: `VOLATILE`

### Código Fuente

```plpgsql
BEGIN
    UPDATE users 
    SET password_hash = extensions.crypt(new_password, extensions.gen_salt('bf'))
    WHERE id = user_id;
END;
```

---

## upsert_overtime_hours

- **Argumentos**: `p_year integer, p_month integer, p_amount numeric, p_notes text, p_user_id integer`
- **Retorna**: `TABLE(success boolean, message text, overtime_id integer)`
- **Lenguaje**: `plpgsql`
- **Volatilidad**: `VOLATILE`

### Código Fuente

```plpgsql
DECLARE
    v_overtime_id INTEGER;
    v_old_amount NUMERIC;
BEGIN
    -- Verificar si ya existe
    SELECT id, amount INTO v_overtime_id, v_old_amount
    FROM t_overtime_hours
    WHERE year = p_year AND month = p_month;
    
    IF v_overtime_id IS NULL THEN
        -- Solo insertar si el monto es mayor a 0
        IF p_amount > 0 THEN
            INSERT INTO t_overtime_hours (year, month, amount, notes, created_by, updated_by)
            VALUES (p_year, p_month, p_amount, p_notes, p_user_id, p_user_id)
            RETURNING id INTO v_overtime_id;
            
            RETURN QUERY SELECT true, 'Horas extras registradas', v_overtime_id;
        ELSE
            -- No insertar registro con 0
            RETURN QUERY SELECT true, 'Sin horas extras', 0;
        END IF;
    ELSE
        -- Si existe y el nuevo valor es 0, eliminar el registro
        IF p_amount = 0 THEN
            DELETE FROM t_overtime_hours WHERE id = v_overtime_id;
            RETURN QUERY SELECT true, 'Horas extras eliminadas', 0;
        ELSE
            -- Actualizar con nuevo valor
            UPDATE t_overtime_hours
            SET amount = p_amount,
                notes = p_notes,
                updated_by = p_user_id,
                updated_at = CURRENT_TIMESTAMP
            WHERE id = v_overtime_id;
            
            RETURN QUERY SELECT true, 
                FORMAT('Actualizado de %s a %s', v_old_amount::money, p_amount::money),
                v_overtime_id;
        END IF;
    END IF;
END;
```

---

# Funciones de Trigger

## audit_attendance_changes

- **Retorna**: `trigger`
- **Lenguaje**: `plpgsql`
- **Usado por triggers**:
  - `trg_attendance_audit` en `t_attendance` (AFTER INSERT OR UPDATE OR DELETE FOR EACH ROW)

### Código Fuente

```plpgsql
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
```

---

## audit_overtime_hours

- **Retorna**: `trigger`
- **Lenguaje**: `plpgsql`
- **Usado por triggers**:
  - `trigger_overtime_audit` en `t_overtime_hours` (AFTER INSERT OR UPDATE OR DELETE FOR EACH ROW)

### Código Fuente

```plpgsql
BEGIN
    IF TG_OP = 'INSERT' THEN
        INSERT INTO t_overtime_hours_audit (
            overtime_id, year, month, old_amount, new_amount, 
            change_type, changed_by
        ) VALUES (
            NEW.id, NEW.year, NEW.month, NULL, NEW.amount, 
            'INSERT', NEW.created_by
        );
    ELSIF TG_OP = 'UPDATE' THEN
        IF OLD.amount != NEW.amount THEN
            INSERT INTO t_overtime_hours_audit (
                overtime_id, year, month, old_amount, new_amount, 
                change_type, changed_by
            ) VALUES (
                NEW.id, NEW.year, NEW.month, OLD.amount, NEW.amount, 
                'UPDATE', NEW.updated_by
            );
        END IF;
    ELSIF TG_OP = 'DELETE' THEN
        INSERT INTO t_overtime_hours_audit (
            overtime_id, year, month, old_amount, new_amount, 
            change_type, changed_by
        ) VALUES (
            OLD.id, OLD.year, OLD.month, OLD.amount, NULL, 
            'DELETE', OLD.updated_by
        );
    END IF;
    RETURN NEW;
END;
```

---

## audit_vacation_changes

- **Retorna**: `trigger`
- **Lenguaje**: `plpgsql`
- **Usado por triggers**:
  - `trg_vacation_audit` en `t_vacation` (AFTER INSERT OR UPDATE OR DELETE FOR EACH ROW)

### Código Fuente

```plpgsql
BEGIN
    IF TG_OP = 'INSERT' THEN
        INSERT INTO t_vacation_audit (
            vacation_id, employee_id, action,
            new_start_date, new_end_date, new_status, new_notes,
            changed_by
        ) VALUES (
            NEW.id, NEW.employee_id, 'INSERT',
            NEW.start_date, NEW.end_date, NEW.status, NEW.notes,
            NEW.created_by
        );
        RETURN NEW;

    ELSIF TG_OP = 'UPDATE' THEN
        INSERT INTO t_vacation_audit (
            vacation_id, employee_id, action,
            old_start_date, old_end_date, old_status, old_notes,
            new_start_date, new_end_date, new_status, new_notes,
            changed_by
        ) VALUES (
            NEW.id, NEW.employee_id, 'UPDATE',
            OLD.start_date, OLD.end_date, OLD.status, OLD.notes,
            NEW.start_date, NEW.end_date, NEW.status, NEW.notes,
            NEW.updated_by
        );
        RETURN NEW;

    ELSIF TG_OP = 'DELETE' THEN
        INSERT INTO t_vacation_audit (
            vacation_id, employee_id, action,
            old_start_date, old_end_date, old_status, old_notes,
            changed_by
        ) VALUES (
            OLD.id, OLD.employee_id, 'DELETE',
            OLD.start_date, OLD.end_date, OLD.status, OLD.notes,
            OLD.updated_by
        );
        RETURN OLD;
    END IF;

    RETURN NULL;
END;
```

---

## auto_pay_zero_credit_expense

- **Retorna**: `trigger`
- **Lenguaje**: `plpgsql`
- **Usado por triggers**:
  - `z_expense_auto_pay_zero_credit` en `t_expense` (BEFORE INSERT OR UPDATE FOR EACH ROW)

### Código Fuente

```plpgsql
DECLARE
    supplier_credit INTEGER;
BEGIN
    -- Solo procesar en INSERT o cuando cambia el proveedor
    IF (TG_OP = 'INSERT' OR 
        (TG_OP = 'UPDATE' AND OLD.f_supplier IS DISTINCT FROM NEW.f_supplier)) THEN
        
        -- Solo si hay proveedor y no está ya pagado
        IF NEW.f_supplier IS NOT NULL 
           AND (NEW.f_status IS NULL OR NEW.f_status != 'PAGADO') THEN
            
            -- Obtener los días de crédito del proveedor
            SELECT f_credit INTO supplier_credit
            FROM t_supplier
            WHERE f_supplier = NEW.f_supplier;
            
            -- Si el proveedor tiene crédito 0 o NULL, marcar como pagado
            IF supplier_credit = 0 OR supplier_credit IS NULL THEN
                NEW.f_status := 'PAGADO';
                NEW.f_paiddate := NEW.f_expensedate;
                NEW.f_paymethod := 'CONTADO';
                
                RAISE NOTICE 'Gasto auto-pagado (proveedor sin crédito)';
            ELSIF NEW.f_status IS NULL THEN
                -- Si tiene crédito y no tiene estado, ponerlo PENDIENTE
                NEW.f_status := 'PENDIENTE';
            END IF;
        END IF;
    END IF;
    
    RETURN NEW;
END;
```

---

## calculate_invoice_due_date

- **Retorna**: `trigger`
- **Lenguaje**: `plpgsql`
- **Usado por triggers**:
  - `trigger_calculate_due_date` en `t_invoice` (BEFORE INSERT OR UPDATE FOR EACH ROW)

### Código Fuente

```plpgsql
DECLARE
    v_credit_days INTEGER;
BEGIN
    -- Solo calcular si hay fecha de recepción y no hay due_date
    IF NEW.f_receptiondate IS NOT NULL THEN
        -- Obtener días de crédito del cliente
        SELECT c.f_credit INTO v_credit_days
        FROM t_order o
        JOIN t_client c ON o.f_client = c.f_client
        WHERE o.f_order = NEW.f_order;
        
        -- Si no se encuentra o es 0, usar 30 días por defecto
        IF v_credit_days IS NULL OR v_credit_days = 0 THEN
            v_credit_days := 30;
        END IF;
        
        -- Calcular fecha programada
        NEW.due_date := NEW.f_receptiondate + (v_credit_days || ' days')::INTERVAL;
        
        -- Actualizar estado de factura
        -- Estado 2 (PENDIENTE) si tiene recepción y programada
        -- Estado 3 (VENCIDA) si ya pasó la fecha programada
        IF NEW.f_paymentdate IS NOT NULL THEN
            NEW.f_invoicestat := 4; -- PAGADA
        ELSIF NEW.due_date < CURRENT_DATE THEN
            NEW.f_invoicestat := 3; -- VENCIDA
        ELSE
            NEW.f_invoicestat := 2; -- PENDIENTE
        END IF;
    END IF;
    
    RETURN NEW;
END;
```

---

## calculate_scheduled_date

- **Retorna**: `trigger`
- **Lenguaje**: `plpgsql`
- **Usado por triggers**:
  - `trigger_expense_scheduled_date` en `t_expense` (BEFORE INSERT OR UPDATE FOR EACH ROW)

### Código Fuente

```plpgsql
DECLARE
    v_credit_days INTEGER;
    v_supplier_name VARCHAR;
BEGIN
    RAISE NOTICE 'TRIGGER INICIADO: Operación=%, Expense ID=%', TG_OP, NEW.f_expense;
    
    -- Verificar si debemos calcular
    IF (TG_OP = 'INSERT') THEN
        RAISE NOTICE 'Es INSERT, calculando fecha programada...';
    ELSIF (TG_OP = 'UPDATE') THEN
        RAISE NOTICE 'Es UPDATE. Old Date=%, New Date=%, Old Supplier=%, New Supplier=%', 
            OLD.f_expensedate, NEW.f_expensedate, OLD.f_supplier, NEW.f_supplier;
    END IF;
    
    -- Solo calcular si tenemos los datos necesarios
    IF NEW.f_expensedate IS NOT NULL AND NEW.f_supplier IS NOT NULL THEN
        -- Obtener información del proveedor
        SELECT f_credit, f_suppliername 
        INTO v_credit_days, v_supplier_name
        FROM t_supplier
        WHERE f_supplier = NEW.f_supplier;
        
        IF v_credit_days IS NULL THEN
            v_credit_days := 30;
            RAISE NOTICE 'Proveedor % no encontrado o sin días de crédito, usando 30 días por defecto', NEW.f_supplier;
        ELSE
            RAISE NOTICE 'Proveedor % (%): % días de crédito', NEW.f_supplier, v_supplier_name, v_credit_days;
        END IF;
        
        -- Calcular fecha programada
        NEW.f_scheduleddate := NEW.f_expensedate + v_credit_days;
        RAISE NOTICE 'CÁLCULO FINAL: % + % días = %', NEW.f_expensedate, v_credit_days, NEW.f_scheduleddate;
    ELSE
        RAISE NOTICE 'Datos incompletos: ExpenseDate=%, Supplier=%', NEW.f_expensedate, NEW.f_supplier;
    END IF;
    
    NEW.updated_at := CURRENT_TIMESTAMP;
    RETURN NEW;
END;
```

---

## create_commission_on_order_creation

- **Retorna**: `trigger`
- **Lenguaje**: `plpgsql`
- **Usado por triggers**:
  - `trigger_create_commission_on_order` en `t_order` (AFTER INSERT FOR EACH ROW)

### Código Fuente

```plpgsql
BEGIN
    -- Solo crear comisión si hay vendedor asignado y tasa de comisión > 0
    IF NEW.f_salesman IS NOT NULL AND NEW.f_commission_rate > 0 THEN
        INSERT INTO t_vendor_commission_payment (
            f_order,
            f_vendor,
            commission_rate,
            commission_amount,
            payment_status,
            created_at,
            updated_at
        ) VALUES (
            NEW.f_order,
            NEW.f_salesman,
            NEW.f_commission_rate,
            (NEW.f_salesubtotal * NEW.f_commission_rate / 100),
            'draft', -- Estado inicial: draft
            CURRENT_TIMESTAMP,
            CURRENT_TIMESTAMP
        );
    END IF;
    
    RETURN NEW;
END;
```

---

## fn_expense_audit

- **Retorna**: `trigger`
- **Lenguaje**: `plpgsql`
- **Usado por triggers**:
  - `trg_expense_audit` en `t_expense` (AFTER INSERT OR UPDATE OR DELETE FOR EACH ROW)

### Código Fuente

```plpgsql
DECLARE
    v_action VARCHAR(20);
    v_amount_change NUMERIC(18,2);
    v_days_old INTEGER;
    v_days_new INTEGER;
    v_supplier_name VARCHAR(200);
    v_order_po VARCHAR(50);
BEGIN
    -- Determinar acción
    IF TG_OP = 'INSERT' THEN
        v_action := 'INSERT';
    ELSIF TG_OP = 'DELETE' THEN
        v_action := 'DELETE';
    ELSIF TG_OP = 'UPDATE' THEN
        IF OLD.f_status != 'PAGADO' AND NEW.f_status = 'PAGADO' THEN
            v_action := 'PAID';
        ELSIF OLD.f_status = 'PAGADO' AND NEW.f_status != 'PAGADO' THEN
            v_action := 'UNPAID';
        ELSE
            v_action := 'UPDATE';
        END IF;
    END IF;

    -- Calcular diferencia de monto
    IF TG_OP = 'UPDATE' THEN
        v_amount_change := COALESCE(NEW.f_totalexpense, 0) - COALESCE(OLD.f_totalexpense, 0);
    ELSIF TG_OP = 'INSERT' THEN
        v_amount_change := NEW.f_totalexpense;
    ELSIF TG_OP = 'DELETE' THEN
        v_amount_change := -OLD.f_totalexpense;
    END IF;

    -- Calcular días hasta vencimiento
    IF TG_OP IN ('UPDATE', 'DELETE') THEN
        v_days_old := COALESCE(OLD.f_scheduleddate, OLD.f_expensedate)::DATE - CURRENT_DATE;
    END IF;
    IF TG_OP IN ('INSERT', 'UPDATE') THEN
        v_days_new := COALESCE(NEW.f_scheduleddate, NEW.f_expensedate)::DATE - CURRENT_DATE;
    END IF;

    -- Obtener nombre del proveedor
    IF TG_OP IN ('INSERT', 'UPDATE') THEN
        SELECT f_suppliername INTO v_supplier_name FROM t_supplier WHERE f_supplier = NEW.f_supplier;
    ELSE
        SELECT f_suppliername INTO v_supplier_name FROM t_supplier WHERE f_supplier = OLD.f_supplier;
    END IF;

    -- Obtener PO de la orden
    IF TG_OP IN ('INSERT', 'UPDATE') AND NEW.f_order IS NOT NULL THEN
        SELECT f_po INTO v_order_po FROM t_order WHERE f_order = NEW.f_order;
    ELSIF TG_OP = 'DELETE' AND OLD.f_order IS NOT NULL THEN
        SELECT f_po INTO v_order_po FROM t_order WHERE f_order = OLD.f_order;
    END IF;

    -- INSERT
    IF TG_OP = 'INSERT' THEN
        INSERT INTO t_expense_audit (
            expense_id, action,
            new_supplier_id, new_description, new_total_expense,
            new_expense_date, new_scheduled_date, new_status,
            new_paid_date, new_pay_method, new_order_id,
            new_expense_category, new_created_by, new_updated_by,
            amount_change, days_until_due_new, supplier_name, order_po
        ) VALUES (
            NEW.f_expense, v_action,
            NEW.f_supplier, NEW.f_description, NEW.f_totalexpense,
            NEW.f_expensedate, NEW.f_scheduleddate, NEW.f_status,
            NEW.f_paiddate, NEW.f_paymethod, NEW.f_order,
            NEW.expense_category, NEW.created_by::TEXT, NEW.updated_by,
            v_amount_change, v_days_new, v_supplier_name, v_order_po
        );
        RETURN NEW;

    -- UPDATE
    ELSIF TG_OP = 'UPDATE' THEN
        INSERT INTO t_expense_audit (
            expense_id, action,
            old_supplier_id, old_description, old_total_expense,
            old_expense_date, old_scheduled_date, old_status,
            old_paid_date, old_pay_method, old_order_id,
            old_expense_category, old_created_by, old_updated_by,
            new_supplier_id, new_description, new_total_expense,
            new_expense_date, new_scheduled_date, new_status,
            new_paid_date, new_pay_method, new_order_id,
            new_expense_category, new_created_by, new_updated_by,
            amount_change, days_until_due_old, days_until_due_new,
            supplier_name, order_po
        ) VALUES (
            NEW.f_expense, v_action,
            OLD.f_supplier, OLD.f_description, OLD.f_totalexpense,
            OLD.f_expensedate, OLD.f_scheduleddate, OLD.f_status,
            OLD.f_paiddate, OLD.f_paymethod, OLD.f_order,
            OLD.expense_category, OLD.created_by::TEXT, OLD.updated_by,
            NEW.f_supplier, NEW.f_description, NEW.f_totalexpense,
            NEW.f_expensedate, NEW.f_scheduleddate, NEW.f_status,
            NEW.f_paiddate, NEW.f_paymethod, NEW.f_order,
            NEW.expense_category, NEW.created_by::TEXT, NEW.updated_by,
            v_amount_change, v_days_old, v_days_new,
            v_supplier_name, v_order_po
        );
        RETURN NEW;

    -- DELETE
    ELSIF TG_OP = 'DELETE' THEN
        INSERT INTO t_expense_audit (
            expense_id, action,
            old_supplier_id, old_description, old_total_expense,
            old_expense_date, old_scheduled_date, old_status,
            old_paid_date, old_pay_method, old_order_id,
            old_expense_category, old_created_by, old_updated_by,
            amount_change, days_until_due_old, supplier_name, order_po
        ) VALUES (
            OLD.f_expense, v_action,
            OLD.f_supplier, OLD.f_description, OLD.f_totalexpense,
            OLD.f_expensedate, OLD.f_scheduleddate, OLD.f_status,
            OLD.f_paiddate, OLD.f_paymethod, OLD.f_order,
            OLD.expense_category, OLD.created_by::TEXT, OLD.updated_by,
            v_amount_change, v_days_old, v_supplier_name, v_order_po
        );
        RETURN OLD;
    END IF;

    RETURN NULL;
END;
```

---

## fn_expense_update_timestamp

- **Retorna**: `trigger`
- **Lenguaje**: `plpgsql`
- **Usado por triggers**:
  - `trg_expense_update_timestamp` en `t_expense` (BEFORE UPDATE FOR EACH ROW)

### Código Fuente

```plpgsql
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
```

---

## fn_log_vendor_removal

- **Retorna**: `trigger`
- **Lenguaje**: `plpgsql`
- **Usado por triggers**:
  - `trg_before_commission_delete` en `t_vendor_commission_payment` (BEFORE DELETE FOR EACH ROW)

### Código Fuente

```plpgsql
BEGIN
    -- Registrar en el historial antes de que se elimine el pago de comisión
    INSERT INTO t_commission_rate_history (
        order_id,
        vendor_id,
        commission_payment_id,
        old_rate,
        old_amount,
        new_rate,
        new_amount,
        order_subtotal,
        order_number,
        vendor_name,
        changed_by,
        changed_by_name,
        changed_at,
        change_reason,
        is_vendor_removal
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
```

---

## recalcular_gasto_operativo

- **Retorna**: `trigger`
- **Lenguaje**: `plpgsql`
- **Usado por triggers**:
  - `trg_recalcular_gasto_operativo` en `order_gastos_operativos` (AFTER INSERT OR UPDATE OR DELETE FOR EACH ROW)

### Código Fuente

```plpgsql
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
```

---

## recalcular_gasto_operativo_por_comision

- **Retorna**: `trigger`
- **Lenguaje**: `plpgsql`
- **Usado por triggers**:
  - `trg_recalcular_gasto_op_por_comision` en `t_vendor_commission_payment` (AFTER INSERT OR UPDATE OR DELETE FOR EACH ROW)

### Código Fuente

```plpgsql
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
```

---

## record_order_history

- **Retorna**: `trigger`
- **Lenguaje**: `plpgsql`
- **Usado por triggers**:
  - `record_order_history_trigger` en `t_order` (AFTER INSERT OR UPDATE OR DELETE FOR EACH ROW)

### Código Fuente

```plpgsql
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
```

---

## set_latest_version

- **Retorna**: `trigger`
- **Lenguaje**: `plpgsql`
- **Usado por triggers**:
  - `trigger_set_latest_version` en `app_versions` (BEFORE INSERT OR UPDATE FOR EACH ROW)

### Código Fuente

```plpgsql
BEGIN
    -- Si la nueva versión es marcada como "latest"
    IF NEW.is_latest = true THEN
        -- Desmarcar todas las demás versiones
        UPDATE app_versions
        SET is_latest = false
        WHERE id != NEW.id AND is_latest = true;
    END IF;

    RETURN NEW;
END;
```

---

## set_order_audit_fields

- **Retorna**: `trigger`
- **Lenguaje**: `plpgsql`
- **Usado por triggers**:
  - `set_order_audit_fields_trigger` en `t_order` (BEFORE INSERT OR UPDATE FOR EACH ROW)

### Código Fuente

```plpgsql
DECLARE
    v_user_id INTEGER;
BEGIN
    -- Obtener user_id del contexto o usar default
    v_user_id := COALESCE(
        current_setting('app.current_user_id', true)::INTEGER, 
        NEW.created_by,
        NEW.updated_by,
        1
    );
    
    IF TG_OP = 'INSERT' THEN
        -- Establecer campos de auditoría para INSERT
        NEW.created_by := COALESCE(NEW.created_by, v_user_id);
        NEW.updated_by := COALESCE(NEW.updated_by, v_user_id);
        NEW.created_at := COALESCE(NEW.created_at, CURRENT_TIMESTAMP);
        NEW.updated_at := CURRENT_TIMESTAMP;
        
    ELSIF TG_OP = 'UPDATE' THEN
        -- Solo actualizar campos de modificación
        NEW.updated_by := COALESCE(NEW.updated_by, OLD.updated_by, v_user_id);
        NEW.updated_at := CURRENT_TIMESTAMP;
    END IF;
    
    RETURN NEW;
END;
```

---

## sync_commission_rate

- **Retorna**: `trigger`
- **Lenguaje**: `plpgsql`
- **Usado por triggers**:
  - `trigger_sync_commission_rate` en `t_vendor_commission_payment` (AFTER UPDATE FOR EACH ROW)

### Código Fuente

```plpgsql
BEGIN
    IF NEW.commission_rate IS DISTINCT FROM OLD.commission_rate THEN
        -- Actualiza f_commission_rate en la orden
        UPDATE public.t_order 
        SET f_commission_rate = NEW.commission_rate,
            updated_at = CURRENT_TIMESTAMP
        WHERE f_order = NEW.f_order;

        -- Recalcula el commission_amount en la fila de vendor_commission_payment
        UPDATE public.t_vendor_commission_payment
        SET commission_amount = (o.f_salesubtotal * NEW.commission_rate / 100),
            updated_at = CURRENT_TIMESTAMP
        FROM public.t_order o
        WHERE t_vendor_commission_payment.id = NEW.id
          AND o.f_order = NEW.f_order;
    END IF;

    RETURN NULL; -- en AFTER triggers debe devolver NULL
END;
```

---

## sync_commission_rate_from_order

- **Retorna**: `trigger`
- **Lenguaje**: `plpgsql`
- **Usado por triggers**:
  - `trigger_sync_commission_from_order` en `t_order` (AFTER UPDATE FOR EACH ROW)

### Código Fuente

```plpgsql
BEGIN
    IF NEW.f_commission_rate IS DISTINCT FROM OLD.f_commission_rate
       OR NEW.f_salesubtotal   IS DISTINCT FROM OLD.f_salesubtotal THEN

        UPDATE public.t_vendor_commission_payment 
        SET commission_rate   = NEW.f_commission_rate,
            commission_amount = (NEW.f_salesubtotal * NEW.f_commission_rate / 100),
            updated_at        = CURRENT_TIMESTAMP
        WHERE f_order = NEW.f_order;
    END IF;

    RETURN NULL; -- AFTER trigger
END;
```

---

## track_fixed_expense_changes

- **Retorna**: `trigger`
- **Lenguaje**: `plpgsql`
- **Usado por triggers**:
  - `fixed_expense_history_trigger` en `t_fixed_expenses` (AFTER INSERT OR UPDATE OR DELETE FOR EACH ROW)

### Código Fuente

```plpgsql
DECLARE
      v_change_type VARCHAR(50);
      v_change_summary TEXT;
      v_effective_date DATE;
  BEGIN
      -- Para DELETE real, registrar y permitir la operación
      IF TG_OP = 'DELETE' THEN
          INSERT INTO t_fixed_expenses_history (
              expense_id, description, monthly_amount, effective_date,
              change_type, change_summary, created_by
          ) VALUES (
              OLD.id, OLD.description, OLD.monthly_amount, CURRENT_DATE,
              'DELETED', 'Gasto eliminado: ' || OLD.description,
              COALESCE(OLD.created_by, 1)
          );
          RETURN OLD;
      END IF;

      -- Determinar fecha efectiva para INSERT/UPDATE
      v_effective_date := COALESCE(NEW.effective_date, CURRENT_DATE);

      -- Procesar UPDATE
      IF TG_OP = 'UPDATE' THEN
          -- ✅ NUEVO: Detectar desactivación (is_active: true -> false)
          IF OLD.is_active = true AND NEW.is_active = false THEN
              INSERT INTO t_fixed_expenses_history (
                  expense_id, description, monthly_amount, effective_date,
                  change_type, change_summary, created_by
              ) VALUES (
                  NEW.id, NEW.description, 0, CURRENT_DATE,
                  'DEACTIVATED', 'Gasto desactivado: ' || NEW.description,
                  COALESCE(NEW.created_by, 1)
              );
              RETURN NEW;
          END IF;

          -- ✅ NUEVO: Detectar reactivación (is_active: false -> true)
          IF OLD.is_active = false AND NEW.is_active = true THEN
              INSERT INTO t_fixed_expenses_history (
                  expense_id, description, monthly_amount, effective_date,
                  change_type, change_summary, created_by
              ) VALUES (
                  NEW.id, NEW.description, NEW.monthly_amount, v_effective_date,
                  'REACTIVATED', 'Gasto reactivado: ' || NEW.description || ' - $' || NEW.monthly_amount::TEXT,
                  COALESCE(NEW.created_by, 1)
              );
              RETURN NEW;
          END IF;

          -- Si no hay cambios relevantes, salir
          IF OLD.monthly_amount = NEW.monthly_amount AND
             OLD.description = NEW.description THEN
              RETURN NEW;
          END IF;

          -- Determinar tipo de cambio
          IF OLD.monthly_amount IS DISTINCT FROM NEW.monthly_amount THEN
              IF OLD.monthly_amount < NEW.monthly_amount THEN
                  v_change_type := 'INCREASE';
                  v_change_summary := 'Aumento de $' || OLD.monthly_amount::TEXT || ' a $' || NEW.monthly_amount::TEXT;
              ELSE
                  v_change_type := 'DECREASE';
                  v_change_summary := 'Reducción de $' || OLD.monthly_amount::TEXT || ' a $' || NEW.monthly_amount::TEXT;
              END IF;
          ELSE
              v_change_type := 'MODIFIED';
              v_change_summary := 'Actualización de descripción';
          END IF;

          -- Evitar duplicados recientes
          IF NOT EXISTS (
              SELECT 1 FROM t_fixed_expenses_history
              WHERE expense_id = NEW.id
                AND effective_date = v_effective_date
                AND monthly_amount = NEW.monthly_amount
                AND created_at > NOW() - INTERVAL '2 minutes'
          ) THEN
              INSERT INTO t_fixed_expenses_history (
                  expense_id, description, monthly_amount, effective_date,
                  change_type, change_summary, created_by
              ) VALUES (
                  NEW.id, NEW.description, NEW.monthly_amount, v_effective_date,
                  v_change_type, v_change_summary, COALESCE(NEW.created_by, 1)
              );
          END IF;

      ELSIF TG_OP = 'INSERT' THEN
          INSERT INTO t_fixed_expenses_history (
              expense_id, description, monthly_amount, effective_date,
              change_type, change_summary, created_by
          ) VALUES (
              NEW.id, NEW.description, NEW.monthly_amount, v_effective_date,
              'CREATED', 'Nuevo gasto: ' || NEW.description || ' - $' || NEW.monthly_amount::TEXT,
              COALESCE(NEW.created_by, 1)
          );
      END IF;

      RETURN NEW;
  END;
```

---

## track_payroll_changes

- **Retorna**: `trigger`
- **Lenguaje**: `plpgsql`
- **Usado por triggers**:
  - `payroll_history_trigger` en `t_payroll` (AFTER INSERT OR UPDATE FOR EACH ROW)

### Código Fuente

```plpgsql
DECLARE
    changes JSONB := '{}'::jsonb;
    change_summary_text TEXT := '';
    change_type_text VARCHAR(50) := 'UPDATE';
BEGIN
    -- Solo procesar si hay cambios reales
    IF TG_OP = 'UPDATE' THEN
        -- Detectar cambios en cada campo
        IF OLD.f_employee IS DISTINCT FROM NEW.f_employee THEN
            changes := changes || jsonb_build_object('f_employee', 
                jsonb_build_object('old', OLD.f_employee, 'new', NEW.f_employee));
            change_summary_text := change_summary_text || 'Nombre: ' || COALESCE(OLD.f_employee, 'N/A') || ' → ' || NEW.f_employee || '; ';
        END IF;
        
        IF OLD.f_title IS DISTINCT FROM NEW.f_title THEN
            changes := changes || jsonb_build_object('f_title', 
                jsonb_build_object('old', OLD.f_title, 'new', NEW.f_title));
            change_summary_text := change_summary_text || 'Puesto: ' || COALESCE(OLD.f_title, 'N/A') || ' → ' || NEW.f_title || '; ';
            change_type_text := 'PROMOTION';
        END IF;
        
        IF OLD.f_range IS DISTINCT FROM NEW.f_range THEN
            changes := changes || jsonb_build_object('f_range', 
                jsonb_build_object('old', OLD.f_range, 'new', NEW.f_range));
            change_summary_text := change_summary_text || 'Rango: ' || COALESCE(OLD.f_range, 'N/A') || ' → ' || NEW.f_range || '; ';
        END IF;
        
        IF OLD.f_condition IS DISTINCT FROM NEW.f_condition THEN
            changes := changes || jsonb_build_object('f_condition', 
                jsonb_build_object('old', OLD.f_condition, 'new', NEW.f_condition));
            change_summary_text := change_summary_text || 'Condición: ' || COALESCE(OLD.f_condition, 'N/A') || ' → ' || NEW.f_condition || '; ';
        END IF;
        
        IF OLD.f_monthlypayroll IS DISTINCT FROM NEW.f_monthlypayroll THEN
            changes := changes || jsonb_build_object('f_monthlypayroll', 
                jsonb_build_object('old', OLD.f_monthlypayroll, 'new', NEW.f_monthlypayroll));
            change_summary_text := change_summary_text || 'Nómina: $' || COALESCE(OLD.f_monthlypayroll::TEXT, '0') || ' → $' || NEW.f_monthlypayroll || '; ';
            IF change_type_text = 'UPDATE' THEN
                change_type_text := 'SALARY_CHANGE';
            END IF;
        END IF;
        
        IF OLD.f_benefitsamount IS DISTINCT FROM NEW.f_benefitsamount THEN
            changes := changes || jsonb_build_object('f_benefitsamount', 
                jsonb_build_object('old', OLD.f_benefitsamount, 'new', NEW.f_benefitsamount));
            change_summary_text := change_summary_text || 'Prestaciones: $' || COALESCE(OLD.f_benefitsamount::TEXT, '0') || ' → $' || NEW.f_benefitsamount || '; ';
            IF change_type_text = 'UPDATE' THEN
                change_type_text := 'BENEFIT_UPDATE';
            END IF;
        END IF;
        
        IF OLD.is_active IS DISTINCT FROM NEW.is_active THEN
            changes := changes || jsonb_build_object('is_active', 
                jsonb_build_object('old', OLD.is_active, 'new', NEW.is_active));
            IF NEW.is_active = FALSE THEN
                change_type_text := 'TERMINATION';
                change_summary_text := change_summary_text || 'EMPLEADO DADO DE BAJA; ';
            ELSE
                change_type_text := 'REACTIVATION';
                change_summary_text := change_summary_text || 'EMPLEADO REACTIVADO; ';
            END IF;
        END IF;
        
        -- Solo insertar si hay cambios
        IF changes != '{}'::jsonb THEN
            INSERT INTO t_payroll_history (
                f_payroll, f_employee, f_title, f_hireddate, f_range, f_condition,
                f_lastraise, f_sspayroll, f_weeklypayroll, f_socialsecurity,
                f_benefits, f_benefitsamount, f_monthlypayroll, employee_code, is_active,
                changed_fields, effective_date, change_type, change_summary, created_by
            ) VALUES (
                NEW.f_payroll, NEW.f_employee, NEW.f_title, NEW.f_hireddate, NEW.f_range, NEW.f_condition,
                NEW.f_lastraise, NEW.f_sspayroll, NEW.f_weeklypayroll, NEW.f_socialsecurity,
                NEW.f_benefits, NEW.f_benefitsamount, NEW.f_monthlypayroll, NEW.employee_code, NEW.is_active,
                changes, CURRENT_DATE, change_type_text, TRIM(change_summary_text), NEW.updated_by
            );
        END IF;
        
    ELSIF TG_OP = 'INSERT' THEN
        -- Para nuevos empleados
        INSERT INTO t_payroll_history (
            f_payroll, f_employee, f_title, f_hireddate, f_range, f_condition,
            f_lastraise, f_sspayroll, f_weeklypayroll, f_socialsecurity,
            f_benefits, f_benefitsamount, f_monthlypayroll, employee_code, is_active,
            changed_fields, effective_date, change_type, change_summary, created_by
        ) VALUES (
            NEW.f_payroll, NEW.f_employee, NEW.f_title, NEW.f_hireddate, NEW.f_range, NEW.f_condition,
            NEW.f_lastraise, NEW.f_sspayroll, NEW.f_weeklypayroll, NEW.f_socialsecurity,
            NEW.f_benefits, NEW.f_benefitsamount, NEW.f_monthlypayroll, NEW.employee_code, NEW.is_active,
            '{"action": "new_hire"}'::jsonb, CURRENT_DATE, 'NEW_HIRE', 
            'Nuevo empleado: ' || NEW.f_employee || ' - ' || NEW.f_title, NEW.created_by
        );
    END IF;
    
    RETURN NEW;
END;
```

---

## update_commission_on_order_status_change

- **Retorna**: `trigger`
- **Lenguaje**: `plpgsql`
- **Usado por triggers**:
  - `trigger_update_commission_on_status_change` en `t_order` (AFTER UPDATE FOR EACH ROW)

### Código Fuente

```plpgsql
BEGIN
    -- MANEJAR CAMBIO DE VENDEDOR (prevenir duplicados)
    IF OLD.f_salesman IS DISTINCT FROM NEW.f_salesman THEN
        -- Si había un vendedor anterior, eliminar su comisión draft
        IF OLD.f_salesman IS NOT NULL THEN
            DELETE FROM t_vendor_commission_payment
            WHERE f_order = NEW.f_order 
              AND f_vendor = OLD.f_salesman
              AND payment_status = 'draft';
        END IF;
        
        -- Si hay nuevo vendedor, crear nueva comisión draft
        IF NEW.f_salesman IS NOT NULL AND NEW.f_commission_rate > 0 THEN
            -- Verificar que no exista ya
            IF NOT EXISTS (
                SELECT 1 FROM t_vendor_commission_payment 
                WHERE f_order = NEW.f_order AND f_vendor = NEW.f_salesman
            ) THEN
                INSERT INTO t_vendor_commission_payment (
                    f_order, f_vendor, commission_rate, commission_amount,
                    payment_status, created_at, updated_at
                ) VALUES (
                    NEW.f_order,
                    NEW.f_salesman,
                    NEW.f_commission_rate,
                    (NEW.f_salesubtotal * NEW.f_commission_rate / 100),
                    'draft',
                    CURRENT_TIMESTAMP,
                    CURRENT_TIMESTAMP
                );
            END IF;
        END IF;
    END IF;
    
    -- MANEJAR CAMBIO DE ESTADO A CERRADA
    IF NEW.f_orderstat = 3 AND (OLD.f_orderstat IS NULL OR OLD.f_orderstat <> 3) THEN
        UPDATE t_vendor_commission_payment
        SET 
            payment_status = 'pending',
            updated_at = CURRENT_TIMESTAMP,
            commission_amount = (NEW.f_salesubtotal * NEW.f_commission_rate / 100)
        WHERE 
            f_order = NEW.f_order 
            AND payment_status = 'draft';
    END IF;
    
    -- MANEJAR ORDEN CANCELADA
    IF NEW.f_orderstat = 5 THEN
        DELETE FROM t_vendor_commission_payment
        WHERE f_order = NEW.f_order 
          AND payment_status = 'draft';
    END IF;
    
    RETURN NEW;
END;
```

---

## update_commission_on_vendor_change

- **Retorna**: `trigger`
- **Lenguaje**: `plpgsql`
- **Usado por triggers**:
  - `trigger_update_commission_on_vendor_change` en `t_order` (BEFORE UPDATE FOR EACH ROW)

### Código Fuente

```plpgsql
DECLARE
    v_vendor_commission DECIMAL;
    v_old_vendor_name VARCHAR;
    v_new_vendor_name VARCHAR;
BEGIN
    -- Solo actuar si el vendedor cambió
    IF (OLD.f_salesman IS DISTINCT FROM NEW.f_salesman) THEN
        
        -- Si se asignó un vendedor (no era NULL y ahora sí tiene)
        IF NEW.f_salesman IS NOT NULL THEN
            -- Obtener la comisión por defecto del vendedor
            SELECT f_commission_rate INTO v_vendor_commission
            FROM t_vendor
            WHERE f_vendor = NEW.f_salesman;
            
            -- Si encontramos el vendedor, actualizar la comisión
            IF v_vendor_commission IS NOT NULL THEN
                NEW.f_commission_rate = v_vendor_commission;
            ELSE
                -- Si no se encuentra, usar 10% por defecto
                NEW.f_commission_rate = 10;
            END IF;
            
            -- Registrar el cambio en order_history
            -- Obtener nombres de vendedores para el historial
            IF OLD.f_salesman IS NOT NULL THEN
                SELECT f_vendorname INTO v_old_vendor_name
                FROM t_vendor WHERE f_vendor = OLD.f_salesman;
            ELSE
                v_old_vendor_name := 'Sin vendedor';
            END IF;
            
            SELECT f_vendorname INTO v_new_vendor_name
            FROM t_vendor WHERE f_vendor = NEW.f_salesman;
            
            -- Insertar en historial
            INSERT INTO order_history (
                order_id, user_id, action, field_name,
                old_value, new_value, change_description, changed_at
            ) VALUES (
                NEW.f_order,
                COALESCE(NEW.updated_by, NEW.created_by, 1),
                'UPDATE',
                'f_salesman',
                v_old_vendor_name,
                v_new_vendor_name,
                'Cambio de vendedor asignado',
                NOW()
            );
            
            -- También registrar el cambio de comisión
            IF OLD.f_commission_rate IS DISTINCT FROM NEW.f_commission_rate THEN
                INSERT INTO order_history (
                    order_id, user_id, action, field_name,
                    old_value, new_value, change_description, changed_at
                ) VALUES (
                    NEW.f_order,
                    COALESCE(NEW.updated_by, NEW.created_by, 1),
                    'UPDATE',
                    'f_commission_rate',
                    COALESCE(OLD.f_commission_rate::TEXT, '0'),
                    NEW.f_commission_rate::TEXT,
                    'Actualización automática de comisión por cambio de vendedor',
                    NOW()
                );
            END IF;
            
        ELSE
            -- Si se quitó el vendedor (era algo y ahora es NULL)
            NEW.f_commission_rate = 0;
            
            -- Registrar en historial
            SELECT f_vendorname INTO v_old_vendor_name
            FROM t_vendor WHERE f_vendor = OLD.f_salesman;
            
            INSERT INTO order_history (
                order_id, user_id, action, field_name,
                old_value, new_value, change_description, changed_at
            ) VALUES (
                NEW.f_order,
                COALESCE(NEW.updated_by, NEW.created_by, 1),
                'UPDATE',
                'f_salesman',
                v_old_vendor_name,
                'Sin vendedor',
                'Se removió el vendedor asignado',
                NOW()
            );
        END IF;
    END IF;
    
    RETURN NEW;
END;
```

---

## update_invoice_status

- **Retorna**: `trigger`
- **Lenguaje**: `plpgsql`
- **Usado por triggers**:
  - `trigger_update_invoice_status` en `t_invoice` (BEFORE INSERT OR UPDATE FOR EACH ROW)

### Código Fuente

```plpgsql
BEGIN
    -- Estado 4 (PAGADA): Si tiene fecha de pago
    IF NEW.f_paymentdate IS NOT NULL THEN
        NEW.f_invoicestat = 4;
        
    -- Estado 2 (PENDIENTE): Si tiene fecha recepción Y fecha programada (due_date)
    ELSIF NEW.f_receptiondate IS NOT NULL AND NEW.due_date IS NOT NULL THEN
        -- Verificar si está vencida (Estado 3)
        IF NEW.due_date < CURRENT_DATE AND NEW.f_paymentdate IS NULL THEN
            NEW.f_invoicestat = 3; -- VENCIDA
        ELSE
            NEW.f_invoicestat = 2; -- PENDIENTE
        END IF;
        
    -- Estado 1 (CREADA): Estado inicial
    ELSE
        NEW.f_invoicestat = 1;
    END IF;
    
    RETURN NEW;
END;
```

---

## update_order_status_from_invoices

- **Retorna**: `trigger`
- **Lenguaje**: `plpgsql`
- **Usado por triggers**:
  - `trigger_update_order_status_unified` en `t_invoice` (AFTER INSERT OR UPDATE OR DELETE FOR EACH ROW)

### Código Fuente

```plpgsql
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
              -- NUEVO: Actualizar progress_percentage a 100 cuando pasa a CERRADA o COMPLETADA
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
```

---

## update_updated_at_column

- **Retorna**: `trigger`
- **Lenguaje**: `plpgsql`
- **Usado por triggers**:
  - `update_client_updated_at` en `t_client` (BEFORE UPDATE FOR EACH ROW)
  - `update_contact_updated_at` en `t_contact` (BEFORE UPDATE FOR EACH ROW)
  - `update_expense_updated_at` en `t_expense` (BEFORE UPDATE FOR EACH ROW)
  - `update_invoice_updated_at` en `t_invoice` (BEFORE UPDATE FOR EACH ROW)
  - `update_order_updated_at` en `t_order` (BEFORE UPDATE FOR EACH ROW)
  - `update_supplier_updated_at` en `t_supplier` (BEFORE UPDATE FOR EACH ROW)
  - `update_vendor_updated_at` en `t_vendor` (BEFORE UPDATE FOR EACH ROW)

### Código Fuente

```plpgsql
BEGIN
    -- Esta función solo actualiza updated_at, no duplica la auditoría
    NEW.updated_at = CURRENT_TIMESTAMP;
    RETURN NEW;
END;
```

---

# Funciones Huérfanas (sin trigger activo)
> Estas funciones retornan `trigger` pero ningún trigger activo las referencia.
> Pueden ser funciones obsoletas o pendientes de conectar.

## set_created_by

- **Retorna**: `trigger`
- **Lenguaje**: `plpgsql`
- **Estado**: Sin trigger activo

### Código Fuente

```plpgsql
BEGIN
    -- Si created_at es NULL, establecerlo
    IF NEW.created_at IS NULL THEN
        NEW.created_at = CURRENT_TIMESTAMP;
    END IF;
    
    -- Si es INSERT y created_by viene como NULL, intentar usar el valor de la sesión
    IF TG_OP = 'INSERT' AND NEW.created_by IS NULL THEN
        NEW.created_by = COALESCE(
            current_setting('app.current_user_id', true)::INTEGER,
            NEW.created_by
        );
    END IF;
    
    RETURN NEW;
END;
```

---

## update_order_audit_fields

- **Retorna**: `trigger`
- **Lenguaje**: `plpgsql`
- **Estado**: Sin trigger activo

### Código Fuente

```plpgsql
BEGIN
    -- Actualizar updated_at
    NEW.updated_at = CURRENT_TIMESTAMP;
    
    -- Si updated_by viene como NULL, intentar usar el valor de la sesión
    IF NEW.updated_by IS NULL THEN
        NEW.updated_by = COALESCE(
            current_setting('app.current_user_id', true)::INTEGER,
            NEW.updated_by,
            OLD.updated_by
        );
    END IF;
    
    RETURN NEW;
END;
```

---

## update_order_on_invoice_change

- **Retorna**: `trigger`
- **Lenguaje**: `plpgsql`
- **Estado**: Sin trigger activo

### Código Fuente

```plpgsql
DECLARE
    v_order_total DECIMAL;
    v_invoiced_total DECIMAL;
    v_percentage DECIMAL;
    v_all_invoices_paid BOOLEAN;
    v_has_pending_invoices BOOLEAN;
BEGIN
    -- Obtener el total de la orden
    SELECT f_saletotal INTO v_order_total
    FROM t_order
    WHERE f_order = NEW.f_order;
    
    -- Calcular total facturado
    SELECT COALESCE(SUM(f_total), 0) INTO v_invoiced_total
    FROM t_invoice
    WHERE f_order = NEW.f_order;
    
    -- Calcular porcentaje facturado
    IF v_order_total > 0 THEN
        v_percentage = (v_invoiced_total / v_order_total) * 100;
    ELSE
        v_percentage = 0;
    END IF;
    
    -- Verificar si todas las facturas están pagadas
    SELECT NOT EXISTS(
        SELECT 1 FROM t_invoice 
        WHERE f_order = NEW.f_order 
        AND f_invoicestat != 4
    ) INTO v_all_invoices_paid;
    
    -- Verificar si hay facturas pendientes (estado 2)
    SELECT EXISTS(
        SELECT 1 FROM t_invoice 
        WHERE f_order = NEW.f_order 
        AND f_invoicestat = 2
    ) INTO v_has_pending_invoices;
    
    -- Actualizar el estado de la orden según las condiciones
    UPDATE t_order
    SET 
        invoiced = CASE 
            WHEN v_invoiced_total > 0 THEN true 
            ELSE false 
        END,
        last_invoice_date = CURRENT_DATE,
        f_orderstat = CASE
            -- Si todas las facturas están pagadas y se facturó el 100%
            WHEN v_all_invoices_paid AND v_percentage >= 99.5 THEN 5  -- PAGADA
            -- Si se facturó el 100% pero hay facturas pendientes
            WHEN v_percentage >= 99.5 AND v_has_pending_invoices THEN 4  -- FACTURADA
            -- Si la orden ya estaba en estado 3 (COMPLETADA) y hay facturas
            WHEN f_orderstat = 3 AND v_invoiced_total > 0 THEN 3  -- Mantener COMPLETADA
            -- Mantener el estado actual si no aplican las condiciones anteriores
            ELSE f_orderstat
        END,
        updated_at = NOW(),
        updated_by = COALESCE(NEW.created_by, 1)
    WHERE f_order = NEW.f_order;
    
    -- Si la orden cambió a COMPLETADA (3) y tiene vendedor, crear comisión
    IF v_invoiced_total > 0 THEN
        PERFORM create_vendor_commission_if_needed(NEW.f_order);
    END IF;
    
    RETURN NEW;
END;
```

---

## update_order_status_on_invoice

- **Retorna**: `trigger`
- **Lenguaje**: `plpgsql`
- **Estado**: Sin trigger activo

### Código Fuente

```plpgsql
DECLARE
    v_order_total DECIMAL;
    v_invoiced_total DECIMAL;
    v_percentage DECIMAL;
    v_current_status INTEGER;
    v_all_invoices_have_reception BOOLEAN;
    v_all_invoices_paid BOOLEAN;
BEGIN
    -- Obtener el total y estado actual de la orden
    SELECT f_saletotal, f_orderstat 
    INTO v_order_total, v_current_status
    FROM t_order
    WHERE f_order = NEW.f_order;
    
    -- Calcular total facturado
    SELECT COALESCE(SUM(f_total), 0) 
    INTO v_invoiced_total
    FROM t_invoice
    WHERE f_order = NEW.f_order;
    
    -- Calcular porcentaje facturado
    IF v_order_total > 0 THEN
        v_percentage := (v_invoiced_total / v_order_total) * 100;
    ELSE
        v_percentage := 0;
    END IF;
    
    -- Verificar si todas las facturas tienen fecha de recepción y programada
    SELECT NOT EXISTS(
        SELECT 1 FROM t_invoice 
        WHERE f_order = NEW.f_order 
        AND (f_receptiondate IS NULL OR due_date IS NULL)
    ) INTO v_all_invoices_have_reception;
    
    -- Verificar si todas las facturas están pagadas
    SELECT NOT EXISTS(
        SELECT 1 FROM t_invoice 
        WHERE f_order = NEW.f_order 
        AND f_paymentdate IS NULL
    ) INTO v_all_invoices_paid;
    
    -- Actualizar estado de la orden según las condiciones
    -- Solo actualizar si el estado actual es <= 2 (CREADA, EN PROCESO, LIBERADA)
    IF v_current_status <= 2 THEN
        -- Si se facturó el 100% y todas tienen recepción/programada
        IF v_percentage >= 99.5 AND v_all_invoices_have_reception THEN
            UPDATE t_order
            SET 
                f_orderstat = 3, -- CERRADA (el trigger de orden creará la comisión)
                invoiced = true,
                last_invoice_date = CURRENT_DATE,
                updated_at = NOW()
            WHERE f_order = NEW.f_order;
            
            -- Registrar en historial
            INSERT INTO order_history (
                order_id, user_id, action, field_name,
                old_value, new_value, change_description, changed_at
            ) VALUES (
                NEW.f_order,
                COALESCE(NEW.created_by, 1),
                'STATUS_CHANGE',
                'f_orderstat',
                v_current_status::TEXT,
                '3',
                'Orden cerrada automáticamente - Facturación al 100% con recepción',
                NOW()
            );
        END IF;
    END IF;
    
    -- Si el estado es 3 (CERRADA) y todas las facturas están pagadas
    IF v_current_status = 3 AND v_all_invoices_paid AND v_percentage >= 99.5 THEN
        UPDATE t_order
        SET 
            f_orderstat = 4, -- COMPLETADA
            updated_at = NOW()
        WHERE f_order = NEW.f_order;
        
        -- Registrar en historial
        INSERT INTO order_history (
            order_id, user_id, action, field_name,
            old_value, new_value, change_description, changed_at
        ) VALUES (
            NEW.f_order,
            COALESCE(NEW.created_by, 1),
            'STATUS_CHANGE',
            'f_orderstat',
            '3',
            '4',
            'Orden completada - Todas las facturas pagadas',
            NOW()
        );
    END IF;
    
    -- ELIMINADO: La creación de comisiones (lo hace el trigger de t_order)
    
    RETURN NEW;
END;
```

---

# Triggers por Tabla

## Resumen

| Tabla | # Triggers | Triggers |
|-------|-----------|----------|
| `app_versions` | 1 | trigger_set_latest_version |
| `order_gastos_operativos` | 1 | trg_recalcular_gasto_operativo |
| `t_attendance` | 1 | trg_attendance_audit |
| `t_client` | 1 | update_client_updated_at |
| `t_contact` | 1 | update_contact_updated_at |
| `t_expense` | 5 | trg_expense_audit, trg_expense_update_timestamp, trigger_expense_scheduled_date, update_expense_updated_at, z_expense_auto_pay_zero_credit |
| `t_fixed_expenses` | 1 | fixed_expense_history_trigger |
| `t_invoice` | 4 | trigger_calculate_due_date, trigger_update_invoice_status, trigger_update_order_status_unified, update_invoice_updated_at |
| `t_order` | 7 | record_order_history_trigger, set_order_audit_fields_trigger, trigger_create_commission_on_order, trigger_sync_commission_from_order, trigger_update_commission_on_status_change, trigger_update_commission_on_vendor_change, update_order_updated_at |
| `t_overtime_hours` | 1 | trigger_overtime_audit |
| `t_payroll` | 1 | payroll_history_trigger |
| `t_supplier` | 1 | update_supplier_updated_at |
| `t_vacation` | 1 | trg_vacation_audit |
| `t_vendor` | 1 | update_vendor_updated_at |
| `t_vendor_commission_payment` | 3 | trg_before_commission_delete, trg_recalcular_gasto_op_por_comision, trigger_sync_commission_rate |

### app_versions

- **`trigger_set_latest_version`**: BEFORE INSERT OR UPDATE FOR EACH ROW -> `set_latest_version()` [Habilitado]

### order_gastos_operativos

- **`trg_recalcular_gasto_operativo`**: AFTER INSERT OR UPDATE OR DELETE FOR EACH ROW -> `recalcular_gasto_operativo()` [Habilitado]

### t_attendance

- **`trg_attendance_audit`**: AFTER INSERT OR UPDATE OR DELETE FOR EACH ROW -> `audit_attendance_changes()` [Habilitado]

### t_client

- **`update_client_updated_at`**: BEFORE UPDATE FOR EACH ROW -> `update_updated_at_column()` [Habilitado]

### t_contact

- **`update_contact_updated_at`**: BEFORE UPDATE FOR EACH ROW -> `update_updated_at_column()` [Habilitado]

### t_expense

- **`trg_expense_audit`**: AFTER INSERT OR UPDATE OR DELETE FOR EACH ROW -> `fn_expense_audit()` [Habilitado]
- **`trg_expense_update_timestamp`**: BEFORE UPDATE FOR EACH ROW -> `fn_expense_update_timestamp()` [Habilitado]
- **`trigger_expense_scheduled_date`**: BEFORE INSERT OR UPDATE FOR EACH ROW -> `calculate_scheduled_date()` [Habilitado]
- **`update_expense_updated_at`**: BEFORE UPDATE FOR EACH ROW -> `update_updated_at_column()` [Habilitado]
- **`z_expense_auto_pay_zero_credit`**: BEFORE INSERT OR UPDATE FOR EACH ROW -> `auto_pay_zero_credit_expense()` [Habilitado]

### t_fixed_expenses

- **`fixed_expense_history_trigger`**: AFTER INSERT OR UPDATE OR DELETE FOR EACH ROW -> `track_fixed_expense_changes()` [Habilitado]

### t_invoice

- **`trigger_calculate_due_date`**: BEFORE INSERT OR UPDATE FOR EACH ROW -> `calculate_invoice_due_date()` [Habilitado]
- **`trigger_update_invoice_status`**: BEFORE INSERT OR UPDATE FOR EACH ROW -> `update_invoice_status()` [Habilitado]
- **`trigger_update_order_status_unified`**: AFTER INSERT OR UPDATE OR DELETE FOR EACH ROW -> `update_order_status_from_invoices()` [Habilitado]
- **`update_invoice_updated_at`**: BEFORE UPDATE FOR EACH ROW -> `update_updated_at_column()` [Habilitado]

### t_order

- **`record_order_history_trigger`**: AFTER INSERT OR UPDATE OR DELETE FOR EACH ROW -> `record_order_history()` [Habilitado]
- **`set_order_audit_fields_trigger`**: BEFORE INSERT OR UPDATE FOR EACH ROW -> `set_order_audit_fields()` [Habilitado]
- **`trigger_create_commission_on_order`**: AFTER INSERT FOR EACH ROW -> `create_commission_on_order_creation()` [Habilitado]
- **`trigger_sync_commission_from_order`**: AFTER UPDATE FOR EACH ROW -> `sync_commission_rate_from_order()` [Habilitado]
- **`trigger_update_commission_on_status_change`**: AFTER UPDATE FOR EACH ROW -> `update_commission_on_order_status_change()` [Habilitado]
- **`trigger_update_commission_on_vendor_change`**: BEFORE UPDATE FOR EACH ROW -> `update_commission_on_vendor_change()` [Habilitado]
- **`update_order_updated_at`**: BEFORE UPDATE FOR EACH ROW -> `update_updated_at_column()` [Habilitado]

### t_overtime_hours

- **`trigger_overtime_audit`**: AFTER INSERT OR UPDATE OR DELETE FOR EACH ROW -> `audit_overtime_hours()` [Habilitado]

### t_payroll

- **`payroll_history_trigger`**: AFTER INSERT OR UPDATE FOR EACH ROW -> `track_payroll_changes()` [Habilitado]

### t_supplier

- **`update_supplier_updated_at`**: BEFORE UPDATE FOR EACH ROW -> `update_updated_at_column()` [Habilitado]

### t_vacation

- **`trg_vacation_audit`**: AFTER INSERT OR UPDATE OR DELETE FOR EACH ROW -> `audit_vacation_changes()` [Habilitado]

### t_vendor

- **`update_vendor_updated_at`**: BEFORE UPDATE FOR EACH ROW -> `update_updated_at_column()` [Habilitado]

### t_vendor_commission_payment

- **`trg_before_commission_delete`**: BEFORE DELETE FOR EACH ROW -> `fn_log_vendor_removal()` [Habilitado]
- **`trg_recalcular_gasto_op_por_comision`**: AFTER INSERT OR UPDATE OR DELETE FOR EACH ROW -> `recalcular_gasto_operativo_por_comision()` [Habilitado]
- **`trigger_sync_commission_rate`**: AFTER UPDATE FOR EACH ROW -> `sync_commission_rate()` [Habilitado]
