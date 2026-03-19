# Documentación de Indexes - Base de Datos IMA Mecatrónica
Generado: 2026-02-26 22:31:45
Total indexes: 109 | Tamaño total: 1.8 MB

## Resumen

| Tipo | Cantidad |
|------|----------|
| PRIMARY KEY | 34 |
| UNIQUE | 10 |
| Regular | 65 |
| **Total** | **109** |

Método dominante: btree (109/109)

## Indexes por Tabla

### app_versions

| Index | Tipo | Columnas | Método | Tamaño | Scans | Valid |
|-------|------|----------|--------|--------|-------|-------|
| `app_versions_pkey` | PK | id | btree | 16 kB | 79 | OK |
| `app_versions_version_key` | UNIQUE | version | btree | 16 kB | 19 | OK |
| `idx_app_versions_latest` | INDEX | is_latest, is_active | btree | 16 kB | 495 | OK |
| `idx_app_versions_release_date` | INDEX | release_date | btree | 16 kB | 2 | OK |

### audit_log

| Index | Tipo | Columnas | Método | Tamaño | Scans | Valid |
|-------|------|----------|--------|--------|-------|-------|
| `audit_log_pkey` | PK | id | btree | 8192 bytes | 0 | OK |
| `idx_audit_table` | INDEX | table_name | btree | 8192 bytes | 0 | OK |
| `idx_audit_user` | INDEX | user_id | btree | 8192 bytes | 41 | OK |

### invoice_audit

| Index | Tipo | Columnas | Método | Tamaño | Scans | Valid |
|-------|------|----------|--------|--------|-------|-------|
| `invoice_audit_pkey` | PK | id | btree | 8192 bytes | 0 | OK |

### invoice_status

| Index | Tipo | Columnas | Método | Tamaño | Scans | Valid |
|-------|------|----------|--------|--------|-------|-------|
| `invoice_status_pkey` | PK | f_invoicestat | btree | 16 kB | 3,112 | OK |

### mv_balance_completo

| Index | Tipo | Columnas | Método | Tamaño | Scans | Valid |
|-------|------|----------|--------|--------|-------|-------|
| `idx_mv_balance_pk` | UNIQUE | año, mes_numero | btree | 16 kB | 14 | OK |

### order_gastos_indirectos

| Index | Tipo | Columnas | Método | Tamaño | Scans | Valid |
|-------|------|----------|--------|--------|-------|-------|
| `idx_gastos_indirectos_order` | INDEX | f_order | btree | 16 kB | 224 | OK |
| `order_gastos_indirectos_pkey` | PK | id | btree | 16 kB | 25 | OK |

### order_gastos_operativos

| Index | Tipo | Columnas | Método | Tamaño | Scans | Valid |
|-------|------|----------|--------|--------|-------|-------|
| `idx_gastos_operativos_order` | INDEX | f_order | btree | 16 kB | 92 | OK |
| `order_gastos_operativos_pkey` | PK | id | btree | 16 kB | 24 | OK |

### order_history

| Index | Tipo | Columnas | Método | Tamaño | Scans | Valid |
|-------|------|----------|--------|--------|-------|-------|
| `idx_order_history_date` | INDEX | changed_at | btree | 48 kB | 3,176 | OK |
| `idx_order_history_order` | INDEX | order_id | btree | 40 kB | 1,201 | OK |
| `idx_order_history_user` | INDEX | user_id | btree | 16 kB | 33 | OK |
| `order_history_pkey` | PK | id | btree | 48 kB | 9 | OK |

### order_status

| Index | Tipo | Columnas | Método | Tamaño | Scans | Valid |
|-------|------|----------|--------|--------|-------|-------|
| `order_status_pkey` | PK | f_orderstatus | btree | 16 kB | 4,145 | OK |

### t_attendance

