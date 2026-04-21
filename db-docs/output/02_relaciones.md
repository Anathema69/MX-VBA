# Relaciones entre Tablas - Base de Datos IMA Mecatrónica
Generado: 2026-04-20 23:21:24
Total Foreign Keys: 68

## Resumen de Conectividad

| Tabla | FK Salientes | FK Entrantes | Rol |
|-------|-------------|-------------|-----|
| `app_versions` | 0 | 0 | Aislada |
| `audit_log` | 1 | 0 | Hoja (solo referencia) |
| `drive_activity` | 2 | 0 | Hoja (solo referencia) |
| `drive_audit` | 1 | 0 | Hoja (solo referencia) |
| `drive_files` | 2 | 0 | Hoja (solo referencia) |
| `drive_folders` | 3 | 3 | Intermedia |
| `inventory_audit` | 1 | 0 | Hoja (solo referencia) |
| `inventory_categories` | 2 | 1 | Intermedia |
| `inventory_movements` | 2 | 0 | Hoja (solo referencia) |
| `inventory_products` | 4 | 1 | Intermedia |
| `invoice_audit` | 1 | 0 | Hoja (solo referencia) |
| `invoice_status` | 0 | 1 | Catálogo/Lookup |
| `order_ejecutores` | 3 | 0 | Hoja (solo referencia) |
| `order_files` | 4 | 0 | Hoja (solo referencia) |
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
| `t_order` | 6 | 10 | Tabla Central |
| `t_order_deleted` | 0 | 0 | Aislada |
| `t_overtime_hours` | 2 | 0 | Hoja (solo referencia) |
| `t_overtime_hours_audit` | 1 | 0 | Hoja (solo referencia) |
| `t_payroll` | 2 | 5 | Tabla Central |
| `t_payroll_history` | 2 | 0 | Hoja (solo referencia) |
| `t_payrollovertime` | 2 | 0 | Hoja (solo referencia) |
| `t_supplier` | 0 | 2 | Catálogo/Lookup |
| `t_vacation` | 1 | 0 | Hoja (solo referencia) |
| `t_vacation_audit` | 0 | 0 | Aislada |
| `t_vendor` | 1 | 4 | Tabla Central |
| `t_vendor_commission_payment` | 4 | 2 | Intermedia |
| `t_workday_config` | 0 | 0 | Aislada |
| `users` | 0 | 34 | Tabla Central |

## Todas las Foreign Keys

