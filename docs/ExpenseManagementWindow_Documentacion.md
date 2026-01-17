# Documentacion Detallada: ExpenseManagementWindow (Portal de Proveedores)

## Objetivo de la Migracion
Fusionar las funcionalidades de `ExpenseManagementWindow.xaml` (vista antigua con DataGrid) con la nueva vista moderna `SupplierPendingView.xaml` (vista con tarjetas por proveedor).

---

## 1. ESTRUCTURA GENERAL DE LA VENTANA

### 1.1 Layout Principal (Grid con 6 filas)
| Fila | Contenido | Altura |
|------|-----------|--------|
| 0 | Header con titulo y boton Volver | Auto |
| 1 | Panel de proveedor filtrado (oculto por defecto) | Auto |
| 2 | Tarjetas de resumen (4 cards) | Auto |
| 3 | Barra de herramientas (filtros + botones) | Auto |
| 4 | DataGrid principal | * |
| 5 | Barra de estado | Auto |

### 1.2 Dimensiones de Ventana
- **Width:** 1500
- **Height:** 900
- **WindowStyle:** None
- **ResizeMode:** NoResize
- **Comportamiento:** MaximizeWithTaskbar() - Maximiza sin cubrir barra de tareas

---

## 2. COMPONENTES DE UI

### 2.1 Header (Row 0)
- **Titulo:** "PORTAL DE PROVEEDORES"
- **Subtitulo:** "Gestion de Gastos y Pagos"
- **Boton Volver:** BackButton -> BackButton_Click()

### 2.2 Panel de Proveedor Filtrado (Row 1)
- **Nombre:** FilteredSupplierPanel
- **Visibilidad:** Collapsed por defecto
- **Contenido:**
  - Icono de edificio
  - Nombre del proveedor filtrado (FilteredSupplierNameText)
  - Boton "X Quitar filtro" -> ClearSupplierFilter_Click()
- **Activacion:** Se muestra al filtrar por proveedor especifico

### 2.3 Tarjetas de Resumen (Row 2)
| Card | Nombre Control | Color | Descripcion |
|------|----------------|-------|-------------|
| TOTAL GENERAL | TotalGeneralText | DarkText | Suma de todos los gastos filtrados |
| PENDIENTE | TotalPendienteText | WarningColor (amarillo) | Suma gastos con Status="PENDIENTE" |
| PAGADO | TotalPagadoText | SuccessColor (verde) | Suma gastos con Status="PAGADO" |
| REGISTROS | RecordCountText | PrimaryColor | Cantidad de registros (COMENTADO) |

### 2.4 Barra de Herramientas (Row 3)

#### Filtros (izquierda):
| Control | Nombre | Funcion | Evento |
|---------|--------|---------|--------|
| TextBox | SearchBox | Busqueda por texto | SearchBox_TextChanged |
| ComboBox | SupplierFilterCombo | Filtro por proveedor | SupplierFilterCombo_SelectionChanged |
| ComboBox | OrderFilterCombo | Filtro por orden | OrderFilterCombo_SelectionChanged |
| ComboBox | StatusFilterCombo | Filtro por estado | StatusFilterCombo_SelectionChanged |

**Opciones de StatusFilterCombo:**
- Todos
- PENDIENTE
- PAGADO

#### Botones (derecha):
| Boton | Nombre | Icono | Funcion | Evento |
|-------|--------|-------|---------|--------|
| Actualizar | RefreshButton | Recarga datos desde BD | RefreshButton_Click |
| Proveedores | SuppliersButton | Abre SupplierManagementWindow | SuppliersButton_Click |
| Nuevo Gasto | NewExpenseButton | Crea fila nueva inline | NewExpenseButton_Click |

### 2.5 DataGrid Principal (Row 4)
**Nombre:** ExpensesDataGrid
**ItemsSource:** _filteredExpenses (ObservableCollection<ExpenseViewModel>)