| Index | Tipo | Columnas | Método | Tamaño | Scans | Valid |
|-------|------|----------|--------|--------|-------|-------|
| `idx_attendance_date` | INDEX | attendance_date | btree | 16 kB | 285 | OK |
| `idx_attendance_employee` | INDEX | employee_id | btree | 16 kB | 2 | OK |
| `idx_attendance_status` | INDEX | status | btree | 16 kB | 0 | OK |
| `t_attendance_employee_id_attendance_date_key` | UNIQUE | employee_id, attendance_date | btree | 16 kB | 0 | OK |
| `t_attendance_pkey` | PK | id | btree | 16 kB | 5 | OK |

### t_attendance_audit

| Index | Tipo | Columnas | Método | Tamaño | Scans | Valid |
|-------|------|----------|--------|--------|-------|-------|
| `idx_audit_action` | INDEX | action | btree | 16 kB | 0 | OK |
| `idx_audit_attendance_id` | INDEX | attendance_id | btree | 16 kB | 0 | OK |
| `idx_audit_changed_at` | INDEX | changed_at | btree | 16 kB | 1 | OK |
| `idx_audit_date` | INDEX | attendance_date | btree | 16 kB | 0 | OK |
| `idx_audit_employee_id` | INDEX | employee_id | btree | 16 kB | 0 | OK |
| `t_attendance_audit_pkey` | PK | id | btree | 16 kB | 6 | OK |

### t_balance_adjustments

| Index | Tipo | Columnas | Método | Tamaño | Scans | Valid |
|-------|------|----------|--------|--------|-------|-------|
| `t_balance_adjustments_pkey` | PK | id | btree | 8192 bytes | 0 | OK |
| `t_balance_adjustments_year_month_adjustment_type_key` | UNIQUE | year, month, adjustment_type | btree | 8192 bytes | 0 | OK |

### t_client

| Index | Tipo | Columnas | Método | Tamaño | Scans | Valid |
|-------|------|----------|--------|--------|-------|-------|
| `idx_client_name` | INDEX | f_name | btree | 16 kB | 4 | OK |
| `t_client_pkey` | PK | f_client | btree | 16 kB | 4,422 | OK |

### t_commission_rate_history

| Index | Tipo | Columnas | Método | Tamaño | Scans | Valid |
|-------|------|----------|--------|--------|-------|-------|
| `idx_commission_history_date` | INDEX | changed_at | btree | 16 kB | 1 | OK |
| `idx_commission_history_order` | INDEX | order_id | btree | 16 kB | 1 | OK |
| `idx_commission_history_vendor` | INDEX | vendor_id | btree | 16 kB | 3 | OK |
| `t_commission_rate_history_pkey` | PK | id | btree | 16 kB | 0 | OK |

### t_contact

| Index | Tipo | Columnas | Método | Tamaño | Scans | Valid |
|-------|------|----------|--------|--------|-------|-------|
| `idx_contact_client` | INDEX | f_client | btree | 16 kB | 243 | OK |
| `idx_contact_client_active` | INDEX | f_client | btree | 16 kB | 4 | OK |
| `idx_contact_email` | INDEX | f_email | btree | 16 kB | 0 | OK |
| `t_contact_pkey` | PK | f_contact | btree | 16 kB | 1,890 | OK |

### t_expense

| Index | Tipo | Columnas | Método | Tamaño | Scans | Valid |
|-------|------|----------|--------|--------|-------|-------|
| `idx_expense_date` | INDEX | f_expensedate | btree | 16 kB | 210 | OK |
| `idx_expense_order_date` | INDEX | f_order, f_expensedate | btree | 16 kB | 1,034 | OK |
| `idx_expense_order_status` | INDEX | f_order, f_status | btree | 16 kB | 0 | OK |
| `idx_expense_status_scheduled` | INDEX | f_status, f_scheduleddate | btree | 16 kB | 0 | OK |
| `idx_expense_supplier` | INDEX | f_supplier | btree | 16 kB | 591 | OK |
| `idx_expense_updated_by` | INDEX | updated_by | btree | 16 kB | 0 | OK |
| `t_expense_pkey` | PK | f_expense | btree | 16 kB | 2,778 | OK |

### t_expense_audit

