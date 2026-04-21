# Documentación de Tablas - Base de Datos IMA Mecatrónica
Generado: 2026-04-20 23:20:39
PostgreSQL 17.4 | Supabase | 44 tablas

## Índice

1. [app_versions](#app_versions)
2. [audit_log](#audit_log)
3. [drive_activity](#drive_activity)
4. [drive_audit](#drive_audit)
5. [drive_files](#drive_files)
6. [drive_folders](#drive_folders)
7. [inventory_audit](#inventory_audit)
8. [inventory_categories](#inventory_categories)
9. [inventory_movements](#inventory_movements)
10. [inventory_products](#inventory_products)
11. [invoice_audit](#invoice_audit)
12. [invoice_status](#invoice_status)
13. [order_ejecutores](#order_ejecutores)
14. [order_files](#order_files)
15. [order_gastos_indirectos](#order_gastos_indirectos)
16. [order_gastos_operativos](#order_gastos_operativos)
17. [order_history](#order_history)
18. [order_status](#order_status)
19. [t_attendance](#t_attendance)
20. [t_attendance_audit](#t_attendance_audit)
21. [t_balance_adjustments](#t_balance_adjustments)
22. [t_client](#t_client)
23. [t_commission_rate_history](#t_commission_rate_history)
24. [t_contact](#t_contact)
25. [t_expense](#t_expense)
26. [t_expense_audit](#t_expense_audit)
27. [t_fixed_expenses](#t_fixed_expenses)
28. [t_fixed_expenses_history](#t_fixed_expenses_history)
29. [t_holiday](#t_holiday)
30. [t_invoice](#t_invoice)
31. [t_order](#t_order)
32. [t_order_deleted](#t_order_deleted)
33. [t_overtime_hours](#t_overtime_hours)
34. [t_overtime_hours_audit](#t_overtime_hours_audit)
35. [t_payroll](#t_payroll)
36. [t_payroll_history](#t_payroll_history)
37. [t_payrollovertime](#t_payrollovertime)
38. [t_supplier](#t_supplier)
39. [t_vacation](#t_vacation)
40. [t_vacation_audit](#t_vacation_audit)
41. [t_vendor](#t_vendor)
42. [t_vendor_commission_payment](#t_vendor_commission_payment)
43. [t_workday_config](#t_workday_config)
44. [users](#users)

---

## app_versions
Filas estimadas: ~24

### Columnas

| # | Columna | Tipo | Nullable | Default | PK | FK | Comentario |
|---|---------|------|----------|---------|----|----|------------|
| 1 | `id` | `integer` | NOT NULL | nextval('app_versions_id_seq'::regclass) | PK |  |  |
| 2 | `version` | `varchar(20)` | NOT NULL |  |  |  |  |
| 3 | `release_date` | `timestamp` | NOT NULL | now() |  |  |  |
| 4 | `is_latest` | `boolean` | NOT NULL | false |  |  |  |
| 5 | `is_mandatory` | `boolean` | NOT NULL | false |  |  |  |
| 6 | `download_url` | `text` | NOT NULL |  |  |  |  |
| 7 | `file_size_mb` | `numeric(10,2)` | NULL |  |  |  |  |
| 8 | `release_notes` | `text` | NULL |  |  |  |  |
| 9 | `min_version` | `varchar(20)` | NULL |  |  |  |  |
| 10 | `created_by` | `varchar(100)` | NULL |  |  |  |  |
| 11 | `is_active` | `boolean` | NOT NULL | true |  |  |  |
| 12 | `downloads_count` | `integer` | NULL | 0 |  |  |  |
| 13 | `changelog` | `jsonb` | NULL |  |  |  |  |

### Primary Key
- `app_versions_pkey` (id)

### Unique Constraints
- `app_versions_version_key` (version)

### Check Constraints
- `version_format`: `((version)::text ~ '^\d+\.\d+\.\d+$'::text)`

### Indexes
- `app_versions_pkey` [PRIMARY]: `CREATE UNIQUE INDEX app_versions_pkey ON public.app_versions USING btree (id)`
- `app_versions_version_key` [UNIQUE]: `CREATE UNIQUE INDEX app_versions_version_key ON public.app_versions USING btree (version)`
- `idx_app_versions_latest`: `CREATE INDEX idx_app_versions_latest ON public.app_versions USING btree (is_latest, is_active) WHERE (is_latest = true)`
- `idx_app_versions_release_date`: `CREATE INDEX idx_app_versions_release_date ON public.app_versions USING btree (release_date DESC)`

---

## audit_log
> Log de auditoría - nueva tabla para tracking
Filas estimadas: ~-1

### Columnas

| # | Columna | Tipo | Nullable | Default | PK | FK | Comentario |
|---|---------|------|----------|---------|----|----|------------|
| 1 | `id` | `integer` | NOT NULL | nextval('audit_log_id_seq'::regclass) | PK |  |  |
| 2 | `user_id` | `integer` | NULL |  |  | -> users.id |  |
| 3 | `table_name` | `varchar(100)` | NULL |  |  |  |  |
| 4 | `action` | `varchar(50)` | NULL |  |  |  |  |
| 5 | `record_id` | `integer` | NULL |  |  |  |  |
| 6 | `old_values` | `jsonb` | NULL |  |  |  |  |
| 7 | `new_values` | `jsonb` | NULL |  |  |  |  |
| 8 | `ip_address` | `varchar(45)` | NULL |  |  |  |  |
| 9 | `user_agent` | `text` | NULL |  |  |  |  |
| 10 | `created_at` | `timestamp` | NULL | CURRENT_TIMESTAMP |  |  |  |

### Primary Key
- `audit_log_pkey` (id)

### Foreign Keys
- `audit_log_user_id_fkey`: `user_id` -> `users.id` (ON UPDATE NO ACTION, ON DELETE NO ACTION)

### Indexes
- `audit_log_pkey` [PRIMARY]: `CREATE UNIQUE INDEX audit_log_pkey ON public.audit_log USING btree (id)`
- `idx_audit_table`: `CREATE INDEX idx_audit_table ON public.audit_log USING btree (table_name)`
- `idx_audit_user`: `CREATE INDEX idx_audit_user ON public.audit_log USING btree (user_id)`

---

## drive_activity
Filas estimadas: ~4,807

### Columnas

| # | Columna | Tipo | Nullable | Default | PK | FK | Comentario |
|---|---------|------|----------|---------|----|----|------------|
| 1 | `id` | `integer` | NOT NULL | nextval('drive_activity_id_seq'::regc... | PK |  |  |
| 2 | `user_id` | `integer` | NULL |  |  | -> users.id |  |
| 3 | `action` | `varchar(20)` | NOT NULL |  |  |  |  |
| 4 | `target_type` | `varchar(10)` | NOT NULL |  |  |  |  |
| 5 | `target_id` | `integer` | NOT NULL |  |  |  |  |
| 6 | `target_name` | `varchar(255)` | NULL |  |  |  |  |
| 7 | `folder_id` | `integer` | NULL |  |  | -> drive_folders.id |  |
| 8 | `metadata` | `jsonb` | NULL |  |  |  |  |
| 9 | `created_at` | `timestamp` | NULL | CURRENT_TIMESTAMP |  |  |  |

### Primary Key
- `drive_activity_pkey` (id)

### Foreign Keys
- `drive_activity_folder_id_fkey`: `folder_id` -> `drive_folders.id` (ON UPDATE NO ACTION, ON DELETE SET NULL)
- `drive_activity_user_id_fkey`: `user_id` -> `users.id` (ON UPDATE NO ACTION, ON DELETE NO ACTION)

### Indexes
- `drive_activity_pkey` [PRIMARY]: `CREATE UNIQUE INDEX drive_activity_pkey ON public.drive_activity USING btree (id)`
- `idx_drive_activity_folder`: `CREATE INDEX idx_drive_activity_folder ON public.drive_activity USING btree (folder_id, created_at DESC)`
- `idx_drive_activity_recent`: `CREATE INDEX idx_drive_activity_recent ON public.drive_activity USING btree (created_at DESC)`
- `idx_drive_activity_user`: `CREATE INDEX idx_drive_activity_user ON public.drive_activity USING btree (user_id, created_at DESC)`

---

## drive_audit
Filas estimadas: ~14,617

### Columnas

| # | Columna | Tipo | Nullable | Default | PK | FK | Comentario |
|---|---------|------|----------|---------|----|----|------------|
| 1 | `id` | `integer` | NOT NULL | nextval('drive_audit_id_seq'::regclass) | PK |  |  |
| 2 | `action` | `varchar(20)` | NOT NULL |  |  |  |  |
| 3 | `target_type` | `varchar(10)` | NOT NULL |  |  |  |  |
| 4 | `target_id` | `integer` | NOT NULL |  |  |  |  |
| 5 | `target_name` | `varchar(255)` | NULL |  |  |  |  |
| 6 | `folder_id` | `integer` | NULL |  |  |  |  |
| 7 | `old_value` | `text` | NULL |  |  |  |  |
| 8 | `new_value` | `text` | NULL |  |  |  |  |
| 9 | `user_id` | `integer` | NULL |  |  | -> users.id |  |
| 10 | `created_at` | `timestamp` | NULL | CURRENT_TIMESTAMP |  |  |  |

### Primary Key
- `drive_audit_pkey` (id)

### Foreign Keys
- `drive_audit_user_id_fkey`: `user_id` -> `users.id` (ON UPDATE NO ACTION, ON DELETE NO ACTION)

### Indexes
- `drive_audit_pkey` [PRIMARY]: `CREATE UNIQUE INDEX drive_audit_pkey ON public.drive_audit USING btree (id)`
- `idx_drive_audit_date`: `CREATE INDEX idx_drive_audit_date ON public.drive_audit USING btree (created_at DESC)`
- `idx_drive_audit_user`: `CREATE INDEX idx_drive_audit_user ON public.drive_audit USING btree (user_id)`

---

## drive_files
Filas estimadas: ~1,308

### Columnas

| # | Columna | Tipo | Nullable | Default | PK | FK | Comentario |
|---|---------|------|----------|---------|----|----|------------|
| 1 | `id` | `integer` | NOT NULL | nextval('drive_files_id_seq'::regclass) | PK |  |  |
| 2 | `folder_id` | `integer` | NOT NULL |  |  | -> drive_folders.id |  |
| 3 | `file_name` | `varchar(255)` | NOT NULL |  |  |  |  |
| 4 | `storage_path` | `text` | NOT NULL |  |  |  |  |
| 5 | `file_size` | `bigint` | NULL |  |  |  |  |
| 6 | `content_type` | `varchar(100)` | NULL |  |  |  |  |
| 7 | `uploaded_by` | `integer` | NULL |  |  | -> users.id |  |
| 8 | `uploaded_at` | `timestamp` | NULL | CURRENT_TIMESTAMP |  |  |  |

### Primary Key
- `drive_files_pkey` (id)

### Foreign Keys
- `drive_files_folder_id_fkey`: `folder_id` -> `drive_folders.id` (ON UPDATE NO ACTION, ON DELETE CASCADE)
- `drive_files_uploaded_by_fkey`: `uploaded_by` -> `users.id` (ON UPDATE NO ACTION, ON DELETE NO ACTION)

### Unique Constraints
- `drive_files_folder_id_file_name_key` (folder_id, file_name)

### Indexes
- `drive_files_folder_id_file_name_key` [UNIQUE]: `CREATE UNIQUE INDEX drive_files_folder_id_file_name_key ON public.drive_files USING btree (folder_id, file_name)`
- `drive_files_pkey` [PRIMARY]: `CREATE UNIQUE INDEX drive_files_pkey ON public.drive_files USING btree (id)`
- `idx_drive_files_folder`: `CREATE INDEX idx_drive_files_folder ON public.drive_files USING btree (folder_id)`

---

## drive_folders
Filas estimadas: ~159

### Columnas

| # | Columna | Tipo | Nullable | Default | PK | FK | Comentario |
|---|---------|------|----------|---------|----|----|------------|
| 1 | `id` | `integer` | NOT NULL | nextval('drive_folders_id_seq'::regcl... | PK |  |  |
| 2 | `parent_id` | `integer` | NULL |  |  | -> drive_folders.id |  |
| 3 | `name` | `varchar(255)` | NOT NULL |  |  |  |  |
| 4 | `linked_order_id` | `integer` | NULL |  |  | -> t_order.f_order |  |
| 5 | `created_by` | `integer` | NULL |  |  | -> users.id |  |
| 6 | `created_at` | `timestamp` | NULL | CURRENT_TIMESTAMP |  |  |  |
| 7 | `updated_at` | `timestamp` | NULL | CURRENT_TIMESTAMP |  |  |  |

### Primary Key
- `drive_folders_pkey` (id)

### Foreign Keys
- `drive_folders_created_by_fkey`: `created_by` -> `users.id` (ON UPDATE NO ACTION, ON DELETE NO ACTION)
- `drive_folders_linked_order_id_fkey`: `linked_order_id` -> `t_order.f_order` (ON UPDATE NO ACTION, ON DELETE SET NULL)
- `drive_folders_parent_id_fkey`: `parent_id` -> `drive_folders.id` (ON UPDATE NO ACTION, ON DELETE CASCADE)

### Unique Constraints
- `drive_folders_parent_id_name_key` (parent_id, name)

### Indexes
- `drive_folders_parent_id_name_key` [UNIQUE]: `CREATE UNIQUE INDEX drive_folders_parent_id_name_key ON public.drive_folders USING btree (parent_id, name)`
- `drive_folders_pkey` [PRIMARY]: `CREATE UNIQUE INDEX drive_folders_pkey ON public.drive_folders USING btree (id)`
- `idx_drive_folders_order`: `CREATE INDEX idx_drive_folders_order ON public.drive_folders USING btree (linked_order_id) WHERE (linked_order_id IS NOT NULL)`
- `idx_drive_folders_parent`: `CREATE INDEX idx_drive_folders_parent ON public.drive_folders USING btree (parent_id)`

---

## inventory_audit
Filas estimadas: ~68

### Columnas

| # | Columna | Tipo | Nullable | Default | PK | FK | Comentario |
|---|---------|------|----------|---------|----|----|------------|
| 1 | `id` | `integer` | NOT NULL | nextval('inventory_audit_id_seq'::reg... | PK |  |  |
| 2 | `table_name` | `varchar(50)` | NOT NULL |  |  |  |  |
| 3 | `record_id` | `integer` | NOT NULL |  |  |  |  |
| 4 | `action` | `varchar(10)` | NOT NULL |  |  |  |  |
| 5 | `old_values` | `jsonb` | NULL |  |  |  |  |
| 6 | `new_values` | `jsonb` | NULL |  |  |  |  |
| 7 | `user_id` | `integer` | NULL |  |  | -> users.id |  |
| 8 | `created_at` | `timestamp` | NULL | CURRENT_TIMESTAMP |  |  |  |

### Primary Key
- `inventory_audit_pkey` (id)

### Foreign Keys
- `inventory_audit_user_id_fkey`: `user_id` -> `users.id` (ON UPDATE NO ACTION, ON DELETE NO ACTION)

### Check Constraints
- `inventory_audit_action_check`: `((action)::text = ANY ((ARRAY['INSERT'::character varying, 'UPDATE'::character varying, 'DELETE'::character varying])::text[]))`

### Indexes
- `idx_inv_audit_date`: `CREATE INDEX idx_inv_audit_date ON public.inventory_audit USING btree (created_at DESC)`
- `idx_inv_audit_table`: `CREATE INDEX idx_inv_audit_table ON public.inventory_audit USING btree (table_name, record_id)`
- `inventory_audit_pkey` [PRIMARY]: `CREATE UNIQUE INDEX inventory_audit_pkey ON public.inventory_audit USING btree (id)`

---

## inventory_categories
Filas estimadas: ~-1

### Columnas

| # | Columna | Tipo | Nullable | Default | PK | FK | Comentario |
|---|---------|------|----------|---------|----|----|------------|
| 1 | `id` | `integer` | NOT NULL | nextval('inventory_categories_id_seq'... | PK |  |  |
| 2 | `name` | `varchar(100)` | NOT NULL |  |  |  |  |
| 3 | `description` | `text` | NULL |  |  |  |  |
| 4 | `color` | `varchar(7)` | NULL | '#3498DB'::character varying |  |  |  |
| 5 | `icon` | `varchar(50)` | NULL |  |  |  |  |
| 6 | `display_order` | `integer` | NULL | 0 |  |  |  |
| 7 | `is_active` | `boolean` | NULL | true |  |  |  |
| 8 | `created_by` | `integer` | NULL |  |  | -> users.id |  |
| 9 | `updated_by` | `integer` | NULL |  |  | -> users.id |  |
| 10 | `created_at` | `timestamp` | NULL | CURRENT_TIMESTAMP |  |  |  |
| 11 | `updated_at` | `timestamp` | NULL | CURRENT_TIMESTAMP |  |  |  |

### Primary Key
- `inventory_categories_pkey` (id)

### Foreign Keys
- `inventory_categories_created_by_fkey`: `created_by` -> `users.id` (ON UPDATE NO ACTION, ON DELETE NO ACTION)
- `inventory_categories_updated_by_fkey`: `updated_by` -> `users.id` (ON UPDATE NO ACTION, ON DELETE NO ACTION)

### Indexes
- `idx_inv_categories_active`: `CREATE INDEX idx_inv_categories_active ON public.inventory_categories USING btree (is_active, display_order)`
- `inventory_categories_pkey` [PRIMARY]: `CREATE UNIQUE INDEX inventory_categories_pkey ON public.inventory_categories USING btree (id)`

---

## inventory_movements
Filas estimadas: ~-1

### Columnas

| # | Columna | Tipo | Nullable | Default | PK | FK | Comentario |
|---|---------|------|----------|---------|----|----|------------|
| 1 | `id` | `integer` | NOT NULL | nextval('inventory_movements_id_seq':... | PK |  |  |
| 2 | `product_id` | `integer` | NOT NULL |  |  | -> inventory_products.id |  |
| 3 | `movement_type` | `varchar(20)` | NOT NULL |  |  |  |  |
| 4 | `quantity` | `numeric(10,2)` | NOT NULL |  |  |  |  |
| 5 | `previous_stock` | `numeric(10,2)` | NULL |  |  |  |  |
| 6 | `new_stock` | `numeric(10,2)` | NULL |  |  |  |  |
| 7 | `reference_type` | `varchar(50)` | NULL |  |  |  |  |
| 8 | `reference_id` | `integer` | NULL |  |  |  |  |
| 9 | `notes` | `text` | NULL |  |  |  |  |
| 10 | `created_by` | `integer` | NULL |  |  | -> users.id |  |
| 11 | `created_at` | `timestamp` | NULL | CURRENT_TIMESTAMP |  |  |  |

### Primary Key
- `inventory_movements_pkey` (id)

### Foreign Keys
- `inventory_movements_created_by_fkey`: `created_by` -> `users.id` (ON UPDATE NO ACTION, ON DELETE NO ACTION)
- `inventory_movements_product_id_fkey`: `product_id` -> `inventory_products.id` (ON UPDATE NO ACTION, ON DELETE CASCADE)

### Check Constraints
- `inventory_movements_movement_type_check`: `((movement_type)::text = ANY ((ARRAY['entrada'::character varying, 'salida'::character varying, 'ajuste'::character varying])::text[]))`

### Indexes
- `idx_inv_movements_date`: `CREATE INDEX idx_inv_movements_date ON public.inventory_movements USING btree (created_at DESC)`
- `idx_inv_movements_product`: `CREATE INDEX idx_inv_movements_product ON public.inventory_movements USING btree (product_id, created_at DESC)`
- `idx_inv_movements_type`: `CREATE INDEX idx_inv_movements_type ON public.inventory_movements USING btree (movement_type, created_at DESC)`
- `inventory_movements_pkey` [PRIMARY]: `CREATE UNIQUE INDEX inventory_movements_pkey ON public.inventory_movements USING btree (id)`

---

## inventory_products
Filas estimadas: ~60

### Columnas

| # | Columna | Tipo | Nullable | Default | PK | FK | Comentario |
|---|---------|------|----------|---------|----|----|------------|
| 1 | `id` | `integer` | NOT NULL | nextval('inventory_products_id_seq'::... | PK |  |  |
| 2 | `category_id` | `integer` | NOT NULL |  |  | -> inventory_categories.id |  |
| 3 | `code` | `varchar(50)` | NULL |  |  |  |  |
| 4 | `name` | `varchar(255)` | NOT NULL |  |  |  |  |
| 5 | `description` | `text` | NULL |  |  |  |  |
| 6 | `stock_current` | `numeric(10,2)` | NULL | 0 |  |  |  |
| 7 | `stock_minimum` | `numeric(10,2)` | NULL | 0 |  |  |  |
| 8 | `unit` | `varchar(20)` | NULL | 'pza'::character varying |  |  |  |
| 9 | `unit_price` | `numeric(12,2)` | NULL | 0 |  |  |  |
| 10 | `location` | `varchar(100)` | NULL |  |  |  |  |
| 11 | `supplier_id` | `integer` | NULL |  |  | -> t_supplier.f_supplier |  |
| 12 | `notes` | `text` | NULL |  |  |  |  |
| 13 | `is_active` | `boolean` | NULL | true |  |  |  |
| 14 | `created_by` | `integer` | NULL |  |  | -> users.id |  |
| 15 | `updated_by` | `integer` | NULL |  |  | -> users.id |  |
| 16 | `created_at` | `timestamp` | NULL | CURRENT_TIMESTAMP |  |  |  |
| 17 | `updated_at` | `timestamp` | NULL | CURRENT_TIMESTAMP |  |  |  |

### Primary Key
- `inventory_products_pkey` (id)

### Foreign Keys
- `inventory_products_category_id_fkey`: `category_id` -> `inventory_categories.id` (ON UPDATE NO ACTION, ON DELETE CASCADE)
- `inventory_products_created_by_fkey`: `created_by` -> `users.id` (ON UPDATE NO ACTION, ON DELETE NO ACTION)
- `inventory_products_supplier_id_fkey`: `supplier_id` -> `t_supplier.f_supplier` (ON UPDATE NO ACTION, ON DELETE NO ACTION)
- `inventory_products_updated_by_fkey`: `updated_by` -> `users.id` (ON UPDATE NO ACTION, ON DELETE NO ACTION)

### Unique Constraints
- `inventory_products_code_key` (code)

### Indexes
- `idx_inv_products_category`: `CREATE INDEX idx_inv_products_category ON public.inventory_products USING btree (category_id) WHERE (is_active = true)`
- `idx_inv_products_code`: `CREATE INDEX idx_inv_products_code ON public.inventory_products USING btree (code)`
- `idx_inv_products_location`: `CREATE INDEX idx_inv_products_location ON public.inventory_products USING btree (location) WHERE (is_active = true)`
- `idx_inv_products_low_stock`: `CREATE INDEX idx_inv_products_low_stock ON public.inventory_products USING btree (category_id) WHERE ((stock_current < stock_minimum) AND (is_active = true))`
- `idx_inv_products_supplier`: `CREATE INDEX idx_inv_products_supplier ON public.inventory_products USING btree (supplier_id) WHERE (supplier_id IS NOT NULL)`
- `inventory_products_code_key` [UNIQUE]: `CREATE UNIQUE INDEX inventory_products_code_key ON public.inventory_products USING btree (code)`
- `inventory_products_pkey` [PRIMARY]: `CREATE UNIQUE INDEX inventory_products_pkey ON public.inventory_products USING btree (id)`

---

## invoice_audit
Filas estimadas: ~-1

### Columnas

| # | Columna | Tipo | Nullable | Default | PK | FK | Comentario |
|---|---------|------|----------|---------|----|----|------------|
| 1 | `id` | `integer` | NOT NULL | nextval('invoice_audit_id_seq'::regcl... | PK |  |  |
| 2 | `invoice_id` | `integer` | NULL |  |  |  |  |
| 3 | `action` | `varchar(20)` | NULL |  |  |  |  |
| 4 | `old_values` | `jsonb` | NULL |  |  |  |  |
| 5 | `new_values` | `jsonb` | NULL |  |  |  |  |
| 6 | `user_id` | `integer` | NULL |  |  | -> users.id |  |
| 7 | `created_at` | `timestamp` | NULL | CURRENT_TIMESTAMP |  |  |  |

### Primary Key
- `invoice_audit_pkey` (id)

### Foreign Keys
- `invoice_audit_user_id_fkey`: `user_id` -> `users.id` (ON UPDATE NO ACTION, ON DELETE NO ACTION)

### Indexes
- `invoice_audit_pkey` [PRIMARY]: `CREATE UNIQUE INDEX invoice_audit_pkey ON public.invoice_audit USING btree (id)`

---

## invoice_status
Filas estimadas: ~-1

### Columnas

| # | Columna | Tipo | Nullable | Default | PK | FK | Comentario |
|---|---------|------|----------|---------|----|----|------------|
| 1 | `f_invoicestat` | `integer` | NOT NULL | nextval('invoice_status_f_invoicestat... | PK |  |  |
| 2 | `f_name` | `varchar(255)` | NOT NULL |  |  |  |  |
| 3 | `is_active` | `boolean` | NULL | true |  |  |  |
| 4 | `display_order` | `integer` | NULL | 0 |  |  |  |
| 5 | `created_at` | `timestamp` | NULL | CURRENT_TIMESTAMP |  |  |  |

### Primary Key
- `invoice_status_pkey` (f_invoicestat)

### Indexes
- `invoice_status_pkey` [PRIMARY]: `CREATE UNIQUE INDEX invoice_status_pkey ON public.invoice_status USING btree (f_invoicestat)`

---

## order_ejecutores
Filas estimadas: ~-1

### Columnas

| # | Columna | Tipo | Nullable | Default | PK | FK | Comentario |
|---|---------|------|----------|---------|----|----|------------|
| 1 | `id` | `integer` | NOT NULL | nextval('order_ejecutores_id_seq'::re... | PK |  |  |
| 2 | `f_order` | `integer` | NOT NULL |  |  | -> t_order.f_order |  |
| 3 | `payroll_id` | `integer` | NOT NULL |  |  | -> t_payroll.f_payroll |  |
| 4 | `assigned_at` | `timestamp` | NULL | CURRENT_TIMESTAMP |  |  |  |
| 5 | `assigned_by` | `integer` | NULL |  |  | -> users.id |  |

### Primary Key
- `order_ejecutores_pkey` (id)

### Foreign Keys
- `order_ejecutores_assigned_by_fkey`: `assigned_by` -> `users.id` (ON UPDATE NO ACTION, ON DELETE NO ACTION)
- `order_ejecutores_f_order_fkey`: `f_order` -> `t_order.f_order` (ON UPDATE NO ACTION, ON DELETE CASCADE)
- `order_ejecutores_payroll_id_fkey`: `payroll_id` -> `t_payroll.f_payroll` (ON UPDATE NO ACTION, ON DELETE CASCADE)

### Unique Constraints
- `order_ejecutores_f_order_payroll_id_key` (f_order, payroll_id)

### Indexes
- `idx_order_ejecutores_order`: `CREATE INDEX idx_order_ejecutores_order ON public.order_ejecutores USING btree (f_order)`
- `idx_order_ejecutores_payroll`: `CREATE INDEX idx_order_ejecutores_payroll ON public.order_ejecutores USING btree (payroll_id)`
- `order_ejecutores_f_order_payroll_id_key` [UNIQUE]: `CREATE UNIQUE INDEX order_ejecutores_f_order_payroll_id_key ON public.order_ejecutores USING btree (f_order, payroll_id)`
- `order_ejecutores_pkey` [PRIMARY]: `CREATE UNIQUE INDEX order_ejecutores_pkey ON public.order_ejecutores USING btree (id)`

---

## order_files
Filas estimadas: ~1

### Columnas

| # | Columna | Tipo | Nullable | Default | PK | FK | Comentario |
|---|---------|------|----------|---------|----|----|------------|
| 1 | `id` | `integer` | NOT NULL | nextval('order_files_id_seq'::regclass) | PK |  |  |
| 2 | `f_order` | `integer` | NOT NULL |  |  | -> t_order.f_order |  |
| 3 | `file_name` | `varchar(255)` | NOT NULL |  |  |  |  |
| 4 | `storage_path` | `text` | NOT NULL |  |  |  |  |
| 5 | `file_size` | `bigint` | NULL |  |  |  |  |
| 6 | `content_type` | `varchar(100)` | NULL |  |  |  |  |
| 7 | `uploaded_by` | `integer` | NULL |  |  | -> users.id |  |
| 8 | `vendor_id` | `integer` | NULL |  |  | -> t_vendor.f_vendor |  |
| 9 | `commission_id` | `integer` | NULL |  |  | -> t_vendor_commission_payment.id |  |
| 10 | `created_at` | `timestamp` | NULL | CURRENT_TIMESTAMP |  |  |  |

### Primary Key
- `order_files_pkey` (id)

### Foreign Keys
- `order_files_commission_id_fkey`: `commission_id` -> `t_vendor_commission_payment.id` (ON UPDATE NO ACTION, ON DELETE NO ACTION)
- `order_files_f_order_fkey`: `f_order` -> `t_order.f_order` (ON UPDATE NO ACTION, ON DELETE CASCADE)
- `order_files_uploaded_by_fkey`: `uploaded_by` -> `users.id` (ON UPDATE NO ACTION, ON DELETE NO ACTION)
- `order_files_vendor_id_fkey`: `vendor_id` -> `t_vendor.f_vendor` (ON UPDATE NO ACTION, ON DELETE NO ACTION)

### Indexes
- `idx_order_files_commission`: `CREATE INDEX idx_order_files_commission ON public.order_files USING btree (commission_id)`
- `idx_order_files_order`: `CREATE INDEX idx_order_files_order ON public.order_files USING btree (f_order)`
- `idx_order_files_vendor`: `CREATE INDEX idx_order_files_vendor ON public.order_files USING btree (vendor_id)`
- `order_files_pkey` [PRIMARY]: `CREATE UNIQUE INDEX order_files_pkey ON public.order_files USING btree (id)`

---

## order_gastos_indirectos
Filas estimadas: ~-1

### Columnas

| # | Columna | Tipo | Nullable | Default | PK | FK | Comentario |
|---|---------|------|----------|---------|----|----|------------|
| 1 | `id` | `integer` | NOT NULL | nextval('order_gastos_indirectos_id_s... | PK |  |  |
| 2 | `f_order` | `integer` | NOT NULL |  |  | -> t_order.f_order |  |
| 3 | `monto` | `numeric(15,2)` | NOT NULL |  |  |  |  |
| 4 | `descripcion` | `varchar(255)` | NOT NULL |  |  |  |  |
| 5 | `fecha_gasto` | `timestamp` | NULL | CURRENT_TIMESTAMP |  |  |  |
| 6 | `created_at` | `timestamp` | NULL | CURRENT_TIMESTAMP |  |  |  |
| 7 | `created_by` | `integer` | NULL |  |  |  |  |
| 8 | `updated_at` | `timestamp` | NULL |  |  |  |  |
| 9 | `updated_by` | `integer` | NULL |  |  |  |  |

### Primary Key
- `order_gastos_indirectos_pkey` (id)

### Foreign Keys
- `order_gastos_indirectos_f_order_fkey`: `f_order` -> `t_order.f_order` (ON UPDATE NO ACTION, ON DELETE CASCADE)

### Indexes
- `idx_gastos_indirectos_order`: `CREATE INDEX idx_gastos_indirectos_order ON public.order_gastos_indirectos USING btree (f_order)`
- `order_gastos_indirectos_pkey` [PRIMARY]: `CREATE UNIQUE INDEX order_gastos_indirectos_pkey ON public.order_gastos_indirectos USING btree (id)`

---

## order_gastos_operativos
Filas estimadas: ~12

### Columnas

| # | Columna | Tipo | Nullable | Default | PK | FK | Comentario |
|---|---------|------|----------|---------|----|----|------------|
| 1 | `id` | `integer` | NOT NULL | nextval('order_gastos_operativos_id_s... | PK |  |  |
| 2 | `f_order` | `integer` | NOT NULL |  |  | -> t_order.f_order |  |
| 3 | `monto` | `numeric(15,2)` | NOT NULL | 0 |  |  |  |
| 4 | `descripcion` | `varchar(255)` | NOT NULL |  |  |  |  |
| 5 | `categoria` | `varchar(50)` | NULL |  |  |  |  |
| 6 | `fecha_gasto` | `timestamp` | NULL | CURRENT_TIMESTAMP |  |  |  |
| 7 | `created_at` | `timestamp` | NULL | CURRENT_TIMESTAMP |  |  |  |
| 8 | `created_by` | `integer` | NULL |  |  |  |  |
| 9 | `updated_at` | `timestamp` | NULL | CURRENT_TIMESTAMP |  |  |  |
| 10 | `updated_by` | `integer` | NULL |  |  |  |  |

### Primary Key
- `order_gastos_operativos_pkey` (id)

### Foreign Keys
- `order_gastos_operativos_f_order_fkey`: `f_order` -> `t_order.f_order` (ON UPDATE NO ACTION, ON DELETE CASCADE)

### Indexes
- `idx_gastos_operativos_order`: `CREATE INDEX idx_gastos_operativos_order ON public.order_gastos_operativos USING btree (f_order)`
- `order_gastos_operativos_pkey` [PRIMARY]: `CREATE UNIQUE INDEX order_gastos_operativos_pkey ON public.order_gastos_operativos USING btree (id)`

---

## order_history
> Histórico de cambios en las órdenes de compra
Filas estimadas: ~1,358

### Columnas

| # | Columna | Tipo | Nullable | Default | PK | FK | Comentario |
|---|---------|------|----------|---------|----|----|------------|
| 1 | `id` | `integer` | NOT NULL | nextval('order_history_id_seq'::regcl... | PK |  |  |
| 2 | `order_id` | `integer` | NOT NULL |  |  | -> t_order.f_order |  |
| 3 | `user_id` | `integer` | NOT NULL |  |  | -> users.id |  |
| 4 | `action` | `varchar(50)` | NOT NULL |  |  |  | Tipo de acción: CREATE, UPDATE, DELETE |
| 5 | `field_name` | `varchar(100)` | NULL |  |  |  | Nombre del campo modificado (NULL para CREATE/DELETE) |
| 6 | `old_value` | `text` | NULL |  |  |  |  |
| 7 | `new_value` | `text` | NULL |  |  |  |  |
| 8 | `change_description` | `text` | NULL |  |  |  |  |
| 9 | `ip_address` | `varchar(45)` | NULL |  |  |  |  |
| 10 | `changed_at` | `timestamp` | NULL | CURRENT_TIMESTAMP |  |  |  |

### Primary Key
- `order_history_pkey` (id)

### Foreign Keys
- `order_history_order_id_fkey`: `order_id` -> `t_order.f_order` (ON UPDATE NO ACTION, ON DELETE CASCADE)
- `order_history_user_id_fkey`: `user_id` -> `users.id` (ON UPDATE NO ACTION, ON DELETE NO ACTION)

### Indexes
- `idx_order_history_date`: `CREATE INDEX idx_order_history_date ON public.order_history USING btree (changed_at)`
- `idx_order_history_order`: `CREATE INDEX idx_order_history_order ON public.order_history USING btree (order_id)`
- `idx_order_history_user`: `CREATE INDEX idx_order_history_user ON public.order_history USING btree (user_id)`
- `order_history_pkey` [PRIMARY]: `CREATE UNIQUE INDEX order_history_pkey ON public.order_history USING btree (id)`

---

## order_status
Filas estimadas: ~-1

### Columnas

| # | Columna | Tipo | Nullable | Default | PK | FK | Comentario |
|---|---------|------|----------|---------|----|----|------------|
| 1 | `f_orderstatus` | `integer` | NOT NULL | nextval('order_status_f_orderstatus_s... | PK |  |  |
| 2 | `f_name` | `varchar(255)` | NOT NULL |  |  |  |  |
| 3 | `is_active` | `boolean` | NULL | true |  |  |  |
| 4 | `display_order` | `integer` | NULL | 0 |  |  |  |
| 5 | `created_at` | `timestamp` | NULL | CURRENT_TIMESTAMP |  |  |  |

### Primary Key
- `order_status_pkey` (f_orderstatus)

### Indexes
- `order_status_pkey` [PRIMARY]: `CREATE UNIQUE INDEX order_status_pkey ON public.order_status USING btree (f_orderstatus)`

---

## t_attendance
> Registro diario de asistencia del personal
Filas estimadas: ~539

### Columnas

| # | Columna | Tipo | Nullable | Default | PK | FK | Comentario |
|---|---------|------|----------|---------|----|----|------------|
| 1 | `id` | `integer` | NOT NULL | nextval('t_attendance_id_seq'::regclass) | PK |  |  |
| 2 | `employee_id` | `integer` | NOT NULL |  |  | -> t_payroll.f_payroll |  |
| 3 | `attendance_date` | `date` | NOT NULL |  |  |  |  |
| 4 | `status` | `varchar(20)` | NOT NULL |  |  |  | Estado: ASISTENCIA, RETARDO, FALTA, VACACIONES, FERIADO, DESCANSO |
| 5 | `check_in_time` | `time without time zone` | NULL |  |  |  |  |
| 6 | `check_out_time` | `time without time zone` | NULL |  |  |  |  |
| 7 | `late_minutes` | `integer` | NULL | 0 |  |  | Minutos de retardo (solo aplica si status=RETARDO) |
| 8 | `notes` | `text` | NULL |  |  |  |  |
| 9 | `is_justified` | `boolean` | NULL | false |  |  | Indica si una falta o retardo está justificado |
| 10 | `justification` | `text` | NULL |  |  |  |  |
| 11 | `created_by` | `integer` | NULL |  |  |  |  |
| 12 | `created_at` | `timestamp` | NULL | CURRENT_TIMESTAMP |  |  |  |
| 13 | `updated_by` | `integer` | NULL |  |  |  |  |
| 14 | `updated_at` | `timestamp` | NULL |  |  |  |  |

### Primary Key
- `t_attendance_pkey` (id)

### Foreign Keys
- `t_attendance_employee_id_fkey`: `employee_id` -> `t_payroll.f_payroll` (ON UPDATE NO ACTION, ON DELETE CASCADE)

### Unique Constraints
- `t_attendance_employee_id_attendance_date_key` (employee_id, attendance_date)

### Check Constraints
- `t_attendance_status_check`: `((status)::text = ANY ((ARRAY['ASISTENCIA'::character varying, 'RETARDO'::character varying, 'FALTA'::character varying, 'VACACIONES'::character varying, 'FERIADO'::character varying, 'DESCANSO'::character varying])::text[]))`

### Indexes
- `idx_attendance_date`: `CREATE INDEX idx_attendance_date ON public.t_attendance USING btree (attendance_date)`
- `idx_attendance_employee`: `CREATE INDEX idx_attendance_employee ON public.t_attendance USING btree (employee_id)`
- `idx_attendance_status`: `CREATE INDEX idx_attendance_status ON public.t_attendance USING btree (status)`
- `t_attendance_employee_id_attendance_date_key` [UNIQUE]: `CREATE UNIQUE INDEX t_attendance_employee_id_attendance_date_key ON public.t_attendance USING btree (employee_id, attendance_date)`
- `t_attendance_pkey` [PRIMARY]: `CREATE UNIQUE INDEX t_attendance_pkey ON public.t_attendance USING btree (id)`

---

## t_attendance_audit
> Historial de cambios en registros de asistencia
Filas estimadas: ~619

### Columnas

| # | Columna | Tipo | Nullable | Default | PK | FK | Comentario |
|---|---------|------|----------|---------|----|----|------------|
| 1 | `id` | `integer` | NOT NULL | nextval('t_attendance_audit_id_seq'::... | PK |  |  |
| 2 | `attendance_id` | `integer` | NOT NULL |  |  |  |  |
| 3 | `employee_id` | `integer` | NOT NULL |  |  |  |  |
| 4 | `attendance_date` | `date` | NOT NULL |  |  |  |  |
| 5 | `action` | `varchar(10)` | NOT NULL |  |  |  | Tipo de operación: INSERT, UPDATE, DELETE |
| 6 | `old_status` | `varchar(20)` | NULL |  |  |  |  |
| 7 | `old_check_in_time` | `time without time zone` | NULL |  |  |  |  |
| 8 | `old_check_out_time` | `time without time zone` | NULL |  |  |  |  |
| 9 | `old_late_minutes` | `integer` | NULL |  |  |  |  |
| 10 | `old_notes` | `text` | NULL |  |  |  |  |
| 11 | `old_is_justified` | `boolean` | NULL |  |  |  |  |
| 12 | `new_status` | `varchar(20)` | NULL |  |  |  |  |
| 13 | `new_check_in_time` | `time without time zone` | NULL |  |  |  |  |
| 14 | `new_check_out_time` | `time without time zone` | NULL |  |  |  |  |
| 15 | `new_late_minutes` | `integer` | NULL |  |  |  |  |
| 16 | `new_notes` | `text` | NULL |  |  |  |  |
| 17 | `new_is_justified` | `boolean` | NULL |  |  |  |  |
| 18 | `changed_by` | `integer` | NULL |  |  |  | ID del usuario que realizó el cambio |
| 19 | `changed_at` | `timestamp` | NULL | CURRENT_TIMESTAMP |  |  |  |
| 20 | `ip_address` | `inet` | NULL |  |  |  |  |
| 21 | `user_agent` | `text` | NULL |  |  |  |  |
| 22 | `change_reason` | `text` | NULL |  |  |  |  |

### Primary Key
- `t_attendance_audit_pkey` (id)

### Check Constraints
- `t_attendance_audit_action_check`: `((action)::text = ANY ((ARRAY['INSERT'::character varying, 'UPDATE'::character varying, 'DELETE'::character varying])::text[]))`

### Indexes
- `idx_audit_action`: `CREATE INDEX idx_audit_action ON public.t_attendance_audit USING btree (action)`
- `idx_audit_attendance_id`: `CREATE INDEX idx_audit_attendance_id ON public.t_attendance_audit USING btree (attendance_id)`
- `idx_audit_changed_at`: `CREATE INDEX idx_audit_changed_at ON public.t_attendance_audit USING btree (changed_at)`
- `idx_audit_date`: `CREATE INDEX idx_audit_date ON public.t_attendance_audit USING btree (attendance_date)`
- `idx_audit_employee_id`: `CREATE INDEX idx_audit_employee_id ON public.t_attendance_audit USING btree (employee_id)`
- `t_attendance_audit_pkey` [PRIMARY]: `CREATE UNIQUE INDEX t_attendance_audit_pkey ON public.t_attendance_audit USING btree (id)`

---

## t_balance_adjustments
Filas estimadas: ~-1

### Columnas

| # | Columna | Tipo | Nullable | Default | PK | FK | Comentario |
|---|---------|------|----------|---------|----|----|------------|
| 1 | `id` | `integer` | NOT NULL | nextval('t_balance_adjustments_id_seq... | PK |  |  |
| 2 | `year` | `integer` | NOT NULL |  |  |  |  |
| 3 | `month` | `integer` | NOT NULL |  |  |  |  |
| 4 | `adjustment_type` | `varchar(50)` | NULL |  |  |  |  |
| 5 | `original_amount` | `numeric(15,2)` | NULL |  |  |  |  |
| 6 | `adjusted_amount` | `numeric(15,2)` | NULL |  |  |  |  |
| 7 | `difference` | `numeric(15,2)` | NULL |  |  |  |  |
| 8 | `reason` | `text` | NULL |  |  |  |  |
| 9 | `created_by` | `integer` | NULL |  |  | -> users.id |  |
| 10 | `created_at` | `timestamp` | NULL | CURRENT_TIMESTAMP |  |  |  |

### Primary Key
- `t_balance_adjustments_pkey` (id)

### Foreign Keys
- `t_balance_adjustments_created_by_fkey`: `created_by` -> `users.id` (ON UPDATE NO ACTION, ON DELETE NO ACTION)

### Unique Constraints
- `t_balance_adjustments_year_month_adjustment_type_key` (year, month, adjustment_type)

### Indexes
- `t_balance_adjustments_pkey` [PRIMARY]: `CREATE UNIQUE INDEX t_balance_adjustments_pkey ON public.t_balance_adjustments USING btree (id)`
- `t_balance_adjustments_year_month_adjustment_type_key` [UNIQUE]: `CREATE UNIQUE INDEX t_balance_adjustments_year_month_adjustment_type_key ON public.t_balance_adjustments USING btree (year, month, adjustment_type)`

---

## t_client
> Tabla de clientes - migrada de Access T_CLIENT
Filas estimadas: ~33

### Columnas

| # | Columna | Tipo | Nullable | Default | PK | FK | Comentario |
|---|---------|------|----------|---------|----|----|------------|
| 1 | `f_client` | `integer` | NOT NULL | nextval('t_client_f_client_seq'::regc... | PK |  |  |
| 2 | `f_name` | `varchar(255)` | NOT NULL |  |  |  |  |
| 3 | `f_address1` | `varchar(255)` | NULL |  |  |  |  |
| 4 | `f_address2` | `varchar(255)` | NULL |  |  |  |  |
| 5 | `f_credit` | `integer` | NULL | 0 |  |  |  |
| 6 | `tax_id` | `varchar(50)` | NULL |  |  |  |  |
| 7 | `phone` | `varchar(50)` | NULL |  |  |  |  |
| 8 | `email` | `varchar(255)` | NULL |  |  |  |  |
| 9 | `is_active` | `boolean` | NULL | true |  |  |  |
| 10 | `created_at` | `timestamp` | NULL | CURRENT_TIMESTAMP |  |  |  |
| 11 | `updated_at` | `timestamp` | NULL | CURRENT_TIMESTAMP |  |  |  |
| 12 | `created_by` | `integer` | NULL |  |  | -> users.id |  |
| 13 | `updated_by` | `integer` | NULL |  |  | -> users.id |  |

### Primary Key
- `t_client_pkey` (f_client)

### Foreign Keys
- `t_client_created_by_fkey`: `created_by` -> `users.id` (ON UPDATE NO ACTION, ON DELETE NO ACTION)
- `t_client_updated_by_fkey`: `updated_by` -> `users.id` (ON UPDATE NO ACTION, ON DELETE NO ACTION)

### Indexes
- `idx_client_name`: `CREATE INDEX idx_client_name ON public.t_client USING btree (f_name)`
- `t_client_pkey` [PRIMARY]: `CREATE UNIQUE INDEX t_client_pkey ON public.t_client USING btree (f_client)`

---

## t_commission_rate_history
Filas estimadas: ~9

### Columnas

| # | Columna | Tipo | Nullable | Default | PK | FK | Comentario |
|---|---------|------|----------|---------|----|----|------------|
| 1 | `id` | `integer` | NOT NULL | nextval('t_commission_rate_history_id... | PK |  |  |
| 2 | `order_id` | `integer` | NOT NULL |  |  | -> t_order.f_order |  |
| 3 | `vendor_id` | `integer` | NOT NULL |  |  | -> t_vendor.f_vendor |  |
| 4 | `commission_payment_id` | `integer` | NULL |  |  | -> t_vendor_commission_payment.id |  |
| 5 | `old_rate` | `numeric(5,2)` | NOT NULL |  |  |  |  |
| 6 | `old_amount` | `numeric(12,2)` | NOT NULL |  |  |  |  |
| 7 | `new_rate` | `numeric(5,2)` | NOT NULL |  |  |  |  |
| 8 | `new_amount` | `numeric(12,2)` | NOT NULL |  |  |  |  |
| 9 | `order_subtotal` | `numeric(12,2)` | NULL |  |  |  |  |
| 10 | `order_number` | `varchar(100)` | NULL |  |  |  |  |
| 11 | `vendor_name` | `varchar(200)` | NULL |  |  |  |  |
| 12 | `changed_by` | `integer` | NOT NULL |  |  |  |  |
| 13 | `changed_by_name` | `varchar(200)` | NULL |  |  |  |  |
| 14 | `changed_at` | `timestamp` | NULL | now() |  |  |  |
| 15 | `change_reason` | `text` | NULL |  |  |  |  |
| 16 | `ip_address` | `varchar(45)` | NULL |  |  |  |  |
| 17 | `is_vendor_removal` | `boolean` | NULL | false |  |  |  |

### Primary Key
- `t_commission_rate_history_pkey` (id)

### Foreign Keys
- `t_commission_rate_history_commission_payment_id_fkey`: `commission_payment_id` -> `t_vendor_commission_payment.id` (ON UPDATE NO ACTION, ON DELETE SET NULL)
- `t_commission_rate_history_order_id_fkey`: `order_id` -> `t_order.f_order` (ON UPDATE NO ACTION, ON DELETE NO ACTION)
- `t_commission_rate_history_vendor_id_fkey`: `vendor_id` -> `t_vendor.f_vendor` (ON UPDATE NO ACTION, ON DELETE NO ACTION)

### Indexes
- `idx_commission_history_date`: `CREATE INDEX idx_commission_history_date ON public.t_commission_rate_history USING btree (changed_at)`
- `idx_commission_history_order`: `CREATE INDEX idx_commission_history_order ON public.t_commission_rate_history USING btree (order_id)`
- `idx_commission_history_vendor`: `CREATE INDEX idx_commission_history_vendor ON public.t_commission_rate_history USING btree (vendor_id)`
- `t_commission_rate_history_pkey` [PRIMARY]: `CREATE UNIQUE INDEX t_commission_rate_history_pkey ON public.t_commission_rate_history USING btree (id)`

---

## t_contact
Filas estimadas: ~68

### Columnas

| # | Columna | Tipo | Nullable | Default | PK | FK | Comentario |
|---|---------|------|----------|---------|----|----|------------|
| 1 | `f_contact` | `integer` | NOT NULL | nextval('t_contact_f_contact_seq'::re... | PK |  |  |
| 2 | `f_client` | `integer` | NULL |  |  | -> t_client.f_client |  |
| 3 | `f_contactname` | `varchar(255)` | NULL |  |  |  |  |
| 4 | `f_email` | `varchar(255)` | NULL |  |  |  |  |
| 5 | `f_phone` | `varchar(255)` | NULL |  |  |  |  |
| 6 | `position` | `varchar(100)` | NULL |  |  |  |  |
| 7 | `is_primary` | `boolean` | NULL | false |  |  |  |
| 8 | `is_active` | `boolean` | NULL | true |  |  |  |
| 9 | `created_at` | `timestamp` | NULL | CURRENT_TIMESTAMP |  |  |  |
| 10 | `updated_at` | `timestamp` | NULL | CURRENT_TIMESTAMP |  |  |  |

### Primary Key
- `t_contact_pkey` (f_contact)

### Foreign Keys
- `t_contact_f_client_fkey`: `f_client` -> `t_client.f_client` (ON UPDATE NO ACTION, ON DELETE CASCADE)

### Indexes
- `idx_contact_client`: `CREATE INDEX idx_contact_client ON public.t_contact USING btree (f_client)`
- `idx_contact_client_active`: `CREATE INDEX idx_contact_client_active ON public.t_contact USING btree (f_client) WHERE (is_active = true)`
- `idx_contact_email`: `CREATE INDEX idx_contact_email ON public.t_contact USING btree (f_email)`
- `t_contact_pkey` [PRIMARY]: `CREATE UNIQUE INDEX t_contact_pkey ON public.t_contact USING btree (f_contact)`

---

## t_expense
Filas estimadas: ~398

### Columnas

| # | Columna | Tipo | Nullable | Default | PK | FK | Comentario |
|---|---------|------|----------|---------|----|----|------------|
| 1 | `f_expense` | `integer` | NOT NULL | nextval('t_expense_f_expense_seq'::re... | PK |  |  |
| 2 | `f_supplier` | `integer` | NULL |  |  | -> t_supplier.f_supplier |  |
| 3 | `f_description` | `varchar(255)` | NULL |  |  |  |  |
| 4 | `f_expensedate` | `date` | NULL |  |  |  |  |
| 5 | `f_totalexpense` | `numeric(15,2)` | NULL | 0 |  |  |  |
| 6 | `f_status` | `varchar(255)` | NULL |  |  |  |  |
| 7 | `f_paiddate` | `date` | NULL |  |  |  |  |
| 8 | `f_paymethod` | `varchar(255)` | NULL |  |  |  |  |
| 9 | `f_order` | `integer` | NULL |  |  | -> t_order.f_order |  |
| 10 | `expense_category` | `varchar(100)` | NULL |  |  |  |  |
| 11 | `created_at` | `timestamp` | NULL | CURRENT_TIMESTAMP |  |  |  |
| 12 | `updated_at` | `timestamp` | NULL | CURRENT_TIMESTAMP |  |  |  |
| 13 | `created_by` | `integer` | NULL |  |  | -> users.id |  |
| 14 | `f_scheduleddate` | `date` | NULL |  |  |  |  |
| 15 | `updated_by` | `varchar(100)` | NULL |  |  |  | Usuario que realizó la última modificación al gasto |

### Primary Key
- `t_expense_pkey` (f_expense)

### Foreign Keys
- `t_expense_created_by_fkey`: `created_by` -> `users.id` (ON UPDATE NO ACTION, ON DELETE NO ACTION)
- `t_expense_f_order_fkey`: `f_order` -> `t_order.f_order` (ON UPDATE NO ACTION, ON DELETE NO ACTION)
- `t_expense_f_supplier_fkey`: `f_supplier` -> `t_supplier.f_supplier` (ON UPDATE NO ACTION, ON DELETE NO ACTION)

### Indexes
- `idx_expense_date`: `CREATE INDEX idx_expense_date ON public.t_expense USING btree (f_expensedate)`
- `idx_expense_order_date`: `CREATE INDEX idx_expense_order_date ON public.t_expense USING btree (f_order, f_expensedate DESC) WHERE (f_order IS NOT NULL)`
- `idx_expense_order_status`: `CREATE INDEX idx_expense_order_status ON public.t_expense USING btree (f_order, f_status) WHERE (f_order IS NOT NULL)`
- `idx_expense_status_scheduled`: `CREATE INDEX idx_expense_status_scheduled ON public.t_expense USING btree (f_status, f_scheduleddate) WHERE ((f_status)::text = 'PENDIENTE'::text)`
- `idx_expense_supplier`: `CREATE INDEX idx_expense_supplier ON public.t_expense USING btree (f_supplier)`
- `idx_expense_updated_by`: `CREATE INDEX idx_expense_updated_by ON public.t_expense USING btree (updated_by)`
- `t_expense_pkey` [PRIMARY]: `CREATE UNIQUE INDEX t_expense_pkey ON public.t_expense USING btree (f_expense)`

---

## t_expense_audit
Filas estimadas: ~283

### Columnas

| # | Columna | Tipo | Nullable | Default | PK | FK | Comentario |
|---|---------|------|----------|---------|----|----|------------|
| 1 | `id` | `integer` | NOT NULL | nextval('t_expense_audit_id_seq'::reg... | PK |  |  |
| 2 | `expense_id` | `integer` | NULL |  |  |  |  |
| 3 | `action` | `varchar(20)` | NOT NULL |  |  |  |  |
| 4 | `old_supplier_id` | `integer` | NULL |  |  |  |  |
| 5 | `old_description` | `text` | NULL |  |  |  |  |
| 6 | `old_total_expense` | `numeric(18,2)` | NULL |  |  |  |  |
| 7 | `old_expense_date` | `date` | NULL |  |  |  |  |
| 8 | `old_scheduled_date` | `date` | NULL |  |  |  |  |
| 9 | `old_status` | `varchar(20)` | NULL |  |  |  |  |
| 10 | `old_paid_date` | `date` | NULL |  |  |  |  |
| 11 | `old_pay_method` | `varchar(50)` | NULL |  |  |  |  |
| 12 | `old_order_id` | `integer` | NULL |  |  |  |  |
| 13 | `old_expense_category` | `varchar(50)` | NULL |  |  |  |  |
| 14 | `old_created_by` | `text` | NULL |  |  |  |  |
| 15 | `old_updated_by` | `varchar(100)` | NULL |  |  |  |  |
| 16 | `new_supplier_id` | `integer` | NULL |  |  |  |  |
| 17 | `new_description` | `text` | NULL |  |  |  |  |
| 18 | `new_total_expense` | `numeric(18,2)` | NULL |  |  |  |  |
| 19 | `new_expense_date` | `date` | NULL |  |  |  |  |
| 20 | `new_scheduled_date` | `date` | NULL |  |  |  |  |
| 21 | `new_status` | `varchar(20)` | NULL |  |  |  |  |
| 22 | `new_paid_date` | `date` | NULL |  |  |  |  |
| 23 | `new_pay_method` | `varchar(50)` | NULL |  |  |  |  |
| 24 | `new_order_id` | `integer` | NULL |  |  |  |  |
| 25 | `new_expense_category` | `varchar(50)` | NULL |  |  |  |  |
| 26 | `new_created_by` | `text` | NULL |  |  |  |  |
| 27 | `new_updated_by` | `varchar(100)` | NULL |  |  |  |  |
| 28 | `changed_at` | `timestamptz` | NULL | now() |  |  |  |
| 29 | `amount_change` | `numeric(18,2)` | NULL |  |  |  |  |
| 30 | `days_until_due_old` | `integer` | NULL |  |  |  |  |
| 31 | `days_until_due_new` | `integer` | NULL |  |  |  |  |
| 32 | `supplier_name` | `varchar(200)` | NULL |  |  |  |  |
| 33 | `order_po` | `varchar(50)` | NULL |  |  |  |  |
| 34 | `environment` | `varchar(20)` | NULL | 'production'::character varying |  |  |  |

### Primary Key
- `t_expense_audit_pkey` (id)

### Indexes
- `idx_expense_audit_action`: `CREATE INDEX idx_expense_audit_action ON public.t_expense_audit USING btree (action)`
- `idx_expense_audit_changed_at`: `CREATE INDEX idx_expense_audit_changed_at ON public.t_expense_audit USING btree (changed_at DESC)`
- `idx_expense_audit_expense_id`: `CREATE INDEX idx_expense_audit_expense_id ON public.t_expense_audit USING btree (expense_id)`
- `idx_expense_audit_updated_by`: `CREATE INDEX idx_expense_audit_updated_by ON public.t_expense_audit USING btree (new_updated_by)`
- `t_expense_audit_pkey` [PRIMARY]: `CREATE UNIQUE INDEX t_expense_audit_pkey ON public.t_expense_audit USING btree (id)`

---

## t_fixed_expenses
Filas estimadas: ~9

### Columnas

| # | Columna | Tipo | Nullable | Default | PK | FK | Comentario |
|---|---------|------|----------|---------|----|----|------------|
| 1 | `id` | `integer` | NOT NULL | nextval('t_fixed_expenses_id_seq'::re... | PK |  |  |
| 2 | `expense_type` | `varchar(50)` | NOT NULL |  |  |  |  |
| 3 | `description` | `varchar(200)` | NULL |  |  |  |  |
| 4 | `monthly_amount` | `numeric(10,2)` | NULL | 0 |  |  |  |
| 5 | `is_active` | `boolean` | NULL | true |  |  |  |
| 6 | `created_at` | `timestamp` | NULL | CURRENT_TIMESTAMP |  |  |  |
| 7 | `updated_at` | `timestamp` | NULL | CURRENT_TIMESTAMP |  |  |  |
| 8 | `created_by` | `integer` | NULL |  |  | -> users.id |  |
| 9 | `effective_date` | `date` | NULL | CURRENT_DATE |  |  |  |

### Primary Key
- `t_fixed_expenses_pkey` (id)

### Foreign Keys
- `t_fixed_expenses_created_by_fkey`: `created_by` -> `users.id` (ON UPDATE NO ACTION, ON DELETE NO ACTION)

### Indexes
- `t_fixed_expenses_pkey` [PRIMARY]: `CREATE UNIQUE INDEX t_fixed_expenses_pkey ON public.t_fixed_expenses USING btree (id)`

---

## t_fixed_expenses_history
Filas estimadas: ~24

### Columnas

| # | Columna | Tipo | Nullable | Default | PK | FK | Comentario |
|---|---------|------|----------|---------|----|----|------------|
| 1 | `id` | `integer` | NOT NULL | nextval('t_fixed_expenses_history_id_... | PK |  |  |
| 2 | `expense_id` | `integer` | NULL |  |  | -> t_fixed_expenses.id |  |
| 3 | `description` | `varchar` | NULL |  |  |  |  |
| 4 | `monthly_amount` | `numeric` | NULL |  |  |  |  |
| 5 | `effective_date` | `date` | NOT NULL |  |  |  |  |
| 6 | `change_type` | `varchar` | NULL |  |  |  |  |
| 7 | `change_summary` | `text` | NULL |  |  |  |  |
| 8 | `created_by` | `integer` | NULL |  |  | -> users.id |  |
| 9 | `created_at` | `timestamp` | NULL | CURRENT_TIMESTAMP |  |  |  |

### Primary Key
- `t_fixed_expenses_history_pkey` (id)

### Foreign Keys
- `t_fixed_expenses_history_created_by_fkey`: `created_by` -> `users.id` (ON UPDATE NO ACTION, ON DELETE NO ACTION)
- `t_fixed_expenses_history_expense_id_fkey`: `expense_id` -> `t_fixed_expenses.id` (ON UPDATE NO ACTION, ON DELETE NO ACTION)

### Indexes
- `idx_fixed_expenses_history_lookup`: `CREATE INDEX idx_fixed_expenses_history_lookup ON public.t_fixed_expenses_history USING btree (expense_id, effective_date DESC) WHERE ((change_type)::text <> ALL ((ARRAY['DEACTIVATED'::character varying, 'DELETED'::character varying])::text[]))`
- `t_fixed_expenses_history_pkey` [PRIMARY]: `CREATE UNIQUE INDEX t_fixed_expenses_history_pkey ON public.t_fixed_expenses_history USING btree (id)`

---

## t_holiday
> Días feriados oficiales y personalizados
Filas estimadas: ~-1

### Columnas

| # | Columna | Tipo | Nullable | Default | PK | FK | Comentario |
|---|---------|------|----------|---------|----|----|------------|
| 1 | `id` | `integer` | NOT NULL | nextval('t_holiday_id_seq'::regclass) | PK |  |  |
| 2 | `holiday_date` | `date` | NOT NULL |  |  |  |  |
| 3 | `name` | `varchar(100)` | NOT NULL |  |  |  |  |
| 4 | `description` | `text` | NULL |  |  |  |  |
| 5 | `is_mandatory` | `boolean` | NULL | true |  |  | TRUE = Feriado obligatorio por ley |
| 6 | `is_recurring` | `boolean` | NULL | true |  |  | TRUE = Se repite cada año |
| 7 | `recurring_month` | `integer` | NULL |  |  |  |  |
| 8 | `recurring_day` | `integer` | NULL |  |  |  |  |
| 9 | `recurring_rule` | `varchar(50)` | NULL |  |  |  | Regla para feriados móviles (ej: THIRD_MONDAY_MAR) |
| 10 | `year` | `integer` | NULL |  |  |  |  |
| 11 | `created_by` | `integer` | NULL |  |  |  |  |
| 12 | `created_at` | `timestamp` | NULL | CURRENT_TIMESTAMP |  |  |  |
| 13 | `updated_by` | `integer` | NULL |  |  |  |  |
| 14 | `updated_at` | `timestamp` | NULL |  |  |  |  |

### Primary Key
- `t_holiday_pkey` (id)

### Unique Constraints
- `t_holiday_holiday_date_key` (holiday_date)

### Indexes
- `idx_holiday_date`: `CREATE INDEX idx_holiday_date ON public.t_holiday USING btree (holiday_date)`
- `idx_holiday_year`: `CREATE INDEX idx_holiday_year ON public.t_holiday USING btree (EXTRACT(year FROM holiday_date))`
- `t_holiday_holiday_date_key` [UNIQUE]: `CREATE UNIQUE INDEX t_holiday_holiday_date_key ON public.t_holiday USING btree (holiday_date)`
- `t_holiday_pkey` [PRIMARY]: `CREATE UNIQUE INDEX t_holiday_pkey ON public.t_holiday USING btree (id)`

---

## t_invoice
> Facturas - migrada de Access T_INVOICE
Filas estimadas: ~434

### Columnas

| # | Columna | Tipo | Nullable | Default | PK | FK | Comentario |
|---|---------|------|----------|---------|----|----|------------|
| 1 | `f_invoice` | `integer` | NOT NULL | nextval('t_invoice_f_invoice_seq'::re... | PK |  |  |
| 2 | `f_order` | `integer` | NULL |  |  | -> t_order.f_order |  |
| 3 | `f_folio` | `varchar(255)` | NULL |  |  |  |  |
| 4 | `f_invoicedate` | `date` | NULL |  |  |  |  |
| 5 | `f_receptiondate` | `date` | NULL |  |  |  |  |
| 6 | `f_subtotal` | `numeric(15,2)` | NULL | 0 |  |  |  |
| 7 | `f_total` | `numeric(15,2)` | NULL | 0 |  |  |  |
| 8 | `f_downpayment` | `varchar(255)` | NULL |  |  |  |  |
| 9 | `f_invoicestat` | `integer` | NULL |  |  | -> invoice_status.f_invoicestat |  |
| 10 | `f_paymentdate` | `date` | NULL |  |  |  |  |
| 11 | `due_date` | `date` | NULL |  |  |  |  |
| 12 | `payment_method` | `varchar(50)` | NULL |  |  |  |  |
| 13 | `payment_reference` | `varchar(100)` | NULL |  |  |  |  |
| 14 | `balance_due` | `numeric(15,2)` | NULL |  |  |  |  |
| 15 | `created_at` | `timestamp` | NULL | CURRENT_TIMESTAMP |  |  |  |
| 16 | `updated_at` | `timestamp` | NULL | CURRENT_TIMESTAMP |  |  |  |
| 17 | `created_by` | `integer` | NULL |  |  | -> users.id |  |

### Primary Key
- `t_invoice_pkey` (f_invoice)

### Foreign Keys
- `t_invoice_created_by_fkey`: `created_by` -> `users.id` (ON UPDATE NO ACTION, ON DELETE NO ACTION)
- `t_invoice_f_invoicestat_fkey`: `f_invoicestat` -> `invoice_status.f_invoicestat` (ON UPDATE NO ACTION, ON DELETE NO ACTION)
- `t_invoice_f_order_fkey`: `f_order` -> `t_order.f_order` (ON UPDATE NO ACTION, ON DELETE NO ACTION)

### Indexes
- `idx_invoice_folio`: `CREATE INDEX idx_invoice_folio ON public.t_invoice USING btree (f_folio)`
- `idx_invoice_order`: `CREATE INDEX idx_invoice_order ON public.t_invoice USING btree (f_order)`
- `idx_invoice_order_folio` [UNIQUE]: `CREATE UNIQUE INDEX idx_invoice_order_folio ON public.t_invoice USING btree (f_order, f_folio) WHERE (f_folio IS NOT NULL)`
- `idx_invoice_status`: `CREATE INDEX idx_invoice_status ON public.t_invoice USING btree (f_invoicestat)`
- `t_invoice_pkey` [PRIMARY]: `CREATE UNIQUE INDEX t_invoice_pkey ON public.t_invoice USING btree (f_invoice)`

---

## t_order
> Órdenes de compra - migrada de Access T_ORDER
Filas estimadas: ~369

### Columnas

| # | Columna | Tipo | Nullable | Default | PK | FK | Comentario |
|---|---------|------|----------|---------|----|----|------------|
| 1 | `f_order` | `integer` | NOT NULL | nextval('t_order_f_order_seq'::regclass) | PK |  |  |
| 2 | `f_client` | `integer` | NULL |  |  | -> t_client.f_client |  |
| 3 | `f_contact` | `integer` | NULL |  |  | -> t_contact.f_contact |  |
| 4 | `f_quote` | `varchar(255)` | NULL |  |  |  |  |
| 5 | `f_po` | `varchar(255)` | NULL |  |  |  |  |
| 6 | `f_podate` | `date` | NULL |  |  |  |  |
| 7 | `f_estdelivery` | `date` | NULL |  |  |  |  |
| 8 | `f_description` | `varchar(255)` | NULL |  |  |  |  |
| 10 | `f_salesubtotal` | `numeric(15,2)` | NULL | 0 |  |  |  |
| 11 | `f_saletotal` | `numeric(15,2)` | NULL | 0 |  |  |  |
| 12 | `f_orderstat` | `integer` | NULL | 0 |  | -> order_status.f_orderstatus |  |
| 13 | `f_expense` | `numeric(15,2)` | NULL | 0 |  |  |  |
| 14 | `actual_delivery` | `date` | NULL |  |  |  |  |
| 15 | `profit_amount` | `numeric(15,2)` | NULL |  |  |  |  |
| 16 | `created_at` | `timestamp` | NULL | CURRENT_TIMESTAMP |  |  |  |
| 17 | `updated_at` | `timestamp` | NULL | CURRENT_TIMESTAMP |  |  |  |
| 18 | `created_by` | `integer` | NULL |  |  | -> users.id |  |
| 19 | `progress_percentage` | `integer` | NULL | 0 |  |  | Porcentaje de avance de la orden (0-100) - Editable por Coordinador y Admin |
| 20 | `order_percentage` | `integer` | NULL | 0 |  |  | Porcentaje de la orden (0-100) - Solo visible y editable por Admin |
| 21 | `invoiced` | `boolean` | NULL | false |  |  | Indica si la orden ha sido facturada |
| 22 | `last_invoice_date` | `date` | NULL |  |  |  | Fecha de la última factura generada |
| 23 | `f_salesman` | `integer` | NULL |  |  | -> t_vendor.f_vendor | ID del vendedor (referencia a t_vendor.f_vendor). Campo único después de eliminar vendor_id redundante |
| 24 | `updated_by` | `integer` | NULL |  |  | -> users.id |  |
| 25 | `f_commission_rate` | `numeric` | NULL | 0 |  |  | Porcentaje de comisión específico para esta orden. Si es NULL, usa el porcentaje por defecto del vendedor |
| 26 | `gasto_operativo` | `numeric(15,2)` | NULL | 0 |  |  |  |
| 27 | `gasto_indirecto` | `numeric(15,2)` | NULL | 0 |  |  |  |

### Primary Key
- `t_order_pkey` (f_order)

### Foreign Keys
- `t_order_created_by_fkey`: `created_by` -> `users.id` (ON UPDATE NO ACTION, ON DELETE NO ACTION)
- `t_order_f_client_fkey`: `f_client` -> `t_client.f_client` (ON UPDATE NO ACTION, ON DELETE NO ACTION)
- `t_order_f_contact_fkey`: `f_contact` -> `t_contact.f_contact` (ON UPDATE NO ACTION, ON DELETE NO ACTION)
- `t_order_f_orderstat_fkey`: `f_orderstat` -> `order_status.f_orderstatus` (ON UPDATE NO ACTION, ON DELETE NO ACTION)
- `t_order_f_salesman_fkey`: `f_salesman` -> `t_vendor.f_vendor` (ON UPDATE NO ACTION, ON DELETE NO ACTION)
- `t_order_updated_by_fkey`: `updated_by` -> `users.id` (ON UPDATE NO ACTION, ON DELETE NO ACTION)

### Check Constraints
- `t_order_order_percentage_check`: `((order_percentage >= 0) AND (order_percentage <= 100))`
- `t_order_progress_percentage_check`: `((progress_percentage >= 0) AND (progress_percentage <= 100))`

### Indexes
- `idx_order_client`: `CREATE INDEX idx_order_client ON public.t_order USING btree (f_client)`
- `idx_order_po`: `CREATE INDEX idx_order_po ON public.t_order USING btree (f_po)`
- `idx_order_podate`: `CREATE INDEX idx_order_podate ON public.t_order USING btree (f_podate)`
- `idx_order_status`: `CREATE INDEX idx_order_status ON public.t_order USING btree (f_orderstat)`
- `idx_order_status_podate`: `CREATE INDEX idx_order_status_podate ON public.t_order USING btree (f_orderstat, f_podate)`
- `t_order_pkey` [PRIMARY]: `CREATE UNIQUE INDEX t_order_pkey ON public.t_order USING btree (f_order)`

---

## t_order_deleted
> Auditoría de órdenes eliminadas permanentemente
Filas estimadas: ~-1

### Columnas

| # | Columna | Tipo | Nullable | Default | PK | FK | Comentario |
|---|---------|------|----------|---------|----|----|------------|
| 1 | `id` | `integer` | NOT NULL | nextval('t_order_deleted_id_seq'::reg... | PK |  |  |
| 2 | `original_order_id` | `integer` | NOT NULL |  |  |  |  |
| 3 | `f_po` | `varchar(100)` | NULL |  |  |  |  |
| 4 | `f_quote` | `varchar(100)` | NULL |  |  |  |  |
| 5 | `f_client` | `integer` | NULL |  |  |  |  |
| 6 | `f_contact` | `integer` | NULL |  |  |  |  |
| 7 | `f_salesman` | `integer` | NULL |  |  |  |  |
| 8 | `f_podate` | `date` | NULL |  |  |  |  |
| 9 | `f_estdelivery` | `date` | NULL |  |  |  |  |
| 10 | `f_description` | `text` | NULL |  |  |  |  |
| 11 | `f_salesubtotal` | `numeric` | NULL |  |  |  |  |
| 12 | `f_saletotal` | `numeric` | NULL |  |  |  |  |
| 13 | `f_orderstat` | `integer` | NULL |  |  |  |  |
| 14 | `f_expense` | `numeric` | NULL |  |  |  |  |
| 15 | `progress_percentage` | `integer` | NULL |  |  |  |  |
| 16 | `order_percentage` | `integer` | NULL |  |  |  |  |
| 17 | `f_commission_rate` | `numeric` | NULL |  |  |  |  |
| 18 | `deleted_by` | `integer` | NOT NULL |  |  |  |  |
| 19 | `deleted_at` | `timestamp` | NULL | CURRENT_TIMESTAMP |  |  |  |
| 20 | `deletion_reason` | `text` | NULL |  |  |  |  |
| 21 | `full_order_snapshot` | `jsonb` | NULL |  |  |  |  |

### Primary Key
- `t_order_deleted_pkey` (id)

### Indexes
- `idx_order_deleted_date`: `CREATE INDEX idx_order_deleted_date ON public.t_order_deleted USING btree (deleted_at)`
- `idx_order_deleted_original_id`: `CREATE INDEX idx_order_deleted_original_id ON public.t_order_deleted USING btree (original_order_id)`
- `idx_order_deleted_po`: `CREATE INDEX idx_order_deleted_po ON public.t_order_deleted USING btree (f_po)`
- `t_order_deleted_pkey` [PRIMARY]: `CREATE UNIQUE INDEX t_order_deleted_pkey ON public.t_order_deleted USING btree (id)`

---

## t_overtime_hours
Filas estimadas: ~-1

### Columnas

| # | Columna | Tipo | Nullable | Default | PK | FK | Comentario |
|---|---------|------|----------|---------|----|----|------------|
| 1 | `id` | `integer` | NOT NULL | nextval('t_overtime_hours_id_seq'::re... | PK |  |  |
| 2 | `year` | `integer` | NOT NULL |  |  |  |  |
| 3 | `month` | `integer` | NOT NULL |  |  |  |  |
| 4 | `amount` | `numeric` | NULL | 0 |  |  |  |
| 5 | `notes` | `text` | NULL |  |  |  |  |
| 6 | `created_by` | `integer` | NULL |  |  | -> users.id |  |
| 7 | `updated_by` | `integer` | NULL |  |  | -> users.id |  |
| 8 | `created_at` | `timestamp` | NULL | CURRENT_TIMESTAMP |  |  |  |
| 9 | `updated_at` | `timestamp` | NULL | CURRENT_TIMESTAMP |  |  |  |

### Primary Key
- `t_overtime_hours_pkey` (id)

### Foreign Keys
- `t_overtime_hours_created_by_fkey`: `created_by` -> `users.id` (ON UPDATE NO ACTION, ON DELETE NO ACTION)
- `t_overtime_hours_updated_by_fkey`: `updated_by` -> `users.id` (ON UPDATE NO ACTION, ON DELETE NO ACTION)

### Unique Constraints
- `t_overtime_hours_year_month_key` (year, month)

### Indexes
- `idx_overtime_year_month`: `CREATE INDEX idx_overtime_year_month ON public.t_overtime_hours USING btree (year, month)`
- `t_overtime_hours_pkey` [PRIMARY]: `CREATE UNIQUE INDEX t_overtime_hours_pkey ON public.t_overtime_hours USING btree (id)`
- `t_overtime_hours_year_month_key` [UNIQUE]: `CREATE UNIQUE INDEX t_overtime_hours_year_month_key ON public.t_overtime_hours USING btree (year, month)`

---

## t_overtime_hours_audit
Filas estimadas: ~-1

### Columnas

| # | Columna | Tipo | Nullable | Default | PK | FK | Comentario |
|---|---------|------|----------|---------|----|----|------------|
| 1 | `id` | `integer` | NOT NULL | nextval('t_overtime_hours_audit_id_se... | PK |  |  |
| 2 | `overtime_id` | `integer` | NOT NULL |  |  |  |  |
| 3 | `year` | `integer` | NOT NULL |  |  |  |  |
| 4 | `month` | `integer` | NOT NULL |  |  |  |  |
| 5 | `old_amount` | `numeric` | NULL |  |  |  |  |
| 6 | `new_amount` | `numeric` | NULL |  |  |  |  |
| 7 | `change_type` | `varchar(20)` | NULL |  |  |  |  |
| 8 | `change_reason` | `text` | NULL |  |  |  |  |
| 9 | `changed_by` | `integer` | NULL |  |  | -> users.id |  |
| 10 | `changed_at` | `timestamp` | NULL | CURRENT_TIMESTAMP |  |  |  |
| 11 | `ip_address` | `varchar(45)` | NULL |  |  |  |  |
| 12 | `user_agent` | `text` | NULL |  |  |  |  |

### Primary Key
- `t_overtime_hours_audit_pkey` (id)

### Foreign Keys
- `t_overtime_hours_audit_changed_by_fkey`: `changed_by` -> `users.id` (ON UPDATE NO ACTION, ON DELETE NO ACTION)

### Indexes
- `idx_overtime_audit_date`: `CREATE INDEX idx_overtime_audit_date ON public.t_overtime_hours_audit USING btree (changed_at)`
- `t_overtime_hours_audit_pkey` [PRIMARY]: `CREATE UNIQUE INDEX t_overtime_hours_audit_pkey ON public.t_overtime_hours_audit USING btree (id)`

---

## t_payroll
Filas estimadas: ~13

### Columnas

| # | Columna | Tipo | Nullable | Default | PK | FK | Comentario |
|---|---------|------|----------|---------|----|----|------------|
| 1 | `f_payroll` | `integer` | NOT NULL | nextval('t_payroll_f_payroll_seq'::re... | PK |  |  |
| 2 | `f_employee` | `varchar(255)` | NULL |  |  |  |  |
| 3 | `f_title` | `varchar(255)` | NULL |  |  |  |  |
| 4 | `f_hireddate` | `date` | NULL |  |  |  |  |
| 5 | `f_range` | `varchar(255)` | NULL |  |  |  |  |
| 6 | `f_condition` | `varchar(255)` | NULL |  |  |  |  |
| 7 | `f_lastraise` | `date` | NULL |  |  |  |  |
| 8 | `f_sspayroll` | `numeric(15,2)` | NULL | 0 |  |  |  |
| 9 | `f_weeklypayroll` | `numeric(15,2)` | NULL | 0 |  |  |  |
| 10 | `f_socialsecurity` | `numeric(15,2)` | NULL | 0 |  |  |  |
| 11 | `f_benefits` | `varchar(255)` | NULL |  |  |  |  |
| 12 | `f_benefitsamount` | `numeric(15,2)` | NULL | 0 |  |  |  |
| 13 | `f_monthlypayroll` | `numeric(15,2)` | NULL | 0 |  |  |  |
| 14 | `is_active` | `boolean` | NULL | true |  |  |  |
| 15 | `employee_code` | `varchar(50)` | NULL |  |  |  |  |
| 16 | `created_at` | `timestamp` | NULL | CURRENT_TIMESTAMP |  |  |  |
| 17 | `updated_at` | `timestamp` | NULL | CURRENT_TIMESTAMP |  |  |  |
| 18 | `updated_by` | `integer` | NULL |  |  | -> users.id |  |
| 19 | `created_by` | `integer` | NULL |  |  | -> users.id |  |

### Primary Key
- `t_payroll_pkey` (f_payroll)

### Foreign Keys
- `t_payroll_created_by_fkey`: `created_by` -> `users.id` (ON UPDATE NO ACTION, ON DELETE NO ACTION)
- `t_payroll_updated_by_fkey`: `updated_by` -> `users.id` (ON UPDATE NO ACTION, ON DELETE NO ACTION)

### Indexes
- `idx_payroll_active`: `CREATE INDEX idx_payroll_active ON public.t_payroll USING btree (is_active) WHERE (is_active = true)`
- `t_payroll_pkey` [PRIMARY]: `CREATE UNIQUE INDEX t_payroll_pkey ON public.t_payroll USING btree (f_payroll)`

---

## t_payroll_history
Filas estimadas: ~-1

### Columnas

| # | Columna | Tipo | Nullable | Default | PK | FK | Comentario |
|---|---------|------|----------|---------|----|----|------------|
| 1 | `id` | `integer` | NOT NULL | nextval('t_payroll_history_id_seq'::r... | PK |  |  |
| 2 | `f_payroll` | `integer` | NULL |  |  | -> t_payroll.f_payroll |  |
| 3 | `f_employee` | `varchar(100)` | NULL |  |  |  |  |
| 4 | `f_title` | `varchar(100)` | NULL |  |  |  |  |
| 5 | `f_hireddate` | `date` | NULL |  |  |  |  |
| 6 | `f_range` | `varchar(20)` | NULL |  |  |  |  |
| 7 | `f_condition` | `varchar(20)` | NULL |  |  |  |  |
| 8 | `f_lastraise` | `date` | NULL |  |  |  |  |
| 9 | `f_sspayroll` | `numeric(10,2)` | NULL |  |  |  |  |
| 10 | `f_weeklypayroll` | `numeric(10,2)` | NULL |  |  |  |  |
| 11 | `f_socialsecurity` | `numeric(10,2)` | NULL |  |  |  |  |
| 12 | `f_benefits` | `varchar(200)` | NULL |  |  |  |  |
| 13 | `f_benefitsamount` | `numeric(10,2)` | NULL |  |  |  |  |
| 14 | `f_monthlypayroll` | `numeric(10,2)` | NULL |  |  |  |  |
| 15 | `employee_code` | `varchar(20)` | NULL |  |  |  |  |
| 16 | `is_active` | `boolean` | NULL |  |  |  |  |
| 17 | `changed_fields` | `jsonb` | NULL |  |  |  |  |
| 18 | `effective_date` | `date` | NOT NULL |  |  |  |  |
| 19 | `change_type` | `varchar(50)` | NULL |  |  |  |  |
| 20 | `change_summary` | `text` | NULL |  |  |  |  |
| 21 | `created_by` | `integer` | NULL |  |  | -> users.id |  |
| 22 | `created_at` | `timestamp` | NULL | CURRENT_TIMESTAMP |  |  |  |

### Primary Key
- `t_payroll_history_pkey` (id)

### Foreign Keys
- `t_payroll_history_created_by_fkey`: `created_by` -> `users.id` (ON UPDATE NO ACTION, ON DELETE NO ACTION)
- `t_payroll_history_f_payroll_fkey`: `f_payroll` -> `t_payroll.f_payroll` (ON UPDATE NO ACTION, ON DELETE NO ACTION)

### Indexes
- `idx_payroll_history_active`: `CREATE INDEX idx_payroll_history_active ON public.t_payroll_history USING btree (f_payroll, is_active)`
- `idx_payroll_history_date`: `CREATE INDEX idx_payroll_history_date ON public.t_payroll_history USING btree (effective_date)`
- `idx_payroll_history_employee`: `CREATE INDEX idx_payroll_history_employee ON public.t_payroll_history USING btree (f_payroll, effective_date DESC)`
- `t_payroll_history_pkey` [PRIMARY]: `CREATE UNIQUE INDEX t_payroll_history_pkey ON public.t_payroll_history USING btree (id)`

---

## t_payrollovertime
Filas estimadas: ~110

### Columnas

| # | Columna | Tipo | Nullable | Default | PK | FK | Comentario |
|---|---------|------|----------|---------|----|----|------------|
| 1 | `f_payrollovertime` | `integer` | NOT NULL | nextval('t_payrollovertime_f_payrollo... | PK |  |  |
| 2 | `f_date` | `date` | NULL |  |  |  |  |
| 3 | `f_payroll` | `numeric(15,2)` | NULL | 0 |  |  |  |
| 4 | `f_overtime` | `numeric(15,2)` | NULL | 0 |  |  |  |
| 5 | `f_fixedexpense` | `numeric(15,2)` | NULL | 0 |  |  |  |
| 6 | `f_estimate` | `integer` | NULL | 0 |  |  |  |
| 7 | `employee_id` | `integer` | NULL |  |  | -> t_payroll.f_payroll |  |
| 8 | `hours_worked` | `numeric(10,2)` | NULL |  |  |  |  |
| 9 | `created_at` | `timestamp` | NULL | CURRENT_TIMESTAMP |  |  |  |
| 10 | `created_by` | `integer` | NULL |  |  | -> users.id |  |

### Primary Key
- `t_payrollovertime_pkey` (f_payrollovertime)

### Foreign Keys
- `t_payrollovertime_created_by_fkey`: `created_by` -> `users.id` (ON UPDATE NO ACTION, ON DELETE NO ACTION)
- `t_payrollovertime_employee_id_fkey`: `employee_id` -> `t_payroll.f_payroll` (ON UPDATE NO ACTION, ON DELETE NO ACTION)

### Indexes
- `t_payrollovertime_pkey` [PRIMARY]: `CREATE UNIQUE INDEX t_payrollovertime_pkey ON public.t_payrollovertime USING btree (f_payrollovertime)`

---

## t_supplier
Filas estimadas: ~179

### Columnas

| # | Columna | Tipo | Nullable | Default | PK | FK | Comentario |
|---|---------|------|----------|---------|----|----|------------|
| 1 | `f_supplier` | `integer` | NOT NULL | nextval('t_supplier_f_supplier_seq'::... | PK |  |  |
| 2 | `f_suppliername` | `varchar(255)` | NOT NULL |  |  |  |  |
| 3 | `f_credit` | `integer` | NULL | 0 |  |  |  |
| 4 | `tax_id` | `varchar(50)` | NULL |  |  |  |  |
| 5 | `phone` | `varchar(50)` | NULL |  |  |  |  |
| 6 | `email` | `varchar(255)` | NULL |  |  |  |  |
| 7 | `address` | `text` | NULL |  |  |  |  |
| 8 | `is_active` | `boolean` | NULL | true |  |  |  |
| 9 | `created_at` | `timestamp` | NULL | CURRENT_TIMESTAMP |  |  |  |
| 10 | `updated_at` | `timestamp` | NULL | CURRENT_TIMESTAMP |  |  |  |

### Primary Key
- `t_supplier_pkey` (f_supplier)

### Indexes
- `t_supplier_pkey` [PRIMARY]: `CREATE UNIQUE INDEX t_supplier_pkey ON public.t_supplier USING btree (f_supplier)`

---

## t_vacation
> Períodos de vacaciones del personal
Filas estimadas: ~-1

### Columnas

| # | Columna | Tipo | Nullable | Default | PK | FK | Comentario |
|---|---------|------|----------|---------|----|----|------------|
| 1 | `id` | `integer` | NOT NULL | nextval('t_vacation_id_seq'::regclass) | PK |  |  |
| 2 | `employee_id` | `integer` | NOT NULL |  |  | -> t_payroll.f_payroll |  |
| 3 | `start_date` | `date` | NOT NULL |  |  |  |  |
| 4 | `end_date` | `date` | NOT NULL |  |  |  |  |
| 5 | `total_days` | `integer` | NULL |  |  |  | Días totales de vacaciones (calculado automáticamente) |
| 6 | `notes` | `text` | NULL |  |  |  |  |
| 7 | `status` | `varchar(20)` | NULL | 'PENDIENTE'::character varying |  |  |  |
| 8 | `approved_by` | `integer` | NULL |  |  |  |  |
| 9 | `approved_at` | `timestamp` | NULL |  |  |  |  |
| 10 | `rejection_reason` | `text` | NULL |  |  |  |  |
| 11 | `created_by` | `integer` | NULL |  |  |  |  |
| 12 | `created_at` | `timestamp` | NULL | CURRENT_TIMESTAMP |  |  |  |
| 13 | `updated_by` | `integer` | NULL |  |  |  |  |
| 14 | `updated_at` | `timestamp` | NULL |  |  |  |  |

### Primary Key
- `t_vacation_pkey` (id)

### Foreign Keys
- `t_vacation_employee_id_fkey`: `employee_id` -> `t_payroll.f_payroll` (ON UPDATE NO ACTION, ON DELETE CASCADE)

### Check Constraints
- `t_vacation_check`: `(end_date >= start_date)`
- `t_vacation_status_check`: `((status)::text = ANY ((ARRAY['PENDIENTE'::character varying, 'APROBADA'::character varying, 'RECHAZADA'::character varying, 'CANCELADA'::character varying])::text[]))`

### Indexes
- `idx_vacation_dates`: `CREATE INDEX idx_vacation_dates ON public.t_vacation USING btree (start_date, end_date)`
- `idx_vacation_employee`: `CREATE INDEX idx_vacation_employee ON public.t_vacation USING btree (employee_id)`
- `idx_vacation_status`: `CREATE INDEX idx_vacation_status ON public.t_vacation USING btree (status)`
- `t_vacation_pkey` [PRIMARY]: `CREATE UNIQUE INDEX t_vacation_pkey ON public.t_vacation USING btree (id)`

---

## t_vacation_audit
> Historial de cambios en registros de vacaciones
Filas estimadas: ~-1

### Columnas

| # | Columna | Tipo | Nullable | Default | PK | FK | Comentario |
|---|---------|------|----------|---------|----|----|------------|
| 1 | `id` | `integer` | NOT NULL | nextval('t_vacation_audit_id_seq'::re... | PK |  |  |
| 2 | `vacation_id` | `integer` | NOT NULL |  |  |  |  |
| 3 | `employee_id` | `integer` | NOT NULL |  |  |  |  |
| 4 | `action` | `varchar(10)` | NOT NULL |  |  |  |  |
| 5 | `old_start_date` | `date` | NULL |  |  |  |  |
| 6 | `old_end_date` | `date` | NULL |  |  |  |  |
| 7 | `old_status` | `varchar(20)` | NULL |  |  |  |  |
| 8 | `old_notes` | `text` | NULL |  |  |  |  |
| 9 | `new_start_date` | `date` | NULL |  |  |  |  |
| 10 | `new_end_date` | `date` | NULL |  |  |  |  |
| 11 | `new_status` | `varchar(20)` | NULL |  |  |  |  |
| 12 | `new_notes` | `text` | NULL |  |  |  |  |
| 13 | `changed_by` | `integer` | NULL |  |  |  |  |
| 14 | `changed_at` | `timestamp` | NULL | CURRENT_TIMESTAMP |  |  |  |
| 15 | `change_reason` | `text` | NULL |  |  |  |  |

### Primary Key
- `t_vacation_audit_pkey` (id)

### Check Constraints
- `t_vacation_audit_action_check`: `((action)::text = ANY ((ARRAY['INSERT'::character varying, 'UPDATE'::character varying, 'DELETE'::character varying])::text[]))`

### Indexes
- `idx_vacation_audit_employee`: `CREATE INDEX idx_vacation_audit_employee ON public.t_vacation_audit USING btree (employee_id)`
- `idx_vacation_audit_id`: `CREATE INDEX idx_vacation_audit_id ON public.t_vacation_audit USING btree (vacation_id)`
- `t_vacation_audit_pkey` [PRIMARY]: `CREATE UNIQUE INDEX t_vacation_audit_pkey ON public.t_vacation_audit USING btree (id)`

---

## t_vendor
Filas estimadas: ~-1

### Columnas

| # | Columna | Tipo | Nullable | Default | PK | FK | Comentario |
|---|---------|------|----------|---------|----|----|------------|
| 1 | `f_vendor` | `integer` | NOT NULL | nextval('t_vendor_f_vendor_seq'::regc... | PK |  |  |
| 2 | `f_vendorname` | `varchar(255)` | NOT NULL |  |  |  |  |
| 3 | `f_user_id` | `integer` | NULL |  |  | -> users.id |  |
| 4 | `f_commission_rate` | `numeric(5,2)` | NULL | 0 |  |  |  |
| 5 | `f_phone` | `varchar(50)` | NULL |  |  |  |  |
| 6 | `f_email` | `varchar(255)` | NULL |  |  |  |  |
| 7 | `is_active` | `boolean` | NULL | true |  |  |  |
| 8 | `created_at` | `timestamp` | NULL | CURRENT_TIMESTAMP |  |  |  |
| 9 | `updated_at` | `timestamp` | NULL | CURRENT_TIMESTAMP |  |  |  |

### Primary Key
- `t_vendor_pkey` (f_vendor)

### Foreign Keys
- `t_vendor_f_user_id_fkey`: `f_user_id` -> `users.id` (ON UPDATE NO ACTION, ON DELETE NO ACTION)

### Indexes
- `idx_vendor_name`: `CREATE INDEX idx_vendor_name ON public.t_vendor USING btree (f_vendorname)`
- `idx_vendor_user`: `CREATE INDEX idx_vendor_user ON public.t_vendor USING btree (f_user_id)`
- `t_vendor_pkey` [PRIMARY]: `CREATE UNIQUE INDEX t_vendor_pkey ON public.t_vendor USING btree (f_vendor)`

---

## t_vendor_commission_payment
Filas estimadas: ~21

### Columnas

| # | Columna | Tipo | Nullable | Default | PK | FK | Comentario |
|---|---------|------|----------|---------|----|----|------------|
| 1 | `id` | `integer` | NOT NULL | nextval('t_vendor_commission_payment_... | PK |  |  |
| 2 | `f_order` | `integer` | NOT NULL |  |  | -> t_order.f_order |  |
| 3 | `f_vendor` | `integer` | NOT NULL |  |  | -> t_vendor.f_vendor |  |
| 4 | `commission_amount` | `numeric` | NOT NULL | 0 |  |  |  |
| 5 | `commission_rate` | `numeric` | NOT NULL |  |  |  |  |
| 6 | `payment_status` | `varchar(20)` | NOT NULL | 'draft'::character varying |  |  |  |
| 7 | `payment_date` | `date` | NULL |  |  |  |  |
| 8 | `payment_reference` | `varchar(100)` | NULL |  |  |  |  |
| 9 | `notes` | `text` | NULL |  |  |  |  |
| 10 | `created_at` | `timestamp` | NULL | CURRENT_TIMESTAMP |  |  |  |
| 11 | `updated_at` | `timestamp` | NULL | CURRENT_TIMESTAMP |  |  |  |
| 12 | `created_by` | `integer` | NULL |  |  | -> users.id |  |
| 13 | `updated_by` | `integer` | NULL |  |  | -> users.id |  |

### Primary Key
- `t_vendor_commission_payment_pkey` (id)

### Foreign Keys
- `fk_commission_created_by`: `created_by` -> `users.id` (ON UPDATE NO ACTION, ON DELETE NO ACTION)
- `fk_commission_order`: `f_order` -> `t_order.f_order` (ON UPDATE NO ACTION, ON DELETE NO ACTION)
- `fk_commission_vendor`: `f_vendor` -> `t_vendor.f_vendor` (ON UPDATE NO ACTION, ON DELETE NO ACTION)
- `fk_commission_updated_by`: `updated_by` -> `users.id` (ON UPDATE NO ACTION, ON DELETE NO ACTION)

### Check Constraints
- `t_vendor_commission_payment_payment_status_check`: `((payment_status)::text = ANY (ARRAY[('draft'::character varying)::text, ('pending'::character varying)::text, ('paid'::character varying)::text]))`

### Indexes
- `idx_commission_payment_order`: `CREATE INDEX idx_commission_payment_order ON public.t_vendor_commission_payment USING btree (f_order)`
- `idx_commission_payment_status`: `CREATE INDEX idx_commission_payment_status ON public.t_vendor_commission_payment USING btree (payment_status)`
- `idx_commission_payment_vendor`: `CREATE INDEX idx_commission_payment_vendor ON public.t_vendor_commission_payment USING btree (f_vendor)`
- `t_vendor_commission_payment_pkey` [PRIMARY]: `CREATE UNIQUE INDEX t_vendor_commission_payment_pkey ON public.t_vendor_commission_payment USING btree (id)`

---

## t_workday_config
> Configuración de días laborales de la semana
Filas estimadas: ~-1

### Columnas

| # | Columna | Tipo | Nullable | Default | PK | FK | Comentario |
|---|---------|------|----------|---------|----|----|------------|
| 1 | `id` | `integer` | NOT NULL | nextval('t_workday_config_id_seq'::re... | PK |  |  |
| 2 | `day_of_week` | `integer` | NOT NULL |  |  |  | 0=Domingo, 1=Lunes, ..., 6=Sábado |
| 3 | `is_workday` | `boolean` | NULL | true |  |  |  |
| 4 | `description` | `varchar(50)` | NULL |  |  |  |  |
| 5 | `created_at` | `timestamp` | NULL | CURRENT_TIMESTAMP |  |  |  |
| 6 | `updated_at` | `timestamp` | NULL |  |  |  |  |

### Primary Key
- `t_workday_config_pkey` (id)

### Unique Constraints
- `t_workday_config_day_of_week_key` (day_of_week)

### Check Constraints
- `t_workday_config_day_of_week_check`: `((day_of_week >= 0) AND (day_of_week <= 6))`

### Indexes
- `t_workday_config_day_of_week_key` [UNIQUE]: `CREATE UNIQUE INDEX t_workday_config_day_of_week_key ON public.t_workday_config USING btree (day_of_week)`
- `t_workday_config_pkey` [PRIMARY]: `CREATE UNIQUE INDEX t_workday_config_pkey ON public.t_workday_config USING btree (id)`

---

## users
> Usuarios del sistema - nueva tabla para autenticación
Filas estimadas: ~-1

### Columnas

| # | Columna | Tipo | Nullable | Default | PK | FK | Comentario |
|---|---------|------|----------|---------|----|----|------------|
| 1 | `id` | `integer` | NOT NULL | nextval('users_id_seq'::regclass) | PK |  |  |
| 2 | `username` | `varchar(50)` | NOT NULL |  |  |  |  |
| 3 | `email` | `varchar(255)` | NOT NULL |  |  |  |  |
| 4 | `password_hash` | `varchar(255)` | NOT NULL |  |  |  |  |
| 5 | `full_name` | `varchar(255)` | NOT NULL |  |  |  |  |
| 6 | `role` | `varchar(50)` | NOT NULL |  |  |  |  |
| 7 | `is_active` | `boolean` | NULL | true |  |  |  |
| 8 | `last_login` | `timestamp` | NULL |  |  |  |  |
| 9 | `created_at` | `timestamp` | NULL | CURRENT_TIMESTAMP |  |  |  |
| 10 | `updated_at` | `timestamp` | NULL | CURRENT_TIMESTAMP |  |  |  |

### Primary Key
- `users_pkey` (id)

### Unique Constraints
- `users_email_key` (email)
- `users_username_key` (username)

### Check Constraints
- `users_role_check`: `((role)::text = ANY ((ARRAY['direccion'::character varying, 'administracion'::character varying, 'proyectos'::character varying, 'coordinacion'::character varying, 'ventas'::character varying])::text[]))`

### Indexes
- `idx_users_active`: `CREATE INDEX idx_users_active ON public.users USING btree (is_active) WHERE (is_active = true)`
- `users_email_key` [UNIQUE]: `CREATE UNIQUE INDEX users_email_key ON public.users USING btree (email)`
- `users_pkey` [PRIMARY]: `CREATE UNIQUE INDEX users_pkey ON public.users USING btree (id)`
- `users_username_key` [UNIQUE]: `CREATE UNIQUE INDEX users_username_key ON public.users USING btree (username)`

---