#### Columnas del DataGrid:
| # | Header | Binding | Width | Editable | Notas |
|---|--------|---------|-------|----------|-------|
| 0 | PROVEEDOR | SupplierName | 1.8* | Solo nuevos | ComboBox de proveedores en edicion |
| 1 | DESCRIPCION | Description | 2.5* | Si | TextBox |
| 2 | ORDEN | OrderNumber | 140 | Si | ComboBox de ordenes |
| 3 | F. COMPRA | ExpenseDateDisplay | 100 | Si | DatePicker |
| 4 | TOTAL | TotalExpenseDisplay | 120 | Si | TextBox con validacion numerica |
| 5 | ESTADO | Status | 100 | No | Badge con colores |
| 6 | F. PAGO | PaidDateDisplay | 100 | Si | DatePicker (marca como pagado) |
| 7 | METODO | PayMethod | 120 | Si | ComboBox |
| 8 | ACCIONES | - | 120 | No | Botones de accion |

#### Metodos de pago disponibles:
- TRANSFERENCIA
- EFECTIVO
- CHEQUE
- CREDITO
- DEBITO

#### Botones de Acciones por Fila:
| Boton | Icono | Condicion | Funcion |
|-------|-------|-----------|---------|
| Guardar | Diskette | IsNew=True | SaveNewButton_Click |
| Pagar | Billete | IsPaid=False AND IsNew=False | PayButton_Click |
| Editar | Lapiz | IsNew=False | EditButton_Click |
| Eliminar | Basura | Siempre | DeleteButton_Click |

### 2.6 Barra de Estado (Row 5)
| Control | Nombre | Contenido |
|---------|--------|-----------|
| TextBlock | StatusText | Mensaje de estado (ej: "Listo", "Guardando...") |
| TextBlock | LastUpdateText | "Ultima actualizacion: HH:mm:ss" |
| TextBlock | CurrentUserText | "Usuario: NombreCompleto (rol)" |

---

## 3. FUNCIONALIDADES PRINCIPALES

### 3.1 Crear Nuevo Gasto (Inline)
**Metodo:** NewExpenseButton_Click()
**Flujo:**
1. Verifica si ya hay un gasto en creacion (_isCreatingNewExpense)
2. Crea ExpenseViewModel con IsNew=true, IsEditing=true
3. Inserta al inicio de _filteredExpenses
4. Selecciona la fila y entra en modo edicion

**Validaciones al guardar (SaveNewExpense):**
- SupplierId > 0 (obligatorio)
- Description no vacia (obligatorio)
- TotalExpense > 0 (obligatorio)

**Teclas rapidas:**
- Enter: Guarda el gasto
- Escape: Cancela la creacion

### 3.2 Editar Gasto Existente
**Metodo:** EditButton_Click()
**Campos editables:**
- Descripcion
- Orden
- Fecha de compra
- Total
- Fecha de pago
- Metodo de pago

**Campos NO editables:**
- Proveedor
- Estado (se cambia automaticamente)

### 3.3 Registrar Pago
**Metodo:** PayButton_Click()
**Flujo:**
1. Muestra dialogo de confirmacion
2. Registra pago con fecha actual
3. Metodo por defecto: "TRANSFERENCIA"
4. Actualiza Status a "PAGADO"

**Metodo alternativo: editar F. PAGO directamente**
- Al seleccionar fecha en DatePicker -> PaidDatePicker_Changed
- Cambia automaticamente Status a "PAGADO"
- Asigna metodo por defecto "TRANSFERENCIA"

### 3.4 Eliminar Gasto
**Metodo:** DeleteButton_Click()
**Flujo:**
1. Muestra dialogo de confirmacion con datos del gasto
2. Llama a _supabaseService.DeleteExpense()
3. Remueve de _expenses y _filteredExpenses

