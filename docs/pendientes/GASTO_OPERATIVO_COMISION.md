# Incluir Comision del Vendedor en Gasto Operativo

**Estado:** COMPLETADO
**Fecha propuesta:** 2026-02-06
**Fecha implementacion:** 2026-02-06
**Correccion de formula:** 2026-02-09
**Modulo:** Manejo de Ordenes → Gastos Operativos

---

## 1. RESUMEN

El gasto operativo de una orden se calcula como la suma de los gastos individuales **mas** el monto de comision del vendedor (desde `t_vendor_commission_payment`). El calculo se realiza mediante **triggers en BD**.

### Arquitectura de almacenamiento

```
order_gastos_operativos:
  monto = monto BASE (lo que el usuario escribe)

t_vendor_commission_payment:
  commission_amount = f_salesubtotal * commission_rate / 100
  → Calculado automaticamente por triggers de comision

t_order.gasto_operativo = SUM(order_gastos_operativos.monto) + SUM(t_vendor_commission_payment.commission_amount)
  → Calculado automaticamente por triggers
```

### Ejemplo
- Orden con subtotal $10,000, vendedor con comision 5%
- Gastos operativos: $500 + $300 = $800
- Comision vendedor: $10,000 * 5% = $500
- **t_order.gasto_operativo = $800 + $500 = $1,300**

### Historial de formulas
- **v2.0.3 (2026-02-06):** `SUM(monto * (1 + f_commission_rate/100))` — comision por gasto individual
- **v2.0.4 (2026-02-09):** `SUM(monto) + SUM(commission_amount)` — gastos base + comision de vendedor

---

## 2. PROPUESTA UX IMPLEMENTADA

### Subtotal + Linea informativa de comision

El header de gastos operativos muestra "Subtotal" (suma de gastos base). Debajo aparece una linea informativa cuando hay comision:

```
Subtotal:  $800.00
+ Comisión vendedor (5%): $500.00  =  Total en columna: $1,300.00
```

