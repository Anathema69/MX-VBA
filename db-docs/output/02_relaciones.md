# Relaciones entre Tablas - Base de Datos IMA Mecatrónica
Generado: 2026-02-26 22:30:43
Total Foreign Keys: 44

## Resumen de Conectividad

| Tabla | FK Salientes | FK Entrantes | Rol |
|-------|-------------|-------------|-----|
| `app_versions` | 0 | 0 | Aislada |
| `audit_log` | 1 | 0 | Hoja (solo referencia) |
| `invoice_audit` | 1 | 0 | Hoja (solo referencia) |
| `invoice_status` | 0 | 1 | Catálogo/Lookup |
| `order_gastos_indirectos` | 1 | 0 | Hoja (solo referencia) |
| `order_gastos_operativos` | 1 | 0 | Hoja (solo referencia) |
| `order_history` | 2 | 0 | Hoja (solo referencia) |
| `order_status` | 0 | 1 | Catálogo/Lookup |
| `t_attendance` | 1 | 0 | Hoja (solo referencia) |
| `t_attendance_audit` | 0 | 0 | Aislada |
| `t_balance_adjustments` | 1 | 0 | Hoja (solo referencia) |
| `t_client` | 2 | 2 | Intermedia |
| `t_commission_rate_history` | 3 | 0 | Hoja (solo referencia) |
| `t_contact` | 1 | 1 | Intermedia |
| `t_expense` | 3 | 0 | Hoja (solo referencia) |
| `t_expense_audit` | 0 | 0 | Aislada |
| `t_fixed_expenses` | 1 | 1 | Intermedia |
| `t_fixed_expenses_history` | 2 | 0 | Hoja (solo referencia) |
| `t_holiday` | 0 | 0 | Aislada |
| `t_invoice` | 3 | 0 | Hoja (solo referencia) |
| `t_order` | 6 | 7 | Tabla Central |
| `t_order_deleted` | 0 | 0 | Aislada |
| `t_overtime_hours` | 2 | 0 | Hoja (solo referencia) |
| `t_overtime_hours_audit` | 1 | 0 | Hoja (solo referencia) |
| `t_payroll` | 2 | 4 | Tabla Central |
| `t_payroll_history` | 2 | 0 | Hoja (solo referencia) |
| `t_payrollovertime` | 2 | 0 | Hoja (solo referencia) |
| `t_supplier` | 0 | 1 | Catálogo/Lookup |
| `t_vacation` | 1 | 0 | Hoja (solo referencia) |
| `t_vacation_audit` | 0 | 0 | Aislada |
| `t_vendor` | 1 | 3 | Intermedia |
| `t_vendor_commission_payment` | 4 | 1 | Intermedia |
| `t_workday_config` | 0 | 0 | Aislada |
| `users` | 0 | 22 | Tabla Central |

## Todas las Foreign Keys

