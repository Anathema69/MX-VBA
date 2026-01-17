# Módulo de Cuentas por Pagar - Cambios Enero 2026

## Resumen General

Se realizaron mejoras significativas al módulo de **Cuentas por Pagar** (SupplierPendingView) para optimizar el flujo de trabajo de registro y gestión de gastos con proveedores. El objetivo principal fue eliminar ventanas modales innecesarias y permitir edición inline directa.

---

## Archivos Modificados

### Archivos Principales
- `Views/SupplierPendingDetailView.xaml` - Vista de detalle de gastos por proveedor
- `Views/SupplierPendingDetailView.xaml.cs` - Lógica de la vista de detalle
- `Views/SupplierPendingView.xaml.cs` - Vista principal (ajustes menores)

### Archivos Eliminados
- `Views/QuickExpenseDialog.xaml` / `.cs` - Diálogo rápido de gastos (reemplazado por inline)
- `Views/NewExpenseDialog.xaml` / `.cs` - Diálogo de nuevo gasto (reemplazado por inline)
- `Views/EditExpenseDialog.xaml` / `.cs` - Diálogo de edición (reemplazado por inline)

---

## Cambios Implementados

### 1. Vista Adaptativa con Selector de Proveedor

**Descripción:** La ventana de detalle ahora tiene dos modos de operación:

1. **Modo con proveedor preseleccionado:** Se abre desde la vista principal al hacer clic en "Ver detalle" de un proveedor específico.

2. **Modo selector:** Se abre desde el botón "Nuevo Gasto" sin proveedor preseleccionado, mostrando un ComboBox para seleccionar el proveedor.

**Comportamiento inteligente:**
- Al seleccionar un proveedor sin gastos, automáticamente se crea una fila inline para agregar el primer gasto
- El ComboBox muestra indicador de gastos pendientes por proveedor: `"Proveedor X (3 pend.)"`

**Constructores:**
```csharp
// Con proveedor preseleccionado
public SupplierPendingDetailView(UserSession currentUser, int supplierId, string supplierName, bool startInCreateMode = false)

// Sin proveedor (modo selector)
public SupplierPendingDetailView(UserSession currentUser)
```

---

### 2. Edición Inline (Sin Modales)

**Descripción:** Se eliminaron todos los diálogos modales para crear/editar gastos. Ahora todo se hace directamente en el DataGrid.