| # | Constraint | Origen | Columna | Destino | Columna | ON UPDATE | ON DELETE |
|---|-----------|--------|---------|---------|---------|-----------|-----------|
| 1 | `audit_log_user_id_fkey` | `audit_log` | `user_id` | `users` | `id` | NO ACTION | NO ACTION |
| 2 | `drive_activity_folder_id_fkey` | `drive_activity` | `folder_id` | `drive_folders` | `id` | NO ACTION | SET NULL |
| 3 | `drive_activity_user_id_fkey` | `drive_activity` | `user_id` | `users` | `id` | NO ACTION | NO ACTION |
| 4 | `drive_audit_user_id_fkey` | `drive_audit` | `user_id` | `users` | `id` | NO ACTION | NO ACTION |
| 5 | `drive_files_folder_id_fkey` | `drive_files` | `folder_id` | `drive_folders` | `id` | NO ACTION | CASCADE |
| 6 | `drive_files_uploaded_by_fkey` | `drive_files` | `uploaded_by` | `users` | `id` | NO ACTION | NO ACTION |
| 7 | `drive_folders_created_by_fkey` | `drive_folders` | `created_by` | `users` | `id` | NO ACTION | NO ACTION |
| 8 | `drive_folders_linked_order_id_fkey` | `drive_folders` | `linked_order_id` | `t_order` | `f_order` | NO ACTION | SET NULL |
| 9 | `drive_folders_parent_id_fkey` | `drive_folders` | `parent_id` | `drive_folders` | `id` | NO ACTION | CASCADE |
| 10 | `inventory_audit_user_id_fkey` | `inventory_audit` | `user_id` | `users` | `id` | NO ACTION | NO ACTION |
| 11 | `inventory_categories_created_by_fkey` | `inventory_categories` | `created_by` | `users` | `id` | NO ACTION | NO ACTION |
| 12 | `inventory_categories_updated_by_fkey` | `inventory_categories` | `updated_by` | `users` | `id` | NO ACTION | NO ACTION |
| 13 | `inventory_movements_created_by_fkey` | `inventory_movements` | `created_by` | `users` | `id` | NO ACTION | NO ACTION |
| 14 | `inventory_movements_product_id_fkey` | `inventory_movements` | `product_id` | `inventory_products` | `id` | NO ACTION | CASCADE |
| 15 | `inventory_products_category_id_fkey` | `inventory_products` | `category_id` | `inventory_categories` | `id` | NO ACTION | CASCADE |
| 16 | `inventory_products_created_by_fkey` | `inventory_products` | `created_by` | `users` | `id` | NO ACTION | NO ACTION |
| 17 | `inventory_products_supplier_id_fkey` | `inventory_products` | `supplier_id` | `t_supplier` | `f_supplier` | NO ACTION | NO ACTION |
| 18 | `inventory_products_updated_by_fkey` | `inventory_products` | `updated_by` | `users` | `id` | NO ACTION | NO ACTION |
| 19 | `invoice_audit_user_id_fkey` | `invoice_audit` | `user_id` | `users` | `id` | NO ACTION | NO ACTION |
| 20 | `order_ejecutores_assigned_by_fkey` | `order_ejecutores` | `assigned_by` | `users` | `id` | NO ACTION | NO ACTION |
| 21 | `order_ejecutores_f_order_fkey` | `order_ejecutores` | `f_order` | `t_order` | `f_order` | NO ACTION | CASCADE |
| 22 | `order_ejecutores_payroll_id_fkey` | `order_ejecutores` | `payroll_id` | `t_payroll` | `f_payroll` | NO ACTION | CASCADE |
| 23 | `order_files_commission_id_fkey` | `order_files` | `commission_id` | `t_vendor_commission_payment` | `id` | NO ACTION | NO ACTION |
| 24 | `order_files_f_order_fkey` | `order_files` | `f_order` | `t_order` | `f_order` | NO ACTION | CASCADE |
| 25 | `order_files_uploaded_by_fkey` | `order_files` | `uploaded_by` | `users` | `id` | NO ACTION | NO ACTION |
| 26 | `order_files_vendor_id_fkey` | `order_files` | `vendor_id` | `t_vendor` | `f_vendor` | NO ACTION | NO ACTION |
| 27 | `order_gastos_indirectos_f_order_fkey` | `order_gastos_indirectos` | `f_order` | `t_order` | `f_order` | NO ACTION | CASCADE |
| 28 | `order_gastos_operativos_f_order_fkey` | `order_gastos_operativos` | `f_order` | `t_order` | `f_order` | NO ACTION | CASCADE |
| 29 | `order_history_order_id_fkey` | `order_history` | `order_id` | `t_order` | `f_order` | NO ACTION | CASCADE |
| 30 | `order_history_user_id_fkey` | `order_history` | `user_id` | `users` | `id` | NO ACTION | NO ACTION |
| 31 | `t_attendance_employee_id_fkey` | `t_attendance` | `employee_id` | `t_payroll` | `f_payroll` | NO ACTION | CASCADE |
| 32 | `t_balance_adjustments_created_by_fkey` | `t_balance_adjustments` | `created_by` | `users` | `id` | NO ACTION | NO ACTION |
| 33 | `t_client_created_by_fkey` | `t_client` | `created_by` | `users` | `id` | NO ACTION | NO ACTION |
| 34 | `t_client_updated_by_fkey` | `t_client` | `updated_by` | `users` | `id` | NO ACTION | NO ACTION |
| 35 | `t_commission_rate_history_commission_payment_id_fkey` | `t_commission_rate_history` | `commission_payment_id` | `t_vendor_commission_payment` | `id` | NO ACTION | SET NULL |
| 36 | `t_commission_rate_history_order_id_fkey` | `t_commission_rate_history` | `order_id` | `t_order` | `f_order` | NO ACTION | NO ACTION |
| 37 | `t_commission_rate_history_vendor_id_fkey` | `t_commission_rate_history` | `vendor_id` | `t_vendor` | `f_vendor` | NO ACTION | NO ACTION |
| 38 | `t_contact_f_client_fkey` | `t_contact` | `f_client` | `t_client` | `f_client` | NO ACTION | CASCADE |
| 39 | `t_expense_created_by_fkey` | `t_expense` | `created_by` | `users` | `id` | NO ACTION | NO ACTION |
| 40 | `t_expense_f_order_fkey` | `t_expense` | `f_order` | `t_order` | `f_order` | NO ACTION | NO ACTION |
| 41 | `t_expense_f_supplier_fkey` | `t_expense` | `f_supplier` | `t_supplier` | `f_supplier` | NO ACTION | NO ACTION |
| 42 | `t_fixed_expenses_created_by_fkey` | `t_fixed_expenses` | `created_by` | `users` | `id` | NO ACTION | NO ACTION |
| 43 | `t_fixed_expenses_history_created_by_fkey` | `t_fixed_expenses_history` | `created_by` | `users` | `id` | NO ACTION | NO ACTION |
| 44 | `t_fixed_expenses_history_expense_id_fkey` | `t_fixed_expenses_history` | `expense_id` | `t_fixed_expenses` | `id` | NO ACTION | NO ACTION |
| 45 | `t_invoice_created_by_fkey` | `t_invoice` | `created_by` | `users` | `id` | NO ACTION | NO ACTION |
| 46 | `t_invoice_f_invoicestat_fkey` | `t_invoice` | `f_invoicestat` | `invoice_status` | `f_invoicestat` | NO ACTION | NO ACTION |
| 47 | `t_invoice_f_order_fkey` | `t_invoice` | `f_order` | `t_order` | `f_order` | NO ACTION | NO ACTION |
| 48 | `t_order_created_by_fkey` | `t_order` | `created_by` | `users` | `id` | NO ACTION | NO ACTION |
| 49 | `t_order_f_client_fkey` | `t_order` | `f_client` | `t_client` | `f_client` | NO ACTION | NO ACTION |
| 50 | `t_order_f_contact_fkey` | `t_order` | `f_contact` | `t_contact` | `f_contact` | NO ACTION | NO ACTION |
| 51 | `t_order_f_orderstat_fkey` | `t_order` | `f_orderstat` | `order_status` | `f_orderstatus` | NO ACTION | NO ACTION |
| 52 | `t_order_f_salesman_fkey` | `t_order` | `f_salesman` | `t_vendor` | `f_vendor` | NO ACTION | NO ACTION |
| 53 | `t_order_updated_by_fkey` | `t_order` | `updated_by` | `users` | `id` | NO ACTION | NO ACTION |
| 54 | `t_overtime_hours_created_by_fkey` | `t_overtime_hours` | `created_by` | `users` | `id` | NO ACTION | NO ACTION |
| 55 | `t_overtime_hours_updated_by_fkey` | `t_overtime_hours` | `updated_by` | `users` | `id` | NO ACTION | NO ACTION |
| 56 | `t_overtime_hours_audit_changed_by_fkey` | `t_overtime_hours_audit` | `changed_by` | `users` | `id` | NO ACTION | NO ACTION |
| 57 | `t_payroll_created_by_fkey` | `t_payroll` | `created_by` | `users` | `id` | NO ACTION | NO ACTION |
| 58 | `t_payroll_updated_by_fkey` | `t_payroll` | `updated_by` | `users` | `id` | NO ACTION | NO ACTION |
| 59 | `t_payroll_history_created_by_fkey` | `t_payroll_history` | `created_by` | `users` | `id` | NO ACTION | NO ACTION |
| 60 | `t_payroll_history_f_payroll_fkey` | `t_payroll_history` | `f_payroll` | `t_payroll` | `f_payroll` | NO ACTION | NO ACTION |
| 61 | `t_payrollovertime_created_by_fkey` | `t_payrollovertime` | `created_by` | `users` | `id` | NO ACTION | NO ACTION |
| 62 | `t_payrollovertime_employee_id_fkey` | `t_payrollovertime` | `employee_id` | `t_payroll` | `f_payroll` | NO ACTION | NO ACTION |
| 63 | `t_vacation_employee_id_fkey` | `t_vacation` | `employee_id` | `t_payroll` | `f_payroll` | NO ACTION | CASCADE |
| 64 | `t_vendor_f_user_id_fkey` | `t_vendor` | `f_user_id` | `users` | `id` | NO ACTION | NO ACTION |
| 65 | `fk_commission_created_by` | `t_vendor_commission_payment` | `created_by` | `users` | `id` | NO ACTION | NO ACTION |
| 66 | `fk_commission_order` | `t_vendor_commission_payment` | `f_order` | `t_order` | `f_order` | NO ACTION | NO ACTION |
| 67 | `fk_commission_vendor` | `t_vendor_commission_payment` | `f_vendor` | `t_vendor` | `f_vendor` | NO ACTION | NO ACTION |
| 68 | `fk_commission_updated_by` | `t_vendor_commission_payment` | `updated_by` | `users` | `id` | NO ACTION | NO ACTION |

