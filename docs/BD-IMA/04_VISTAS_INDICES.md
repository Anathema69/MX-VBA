# Vistas e Índices - IMA Mecatrónica

**Fecha de extracción:** _Pendiente_

---

## Vistas de BD (Sección 8)

_Pegar resultado de Sección 8 aquí_

```
| nombre_vista | definicion |
|--------------|------------|
```

**Nota:** Si no hay vistas, el resultado estará vacío.

---

## Índices (Sección 4)

_Pegar resultado de Sección 4 aquí_

```
| tabla | nombre_indice | definicion |
|-------|---------------|------------|
```

---

## Índices por Tabla

### users

| Índice | Columnas | Tipo | Propósito |
|--------|----------|------|-----------|
| _pendiente_ |

### t_order

| Índice | Columnas | Tipo | Propósito |
|--------|----------|------|-----------|
| _pendiente_ |

### t_client

| Índice | Columnas | Tipo | Propósito |
|--------|----------|------|-----------|
| _pendiente_ |

### t_invoice

| Índice | Columnas | Tipo | Propósito |
|--------|----------|------|-----------|
| _pendiente_ |

### t_expense

| Índice | Columnas | Tipo | Propósito |
|--------|----------|------|-----------|
| _pendiente_ |

### t_order_deleted

| Índice | Columnas | Tipo | Propósito |
|--------|----------|------|-----------|
| `idx_order_deleted_original_id` | original_order_id | btree | Búsqueda por ID original |
| `idx_order_deleted_po` | f_po | btree | Búsqueda por PO |
| `idx_order_deleted_date` | deleted_at | btree | Búsqueda por fecha |

---

## Índices Recomendados para v2.0

### Para nuevas consultas de Balance

```sql
-- Índice para consultas de gastos por mes
CREATE INDEX IF NOT EXISTS idx_expense_date_status
ON t_expense(f_expensedate, f_status);

-- Índice para consultas de facturas por mes
CREATE INDEX IF NOT EXISTS idx_invoice_payment_date
ON t_invoice(f_paymentdate) WHERE f_invoicestat = 4;
```

### Para nuevas columnas en t_order

```sql
-- Índice para filtros por días estimados
CREATE INDEX IF NOT EXISTS idx_order_dias_estimados
ON t_order(dias_estimados) WHERE dias_estimados IS NOT NULL;
```

---

## Vistas Recomendadas para v2.0

### Vista de Balance Mensual

```sql
CREATE OR REPLACE VIEW v_balance_mensual AS
SELECT
    DATE_TRUNC('month', fecha) as mes,
    SUM(ingresos) as total_ingresos,
    SUM(gastos) as total_gastos,
    -- ... más campos
FROM ...
GROUP BY DATE_TRUNC('month', fecha);
```

### Vista de Órdenes con Gastos

```sql
CREATE OR REPLACE VIEW v_order_gastos AS
SELECT
    o.f_order,
    o.f_po,
    o.f_salesubtotal,
    COALESCE(SUM(e.f_totalexpense), 0) as gasto_material,
    o.gasto_operativo,
    o.gasto_indirecto,
    o.dias_estimados
FROM t_order o
LEFT JOIN t_expense e ON o.f_order = e.f_order AND e.f_status = 'PAGADO'
GROUP BY o.f_order;
```

---

## Análisis de Performance

### Tablas sin índices (revisar)

_Listar tablas que podrían beneficiarse de índices_

### Índices no utilizados

_Pendiente de análisis con pg_stat_user_indexes_

---

## Notas

_Agregar observaciones sobre índices y performance_