- Texto gris (#888), FontSize 10, debajo del subtotal
- Solo visible cuando `f_commission_rate > 0` y `f_salesubtotal > 0`
- Explica por que el total en la columna de ordenes difiere de la suma visible de gastos

---

## 3. COMPORTAMIENTOS IMPLEMENTADOS

### 3.1 Insertar nuevo gasto operativo
- Usuario escribe monto base y descripcion
- Al confirmar (check), se guarda el monto BASE en memoria (ID negativo = nuevo)
- Subtotal se actualiza con linea informativa de comision

### 3.2 Editar gasto existente (inline)
- Al entrar en modo edicion (lapiz), aparece TextBox con monto base
- Al confirmar (check), se actualiza el monto BASE en memoria
- Subtotal se recalcula automaticamente

### 3.3 Auto-commit en GUARDAR CAMBIOS
- Si el usuario esta editando inline y presiona "GUARDAR CAMBIOS" sin confirmar con check:
  - El sistema auto-confirma la edicion pendiente
  - Luego continua con el guardado normal
- Aplica tanto para gastos operativos como indirectos

### 3.4 Gastos indirectos
- NO se aplica comision a gastos indirectos
- El auto-commit de edicion inline SI funciona para indirectos (guarda el valor tal cual)

### 3.5 Acceso por rol
- **direccion**: acceso completo a gastos operativos, indirectos y materiales
- **administracion**: mismo acceso que direccion (agregado 2026-02-06)
- Otros roles: no ven columnas de gastos

---

## 4. TRIGGERS EN BD

### 4.1 trg_recalcular_gasto_operativo

**Tabla:** `order_gastos_operativos`
**Momento:** AFTER INSERT, UPDATE, DELETE
**Funcion:** `recalcular_gasto_operativo()`

Calcula automaticamente `t_order.gasto_operativo` como la suma de gastos base + comision del vendedor:

```sql
CREATE OR REPLACE FUNCTION recalcular_gasto_operativo()
RETURNS TRIGGER AS $$
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
$$ LANGUAGE plpgsql;
```

### 4.2 trg_recalcular_gasto_op_por_comision

**Tabla:** `t_vendor_commission_payment`
**Momento:** AFTER INSERT, UPDATE, DELETE
**Funcion:** `recalcular_gasto_operativo_por_comision()`

Cuando cambia `commission_amount` (por cambio de vendedor, rate o subtotal), recalcula el gasto operativo con la misma formula:

```sql
CREATE OR REPLACE FUNCTION recalcular_gasto_operativo_por_comision()
RETURNS TRIGGER AS $$
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
$$ LANGUAGE plpgsql;
```

### 4.3 Cadena de triggers

```
Cambio en order_gastos_operativos (INSERT/UPDATE/DELETE)
  → trg_recalcular_gasto_operativo
    → UPDATE t_order.gasto_operativo = SUM(monto) + SUM(commission_amount)

Cambio en t_vendor_commission_payment (INSERT/UPDATE/DELETE)
  → trg_recalcular_gasto_op_por_comision
    → UPDATE t_order.gasto_operativo = SUM(monto) + SUM(commission_amount)

Cambio en t_order.f_commission_rate (vendedor/comision)
  → sync_commission_rate_from_order (trigger preexistente)
    → UPDATE t_vendor_commission_payment.commission_rate
      → trg_recalcular_gasto_op_por_comision (se dispara por el UPDATE anterior)
        → UPDATE t_order.gasto_operativo
```

### 4.4 Triggers eliminados (2026-02-09)

- `trg_sync_commission_rate` en t_order → ELIMINADO (ya no se propaga rate a gastos individuales)
- `sync_commission_rate_to_gastos()` → ELIMINADA

### 4.5 Auditoria de cambios de comision

Los cambios de `f_commission_rate` quedan registrados en `t_commission_rate_history` (tabla preexistente con triggers propios de comisiones de vendedores).

---

## 5. CAMBIOS EN BD (APLICADOS)

### 5.1 Cambios v2.0.3 (2026-02-06)
```sql
ALTER TABLE order_gastos_operativos ADD COLUMN f_commission_rate numeric DEFAULT 0;
```
- Migracion de datos historicos: 12 registros actualizados con f_commission_rate

### 5.2 Cambios v2.0.4 (2026-02-09) — Correccion de formula
- Funcion `recalcular_gasto_operativo()` modificada: nueva formula `SUM(monto) + SUM(commission_amount)`
- Nueva funcion `recalcular_gasto_operativo_por_comision()` en `t_vendor_commission_payment`
- Nuevo trigger `trg_recalcular_gasto_op_por_comision` en `t_vendor_commission_payment`
- ELIMINADO trigger `trg_sync_commission_rate` en `t_order`
- ELIMINADA funcion `sync_commission_rate_to_gastos()`
- Recalculo masivo de 21 ordenes: todas verificadas OK
- Script: `SistemaGestionProyectos2/sql/fix_gasto_operativo_formula.sql`

---

## 6. ARCHIVOS MODIFICADOS

| Archivo | Cambio |
|---------|--------|
| `Views/EditOrderWindow.xaml` | Header "Subtotal", linea informativa comision, alineacion DataTemplate |
| `Views/EditOrderWindow.xaml.cs` | Suma simple de montos, calculo y display de comision informativa |
| `Views/OrdersManagementWindow.xaml` | Removido boton Exportar |
| `Views/OrdersManagementWindow.xaml.cs` | Acceso admin a columnas gastos y vista v_order_gastos |
| `Views/MainMenuWindow.xaml` | Removido boton tuerca (settings) |
| `Models/Database/OrderGastoOperativoDb.cs` | Removida propiedad `CommissionRate` (ya no se usa por gasto) |
| `Services/Orders/OrderService.cs` | Eliminado `RecalcularGastoOperativo()`, signatures sin commissionRate |
| `Services/SupabaseService.cs` | Signatures simplificadas sin commissionRate |

### Cambios eliminados del codigo C# (v2.0.4)
- `CommissionRate` en OrderGastoOperativoDb.cs → ya no se guarda rate por gasto
- Preview desglosado (ComisionPreviewBorder, InlineComisionPreviewBorder) → reemplazado por linea informativa
- Handlers de TextChanged para preview → eliminados
- commissionRate en parametros de Add/Update GastoOperativo → eliminado

### Cambios en BD (v2.0.4)
- Funcion `recalcular_gasto_operativo()` modificada con nueva formula
- Nueva funcion `recalcular_gasto_operativo_por_comision()`
- Nuevo trigger `trg_recalcular_gasto_op_por_comision` en `t_vendor_commission_payment`
- ELIMINADO trigger `trg_sync_commission_rate` en `t_order`
- ELIMINADA funcion `sync_commission_rate_to_gastos()`

---

## 7. BUGS CORREGIDOS

### 7.1 Doble comision en refresh (v2.0.3)
**Problema:** Al guardar un gasto con comision y luego refrescar la grilla, el monto aumentaba de nuevo.
**Causa:** `GastoOperativoConComision` en `OrderViewModel` recalculaba comision sobre un valor que ya la incluia.
**Solucion:** Se elimino la propiedad calculada. El DataGrid ahora muestra `GastoOperativo` directamente.

### 7.2 Gastos no cargaban para admin (v2.0.3)
**Problema:** Al abrir la ventana de edicion como administracion, los gastos existentes no se mostraban.
**Causa:** La carga de datos desde BD estaba restringida solo al rol "direccion" en 3 puntos del codigo.
**Solucion:** Se agrego verificacion para "administracion" en las 3 condiciones de carga.

---

## 8. CHECKLIST DE IMPLEMENTACION

- [x] Trigger trg_recalcular_gasto_operativo con nueva formula (SUM + commission)
- [x] Trigger trg_recalcular_gasto_op_por_comision en t_vendor_commission_payment
- [x] Eliminado trigger trg_sync_commission_rate y funcion sync_commission_rate_to_gastos
- [x] RecalcularGastoOperativo eliminado de C#
- [x] CommissionRate eliminado de OrderGastoOperativoDb
- [x] Parametro commissionRate eliminado de Add/UpdateGastoOperativo
- [x] Preview desglosado eliminado, reemplazado por linea informativa
- [x] Header cambiado a "Subtotal" con info de comision debajo
- [x] Auto-commit de edicion inline pendiente en SaveButton_Click
- [x] Acceso admin a gastos (3 columnas + edicion)
- [x] Migracion BD: 21 ordenes recalculadas, todas OK
- [x] Alineacion corregida en DataTemplate de gastos