### 3.5 Gestion de Proveedores
**Metodo:** SuppliersButton_Click()
**Funcion:** Abre SupplierManagementWindow como dialogo
**Post-cierre:** Recarga lista de proveedores

### 3.6 Sistema de Filtros
**Metodo:** ApplyFilters()
**Filtros aplicados en orden:**
1. Texto de busqueda (Description, SupplierName, OrderNumber)
2. Proveedor seleccionado
3. Orden seleccionada
4. Estado (PENDIENTE/PAGADO/Todos)

**Ordenamiento final:**
- Descendente por ExpenseDate
- Luego descendente por ExpenseId

### 3.7 Filtro Rapido por Proveedor (Doble Clic)
**Metodo:** SupplierName_MouseDown()
**Funcionamiento:**
- Doble clic en nombre de proveedor activa filtro
- Muestra panel FilteredSupplierPanel
- Variables: _lastSupplierClick, _lastSupplierClicked

---

## 4. SERVICIOS Y DATOS

### 4.1 Servicios Utilizados
- **_supabaseService:** SupabaseService.Instance

### 4.2 Llamadas a Servicios
| Metodo | Servicio | Descripcion |
|--------|----------|-------------|
| LoadSuppliers | GetActiveSuppliers() | Carga proveedores activos |
| LoadExpenses | GetExpenses() | Carga gastos con filtros |
| LoadOrdersForFilter | GetRecentOrders(100) | Carga ordenes para filtro |
| SaveNewExpense | CreateExpense() | Crear nuevo gasto |
| SaveExpenseChanges | UpdateExpense() | Actualizar gasto existente |
| PayButton_Click | MarkExpenseAsPaid() | Marcar como pagado |
| DeleteButton_Click | DeleteExpense() | Eliminar gasto |

### 4.3 Sistema de Cache
| Cache | Variable | Expiracion |
|-------|----------|------------|
| Proveedores | _suppliers, _lastSuppliersLoad | 5 minutos |
| Gastos | _expenses, _lastExpensesLoad | 5 minutos |
| Ordenes | _ordersCache, _lastOrdersLoad | 5 minutos |

---

## 5. ESTILOS VISUALES

### 5.1 Colores del Sistema
| Nombre | Color | Uso |
|--------|-------|-----|
| PrimaryColor | #6366F1 | Botones principales, seleccion |
| PrimaryLight | #E0E7FF | Hover, fondo seleccionado |
| SuccessColor | #10B981 | Pagado, guardado exitoso |
| SuccessLight | #D1FAE5 | Fondo filas pagadas |
| DangerColor | #EF4444 | Eliminar, error |
| DangerLight | #FEE2E2 | Fondo filas vencidas |
| WarningColor | #F59E0B | Pendiente |
| WarningLight | #FEF3C7 | Fondo filas pendientes |
| InfoColor | #3B82F6 | Informacion |
| BackgroundGray | #F9FAFB | Fondo general |
| BorderGray | #E5E7EB | Bordes |
| TextGray | #6B7280 | Texto secundario |
| DarkText | #111827 | Texto principal |

### 5.2 Estilos de Filas del DataGrid
| Condicion | Efecto Visual |
|-----------|---------------|
| IsNew=True | Background verde claro, borde verde |
| Status=PENDIENTE | Background amarillo claro |
| IsOverdue=True | Background rojo claro, texto bold |
| IsMouseOver | Background azul claro |
| IsSelected | Background azul claro, borde azul |

### 5.3 Badges de Estado
| Estado | Background | Foreground |
|--------|------------|------------|
| PAGADO | SuccessLight | SuccessColor |
| PENDIENTE | WarningLight | WarningColor |

---

## 6. CHECKLIST DE MIGRACION

