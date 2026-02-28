# Bloque 1: Ajustes Cosmeticos (Pendientes Fase 3)

**Complejidad:** Baja
**Estado:** EN PROGRESO
**Dependencias:** Ninguna
**Estimacion:** ~1 sesion de desarrollo

---

## Requerimientos del Cliente

1. Centrar valores en columnas y renglones de todas las tablas
2. Filtro "Ano" en Manejo de Ordenes default al ano actual
3. Renombrar "Portal del Vendedor" -> "Portal Ventas"
4. Renombrar "Portal de Proveedores" -> "Portal Proveedores"
5. Columnas en Manejo de Ordenes: ajustar al ancho del texto, sin ocultarse
6. Balance: ajustar a cualquier monitor
7. Bug: cerrar sesion interrumpe screen share de Meet

---

## Analisis Tecnico por Item

### 1.1 Centrado de valores en tablas
**Impacto:** Todas las vistas con DataGrid
**Archivos a modificar:**
- `Views/OrdersManagementWindow.xaml` - DataGrid principal (14 columnas)
- `Views/ClientManagementWindow.xaml`
- `Views/InvoiceManagementWindow.xaml`
- `Views/ExpenseManagementWindow.xaml`
- `Views/PayrollManagementView.xaml`
- `Views/VendorCommissionsWindow.xaml`
- `Views/PendingIncomesView.xaml` / `PendingIncomesDetailView.xaml`
- `Views/SupplierPendingView.xaml` / `SupplierPendingDetailView.xaml`
- `Views/UserManagementWindow.xaml`
- `Views/SupplierManagementWindow.xaml`

**Implementacion:**
```xml
<!-- Agregar ElementStyle a cada DataGridTextColumn -->
<DataGridTextColumn.ElementStyle>
    <Style TargetType="TextBlock">
        <Setter Property="HorizontalAlignment" Value="Center"/>
        <Setter Property="VerticalAlignment" Value="Center"/>
    </Style>
</DataGridTextColumn.ElementStyle>
```

**Consideraciones:**
- Columnas de texto largo (Descripcion, Empresa) deben mantener alineacion a la izquierda
- Columnas numericas (montos, porcentajes) centradas
- Columnas de fecha centradas
- Headers ya estan centrados generalmente

### 1.2 Filtro Ano default al ano actual
**Archivo:** `Views/OrdersManagementWindow.xaml.cs`
**Estado actual:** El ComboBox de Ano tiene "Todos" seleccionado por defecto
**Implementacion:**
- En `LoadInitialDataAsync()` o en el constructor, establecer `YearFilter.SelectedItem` al ano actual (`DateTime.Now.Year`)
- El filtro de anos se llena dinamicamente; asegurar que el ano actual este en la lista antes de seleccionarlo

### 1.3 y 1.4 Renombrar portales
**Archivo:** `Views/MainMenuWindow.xaml`
**Cambios:**
- Linea del boton "PORTAL DEL VENDEDOR" -> "PORTAL VENTAS"
- Linea del boton "PORTAL DE PROVEEDORES" -> "PORTAL PROVEEDORES"
- Verificar si hay tooltips o textos asociados que tambien necesiten cambio
- Verificar titulo de VendorDashboard.xaml (Window.Title)
- Verificar titulo de SupplierPendingView.xaml (Window.Title)

### 1.5 Columnas en Manejo de Ordenes
**Archivo:** `Views/OrdersManagementWindow.xaml`
**Estado actual:** La columna Descripcion usa `Width="*"` (fill), lo que comprime las demas
**Implementacion:**
- Cambiar anchos fijos a `Width="Auto"` o valores explícitos ajustados
- Considerar `MinWidth` para columnas criticas
- Habilitar scroll horizontal si el contenido excede el monitor
- Probar en resoluciones comunes: 1366x768, 1920x1080, 2560x1440

**Propuesta de anchos:**

