# Funciones y Triggers - IMA Mecatrónica

**Fecha de extracción:** _Pendiente_

---

## Triggers Activos (Sección 5)

_Pegar resultado de Sección 5 aquí_

```
| tabla | nombre_trigger | evento | momento | accion |
|-------|----------------|--------|---------|--------|
```

---

## Lista de Funciones (Sección 6)

_Pegar resultado de Sección 6 aquí_

```
| nombre_funcion | argumentos | retorna | volatilidad | comentario |
|----------------|------------|---------|-------------|------------|
```

---

## Código Fuente de Funciones (Sección 7)

_Pegar resultado de Sección 7 aquí_

---

## Detalle de Funciones Conocidas

### fn_log_vendor_removal()

**Propósito:** Registra en el historial cuando se elimina una comisión (vendedor removido de orden)

**Trigger asociado:** `trg_before_commission_delete`

**Tabla afectada:** `t_vendor_commission_payment`

**Evento:** BEFORE DELETE

```sql
-- Ver código completo en Sección 7
```

---

### delete_order_with_audit()

**Propósito:** Elimina una orden con validaciones y auditoría

**Tipo:** Función invocable (no trigger)

**Parámetros:**
- `p_order_id` INTEGER
- `p_deleted_by` INTEGER
- `p_reason` TEXT (default: 'Orden creada por error')

**Retorna:** TABLE(success, message, deleted_order_id)

**Validaciones:**
1. Orden existe
2. No tiene facturas
3. No tiene gastos
4. No tiene comisiones

---

### can_delete_order()

**Propósito:** Verifica si una orden puede ser eliminada

**Tipo:** Función invocable (no trigger)

**Parámetros:**
- `p_order_id` INTEGER

**Retorna:** TABLE(can_delete, reason, invoice_count, expense_count, commission_count)

---

## Triggers por Tabla

### t_vendor_commission_payment

| Trigger | Evento | Función |
|---------|--------|---------|
| `trg_before_commission_delete` | BEFORE DELETE | `fn_log_vendor_removal()` |

### t_order

| Trigger | Evento | Función |
|---------|--------|---------|
| _pendiente de verificar_ | | |

---

## Funciones Pendientes de Crear (v2.0)

Para la extensión se necesitarán:

### Para Semáforo de Balance

```sql
-- Función para calcular color del semáforo
CREATE OR REPLACE FUNCTION get_semaforo_color(
    p_ventas NUMERIC,
    p_nomina NUMERIC,
    p_gasto_fijo NUMERIC
) RETURNS TEXT AS $$
BEGIN
    -- ROJO: ventas = 0
    -- AMARILLO: ventas > (nomina + gasto_fijo) * 1.1
    -- VERDE: ventas > (nomina + gasto_fijo) * 1.1 + 100000
END;
$$ LANGUAGE plpgsql;
```

### Para Cálculo de Gasto Material

```sql
-- Función para calcular gasto material de una orden
CREATE OR REPLACE FUNCTION get_gasto_material(p_order_id INTEGER)
RETURNS NUMERIC AS $$
BEGIN
    -- Suma de t_expense pagados para la orden
END;
$$ LANGUAGE plpgsql;
```

---

## Notas

_Agregar observaciones sobre funciones y triggers_