| # | Constraint | Origen | Columna | Destino | Columna | ON UPDATE | ON DELETE |
|---|-----------|--------|---------|---------|---------|-----------|-----------|
| 1 | `audit_log_user_id_fkey` | `audit_log` | `user_id` | `users` | `id` | NO ACTION | NO ACTION |
| 2 | `invoice_audit_user_id_fkey` | `invoice_audit` | `user_id` | `users` | `id` | NO ACTION | NO ACTION |
| 3 | `order_gastos_indirectos_f_order_fkey` | `order_gastos_indirectos` | `f_order` | `t_order` | `f_order` | NO ACTION | CASCADE |
| 4 | `order_gastos_operativos_f_order_fkey` | `order_gastos_operativos` | `f_order` | `t_order` | `f_order` | NO ACTION | CASCADE |
| 5 | `order_history_order_id_fkey` | `order_history` | `order_id` | `t_order` | `f_order` | NO ACTION | CASCADE |
| 6 | `order_history_user_id_fkey` | `order_history` | `user_id` | `users` | `id` | NO ACTION | NO ACTION |
| 7 | `t_attendance_employee_id_fkey` | `t_attendance` | `employee_id` | `t_payroll` | `f_payroll` | NO ACTION | CASCADE |
| 8 | `t_balance_adjustments_created_by_fkey` | `t_balance_adjustments` | `created_by` | `users` | `id` | NO ACTION | NO ACTION |
| 9 | `t_client_created_by_fkey` | `t_client` | `created_by` | `users` | `id` | NO ACTION | NO ACTION |
| 10 | `t_client_updated_by_fkey` | `t_client` | `updated_by` | `users` | `id` | NO ACTION | NO ACTION |
| 11 | `t_commission_rate_history_commission_payment_id_fkey` | `t_commission_rate_history` | `commission_payment_id` | `t_vendor_commission_payment` | `id` | NO ACTION | SET NULL |
| 12 | `t_commission_rate_history_order_id_fkey` | `t_commission_rate_history` | `order_id` | `t_order` | `f_order` | NO ACTION | NO ACTION |
| 13 | `t_commission_rate_history_vendor_id_fkey` | `t_commission_rate_history` | `vendor_id` | `t_vendor` | `f_vendor` | NO ACTION | NO ACTION |
| 14 | `t_contact_f_client_fkey` | `t_contact` | `f_client` | `t_client` | `f_client` | NO ACTION | CASCADE |
| 15 | `t_expense_created_by_fkey` | `t_expense` | `created_by` | `users` | `id` | NO ACTION | NO ACTION |
| 16 | `t_expense_f_order_fkey` | `t_expense` | `f_order` | `t_order` | `f_order` | NO ACTION | NO ACTION |
| 17 | `t_expense_f_supplier_fkey` | `t_expense` | `f_supplier` | `t_supplier` | `f_supplier` | NO ACTION | NO ACTION |
| 18 | `t_fixed_expenses_created_by_fkey` | `t_fixed_expenses` | `created_by` | `users` | `id` | NO ACTION | NO ACTION |
| 19 | `t_fixed_expenses_history_created_by_fkey` | `t_fixed_expenses_history` | `created_by` | `users` | `id` | NO ACTION | NO ACTION |
| 20 | `t_fixed_expenses_history_expense_id_fkey` | `t_fixed_expenses_history` | `expense_id` | `t_fixed_expenses` | `id` | NO ACTION | NO ACTION |
| 21 | `t_invoice_created_by_fkey` | `t_invoice` | `created_by` | `users` | `id` | NO ACTION | NO ACTION |
| 22 | `t_invoice_f_invoicestat_fkey` | `t_invoice` | `f_invoicestat` | `invoice_status` | `f_invoicestat` | NO ACTION | NO ACTION |
| 23 | `t_invoice_f_order_fkey` | `t_invoice` | `f_order` | `t_order` | `f_order` | NO ACTION | NO ACTION |
| 24 | `t_order_created_by_fkey` | `t_order` | `created_by` | `users` | `id` | NO ACTION | NO ACTION |
| 25 | `t_order_f_client_fkey` | `t_order` | `f_client` | `t_client` | `f_client` | NO ACTION | NO ACTION |
| 26 | `t_order_f_contact_fkey` | `t_order` | `f_contact` | `t_contact` | `f_contact` | NO ACTION | NO ACTION |
| 27 | `t_order_f_orderstat_fkey` | `t_order` | `f_orderstat` | `order_status` | `f_orderstatus` | NO ACTION | NO ACTION |
| 28 | `t_order_f_salesman_fkey` | `t_order` | `f_salesman` | `t_vendor` | `f_vendor` | NO ACTION | NO ACTION |
| 29 | `t_order_updated_by_fkey` | `t_order` | `updated_by` | `users` | `id` | NO ACTION | NO ACTION |
| 30 | `t_overtime_hours_created_by_fkey` | `t_overtime_hours` | `created_by` | `users` | `id` | NO ACTION | NO ACTION |
| 31 | `t_overtime_hours_updated_by_fkey` | `t_overtime_hours` | `updated_by` | `users` | `id` | NO ACTION | NO ACTION |
| 32 | `t_overtime_hours_audit_changed_by_fkey` | `t_overtime_hours_audit` | `changed_by` | `users` | `id` | NO ACTION | NO ACTION |
| 33 | `t_payroll_created_by_fkey` | `t_payroll` | `created_by` | `users` | `id` | NO ACTION | NO ACTION |
| 34 | `t_payroll_updated_by_fkey` | `t_payroll` | `updated_by` | `users` | `id` | NO ACTION | NO ACTION |
| 35 | `t_payroll_history_created_by_fkey` | `t_payroll_history` | `created_by` | `users` | `id` | NO ACTION | NO ACTION |
| 36 | `t_payroll_history_f_payroll_fkey` | `t_payroll_history` | `f_payroll` | `t_payroll` | `f_payroll` | NO ACTION | NO ACTION |
| 37 | `t_payrollovertime_created_by_fkey` | `t_payrollovertime` | `created_by` | `users` | `id` | NO ACTION | NO ACTION |
| 38 | `t_payrollovertime_employee_id_fkey` | `t_payrollovertime` | `employee_id` | `t_payroll` | `f_payroll` | NO ACTION | NO ACTION |
| 39 | `t_vacation_employee_id_fkey` | `t_vacation` | `employee_id` | `t_payroll` | `f_payroll` | NO ACTION | CASCADE |
| 40 | `t_vendor_f_user_id_fkey` | `t_vendor` | `f_user_id` | `users` | `id` | NO ACTION | NO ACTION |
| 41 | `fk_commission_created_by` | `t_vendor_commission_payment` | `created_by` | `users` | `id` | NO ACTION | NO ACTION |
| 42 | `fk_commission_order` | `t_vendor_commission_payment` | `f_order` | `t_order` | `f_order` | NO ACTION | NO ACTION |
| 43 | `fk_commission_vendor` | `t_vendor_commission_payment` | `f_vendor` | `t_vendor` | `f_vendor` | NO ACTION | NO ACTION |
| 44 | `fk_commission_updated_by` | `t_vendor_commission_payment` | `updated_by` | `users` | `id` | NO ACTION | NO ACTION |