| Columna | Actual | Propuesto |
|---------|--------|-----------|
| O.C. | 100px | 80px (Auto) |
| FECHA O.C. | 100px | 100px |
| EMPRESA | 200px | 180px |
| DESCRIPCION | * (fill) | * con MinWidth=150 |
| VENDEDOR | 150px | 130px |
| FECHA PROMESA | 110px | 100px |
| % AVANCE | 80px | 70px |
| SUBTOTAL | 110px | 100px |
| TOTAL | 110px | 100px |
| FACTURADO | 110px | 100px |
| GASTO MAT. | 110px | 95px |
| GASTO OP. | 110px | 95px |
| GASTO IND. | 110px | 95px |
| ESTADO | 130px | 110px |
| ACCIONES | 150px | 130px |

### 1.6 Balance responsivo
**Archivo:** `Views/BalanceWindowPro.xaml`
**Estado actual:** Tamaños fijos que no se adaptan a monitores pequenos
**Implementacion:**
- Usar `Grid` con proporciones (`*`, `2*`) en lugar de tamaños absolutos
- `ViewBox` para escalar contenido si es necesario
- `ScrollViewer` como fallback para monitores muy pequenos
- Probar en multiples resoluciones

### 1.7 Bug: Cerrar sesion interrumpe Meet screen share
**Archivos:** `Views/MainMenuWindow.xaml.cs`, `Views/VendorDashboard.xaml.cs` (LogoutButton_Click)
**Causa probable:** Al cerrar sesion se ejecuta `this.Close()` + `new LoginWindow().Show()`, lo que destruye el handle de ventana que Meet esta compartiendo
**Solucion propuesta:**
- **Opcion A:** Mantener la ventana principal y cambiar su contenido (navegacion interna). Requiere refactoring significativo.
- **Opcion B:** Abrir LoginWindow ANTES de cerrar la ventana actual, para que Meet capture la nueva ventana. Menos invasivo.
- **Opcion C:** Minimizar la ventana actual en lugar de cerrarla, y mostrar LoginWindow. Al re-login, reutilizar la ventana existente.
- **Recomendacion:** Opcion B como solucion rapida. Investigar si `Window.Owner` puede ayudar a mantener el contexto de screen share.

---

## Checklist de Implementacion

- [x] 1.1 Centrar valores en OrdersManagementWindow
- [x] 1.1 Centrar valores en ClientManagementWindow (ContactsDataGrid)
- [x] 1.1 Centrar valores en InvoiceManagementWindow (ya estaba centrado)
- [x] 1.1 Centrar valores en ExpenseManagementWindow (ya estaba centrado, renombrado titulo)
- [x] 1.1 Centrar valores en PayrollHistoryWindow
- [x] 1.1 Centrar valores en VendorCommissionsWindow (usa cards, no DataGrid - ya centrado)
- [x] 1.1 Centrar valores en PendingIncomesDetailView
- [x] 1.1 Centrar valores en Supplier views (usa cards - ya centrado)
- [x] 1.1 Centrar valores en UserManagementWindow (usa cards - ya centrado)
- [x] 1.2 Default YearFilter al ano actual
- [x] 1.3 Renombrar "Portal del Vendedor" -> "Portal Ventas" (XAML + code-behind + titulo ventana)
- [x] 1.4 Renombrar "Portal de Proveedores" -> "Portal Proveedores" (XAML + code-behind + titulo ventana)
- [x] 1.5 Ajustar anchos de columnas en OrdersManagement + CanUserResizeColumns
- [x] 1.6 Balance responsivo (ya lo era: Maximized + columnas proporcionales + ScrollViewer)
- [x] 1.7 Fix bug screen share al cerrar sesion (Hide antes de Close con delay)
- [x] 1.7b Quitar MessageBox al cerrar sesion como vendedor
- [ ] QA: Probar en resoluciones 1366x768, 1920x1080

---

## Criterios de Aceptacion

1. Todos los valores numericos y fechas centrados visualmente en todas las tablas
2. Al abrir Manejo de Ordenes, el filtro de Ano muestra el ano actual seleccionado
3. Los botones del menu principal dicen "PORTAL VENTAS" y "PORTAL PROVEEDORES"
4. Todas las columnas en Manejo de Ordenes son visibles sin scroll horizontal en 1920x1080
5. Balance se visualiza correctamente en monitores de 14" (1366x768) hasta 27" (2560x1440)
6. Al cerrar sesion mientras se comparte pantalla en Meet, la transmision no se interrumpe