| Index | Tipo | Columnas | Método | Tamaño | Scans | Valid |
|-------|------|----------|--------|--------|-------|-------|
| `idx_expense_audit_action` | INDEX | action | btree | 16 kB | 0 | OK |
| `idx_expense_audit_changed_at` | INDEX | changed_at | btree | 16 kB | 0 | OK |
| `idx_expense_audit_expense_id` | INDEX | expense_id | btree | 16 kB | 0 | OK |
| `idx_expense_audit_updated_by` | INDEX | new_updated_by | btree | 16 kB | 0 | OK |
| `t_expense_audit_pkey` | PK | id | btree | 16 kB | 0 | OK |

### t_fixed_expenses

| Index | Tipo | Columnas | Método | Tamaño | Scans | Valid |
|-------|------|----------|--------|--------|-------|-------|
| `t_fixed_expenses_pkey` | PK | id | btree | 16 kB | 352 | OK |

### t_fixed_expenses_history

| Index | Tipo | Columnas | Método | Tamaño | Scans | Valid |
|-------|------|----------|--------|--------|-------|-------|
| `idx_fixed_expenses_history_lookup` | INDEX | expense_id, effective_date | btree | 16 kB | 0 | OK |
| `t_fixed_expenses_history_pkey` | PK | id | btree | 16 kB | 1 | OK |

### t_holiday

| Index | Tipo | Columnas | Método | Tamaño | Scans | Valid |
|-------|------|----------|--------|--------|-------|-------|
| `idx_holiday_date` | INDEX | holiday_date | btree | 16 kB | 22 | OK |
| `idx_holiday_year` | INDEX | None | btree | 16 kB | 1 | OK |
| `t_holiday_holiday_date_key` | UNIQUE | holiday_date | btree | 16 kB | 14 | OK |
| `t_holiday_pkey` | PK | id | btree | 16 kB | 0 | OK |

### t_invoice

| Index | Tipo | Columnas | Método | Tamaño | Scans | Valid |
|-------|------|----------|--------|--------|-------|-------|
| `idx_invoice_folio` | INDEX | f_folio | btree | 40 kB | 16 | OK |
| `idx_invoice_order` | INDEX | f_order | btree | 16 kB | 12,229 | OK |
| `idx_invoice_order_folio` | UNIQUE | f_order, f_folio | btree | 40 kB | 44 | OK |
| `idx_invoice_status` | INDEX | f_invoicestat | btree | 16 kB | 9 | OK |
| `t_invoice_pkey` | PK | f_invoice | btree | 32 kB | 2,130 | OK |

### t_order

| Index | Tipo | Columnas | Método | Tamaño | Scans | Valid |
|-------|------|----------|--------|--------|-------|-------|
| `idx_order_client` | INDEX | f_client | btree | 16 kB | 4,011 | OK |
| `idx_order_po` | INDEX | f_po | btree | 40 kB | 114 | OK |
| `idx_order_podate` | INDEX | f_podate | btree | 16 kB | 583 | OK |
| `idx_order_status` | INDEX | f_orderstat | btree | 16 kB | 113 | OK |
| `idx_order_status_podate` | INDEX | f_orderstat, f_podate | btree | 16 kB | 0 | OK |
| `t_order_pkey` | PK | f_order | btree | 16 kB | 27,718 | OK |

### t_order_deleted

| Index | Tipo | Columnas | Método | Tamaño | Scans | Valid |
|-------|------|----------|--------|--------|-------|-------|
| `idx_order_deleted_date` | INDEX | deleted_at | btree | 16 kB | 3 | OK |
| `idx_order_deleted_original_id` | INDEX | original_order_id | btree | 16 kB | 0 | OK |
| `idx_order_deleted_po` | INDEX | f_po | btree | 16 kB | 0 | OK |
| `t_order_deleted_pkey` | PK | id | btree | 16 kB | 0 | OK |

### t_overtime_hours

| Index | Tipo | Columnas | Método | Tamaño | Scans | Valid |
|-------|------|----------|--------|--------|-------|-------|
| `idx_overtime_year_month` | INDEX | year, month | btree | 16 kB | 38 | OK |
| `t_overtime_hours_pkey` | PK | id | btree | 16 kB | 14 | OK |
| `t_overtime_hours_year_month_key` | UNIQUE | year, month | btree | 16 kB | 0 | OK |

