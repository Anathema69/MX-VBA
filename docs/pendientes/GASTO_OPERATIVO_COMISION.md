# Incluir Comision del Vendedor en Gasto Operativo

**Estado:** COMPLETADO
**Fecha propuesta:** 2026-02-06
**Fecha implementacion:** 2026-02-06
**Propuesta seleccionada:** Opcion C (Preview desglosado en tiempo real)
**Modulo:** Manejo de Ordenes → Gastos Operativos

---

## 1. RESUMEN

Al insertar o editar un gasto operativo, si la orden tiene un vendedor con comision (`f_commission_rate`), el sistema suma automaticamente la comision al monto antes de guardarlo:

```
monto_guardado = monto_base + (monto_base * commission_rate / 100)
```

### Ejemplo
- Vendedor con comision 5%
- Monto base ingresado: $1,000.00
- Comision calculada: $50.00
- **Monto guardado en BD:** $1,050.00

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
- Al confirmar (check), el monto con comision se guarda en memoria
- Preview se oculta y campo se limpia

### 3.2 Editar gasto existente (inline)
- Al entrar en modo edicion (lapiz), el preview aparece con desglose
- El TextBox muestra el valor almacenado; el usuario escribe el nuevo monto base
- Al confirmar (check), se aplica comision y se actualiza el objeto en memoria
- Preview identico al de insercion

### 3.3 Auto-commit en GUARDAR CAMBIOS
- Si el usuario esta editando inline y presiona "GUARDAR CAMBIOS" sin confirmar con check:
  - El sistema auto-confirma la edicion pendiente (aplicando comision)
  - Luego continua con el guardado normal
- Aplica tanto para gastos operativos como indirectos

### 3.4 Gastos indirectos
- NO se aplica comision a gastos indirectos
- El auto-commit de edicion inline SI funciona para indirectos (guarda el valor tal cual)

---

## 4. ARCHIVOS MODIFICADOS

| Archivo | Cambio |
|---------|--------|
| `Views/EditOrderWindow.xaml` | Preview panel en DataTemplate (nuevo gasto + edicion inline) |
| `Views/EditOrderWindow.xaml.cs` | Logica de comision en `SaveNewGastoButton_Click_Internal`, `SaveInlineEdit`, auto-commit en `SaveButton_Click`, handlers `InlineMontoEdit_TextChanged` y `UpdateInlinePreviewValues` |
| `Views/OrdersManagementWindow.xaml` | Binding directo a `GastoOperativo` (sin doble calculo) |
| `Views/OrdersManagementWindow.xaml.cs` | Restaurado `EditButton_Click` original (removida redireccion a ventana de prueba) |
| `Models/OrderViewModel.cs` | Removidas propiedades `GastoOperativoConComision` y `GastoOperativoConComisionFormatted` (causaban doble-comision). Tooltip muestra "incluye comision X%" |
| `Services/OrderExtensions.cs` | `CommissionRate` mapeado en `ToViewModel()` |

### Archivos eliminados
- `Views/GastoOperativoTestWindow.xaml` (ventana de prueba temporal)
- `Views/GastoOperativoTestWindow.xaml.cs`

### Archivos SIN cambios (cadena posterior funciona identica)
- `Services/Orders/OrderService.cs` - AddGastoOperativo, UpdateGastoOperativo, RecalcularGastoOperativo
- BD: tablas `order_gastos_operativos`, `t_order`
- BD: vistas `v_order_gastos`, `v_balance_completo`
- BD: NO hay triggers involucrados

---

## 5. BUG CORREGIDO: Doble comision en refresh

**Problema:** Al guardar un gasto con comision y luego refrescar la grilla, el monto aumentaba de nuevo.
**Causa:** `GastoOperativoConComision` en `OrderViewModel` recalculaba comision sobre un valor que ya la incluia.
**Solucion:** Se elimino la propiedad calculada. El DataGrid ahora muestra `GastoOperativo` directamente (que ya incluye comision desde BD).

---

## 6. PENDIENTE: Migracion de datos historicos

Existen 4 registros en `order_gastos_operativos` creados antes de esta funcionalidad que NO incluyen comision:
- IDs: 31, 33, 34, 35

Script de migracion preparado pero **pendiente de confirmacion del cliente** antes de ejecutar:

```sql
UPDATE order_gastos_operativos ogo
SET monto = monto + (monto * o.f_commission_rate / 100)
FROM t_order o
WHERE ogo.f_order = o.f_order
  AND o.f_commission_rate > 0
  AND ogo.created_at < '2026-02-06';
```

---

## 7. CHECKLIST DE IMPLEMENTACION

- [x] Propuesta UX seleccionada: Opcion C (Preview en tiempo real)
- [x] Comision aplicada en `SaveNewGastoButton_Click_Internal()`
- [x] Comision aplicada en `SaveInlineEdit()`
- [x] Preview desglosado para nuevo gasto (XAML + TextChanged handler)
- [x] Preview desglosado para edicion inline (mismo diseno que nuevo)
- [x] Auto-commit de edicion inline pendiente en `SaveButton_Click`
- [x] Corregido bug de doble-comision en refresh
- [x] Restaurado `EditButton_Click` en OrdersManagementWindow
- [x] Eliminada ventana de prueba GastoOperativoTestWindow
- [ ] Migracion de datos historicos (pendiente confirmacion cliente)