## Dependencias por Tabla Destino

### drive_folders (3 referencias entrantes)
- `drive_activity.folder_id` -> `drive_folders.id` (ON DELETE SET NULL)
- `drive_files.folder_id` -> `drive_folders.id` (ON DELETE CASCADE)
- `drive_folders.parent_id` -> `drive_folders.id` (ON DELETE CASCADE)

### inventory_categories (1 referencias entrantes)
- `inventory_products.category_id` -> `inventory_categories.id` (ON DELETE CASCADE)

### inventory_products (1 referencias entrantes)
- `inventory_movements.product_id` -> `inventory_products.id` (ON DELETE CASCADE)

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

### t_order (10 referencias entrantes)
- `drive_folders.linked_order_id` -> `t_order.f_order` (ON DELETE SET NULL)
- `order_ejecutores.f_order` -> `t_order.f_order` (ON DELETE CASCADE)
- `order_files.f_order` -> `t_order.f_order` (ON DELETE CASCADE)
- `order_gastos_indirectos.f_order` -> `t_order.f_order` (ON DELETE CASCADE)
- `order_gastos_operativos.f_order` -> `t_order.f_order` (ON DELETE CASCADE)
- `order_history.order_id` -> `t_order.f_order` (ON DELETE CASCADE)
- `t_commission_rate_history.order_id` -> `t_order.f_order` (ON DELETE NO ACTION)
- `t_expense.f_order` -> `t_order.f_order` (ON DELETE NO ACTION)
- `t_invoice.f_order` -> `t_order.f_order` (ON DELETE NO ACTION)
- `t_vendor_commission_payment.f_order` -> `t_order.f_order` (ON DELETE NO ACTION)