### t_overtime_hours_audit

| Index | Tipo | Columnas | Método | Tamaño | Scans | Valid |
|-------|------|----------|--------|--------|-------|-------|
| `idx_overtime_audit_date` | INDEX | changed_at | btree | 16 kB | 0 | OK |
| `t_overtime_hours_audit_pkey` | PK | id | btree | 16 kB | 0 | OK |

### t_payroll

| Index | Tipo | Columnas | Método | Tamaño | Scans | Valid |
|-------|------|----------|--------|--------|-------|-------|
| `idx_payroll_active` | INDEX | is_active | btree | 16 kB | 0 | OK |
| `t_payroll_pkey` | PK | f_payroll | btree | 16 kB | 736 | OK |

### t_payroll_history

| Index | Tipo | Columnas | Método | Tamaño | Scans | Valid |
|-------|------|----------|--------|--------|-------|-------|
| `idx_payroll_history_active` | INDEX | f_payroll, is_active | btree | 16 kB | 9 | OK |
| `idx_payroll_history_date` | INDEX | effective_date | btree | 16 kB | 1 | OK |
| `idx_payroll_history_employee` | INDEX | f_payroll, effective_date | btree | 16 kB | 211,491 | OK |
| `t_payroll_history_pkey` | PK | id | btree | 16 kB | 0 | OK |

### t_payrollovertime

| Index | Tipo | Columnas | Método | Tamaño | Scans | Valid |
|-------|------|----------|--------|--------|-------|-------|
| `t_payrollovertime_pkey` | PK | f_payrollovertime | btree | 16 kB | 226 | OK |

### t_supplier

| Index | Tipo | Columnas | Método | Tamaño | Scans | Valid |
|-------|------|----------|--------|--------|-------|-------|
| `t_supplier_pkey` | PK | f_supplier | btree | 16 kB | 5,187 | OK |

### t_vacation

| Index | Tipo | Columnas | Método | Tamaño | Scans | Valid |
|-------|------|----------|--------|--------|-------|-------|
| `idx_vacation_dates` | INDEX | start_date, end_date | btree | 16 kB | 0 | OK |
| `idx_vacation_employee` | INDEX | employee_id | btree | 16 kB | 0 | OK |
| `idx_vacation_status` | INDEX | status | btree | 16 kB | 317 | OK |
| `t_vacation_pkey` | PK | id | btree | 16 kB | 0 | OK |

### t_vacation_audit

| Index | Tipo | Columnas | Método | Tamaño | Scans | Valid |
|-------|------|----------|--------|--------|-------|-------|
| `idx_vacation_audit_employee` | INDEX | employee_id | btree | 16 kB | 0 | OK |
| `idx_vacation_audit_id` | INDEX | vacation_id | btree | 16 kB | 0 | OK |
| `t_vacation_audit_pkey` | PK | id | btree | 16 kB | 0 | OK |

### t_vendor

| Index | Tipo | Columnas | Método | Tamaño | Scans | Valid |
|-------|------|----------|--------|--------|-------|-------|
| `idx_vendor_name` | INDEX | f_vendorname | btree | 16 kB | 0 | OK |
| `idx_vendor_user` | INDEX | f_user_id | btree | 16 kB | 77 | OK |
| `t_vendor_pkey` | PK | f_vendor | btree | 16 kB | 1,517 | OK |

### t_vendor_commission_payment

| Index | Tipo | Columnas | Método | Tamaño | Scans | Valid |
|-------|------|----------|--------|--------|-------|-------|
| `idx_commission_payment_order` | INDEX | f_order | btree | 16 kB | 286 | OK |
| `idx_commission_payment_status` | INDEX | payment_status | btree | 16 kB | 132 | OK |
| `idx_commission_payment_vendor` | INDEX | f_vendor | btree | 16 kB | 140 | OK |
| `t_vendor_commission_payment_pkey` | PK | id | btree | 16 kB | 65 | OK |

### t_workday_config