**Características:**
- Nueva fila verde (#D1FAE5) al crear nuevo gasto
- Campos editables: Descripción, Orden (ComboBox), Total, Fecha de Compra
- Fecha de vencimiento se calcula automáticamente según días de crédito
- Botones de acción inline: OK (guardar) y X (cancelar)

**Formas de editar:**
- Doble clic en la fila
- Presionar F2 con la fila seleccionada
- Botón "E" en la columna de acciones

**Atajos de teclado:**
- `Enter` - Guardar cambios
- `Escape` - Cancelar edición

---

### 3. Filtros de Estado (Pills)

**Descripción:** Se agregaron filtros visuales estilo "pills" para filtrar gastos por estado.

**Opciones:**
- **Pendiente** (verde #10B981) - Gastos pendientes de pago
- **Pagado** (azul #3B82F6) - Gastos ya pagados
- **Todos** (morado #6366F1) - Todos los gastos

**Selección automática:**
- Si el proveedor no tiene gastos pendientes pero sí pagados, se selecciona "Todos" automáticamente

---

### 4. Selección de Método de Pago al Marcar como Pagado

**Descripción:** Al presionar el botón de pago ($), se muestra un menú contextual para seleccionar el método de pago.

**Métodos disponibles:**
| Método | Icono |
|--------|-------|
| TRANSFERENCIA | 🏦 |
| EFECTIVO | 💵 |
| CHEQUE | 📄 |
| CRÉDITO | 💳 |
| DÉBITO | 💳 |

**Comportamiento anterior:** Se asignaba automáticamente "TRANSFERENCIA"
**Comportamiento nuevo:** El usuario debe seleccionar obligatoriamente el método usado

---

### 5. Visualización de Fecha de Pago

**Descripción:** Para gastos pagados, la columna "PAGO" muestra:
- Método de pago (ej: "TRANSFERENCIA")
- Fecha de pago entre paréntesis (ej: "(15/01/26)")

**Estados visuales:**
- **Pagado:** Muestra método + fecha
- **Pendiente (no editando):** Muestra "—"
- **En edición:** Muestra "Pendiente" en verde cursiva

---

### 6. Columnas Ajustables y Ordenables

**Descripción:** El DataGrid ahora permite personalización de columnas.

**Características habilitadas:**
```xml
CanUserReorderColumns="True"
CanUserResizeColumns="True"
CanUserSortColumns="True"
```

**Columnas con ordenamiento:**
| Columna | Propiedad de ordenamiento |
|---------|---------------------------|
| DESCRIPCION | Description |
| ORDEN | OrderPO |
| TOTAL | Total |
| PAGO | PayMethod |
| COMPRA | ExpenseDate |
| VENCE | DueDate |
| DIAS | DaysRemaining |
| ESTADO | Status |

---

### 7. Tamaño de Texto Aumentado

**Descripción:** Se incrementaron los tamaños de fuente para mejor legibilidad.

**Cambios:**
| Elemento | Antes | Después |
|----------|-------|---------|
| Altura de filas | 45px | 48px |
| Altura fila en edición | 50px | 54px |
| Altura encabezados | 36px | 40px |
| Font encabezados | 11px | 12px |
| Font descripción | - | 13px |
| Font total | 13px | 14px |
| Font fechas | 12px | 13px |
| Font días | 11px | 12px |
| Font método pago | 11px | 12px |
| Font estado | 9px | 10px |
| Font TextBox edición | 13px | 14px |

---

### 8. Protección de Gastos Pagados

**Descripción:** Los gastos marcados como pagados no pueden ser modificados ni eliminados.

**Restricciones implementadas:**
- Botón Editar (E) oculto para gastos pagados
- Botón Eliminar (X) oculto para gastos pagados
- Doble clic no inicia edición en gastos pagados
- F2 no inicia edición en gastos pagados
- Se muestra indicador ✓ verde en lugar del botón de pago

**Propiedad utilizada:**
```csharp
public bool IsPayable => !IsPaid && IsReadOnly;
public bool IsPaid => PaidDate.HasValue;
```

---

## Modelo de Datos (ExpenseDetailViewModel)

### Propiedades Principales
```csharp
public int ExpenseId { get; set; }
public string Description { get; set; }
public string OrderPO { get; set; }
public int SelectedOrderId { get; set; }
public decimal Total { get; set; }
public string TotalFormatted { get; set; }
public string TotalInput { get; set; }  // Para edición
public DateTime ExpenseDate { get; set; }
public DateTime DueDate { get; set; }
public int DaysRemaining { get; set; }
public string DaysText { get; set; }
public string Status { get; set; }
public string PayMethod { get; set; }
public DateTime? PaidDate { get; set; }
public int CreditDays { get; set; }
```

### Propiedades de Estado
```csharp
public bool IsNew { get; set; }        // Es gasto nuevo (sin guardar)
public bool IsEditing { get; set; }    // Está en modo edición
public bool IsEditable => IsNew || IsEditing;
public bool IsReadOnly => !IsEditable;
public bool IsPaid => PaidDate.HasValue;
public bool IsPayable => !IsPaid && IsReadOnly;
```

### Métodos de Pago Disponibles
```csharp
public static List<string> PayMethods => new List<string>
{
    "TRANSFERENCIA",
    "EFECTIVO",
    "CHEQUE",
    "CRÉDITO",
    "DÉBITO"
};
```

---

## Capturas de Pantalla (Referencia)

### Flujo de Nuevo Gasto
1. Usuario hace clic en "Nuevo Gasto"
2. Se abre DetailView en modo selector
3. Usuario selecciona proveedor del ComboBox
4. Se crea automáticamente fila inline verde
5. Usuario completa campos y presiona OK/Enter
6. Gasto guardado, lista actualizada

### Flujo de Pago
1. Usuario hace clic en botón $ de un gasto pendiente
2. Aparece menú con métodos de pago
3. Usuario selecciona método (ej: EFECTIVO)
4. Gasto marcado como pagado con fecha actual
5. Columna PAGO muestra: "EFECTIVO (17/01/26)"

---

## Notas Técnicas

### Cálculo de Fecha de Vencimiento
```csharp
// La fecha de vencimiento se calcula automáticamente
DueDate = ExpenseDate.AddDays(CreditDays);
```

### Recálculo Automático
Cuando cambia `ExpenseDate`, se recalcula automáticamente:
- `DueDate`
- `DaysRemaining`
- `DaysText`

### Filtro Inicial Inteligente
```csharp
// Si no hay gastos pendientes, mostrar TODOS
if (pendingCount == 0 && totalCount > 0)
{
    _currentStatusFilter = "TODOS";
}
```

---

## Fecha de Implementación
**17 de Enero de 2026**

## Autor
Implementado con asistencia de Claude Code (Anthropic)