### t_payroll (5 referencias entrantes)
- `order_ejecutores.payroll_id` -> `t_payroll.f_payroll` (ON DELETE CASCADE)
- `t_attendance.employee_id` -> `t_payroll.f_payroll` (ON DELETE CASCADE)
- `t_payroll_history.f_payroll` -> `t_payroll.f_payroll` (ON DELETE NO ACTION)
- `t_payrollovertime.employee_id` -> `t_payroll.f_payroll` (ON DELETE NO ACTION)
- `t_vacation.employee_id` -> `t_payroll.f_payroll` (ON DELETE CASCADE)

### t_supplier (2 referencias entrantes)
- `inventory_products.supplier_id` -> `t_supplier.f_supplier` (ON DELETE NO ACTION)
- `t_expense.f_supplier` -> `t_supplier.f_supplier` (ON DELETE NO ACTION)

### t_vendor (4 referencias entrantes)
- `order_files.vendor_id` -> `t_vendor.f_vendor` (ON DELETE NO ACTION)
- `t_commission_rate_history.vendor_id` -> `t_vendor.f_vendor` (ON DELETE NO ACTION)
- `t_order.f_salesman` -> `t_vendor.f_vendor` (ON DELETE NO ACTION)
- `t_vendor_commission_payment.f_vendor` -> `t_vendor.f_vendor` (ON DELETE NO ACTION)