| Index | Tipo | Columnas | Método | Tamaño | Scans | Valid |
|-------|------|----------|--------|--------|-------|-------|
| `t_workday_config_day_of_week_key` | UNIQUE | day_of_week | btree | 16 kB | 7 | OK |
| `t_workday_config_pkey` | PK | id | btree | 16 kB | 0 | OK |

### users

| Index | Tipo | Columnas | Método | Tamaño | Scans | Valid |
|-------|------|----------|--------|--------|-------|-------|
| `idx_users_active` | INDEX | is_active | btree | 16 kB | 0 | OK |
| `users_email_key` | UNIQUE | email | btree | 16 kB | 0 | OK |
| `users_pkey` | PK | id | btree | 16 kB | 36 | OK |
| `users_username_key` | UNIQUE | username | btree | 16 kB | 5 | OK |

## Top 10 Indexes Más Usados

| # | Tabla | Index | Scans | Tuples Returned |
|---|-------|-------|-------|-----------------|
| 1 | `t_payroll_history` | `idx_payroll_history_employee` | 211,491 | 36,395 |
| 2 | `t_order` | `t_order_pkey` | 27,718 | 66,757 |
| 3 | `t_invoice` | `idx_invoice_order` | 12,229 | 435,135 |
| 4 | `t_supplier` | `t_supplier_pkey` | 5,187 | 5,059 |
| 5 | `t_client` | `t_client_pkey` | 4,422 | 4,705 |
| 6 | `order_status` | `order_status_pkey` | 4,145 | 4,170 |
| 7 | `t_order` | `idx_order_client` | 4,011 | 12,485 |
| 8 | `order_history` | `idx_order_history_date` | 3,176 | 284,021 |
| 9 | `invoice_status` | `invoice_status_pkey` | 3,112 | 3,136 |
| 10 | `t_expense` | `t_expense_pkey` | 2,778 | 1,237 |

## Indexes Sin Uso (posibles candidatos a revisión)
> Indexes con 0 scans que no son PK ni UNIQUE

| Tabla | Index | Columnas | Tamaño |
|-------|-------|----------|--------|
| `audit_log` | `idx_audit_table` | table_name | 8192 bytes |
| `t_attendance` | `idx_attendance_status` | status | 16 kB |
| `t_attendance_audit` | `idx_audit_action` | action | 16 kB |
| `t_attendance_audit` | `idx_audit_attendance_id` | attendance_id | 16 kB |
| `t_attendance_audit` | `idx_audit_date` | attendance_date | 16 kB |
| `t_attendance_audit` | `idx_audit_employee_id` | employee_id | 16 kB |
| `t_contact` | `idx_contact_email` | f_email | 16 kB |
| `t_expense` | `idx_expense_order_status` | f_order, f_status | 16 kB |
| `t_expense` | `idx_expense_status_scheduled` | f_status, f_scheduleddate | 16 kB |
| `t_expense` | `idx_expense_updated_by` | updated_by | 16 kB |
| `t_expense_audit` | `idx_expense_audit_action` | action | 16 kB |
| `t_expense_audit` | `idx_expense_audit_changed_at` | changed_at | 16 kB |
| `t_expense_audit` | `idx_expense_audit_expense_id` | expense_id | 16 kB |
| `t_expense_audit` | `idx_expense_audit_updated_by` | new_updated_by | 16 kB |
| `t_fixed_expenses_history` | `idx_fixed_expenses_history_lookup` | expense_id, effective_date | 16 kB |
| `t_order` | `idx_order_status_podate` | f_orderstat, f_podate | 16 kB |
| `t_order_deleted` | `idx_order_deleted_original_id` | original_order_id | 16 kB |
| `t_order_deleted` | `idx_order_deleted_po` | f_po | 16 kB |
| `t_overtime_hours_audit` | `idx_overtime_audit_date` | changed_at | 16 kB |
| `t_payroll` | `idx_payroll_active` | is_active | 16 kB |
| `t_vacation` | `idx_vacation_dates` | start_date, end_date | 16 kB |
| `t_vacation` | `idx_vacation_employee` | employee_id | 16 kB |
| `t_vacation_audit` | `idx_vacation_audit_employee` | employee_id | 16 kB |
| `t_vacation_audit` | `idx_vacation_audit_id` | vacation_id | 16 kB |
| `t_vendor` | `idx_vendor_name` | f_vendorname | 16 kB |
| `users` | `idx_users_active` | is_active | 16 kB |