## Dependencias por Tabla Destino

### invoice_status (1 referencias entrantes)
- `t_invoice.f_invoicestat` -> `invoice_status.f_invoicestat` (ON DELETE NO ACTION)

### order_status (1 referencias entrantes)
- `t_order.f_orderstat` -> `order_status.f_orderstatus` (ON DELETE NO ACTION)

### t_client (2 referencias entrantes)
- `t_contact.f_client` -> `t_client.f_client` (ON DELETE CASCADE)
- `t_order.f_client` -> `t_client.f_client` (ON DELETE NO ACTION)

### t_contact (1 referencias entrantes)
- `t_order.f_contact` -> `t_contact.f_contact` (ON DELETE NO ACTION)

### t_fixed_expenses (1 referencias entrantes)
- `t_fixed_expenses_history.expense_id` -> `t_fixed_expenses.id` (ON DELETE NO ACTION)

### t_order (7 referencias entrantes)
- `order_gastos_indirectos.f_order` -> `t_order.f_order` (ON DELETE CASCADE)
- `order_gastos_operativos.f_order` -> `t_order.f_order` (ON DELETE CASCADE)
- `order_history.order_id` -> `t_order.f_order` (ON DELETE CASCADE)
- `t_commission_rate_history.order_id` -> `t_order.f_order` (ON DELETE NO ACTION)
- `t_expense.f_order` -> `t_order.f_order` (ON DELETE NO ACTION)
- `t_invoice.f_order` -> `t_order.f_order` (ON DELETE NO ACTION)
- `t_vendor_commission_payment.f_order` -> `t_order.f_order` (ON DELETE NO ACTION)

### t_payroll (4 referencias entrantes)
- `t_attendance.employee_id` -> `t_payroll.f_payroll` (ON DELETE CASCADE)
- `t_payroll_history.f_payroll` -> `t_payroll.f_payroll` (ON DELETE NO ACTION)
- `t_payrollovertime.employee_id` -> `t_payroll.f_payroll` (ON DELETE NO ACTION)
- `t_vacation.employee_id` -> `t_payroll.f_payroll` (ON DELETE CASCADE)