### 6.1 Funcionalidades a Migrar
- [ ] Crear nuevo gasto (inline o modal)
- [ ] Editar gasto existente
- [ ] Eliminar gasto
- [ ] Marcar como pagado
- [ ] Filtro por texto
- [ ] Filtro por proveedor
- [ ] Filtro por orden
- [ ] Filtro por estado
- [ ] Boton de actualizar/refrescar
- [ ] Boton de gestion de proveedores
- [ ] Doble clic en proveedor para filtrar
- [ ] Panel destacado de proveedor filtrado
- [ ] Tarjetas de resumen (Total, Pendiente, Pagado)
- [ ] Barra de estado con ultima actualizacion

### 6.2 Campos del DataGrid a Mantener
- [ ] Proveedor (seleccion)
- [ ] Descripcion
- [ ] Orden (asociacion)
- [ ] Fecha de compra
- [ ] Total
- [ ] Estado (badge)
- [ ] Fecha de pago
- [ ] Metodo de pago
- [ ] Acciones (Guardar/Pagar/Editar/Eliminar)

### 6.3 Validaciones a Mantener
- [ ] Proveedor obligatorio
- [ ] Descripcion obligatoria
- [ ] Total > 0
- [ ] Confirmacion antes de eliminar
- [ ] Confirmacion antes de pagar
- [ ] Aviso de cambios sin guardar al cerrar

### 6.4 Comportamientos Especiales
- [ ] Pago automatico para proveedores sin credito
- [ ] Cache de datos (5 min expiracion)
- [ ] Ordenamiento: vencidos primero, luego por fecha
- [ ] Teclas rapidas: Enter (guardar), Escape (cancelar), F2 (editar), Tab (navegar)

### 6.5 Integraciones
- [ ] SupplierManagementWindow (CRUD proveedores)
- [ ] Sistema de ordenes (asociacion opcional)

---

## 7. NOTAS PARA LA MIGRACION

### 7.1 Vista Nueva (SupplierPendingView)
La vista nueva ya tiene:
- Vista de tarjetas por proveedor
- Detalle por proveedor (SupplierPendingDetailView)
- Indicadores de vencimiento
- Filtros basicos

### 7.2 Lo que falta agregar a SupplierPendingView:
1. **Boton "Nuevo Gasto"** - Crear nuevo gasto
2. **Boton "Proveedores"** - Abrir CRUD de proveedores
3. **Funcionalidad de edicion** - Editar gasto existente
4. **Funcionalidad de eliminacion** - Eliminar gasto
5. **Funcionalidad de pago** - Marcar como pagado
6. **Filtro por orden** - ComboBox de ordenes
7. **Filtro por estado completo** - Incluir PAGADO

### 7.3 Decision de Diseno
**Opcion A:** Mantener DataGrid en vista de detalle (SupplierPendingDetailView)
- Agregar botones de accion al DataGrid existente
- Agregar formulario modal para nuevo gasto

**Opcion B:** Vista hibrida
- Vista principal: tarjetas por proveedor
- Boton "Ver todos los gastos": abre DataGrid completo
- Formulario modal para CRUD

### 7.4 ViewModel Existente
El `ExpenseViewModel` ya tiene todas las propiedades necesarias:
- ExpenseId, SupplierId, SupplierName
- Description, ExpenseDate, TotalExpense
- ScheduledDate, Status, PaidDate, PayMethod
- OrderId, OrderNumber, ExpenseCategory
- IsNew, IsEditing, IsPaid, IsOverdue

---

## 8. ARCHIVOS RELACIONADOS

| Archivo | Descripcion |
|---------|-------------|
| ExpenseManagementWindow.xaml | Vista XAML |
| ExpenseManagementWindow.xaml.cs | Code-behind |
| ExpenseViewModel.cs | ViewModel de gastos |
| SupplierManagementWindow.xaml | CRUD de proveedores |
| SupplierPendingView.xaml | Nueva vista principal |
| SupplierPendingDetailView.xaml | Vista de detalle nueva |
| SupabaseService.cs | Servicios de datos |
| ExpenseService.cs | Servicio de gastos |
