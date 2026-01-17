# Flujo de Comisiones de Vendedores

## Resumen General

Este documento describe el flujo completo del sistema de comisiones para vendedores, desde la creación de una orden hasta el pago de la comisión.

---

## 1. Tablas Involucradas

### `t_vendor` - Vendedores
| Campo | Descripción |
|-------|-------------|
| `f_vendor` | ID del vendedor (PK) |
| `f_vendorname` | Nombre del vendedor |
| `f_commission_rate` | **Tasa de comisión por defecto** (%) |
| `f_user_id` | Usuario asociado (para login) |
| `is_active` | Estado activo/inactivo |

### `t_order` - Órdenes
| Campo | Descripción |
|-------|-------------|
| `f_order` | ID de la orden (PK) |
| `f_salesman` | ID del vendedor asignado (FK → t_vendor) |
| `f_commission_rate` | **Tasa de comisión para esta orden específica** |
| `f_salesubtotal` | Subtotal de venta (base para calcular comisión) |
| `f_orderstat` | Estado de la orden |

### `t_vendor_commission_payment` - Pagos de Comisiones
| Campo | Descripción |
|-------|-------------|
| `id` | ID del registro (PK) |
| `f_order` | ID de la orden (FK) |
| `f_vendor` | ID del vendedor (FK) |
| `commission_rate` | Tasa de comisión usada |
| `commission_amount` | Monto calculado de la comisión |
| `payment_status` | Estado: `draft`, `pending`, `paid` |
| `payment_date` | Fecha de pago (cuando aplica) |

### `t_commission_rate_history` - Historial de Cambios (Auditoría)
| Campo | Descripción |
|-------|-------------|
| `id` | ID del registro (PK) |
| `order_id` | ID de la orden |
| `vendor_id` | ID del vendedor |
| `old_rate` / `new_rate` | Tasas anterior y nueva |
| `old_amount` / `new_amount` | Montos anterior y nuevo |
| `changed_by` / `changed_by_name` | Usuario que realizó el cambio |
| `changed_at` | Fecha/hora del cambio |
| `change_reason` | Motivo del cambio |

---

## 2. Estados de Comisión

```
┌─────────┐     ┌───────────┐     ┌────────┐
│  DRAFT  │ ──▶ │  PENDING  │ ──▶ │  PAID  │
└─────────┘     └───────────┘     └────────┘
   Futuras       Por Pagar         Pagadas
```

| Estado | Descripción | Editable |
|--------|-------------|----------|
| `draft` | Comisión futura, orden aún no facturada | ✅ Sí |
| `pending` | Comisión por pagar, orden facturada | ✅ Sí |
| `paid` | Comisión ya pagada al vendedor | ❌ No |

---

## 3. Flujo Completo

### 3.1 Creación de Orden con Vendedor

```
┌─────────────────────────────────────────────────────────────────┐
│                    NUEVA ORDEN (NewOrderWindow)                  │
├─────────────────────────────────────────────────────────────────┤
│ 1. Usuario selecciona vendedor del ComboBox                     │
│ 2. Sistema obtiene CommissionRate del vendedor (t_vendor)       │
│    └─ Si no tiene, usa 10% por defecto                          │
│ 3. Se crea orden en t_order con:                                │
│    └─ f_salesman = ID del vendedor                              │
│    └─ f_commission_rate = tasa del vendedor                     │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│              TRIGGER en Base de Datos (automático)              │
├─────────────────────────────────────────────────────────────────┤
│ Al insertar orden con f_salesman != NULL:                       │
│ 1. Calcular: commission_amount = f_salesubtotal × rate / 100    │
│ 2. INSERT en t_vendor_commission_payment:                       │
│    └─ f_order = nueva orden                                     │
│    └─ f_vendor = f_salesman                                     │
│    └─ commission_rate = f_commission_rate                       │
│    └─ commission_amount = monto calculado                       │
│    └─ payment_status = 'draft' (por defecto)                    │
└─────────────────────────────────────────────────────────────────┘
```

### 3.2 Transición de Estados

```
┌─────────────────────────────────────────────────────────────────┐
│                     DRAFT → PENDING                              │
├─────────────────────────────────────────────────────────────────┤
│ Disparador: Orden facturada (tiene registro en t_invoice)       │
│                                                                  │
│ Trigger/Proceso:                                                 │
│ - Cuando se registra factura para la orden                      │
│ - UPDATE t_vendor_commission_payment                             │
│   SET payment_status = 'pending'                                 │
│   WHERE f_order = {orden_facturada}                              │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                     PENDING → PAID                               │
├─────────────────────────────────────────────────────────────────┤
│ Disparador: Usuario marca como pagada (VendorCommissionsWindow) │
│                                                                  │
│ Acción manual desde UI:                                          │
│ - Botón "Marcar Pagado" en cada comisión                        │
│ - Botón "Pagar Todas" para lote                                  │
│                                                                  │
│ UPDATE t_vendor_commission_payment                               │
│ SET payment_status = 'paid',                                     │
│     payment_date = NOW(),                                        │
│     updated_by = {usuario_actual}                                │
│ WHERE id = {commission_id}                                       │
└─────────────────────────────────────────────────────────────────┘
```

---

## 4. Edición de Tasa de Comisión

