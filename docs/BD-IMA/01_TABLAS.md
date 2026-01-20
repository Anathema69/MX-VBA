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
8. [t_fixed_expenses](#t_fixed_expenses)
9. [t_fixed_expenses_history](#t_fixed_expenses_history)
10. [t_invoice](#t_invoice)
11. [t_order](#t_order)
12. [t_order_deleted](#t_order_deleted)
13. [t_payroll](#t_payroll)
14. [t_payroll_history](#t_payroll_history)
15. [t_supplier](#t_supplier)
16. [t_vendor](#t_vendor)
17. [t_vendor_commission_payment](#t_vendor_commission_payment)
18. [t_commission_rate_history](#t_commission_rate_history)
19. [users](#users)

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
| _pendiente_ |

**Propósito:** Gastos a proveedores (cuentas por pagar)

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
