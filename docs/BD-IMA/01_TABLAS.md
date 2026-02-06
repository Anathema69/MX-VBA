# Detalle de Tablas - IMA Mecatrónica

**Fecha de extracción:** _Pendiente_

---

## Índice de Tablas

_Actualizar después de ejecutar Sección 1_

1. [app_versions](#app_versions)
2. [invoice_status](#invoice_status)
3. [order_history](#order_history)
4. [order_status](#order_status)
5. [t_client](#t_client)
6. [t_contact](#t_contact)
7. [t_expense](#t_expense)
8. [t_expense_audit](#t_expense_audit) *(Nueva - 2026-01-27)*
9. [t_fixed_expenses](#t_fixed_expenses)
10. [t_fixed_expenses_history](#t_fixed_expenses_history)
11. [t_invoice](#t_invoice)
12. [t_order](#t_order)
13. [t_order_deleted](#t_order_deleted)
14. [t_payroll](#t_payroll)
15. [t_payroll_history](#t_payroll_history)
16. [t_supplier](#t_supplier)
17. [t_vendor](#t_vendor)
18. [t_vendor_commission_payment](#t_vendor_commission_payment)
19. [t_commission_rate_history](#t_commission_rate_history)
20. [users](#users)
21. [order_gastos_operativos](#order_gastos_operativos) *(Actualizada 2026-02-06)*
22. [order_gastos_indirectos](#order_gastos_indirectos)

---

## Columnas por Tabla (Sección 2)

_Pegar resultado de Sección 2 aquí, organizado por tabla_

### Formato esperado:

```
| tabla | columna | tipo | max_length | precision | nullable | default_value | es_pk |
|-------|---------|------|------------|-----------|----------|---------------|-------|
```

---

## Detalle por Tabla

### app_versions

| Columna | Tipo | Nullable | Default | PK |
|---------|------|----------|---------|:--:|
| _pendiente_ |

**Propósito:** Control de versiones de la aplicación para auto-update

---

### users

| Columna | Tipo | Nullable | Default | PK |
|---------|------|----------|---------|:--:|
| _pendiente_ |

**Propósito:** Usuarios del sistema con autenticación BCrypt

**Roles actuales:**
- `admin` - Acceso total
- `coordinator` - Solo órdenes
- `salesperson` - Solo comisiones

**Roles nuevos (v2.0):**
- `direccion` - Reemplaza admin
- `administracion` - Como admin sin portal vendedores
- `proyectos` - Igual a coordinación (temporal)
- `coordinacion` - Actual coordinator
- `ventas` - Actual salesperson

---

### t_order

| Columna | Tipo | Nullable | Default | PK |
|---------|------|----------|---------|:--:|
| _pendiente_ |

**Propósito:** Órdenes/Proyectos - tabla core del sistema

**Columnas nuevas requeridas (v2.0):**
- `gasto_operativo` (decimal)
- `gasto_indirecto` (decimal)
- `dias_estimados` (int)

---

### t_client

| Columna | Tipo | Nullable | Default | PK |
|---------|------|----------|---------|:--:|
| _pendiente_ |

**Propósito:** Clientes de la empresa

---

### t_contact

| Columna | Tipo | Nullable | Default | PK |
|---------|------|----------|---------|:--:|
| _pendiente_ |

**Propósito:** Contactos asociados a clientes

---

### t_invoice

| Columna | Tipo | Nullable | Default | PK |
|---------|------|----------|---------|:--:|
| _pendiente_ |

**Propósito:** Facturas emitidas por orden

---

### invoice_status

| Columna | Tipo | Nullable | Default | PK |
|---------|------|----------|---------|:--:|
| _pendiente_ |

**Propósito:** Catálogo de estados de factura

---

### t_expense

| Columna | Tipo | Nullable | Default | PK |
|---------|------|----------|---------|:--:|
| f_expense | integer | NO | sequence | ✓ |
| f_supplier | integer | YES | | |
| f_description | varchar | YES | | |
| f_expensedate | date | YES | | |
| f_totalexpense | numeric | YES | 0 | |
| f_status | varchar | YES | | |
| f_paiddate | date | YES | | |
| f_paymethod | varchar | YES | | |
| f_order | integer | YES | | |
| expense_category | varchar | YES | | |
| f_scheduleddate | date | YES | | |
| created_at | timestamp | YES | CURRENT_TIMESTAMP | |
| updated_at | timestamp | YES | CURRENT_TIMESTAMP | |
| created_by | integer | YES | | |
| updated_by | varchar(100) | YES | | |

**Propósito:** Gastos a proveedores (cuentas por pagar)

**Columnas de auditoría:**
- `created_by` - ID del usuario que creó el gasto (INTEGER, FK a users.id)
- `updated_by` - Username del usuario que modificó el gasto (VARCHAR)

**Trigger asociado:** `trg_expense_audit` - Registra cambios en `t_expense_audit`

---

### t_expense_audit

| Columna | Tipo | Nullable | Default | PK |
|---------|------|----------|---------|:--:|
| id | serial | NO | sequence | ✓ |
| expense_id | integer | YES | | |
| action | varchar(20) | NO | | |
| old_* | varios | YES | | |
| new_* | varios | YES | | |
| changed_at | timestamptz | YES | NOW() | |
| amount_change | numeric(18,2) | YES | | |
| days_until_due_old | integer | YES | | |
| days_until_due_new | integer | YES | | |
| supplier_name | varchar(200) | YES | | |
| order_po | varchar(50) | YES | | |
| environment | varchar(20) | YES | 'production' | |

**Propósito:** Auditoría de cambios en gastos a proveedores (t_expense)

**Acciones registradas:**
- `INSERT` - Gasto creado
- `UPDATE` - Gasto modificado
- `DELETE` - Gasto eliminado
- `PAID` - Gasto marcado como pagado
- `UNPAID` - Pago revertido a pendiente

**Campos de usuario:**
- `new_created_by` - ID del usuario que creó (desde t_expense.created_by)
- `new_updated_by` - Username del usuario que modificó (desde t_expense.updated_by)

**Vista asociada:** `v_expense_audit_report` - Vista formateada para reportes

**Fecha de creación:** 2026-01-27

---

### t_supplier

| Columna | Tipo | Nullable | Default | PK |
|---------|------|----------|---------|:--:|
| _pendiente_ |

**Propósito:** Catálogo de proveedores

---

### t_vendor

| Columna | Tipo | Nullable | Default | PK |
|---------|------|----------|---------|:--:|
| _pendiente_ |

**Propósito:** Vendedores con tasa de comisión

---

### t_vendor_commission_payment

| Columna | Tipo | Nullable | Default | PK |
|---------|------|----------|---------|:--:|
| _pendiente_ |

**Propósito:** Pagos de comisiones a vendedores

---

### t_commission_rate_history

| Columna | Tipo | Nullable | Default | PK |
|---------|------|----------|---------|:--:|
| _pendiente_ |

**Propósito:** Auditoría de cambios en tasas de comisión

---

### order_status

| Columna | Tipo | Nullable | Default | PK |
|---------|------|----------|---------|:--:|
| _pendiente_ |

**Propósito:** Catálogo de estados de orden

---

### order_history

| Columna | Tipo | Nullable | Default | PK |
|---------|------|----------|---------|:--:|
| _pendiente_ |

**Propósito:** Historial de cambios en órdenes

---

### t_order_deleted

| Columna | Tipo | Nullable | Default | PK |
|---------|------|----------|---------|:--:|
| _pendiente_ |

**Propósito:** Auditoría de órdenes eliminadas

---

### t_payroll

| Columna | Tipo | Nullable | Default | PK |
|---------|------|----------|---------|:--:|
| _pendiente_ |

**Propósito:** Nómina de empleados

---

### t_payroll_history

| Columna | Tipo | Nullable | Default | PK |
|---------|------|----------|---------|:--:|
| _pendiente_ |

**Propósito:** Historial de cambios en nómina

---

### t_fixed_expenses

| Columna | Tipo | Nullable | Default | PK |
|---------|------|----------|---------|:--:|
| _pendiente_ |

**Propósito:** Gastos fijos mensuales

---

### t_fixed_expenses_history

| Columna | Tipo | Nullable | Default | PK |
|---------|------|----------|---------|:--:|
| _pendiente_ |

**Propósito:** Historial de cambios en gastos fijos

---

### order_gastos_operativos *(Actualizada 2026-02-06)*

| Columna | Tipo | Nullable | Default | PK |
|---------|------|----------|---------|:--:|
| id | integer | NO | sequence | ✓ |
| f_order | integer | NO | - | |
| monto | numeric | NO | 0 | |
| descripcion | varchar | NO | - | |
| categoria | varchar | YES | - | |
| fecha_gasto | timestamp | YES | CURRENT_TIMESTAMP | |
| created_at | timestamp | YES | CURRENT_TIMESTAMP | |
| created_by | integer | YES | - | |
| updated_at | timestamp | YES | CURRENT_TIMESTAMP | |
| updated_by | integer | YES | - | |
| **f_commission_rate** | **numeric** | **YES** | **0** | |

**Propósito:** Gastos operativos individuales por orden

**Columna nueva (2026-02-06):** `f_commission_rate` almacena el snapshot del porcentaje de comisión del vendedor al momento de crear/editar el gasto. Se usa para calcular el monto final con comisión incluida.

**FK:** `f_order` → `t_order(f_order)`

**Trigger asociado:** `trg_recalcular_gasto_operativo` - Al insertar/editar/eliminar, recalcula `t_order.gasto_operativo` como `SUM(monto * (1 + f_commission_rate/100))`

---

### order_gastos_indirectos

| Columna | Tipo | Nullable | Default | PK |
|---------|------|----------|---------|:--:|
| id | integer | NO | sequence | ✓ |
| f_order | integer | NO | - | |
| monto | numeric | NO | - | |
| descripcion | varchar | NO | - | |
| fecha_gasto | timestamp | YES | CURRENT_TIMESTAMP | |
| created_at | timestamp | YES | CURRENT_TIMESTAMP | |
| created_by | integer | YES | - | |
| updated_at | timestamp | YES | - | |
| updated_by | integer | YES | - | |

**Propósito:** Gastos indirectos individuales por orden (SIN comisión)

**FK:** `f_order` → `t_order(f_order)`

---

## Constraints CHECK y UNIQUE (Sección 10)

_Pegar resultado de Sección 10 aquí_

```
| tabla | nombre | tipo | condicion |
|-------|--------|------|-----------|
```

---

## Enums (Sección 9)

_Pegar resultado de Sección 9 aquí (si hay)_

```
| nombre_tipo | valor |
|-------------|-------|
```