### 4.1 Cuándo se puede editar

| Condición | Puede editar |
|-----------|--------------|
| `payment_status = 'draft'` | ✅ Sí |
| `payment_status = 'pending'` | ✅ Sí |
| `payment_status = 'paid'` | ❌ No |

### 4.2 Dónde se edita

**Ventana:** `VendorCommissionsWindow` (Gestión de Comisiones)

**Acceso:** Menú Principal → Portal de Vendedores → Gestión de Comisiones

### 4.3 Cómo editar

1. Seleccionar vendedor de la lista izquierda
2. En la lista de comisiones, hacer **doble clic** en el campo "TASA COMISIÓN"
3. Ingresar nuevo porcentaje (0-100)
4. Presionar **Enter** para guardar o **Escape** para cancelar

### 4.4 Flujo de guardado con auditoría

```
┌─────────────────────────────────────────────────────────────────┐
│              Usuario edita tasa (doble-clic + Enter)            │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│         1. INSERT en t_commission_rate_history                   │
├─────────────────────────────────────────────────────────────────┤
│ Guarda snapshot completo:                                        │
│ - order_id, vendor_id, commission_payment_id                    │
│ - old_rate, old_amount (valores anteriores)                     │
│ - new_rate, new_amount (valores nuevos)                         │
│ - order_subtotal, order_number, vendor_name (contexto)          │
│ - changed_by, changed_by_name, changed_at (auditoría)           │
│ - change_reason: "Cambio manual de tasa: X% → Y%"               │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│        2. UPDATE t_vendor_commission_payment                     │
├─────────────────────────────────────────────────────────────────┤
│ SET commission_rate = {nueva_tasa}                              │
│     commission_amount = subtotal × nueva_tasa / 100             │
│     updated_by = {usuario_actual}                               │
│     updated_at = NOW()                                          │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│           3. UPDATE t_order (sincronización)                     │
├─────────────────────────────────────────────────────────────────┤
│ SET f_commission_rate = {nueva_tasa}                            │
│     updated_by = {usuario_actual}                               │
│     updated_at = NOW()                                          │
│                                                                  │
│ Mantiene consistencia entre tablas                              │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│              Notificación: "✓ Tasa actualizada: X% → Y%"        │
└─────────────────────────────────────────────────────────────────┘
```

---

## 5. Fórmula de Cálculo

```
Comisión = Subtotal de Venta × Tasa de Comisión / 100

Ejemplo:
- Subtotal: $74,000.00
- Tasa: 13%
- Comisión = $74,000 × 13 / 100 = $9,620.00
```

---

## 6. Vistas del Sistema

### 6.1 VendorDashboard (Portal del Vendedor)
- **Acceso:** Usuarios con rol `salesperson`
- **Funcionalidad:** Solo visualización de sus propias comisiones
- **Estados mostrados:** draft, pending, paid

### 6.2 VendorCommissionsWindow (Gestión de Comisiones)
- **Acceso:** Usuarios con rol `admin` o `coordinator`
- **Funcionalidad:**
  - Ver comisiones de todos los vendedores
  - Editar tasa de comisión (draft/pending)
  - Marcar como pagadas
  - Pagar todas las pendientes de un vendedor

---

## 7. Consultas Útiles

### Ver historial de cambios de una orden
```sql
SELECT
    order_number,
    vendor_name,
    old_rate || '% → ' || new_rate || '%' as cambio,
    old_amount || ' → ' || new_amount as monto,
    changed_by_name,
    changed_at,
    change_reason
FROM t_commission_rate_history
WHERE order_id = {ID_ORDEN}
ORDER BY changed_at DESC;
```

### Ver comisiones pendientes por vendedor
```sql
SELECT
    v.f_vendorname,
    COUNT(*) as cantidad,
    SUM(c.commission_amount) as total_pendiente
FROM t_vendor_commission_payment c
JOIN t_vendor v ON c.f_vendor = v.f_vendor
WHERE c.payment_status = 'pending'
GROUP BY v.f_vendorname
ORDER BY total_pendiente DESC;
```

### Ver comisiones de un mes específico
```sql
SELECT
    o.f_po as orden,
    v.f_vendorname as vendedor,
    c.commission_rate as tasa,
    c.commission_amount as monto,
    c.payment_status as estado
FROM t_vendor_commission_payment c
JOIN t_order o ON c.f_order = o.f_order
JOIN t_vendor v ON c.f_vendor = v.f_vendor
WHERE DATE_TRUNC('month', o.f_podate) = '2025-01-01'
ORDER BY o.f_podate;
```

---

## 8. Notas Importantes

1. **Tasa por defecto:** Si un vendedor no tiene `f_commission_rate` definido, se usa 10% por defecto.

2. **Orden sin vendedor:** Si se crea una orden sin vendedor (`f_salesman = NULL`), no se genera registro de comisión.

3. **Cambio de vendedor:** Si se cambia el vendedor de una orden existente, se debe manejar manualmente el registro de comisión.

4. **Auditoría:** Todos los cambios de tasa quedan registrados en `t_commission_rate_history` con el usuario que realizó el cambio.

5. **Comisiones pagadas:** Una vez que una comisión está en estado `paid`, no se puede editar la tasa.

---

*Última actualización: Diciembre 2025*
