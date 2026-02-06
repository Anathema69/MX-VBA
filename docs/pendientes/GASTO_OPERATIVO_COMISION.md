# Incluir Comision del Vendedor en Gasto Operativo

**Estado:** COMPLETADO
**Fecha propuesta:** 2026-02-06
**Fecha implementacion:** 2026-02-06
**Propuesta seleccionada:** Opcion C (Preview desglosado en tiempo real)
**Modulo:** Manejo de Ordenes → Gastos Operativos

---

## 1. RESUMEN

Al insertar o editar un gasto operativo, si la orden tiene un vendedor con comision (`f_commission_rate`), el sistema muestra un preview desglosado en tiempo real. En la BD se almacena el **monto base** (lo que escribe el usuario) junto con el **porcentaje de comision** como snapshot. El calculo del monto final se realiza mediante **triggers en BD**.

### Arquitectura de almacenamiento

```
order_gastos_operativos:
  monto              = monto BASE (lo que el usuario escribe)
  f_commission_rate  = snapshot del % de comision al momento de crear/editar

Calculo del monto final (en trigger):
  monto_final = monto * (1 + f_commission_rate / 100)

t_order.gasto_operativo = SUM(monto * (1 + COALESCE(f_commission_rate, 0) / 100))
  → Calculado automaticamente por trigger trg_recalcular_gasto_operativo
```

### Ejemplo
- Vendedor con comision 5%
- Monto base ingresado: $1,000.00
- **Guardado en BD:** monto=$1,000.00, f_commission_rate=5
- **Monto final calculado por trigger:** $1,050.00
- **t_order.gasto_operativo:** suma de todos los montos finales

---

## 2. PROPUESTA UX IMPLEMENTADA: Opcion C (Preview en tiempo real)

Al escribir el monto, aparece un panel con desglose visual:

```
┌─────────────────────────────────┐
│ Base:           $1,000.00       │
│ Comision (5%):  $50.00          │
│ ─────────────────────────       │
│ Total:          $1,050.00       │
└─────────────────────────────────┘
```

- Fondo amarillo claro (`#FFF8E1`) con borde redondeado (`#FFE0B2`)
- "Base" en gris/negro, "Comision" en naranja semibold, "Total" en verde bold
- Se muestra tanto al insertar nuevo gasto como al editar existente (mismo diseno)
- Si el vendedor no tiene comision, el panel no aparece

---

## 3. COMPORTAMIENTOS IMPLEMENTADOS

### 3.1 Insertar nuevo gasto operativo
- Usuario escribe monto base → preview se actualiza en tiempo real
- Al confirmar (check), se guarda el monto BASE + commission rate en memoria
- Preview se oculta y campo se limpia

### 3.2 Editar gasto existente (inline)
- Al entrar en modo edicion (lapiz), el preview aparece con desglose
- El TextBox muestra el monto base; el usuario escribe el nuevo monto base
- Al confirmar (check), se actualiza el monto BASE y el rate en el objeto en memoria
- Preview identico al de insercion (Border con StackPanel, mismo diseno visual)

### 3.3 Auto-commit en GUARDAR CAMBIOS
- Si el usuario esta editando inline y presiona "GUARDAR CAMBIOS" sin confirmar con check:
  - El sistema auto-confirma la edicion pendiente (aplicando comision)
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

### 4.1 trg_recalcular_gasto_operativo (NUEVO)

**Tabla:** `order_gastos_operativos`
**Momento:** AFTER INSERT, UPDATE, DELETE
**Funcion:** `recalcular_gasto_operativo()`

Calcula automaticamente `t_order.gasto_operativo` como la suma de todos los gastos operativos con comision aplicada:

```sql
CREATE OR REPLACE FUNCTION recalcular_gasto_operativo()
RETURNS TRIGGER AS $$
BEGIN
    UPDATE t_order
    SET gasto_operativo = (
        SELECT COALESCE(SUM(monto * (1 + COALESCE(f_commission_rate, 0) / 100)), 0)
        FROM order_gastos_operativos
        WHERE f_order = COALESCE(NEW.f_order, OLD.f_order)
    )
    WHERE f_order = COALESCE(NEW.f_order, OLD.f_order);
    RETURN COALESCE(NEW, OLD);
END;
$$ LANGUAGE plpgsql;
```