### t_vendor_commission_payment (2 referencias entrantes)
- `order_files.commission_id` -> `t_vendor_commission_payment.id` (ON DELETE NO ACTION)
- `t_commission_rate_history.commission_payment_id` -> `t_vendor_commission_payment.id` (ON DELETE SET NULL)

### users (34 referencias entrantes)
- `audit_log.user_id` -> `users.id` (ON DELETE NO ACTION)
- `drive_activity.user_id` -> `users.id` (ON DELETE NO ACTION)
- `drive_audit.user_id` -> `users.id` (ON DELETE NO ACTION)
- `drive_files.uploaded_by` -> `users.id` (ON DELETE NO ACTION)
- `drive_folders.created_by` -> `users.id` (ON DELETE NO ACTION)
- `inventory_audit.user_id` -> `users.id` (ON DELETE NO ACTION)
- `inventory_categories.created_by` -> `users.id` (ON DELETE NO ACTION)
- `inventory_categories.updated_by` -> `users.id` (ON DELETE NO ACTION)
- `inventory_movements.created_by` -> `users.id` (ON DELETE NO ACTION)
- `inventory_products.created_by` -> `users.id` (ON DELETE NO ACTION)
- `inventory_products.updated_by` -> `users.id` (ON DELETE NO ACTION)
- `invoice_audit.user_id` -> `users.id` (ON DELETE NO ACTION)
- `order_ejecutores.assigned_by` -> `users.id` (ON DELETE NO ACTION)
- `order_files.uploaded_by` -> `users.id` (ON DELETE NO ACTION)
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
| `drive_files` | `folder_id` | `drive_folders` | `id` | `drive_files_folder_id_fkey` |
| `drive_folders` | `parent_id` | `drive_folders` | `id` | `drive_folders_parent_id_fkey` |
| `inventory_movements` | `product_id` | `inventory_products` | `id` | `inventory_movements_product_id_fkey` |
| `inventory_products` | `category_id` | `inventory_categories` | `id` | `inventory_products_category_id_fkey` |
| `order_ejecutores` | `f_order` | `t_order` | `f_order` | `order_ejecutores_f_order_fkey` |
| `order_ejecutores` | `payroll_id` | `t_payroll` | `f_payroll` | `order_ejecutores_payroll_id_fkey` |
| `order_files` | `f_order` | `t_order` | `f_order` | `order_files_f_order_fkey` |
| `order_gastos_indirectos` | `f_order` | `t_order` | `f_order` | `order_gastos_indirectos_f_order_fkey` |
| `order_gastos_operativos` | `f_order` | `t_order` | `f_order` | `order_gastos_operativos_f_order_fkey` |
| `order_history` | `order_id` | `t_order` | `f_order` | `order_history_order_id_fkey` |
| `t_attendance` | `employee_id` | `t_payroll` | `f_payroll` | `t_attendance_employee_id_fkey` |
| `t_contact` | `f_client` | `t_client` | `f_client` | `t_contact_f_client_fkey` |
| `t_vacation` | `employee_id` | `t_payroll` | `f_payroll` | `t_vacation_employee_id_fkey` |