### t_supplier (1 referencias entrantes)
- `t_expense.f_supplier` -> `t_supplier.f_supplier` (ON DELETE NO ACTION)

### t_vendor (3 referencias entrantes)
- `t_commission_rate_history.vendor_id` -> `t_vendor.f_vendor` (ON DELETE NO ACTION)
- `t_order.f_salesman` -> `t_vendor.f_vendor` (ON DELETE NO ACTION)
- `t_vendor_commission_payment.f_vendor` -> `t_vendor.f_vendor` (ON DELETE NO ACTION)

### t_vendor_commission_payment (1 referencias entrantes)
- `t_commission_rate_history.commission_payment_id` -> `t_vendor_commission_payment.id` (ON DELETE SET NULL)

### users (22 referencias entrantes)
- `audit_log.user_id` -> `users.id` (ON DELETE NO ACTION)
- `invoice_audit.user_id` -> `users.id` (ON DELETE NO ACTION)
- `order_history.user_id` -> `users.id` (ON DELETE NO ACTION)
- `t_balance_adjustments.created_by` -> `users.id` (ON DELETE NO ACTION)
- `t_client.created_by` -> `users.id` (ON DELETE NO ACTION)
- `t_client.updated_by` -> `users.id` (ON DELETE NO ACTION)
- `t_expense.created_by` -> `users.id` (ON DELETE NO ACTION)
- `t_fixed_expenses.created_by` -> `users.id` (ON DELETE NO ACTION)
- `t_fixed_expenses_history.created_by` -> `users.id` (ON DELETE NO ACTION)
- `t_invoice.created_by` -> `users.id` (ON DELETE NO ACTION)
- `t_order.created_by` -> `users.id` (ON DELETE NO ACTION)
- `t_order.updated_by` -> `users.id` (ON DELETE NO ACTION)
- `t_overtime_hours.created_by` -> `users.id` (ON DELETE NO ACTION)
- `t_overtime_hours.updated_by` -> `users.id` (ON DELETE NO ACTION)
- `t_overtime_hours_audit.changed_by` -> `users.id` (ON DELETE NO ACTION)
- `t_payroll.created_by` -> `users.id` (ON DELETE NO ACTION)
- `t_payroll.updated_by` -> `users.id` (ON DELETE NO ACTION)
- `t_payroll_history.created_by` -> `users.id` (ON DELETE NO ACTION)
- `t_payrollovertime.created_by` -> `users.id` (ON DELETE NO ACTION)
- `t_vendor.f_user_id` -> `users.id` (ON DELETE NO ACTION)
- `t_vendor_commission_payment.created_by` -> `users.id` (ON DELETE NO ACTION)
- `t_vendor_commission_payment.updated_by` -> `users.id` (ON DELETE NO ACTION)

## Tablas Aisladas (sin FK)

- `app_versions`
- `t_attendance_audit`
- `t_expense_audit`
- `t_holiday`
- `t_order_deleted`
- `t_vacation_audit`
- `t_workday_config`

## Cascadas de Eliminación
> Tablas donde ON DELETE CASCADE puede causar eliminaciones en cadena

| Origen | Columna | Destino | Columna | Constraint |
|--------|---------|---------|---------|------------|
| `order_gastos_indirectos` | `f_order` | `t_order` | `f_order` | `order_gastos_indirectos_f_order_fkey` |
| `order_gastos_operativos` | `f_order` | `t_order` | `f_order` | `order_gastos_operativos_f_order_fkey` |
| `order_history` | `order_id` | `t_order` | `f_order` | `order_history_order_id_fkey` |
| `t_attendance` | `employee_id` | `t_payroll` | `f_payroll` | `t_attendance_employee_id_fkey` |
| `t_contact` | `f_client` | `t_client` | `f_client` | `t_contact_f_client_fkey` |
| `t_vacation` | `employee_id` | `t_payroll` | `f_payroll` | `t_vacation_employee_id_fkey` |