**Reemplaza:** `RecalcularGastoOperativo()` de `OrderService.cs` (eliminado del codigo C#).

### 4.2 trg_sync_commission_rate (NUEVO)

**Tabla:** `t_order`
**Momento:** AFTER UPDATE OF f_commission_rate
**Funcion:** `sync_commission_rate_to_gastos()`

Cuando cambia el `f_commission_rate` de una orden (cambio de vendedor, cambio de %), propaga el nuevo rate a todos los gastos operativos de esa orden. Esto a su vez dispara `trg_recalcular_gasto_operativo` que recalcula la suma.

```sql
CREATE OR REPLACE FUNCTION sync_commission_rate_to_gastos()
RETURNS TRIGGER AS $$
BEGIN
    IF OLD.f_commission_rate IS DISTINCT FROM NEW.f_commission_rate THEN
        UPDATE order_gastos_operativos
        SET f_commission_rate = COALESCE(NEW.f_commission_rate, 0)
        WHERE f_order = NEW.f_order;
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;
```

### 4.3 Cadena de triggers

```
Cambio en order_gastos_operativos (INSERT/UPDATE/DELETE)
  → trg_recalcular_gasto_operativo
    → UPDATE t_order.gasto_operativo = SUM(monto * (1 + rate/100))

Cambio en t_order.f_commission_rate (vendedor/comision)
  → trg_sync_commission_rate
    → UPDATE order_gastos_operativos.f_commission_rate (todos los gastos)
      → trg_recalcular_gasto_operativo (se dispara por el UPDATE anterior)
        → UPDATE t_order.gasto_operativo
```

### 4.4 Auditoria de cambios de comision

Los cambios de `f_commission_rate` quedan registrados en `t_commission_rate_history` (tabla preexistente con triggers propios de comisiones de vendedores).

---

## 5. CAMBIOS EN BD (APLICADOS)

### 5.1 ALTER TABLE
```sql
ALTER TABLE order_gastos_operativos ADD COLUMN f_commission_rate numeric DEFAULT 0;
```

### 5.2 Migracion de datos historicos (COMPLETADA)
Todos los registros existentes fueron actualizados con el `f_commission_rate` de su orden:
```sql
UPDATE order_gastos_operativos g
SET f_commission_rate = COALESCE(o.f_commission_rate, 0)
FROM t_order o
WHERE g.f_order = o.f_order;
```

12 registros actualizados correctamente. Verificado que `t_order.gasto_operativo` coincide con la suma calculada por trigger para todas las ordenes.

---

## 6. ARCHIVOS MODIFICADOS

| Archivo | Cambio |
|---------|--------|
| `Views/EditOrderWindow.xaml` | Preview panel en DataTemplate (nuevo gasto + edicion inline), ventana redimensionable |
| `Views/EditOrderWindow.xaml.cs` | Logica de comision, auto-commit, handlers preview inline, acceso admin a gastos |
| `Views/OrdersManagementWindow.xaml` | Removido boton Exportar |
| `Views/OrdersManagementWindow.xaml.cs` | Acceso admin a columnas gastos y vista v_order_gastos |
| `Views/MainMenuWindow.xaml` | Removido boton tuerca (settings) |
| `Models/Database/OrderGastoOperativoDb.cs` | Propiedad `CommissionRate` para nueva columna BD |
| `Services/Orders/OrderService.cs` | Eliminado `RecalcularGastoOperativo()`, signatures con commissionRate |
| `Services/SupabaseService.cs` | Signatures actualizadas con commissionRate |

### Cambios eliminados del codigo C#
- `RecalcularGastoOperativo()` en OrderService.cs → reemplazado por trigger BD
- Calculo de monto con comision antes de guardar → ahora se guarda monto base

### Cambios en BD
- Nueva columna `f_commission_rate` en `order_gastos_operativos`
- Nuevo trigger `trg_recalcular_gasto_operativo` en `order_gastos_operativos`
- Nuevo trigger `trg_sync_commission_rate` en `t_order`
- Nuevas funciones `recalcular_gasto_operativo()` y `sync_commission_rate_to_gastos()`

---

## 7. BUG CORREGIDO: Doble comision en refresh

**Problema:** Al guardar un gasto con comision y luego refrescar la grilla, el monto aumentaba de nuevo.
**Causa:** `GastoOperativoConComision` en `OrderViewModel` recalculaba comision sobre un valor que ya la incluia.
**Solucion:** Se elimino la propiedad calculada. El DataGrid ahora muestra `GastoOperativo` directamente.

---

## 8. BUG CORREGIDO: Gastos no cargaban para admin

**Problema:** Al abrir la ventana de edicion como administracion, los gastos existentes no se mostraban.
**Causa:** La carga de datos desde BD estaba restringida solo al rol "direccion" en 3 puntos del codigo.
**Solucion:** Se agrego verificacion para "administracion" en las 3 condiciones de carga.

---

## 9. CHECKLIST DE IMPLEMENTACION

- [x] Propuesta UX seleccionada: Opcion C (Preview en tiempo real)
- [x] Comision aplicada en `SaveNewGastoButton_Click_Internal()`
- [x] Comision aplicada en `SaveInlineEdit()`
- [x] Preview desglosado para nuevo gasto (XAML + TextChanged handler)
- [x] Preview desglosado para edicion inline (mismo diseno que nuevo)
- [x] Auto-commit de edicion inline pendiente en `SaveButton_Click`
- [x] Corregido bug de doble-comision en refresh
- [x] Corregido bug gastos no cargan para admin
- [x] Restaurado `EditButton_Click` en OrdersManagementWindow
- [x] Eliminada ventana de prueba GastoOperativoTestWindow
- [x] Nueva columna f_commission_rate en order_gastos_operativos
- [x] Trigger trg_recalcular_gasto_operativo creado
- [x] Trigger trg_sync_commission_rate creado
- [x] RecalcularGastoOperativo eliminado de C#
- [x] Migracion de datos historicos completada
- [x] Acceso admin a gastos (3 columnas + edicion)
- [x] Removidos botones sin funcion (tuerca, exportar)