## Top 10 Indexes por Tamaño

| # | Tabla | Index | Tamaño | Tipo |
|---|-------|-------|--------|------|
| 1 | `order_history` | `idx_order_history_date` | 48 kB | INDEX |
| 2 | `order_history` | `order_history_pkey` | 48 kB | PK |
| 3 | `order_history` | `idx_order_history_order` | 40 kB | INDEX |
| 4 | `t_invoice` | `idx_invoice_folio` | 40 kB | INDEX |
| 5 | `t_invoice` | `idx_invoice_order_folio` | 40 kB | UNIQUE |
| 6 | `t_order` | `idx_order_po` | 40 kB | INDEX |
| 7 | `t_invoice` | `t_invoice_pkey` | 32 kB | PK |
| 8 | `app_versions` | `app_versions_pkey` | 16 kB | PK |
| 9 | `app_versions` | `app_versions_version_key` | 16 kB | UNIQUE |
| 10 | `app_versions` | `idx_app_versions_latest` | 16 kB | INDEX |

## Definiciones Completas

- `app_versions_version_key`: `CREATE UNIQUE INDEX app_versions_version_key ON public.app_versions USING btree (version)`
- `idx_app_versions_latest`: `CREATE INDEX idx_app_versions_latest ON public.app_versions USING btree (is_latest, is_active) WHERE (is_latest = true)`
- `idx_app_versions_release_date`: `CREATE INDEX idx_app_versions_release_date ON public.app_versions USING btree (release_date DESC)`
- `idx_audit_table`: `CREATE INDEX idx_audit_table ON public.audit_log USING btree (table_name)`
- `idx_audit_user`: `CREATE INDEX idx_audit_user ON public.audit_log USING btree (user_id)`
- `idx_mv_balance_pk`: `CREATE UNIQUE INDEX idx_mv_balance_pk ON public.mv_balance_completo USING btree ("año", mes_numero)`
- `idx_gastos_indirectos_order`: `CREATE INDEX idx_gastos_indirectos_order ON public.order_gastos_indirectos USING btree (f_order)`
- `idx_gastos_operativos_order`: `CREATE INDEX idx_gastos_operativos_order ON public.order_gastos_operativos USING btree (f_order)`
- `idx_order_history_date`: `CREATE INDEX idx_order_history_date ON public.order_history USING btree (changed_at)`
- `idx_order_history_order`: `CREATE INDEX idx_order_history_order ON public.order_history USING btree (order_id)`
- `idx_order_history_user`: `CREATE INDEX idx_order_history_user ON public.order_history USING btree (user_id)`
- `idx_attendance_date`: `CREATE INDEX idx_attendance_date ON public.t_attendance USING btree (attendance_date)`
- `idx_attendance_employee`: `CREATE INDEX idx_attendance_employee ON public.t_attendance USING btree (employee_id)`
- `idx_attendance_status`: `CREATE INDEX idx_attendance_status ON public.t_attendance USING btree (status)`
- `t_attendance_employee_id_attendance_date_key`: `CREATE UNIQUE INDEX t_attendance_employee_id_attendance_date_key ON public.t_attendance USING btree (employee_id, attendance_date)`
- `idx_audit_action`: `CREATE INDEX idx_audit_action ON public.t_attendance_audit USING btree (action)`
- `idx_audit_attendance_id`: `CREATE INDEX idx_audit_attendance_id ON public.t_attendance_audit USING btree (attendance_id)`
- `idx_audit_changed_at`: `CREATE INDEX idx_audit_changed_at ON public.t_attendance_audit USING btree (changed_at)`
- `idx_audit_date`: `CREATE INDEX idx_audit_date ON public.t_attendance_audit USING btree (attendance_date)`
- `idx_audit_employee_id`: `CREATE INDEX idx_audit_employee_id ON public.t_attendance_audit USING btree (employee_id)`
- `t_balance_adjustments_year_month_adjustment_type_key`: `CREATE UNIQUE INDEX t_balance_adjustments_year_month_adjustment_type_key ON public.t_balance_adjustments USING btree (year, month, adjustment_type)`
- `idx_client_name`: `CREATE INDEX idx_client_name ON public.t_client USING btree (f_name)`
- `idx_commission_history_date`: `CREATE INDEX idx_commission_history_date ON public.t_commission_rate_history USING btree (changed_at)`
- `idx_commission_history_order`: `CREATE INDEX idx_commission_history_order ON public.t_commission_rate_history USING btree (order_id)`
- `idx_commission_history_vendor`: `CREATE INDEX idx_commission_history_vendor ON public.t_commission_rate_history USING btree (vendor_id)`
- `idx_contact_client`: `CREATE INDEX idx_contact_client ON public.t_contact USING btree (f_client)`
- `idx_contact_client_active`: `CREATE INDEX idx_contact_client_active ON public.t_contact USING btree (f_client) WHERE (is_active = true)`
- `idx_contact_email`: `CREATE INDEX idx_contact_email ON public.t_contact USING btree (f_email)`
- `idx_expense_date`: `CREATE INDEX idx_expense_date ON public.t_expense USING btree (f_expensedate)`
- `idx_expense_order_date`: `CREATE INDEX idx_expense_order_date ON public.t_expense USING btree (f_order, f_expensedate DESC) WHERE (f_order IS NOT NULL)`
- `idx_expense_order_status`: `CREATE INDEX idx_expense_order_status ON public.t_expense USING btree (f_order, f_status) WHERE (f_order IS NOT NULL)`
- `idx_expense_status_scheduled`: `CREATE INDEX idx_expense_status_scheduled ON public.t_expense USING btree (f_status, f_scheduleddate) WHERE ((f_status)::text = 'PENDIENTE'::text)`
- `idx_expense_supplier`: `CREATE INDEX idx_expense_supplier ON public.t_expense USING btree (f_supplier)`
- `idx_expense_updated_by`: `CREATE INDEX idx_expense_updated_by ON public.t_expense USING btree (updated_by)`
- `idx_expense_audit_action`: `CREATE INDEX idx_expense_audit_action ON public.t_expense_audit USING btree (action)`
- `idx_expense_audit_changed_at`: `CREATE INDEX idx_expense_audit_changed_at ON public.t_expense_audit USING btree (changed_at DESC)`
- `idx_expense_audit_expense_id`: `CREATE INDEX idx_expense_audit_expense_id ON public.t_expense_audit USING btree (expense_id)`
- `idx_expense_audit_updated_by`: `CREATE INDEX idx_expense_audit_updated_by ON public.t_expense_audit USING btree (new_updated_by)`
- `idx_fixed_expenses_history_lookup`: `CREATE INDEX idx_fixed_expenses_history_lookup ON public.t_fixed_expenses_history USING btree (expense_id, effective_date DESC) WHERE ((change_type)::text <> ALL ((ARRAY['DEACTIVATED'::character varying, 'DELETED'::character varying])::text[]))`
- `idx_holiday_date`: `CREATE INDEX idx_holiday_date ON public.t_holiday USING btree (holiday_date)`
- `idx_holiday_year`: `CREATE INDEX idx_holiday_year ON public.t_holiday USING btree (EXTRACT(year FROM holiday_date))`
- `t_holiday_holiday_date_key`: `CREATE UNIQUE INDEX t_holiday_holiday_date_key ON public.t_holiday USING btree (holiday_date)`
- `idx_invoice_folio`: `CREATE INDEX idx_invoice_folio ON public.t_invoice USING btree (f_folio)`
- `idx_invoice_order`: `CREATE INDEX idx_invoice_order ON public.t_invoice USING btree (f_order)`
- `idx_invoice_order_folio`: `CREATE UNIQUE INDEX idx_invoice_order_folio ON public.t_invoice USING btree (f_order, f_folio) WHERE (f_folio IS NOT NULL)`
- `idx_invoice_status`: `CREATE INDEX idx_invoice_status ON public.t_invoice USING btree (f_invoicestat)`
- `idx_order_client`: `CREATE INDEX idx_order_client ON public.t_order USING btree (f_client)`
- `idx_order_po`: `CREATE INDEX idx_order_po ON public.t_order USING btree (f_po)`
- `idx_order_podate`: `CREATE INDEX idx_order_podate ON public.t_order USING btree (f_podate)`
- `idx_order_status`: `CREATE INDEX idx_order_status ON public.t_order USING btree (f_orderstat)`
- `idx_order_status_podate`: `CREATE INDEX idx_order_status_podate ON public.t_order USING btree (f_orderstat, f_podate)`
- `idx_order_deleted_date`: `CREATE INDEX idx_order_deleted_date ON public.t_order_deleted USING btree (deleted_at)`
- `idx_order_deleted_original_id`: `CREATE INDEX idx_order_deleted_original_id ON public.t_order_deleted USING btree (original_order_id)`
- `idx_order_deleted_po`: `CREATE INDEX idx_order_deleted_po ON public.t_order_deleted USING btree (f_po)`
- `idx_overtime_year_month`: `CREATE INDEX idx_overtime_year_month ON public.t_overtime_hours USING btree (year, month)`
- `t_overtime_hours_year_month_key`: `CREATE UNIQUE INDEX t_overtime_hours_year_month_key ON public.t_overtime_hours USING btree (year, month)`
- `idx_overtime_audit_date`: `CREATE INDEX idx_overtime_audit_date ON public.t_overtime_hours_audit USING btree (changed_at)`
- `idx_payroll_active`: `CREATE INDEX idx_payroll_active ON public.t_payroll USING btree (is_active) WHERE (is_active = true)`
- `idx_payroll_history_active`: `CREATE INDEX idx_payroll_history_active ON public.t_payroll_history USING btree (f_payroll, is_active)`
- `idx_payroll_history_date`: `CREATE INDEX idx_payroll_history_date ON public.t_payroll_history USING btree (effective_date)`
- `idx_payroll_history_employee`: `CREATE INDEX idx_payroll_history_employee ON public.t_payroll_history USING btree (f_payroll, effective_date DESC)`
- `idx_vacation_dates`: `CREATE INDEX idx_vacation_dates ON public.t_vacation USING btree (start_date, end_date)`
- `idx_vacation_employee`: `CREATE INDEX idx_vacation_employee ON public.t_vacation USING btree (employee_id)`
- `idx_vacation_status`: `CREATE INDEX idx_vacation_status ON public.t_vacation USING btree (status)`
- `idx_vacation_audit_employee`: `CREATE INDEX idx_vacation_audit_employee ON public.t_vacation_audit USING btree (employee_id)`
- `idx_vacation_audit_id`: `CREATE INDEX idx_vacation_audit_id ON public.t_vacation_audit USING btree (vacation_id)`
- `idx_vendor_name`: `CREATE INDEX idx_vendor_name ON public.t_vendor USING btree (f_vendorname)`
- `idx_vendor_user`: `CREATE INDEX idx_vendor_user ON public.t_vendor USING btree (f_user_id)`
- `idx_commission_payment_order`: `CREATE INDEX idx_commission_payment_order ON public.t_vendor_commission_payment USING btree (f_order)`
- `idx_commission_payment_status`: `CREATE INDEX idx_commission_payment_status ON public.t_vendor_commission_payment USING btree (payment_status)`
- `idx_commission_payment_vendor`: `CREATE INDEX idx_commission_payment_vendor ON public.t_vendor_commission_payment USING btree (f_vendor)`
- `t_workday_config_day_of_week_key`: `CREATE UNIQUE INDEX t_workday_config_day_of_week_key ON public.t_workday_config USING btree (day_of_week)`
- `idx_users_active`: `CREATE INDEX idx_users_active ON public.users USING btree (is_active) WHERE (is_active = true)`
- `users_email_key`: `CREATE UNIQUE INDEX users_email_key ON public.users USING btree (email)`
- `users_username_key`: `CREATE UNIQUE INDEX users_username_key ON public.users USING btree (username)`
