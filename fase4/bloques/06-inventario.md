# Bloque 6: Modulo de Inventario

**Complejidad:** Alta
**Estado:** EN PRUEBAS (Fase 6A completada - pendiente feedback cliente)
**Dependencias:** Ninguna (modulo independiente)
**Primer paso obligatorio:** Mockup visual para validacion con el cliente

---

## Requerimientos del Cliente

1. Modulo nuevo completo para gestionar inventario de componentes
2. Categorias dinamicas creadas por el usuario (ej. "Tornilleria", "Cableado", "Conectores")
3. Cards que al hacer clic despliegan los productos de esa categoria
4. Tablas tipo Excel para administrar productos
5. Control de stock: saber que hay disponible y que se necesita pedir
6. **Primer paso: mockup visual para confirmar la idea**

---

## Diseno Conceptual de UI

### Pantalla principal: InventoryWindow

```
+------------------------------------------------------------------+
| INVENTARIO                                    [+ Nueva Categoria] |
+------------------------------------------------------------------+
|                                                                   |
|  +------------------+  +------------------+  +------------------+ |
|  |   TORNILLERIA    |  |    CABLEADO      |  |   CONECTORES     | |
|  |                  |  |                  |  |                  | |
|  |   42 productos   |  |   18 productos   |  |   25 productos   | |
|  |   3 por pedir    |  |   0 por pedir    |  |   7 por pedir    | |
|  +------------------+  +------------------+  +------------------+ |
|                                                                   |
|  +------------------+  +------------------+  +------------------+ |
|  |   HERRAMIENTAS   |  |   SENSORES       |  |   MOTORES        | |
|  |                  |  |                  |  |                  | |
|  |   15 productos   |  |   8 productos    |  |   12 productos   | |
|  |   1 por pedir    |  |   2 por pedir    |  |   0 por pedir    | |
|  +------------------+  +------------------+  +------------------+ |
|                                                                   |
+------------------------------------------------------------------+
```

### Pantalla de productos: CategoryDetailWindow

```
+------------------------------------------------------------------+
| <- Volver    TORNILLERIA                   [+ Nuevo Producto]     |
+------------------------------------------------------------------+
| Buscar: [____________]          Stock bajo: [x] Mostrar alertas  |
+------------------------------------------------------------------+
| Codigo  | Nombre           | Stock | Min | Unidad | Precio | Ubi |
|---------|------------------|-------|-----|--------|--------|-----|
| TOR-001 | Tornillo M3x10   |   150 |  50 | pza    | $0.50  | A-1 |
| TOR-002 | Tornillo M4x15   |    20 |  30 | pza    | $0.75  | A-1 |  <- ALERTA
| TOR-003 | Tornillo M5x20   |   200 | 100 | pza    | $1.00  | A-2 |
| TOR-004 | Tuerca M3        |    45 |  50 | pza    | $0.30  | A-1 |  <- ALERTA
| TOR-005 | Arandela M3      |   500 | 100 | pza    | $0.10  | A-3 |
+------------------------------------------------------------------+
|                                                    Total: 5 items |
+------------------------------------------------------------------+
```

### Alertas de stock:
- Fila en amarillo/naranja cuando `stock < stock_minimo`
- Badge en la card de categoria con conteo de productos por pedir
- Vista global "Productos por pedir" que muestre todos los que estan bajo stock

---

## Esquema de Base de Datos

```sql
-- Categorias de inventario
CREATE TABLE inventory_categories (
    id SERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    description TEXT,
    color VARCHAR(7) DEFAULT '#3498DB',  -- Color de la card
    icon VARCHAR(50),                     -- Icono MDL2 opcional
    display_order INTEGER DEFAULT 0,
    is_active BOOLEAN DEFAULT TRUE,
    created_by INTEGER REFERENCES users(id),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Productos de inventario
CREATE TABLE inventory_products (
    id SERIAL PRIMARY KEY,
    category_id INTEGER NOT NULL REFERENCES inventory_categories(id) ON DELETE CASCADE,
    code VARCHAR(50) UNIQUE,              -- Codigo interno (ej. TOR-001)
    name VARCHAR(255) NOT NULL,
    description TEXT,
    stock_current NUMERIC(10,2) DEFAULT 0,
    stock_minimum NUMERIC(10,2) DEFAULT 0,
    unit VARCHAR(20) DEFAULT 'pza',       -- pza, kg, m, l, etc.
    unit_price NUMERIC(12,2) DEFAULT 0,
    location VARCHAR(100),                -- Ubicacion fisica (ej. A-1, Estante 3)
    supplier_id INTEGER REFERENCES t_supplier(f_supplier),  -- Proveedor preferido
    notes TEXT,
    is_active BOOLEAN DEFAULT TRUE,
    created_by INTEGER REFERENCES users(id),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Movimientos de inventario (entradas/salidas)
CREATE TABLE inventory_movements (
    id SERIAL PRIMARY KEY,
    product_id INTEGER NOT NULL REFERENCES inventory_products(id) ON DELETE CASCADE,
    movement_type VARCHAR(20) NOT NULL,   -- 'entrada', 'salida', 'ajuste'
    quantity NUMERIC(10,2) NOT NULL,
    previous_stock NUMERIC(10,2),
    new_stock NUMERIC(10,2),
    reference_type VARCHAR(50),           -- 'orden', 'compra', 'ajuste_manual'
    reference_id INTEGER,                 -- f_order, purchase_id, etc.
    notes TEXT,
    created_by INTEGER REFERENCES users(id),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Indexes
CREATE INDEX idx_inv_products_category ON inventory_products(category_id);
CREATE INDEX idx_inv_products_stock_low ON inventory_products(stock_current, stock_minimum)
    WHERE stock_current < stock_minimum;
CREATE INDEX idx_inv_movements_product ON inventory_movements(product_id);
CREATE INDEX idx_inv_movements_date ON inventory_movements(created_at);

-- Vista de productos con stock bajo
CREATE OR REPLACE VIEW v_inventory_low_stock AS
SELECT
    p.*,
    c.name as category_name,
    (p.stock_minimum - p.stock_current) as cantidad_por_pedir,
    s.f_name as supplier_name
FROM inventory_products p
JOIN inventory_categories c ON p.category_id = c.id
LEFT JOIN t_supplier s ON p.supplier_id = s.f_supplier
WHERE p.stock_current < p.stock_minimum
  AND p.is_active = TRUE
ORDER BY (p.stock_minimum - p.stock_current) DESC;

-- Vista resumen por categoria
CREATE OR REPLACE VIEW v_inventory_category_summary AS
SELECT
    c.id,
    c.name,
    c.color,
    c.icon,
    c.display_order,
    COUNT(p.id) as total_products,
    COUNT(CASE WHEN p.stock_current < p.stock_minimum THEN 1 END) as low_stock_count,
    SUM(p.stock_current * p.unit_price) as total_value
FROM inventory_categories c
LEFT JOIN inventory_products p ON p.category_id = c.id AND p.is_active = TRUE
WHERE c.is_active = TRUE
GROUP BY c.id, c.name, c.color, c.icon, c.display_order
ORDER BY c.display_order;

-- Trigger para registrar movimientos automaticamente al cambiar stock
CREATE OR REPLACE FUNCTION fn_track_inventory_movement()
RETURNS TRIGGER AS $$
BEGIN
    IF OLD.stock_current IS DISTINCT FROM NEW.stock_current THEN
        INSERT INTO inventory_movements (
            product_id, movement_type, quantity,
            previous_stock, new_stock, notes, created_by
        ) VALUES (
            NEW.id,
            CASE WHEN NEW.stock_current > OLD.stock_current THEN 'entrada' ELSE 'salida' END,
            ABS(NEW.stock_current - OLD.stock_current),
            OLD.stock_current,
            NEW.stock_current,
            'Ajuste directo de stock',
            NEW.created_by
        );
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_track_inventory_movement
    AFTER UPDATE OF stock_current ON inventory_products
    FOR EACH ROW
    EXECUTE FUNCTION fn_track_inventory_movement();
```

---

## Arquitectura de Servicios

```csharp
// Services/InventoryService.cs
public class InventoryService : BaseSupabaseService
{
    // --- Categorias ---
    Task<List<InventoryCategoryDb>> GetCategories(CancellationToken ct);
    Task<InventoryCategoryDb> CreateCategory(InventoryCategoryDb category, CancellationToken ct);
    Task<bool> UpdateCategory(InventoryCategoryDb category, CancellationToken ct);
    Task<bool> DeleteCategory(int categoryId, CancellationToken ct);

    // --- Productos ---
    Task<List<InventoryProductDb>> GetProductsByCategory(int categoryId, CancellationToken ct);
    Task<InventoryProductDb> CreateProduct(InventoryProductDb product, CancellationToken ct);
    Task<bool> UpdateProduct(InventoryProductDb product, CancellationToken ct);
    Task<bool> DeleteProduct(int productId, CancellationToken ct);
    Task<bool> UpdateStock(int productId, decimal newStock, int userId, CancellationToken ct);

    // --- Consultas ---
    Task<List<InventoryProductDb>> GetLowStockProducts(CancellationToken ct);
    Task<List<CategorySummaryDto>> GetCategorySummary(CancellationToken ct);
    Task<List<InventoryMovementDb>> GetMovements(int productId, CancellationToken ct);
}
```

---

## Archivos a Crear

| Archivo | Tipo |
|---------|------|
| `Models/Database/InventoryCategoryDb.cs` | Modelo categoria |
| `Models/Database/InventoryProductDb.cs` | Modelo producto |
| `Models/Database/InventoryMovementDb.cs` | Modelo movimiento |
| `Models/DTOs/CategorySummaryDto.cs` | DTO resumen categoria |
| `Services/InventoryService.cs` | Servicio completo |
| `Views/InventoryWindow.xaml` | Pantalla principal con cards |
| `Views/InventoryWindow.xaml.cs` | Code-behind |
| `Views/CategoryDetailWindow.xaml` | Tabla de productos |
| `Views/CategoryDetailWindow.xaml.cs` | Code-behind |
| `Views/NewCategoryDialog.xaml` | Dialog crear/editar categoria |
| `Views/NewProductDialog.xaml` | Dialog crear/editar producto |
| `sql/bloque6_inventario.sql` | Script completo BD |

## Archivos a Modificar

| Archivo | Cambio |
|---------|--------|
| `Views/MainMenuWindow.xaml` | Agregar boton "INVENTARIO" al menu |
| `Views/MainMenuWindow.xaml.cs` | Handler para abrir InventoryWindow |
| `Services/SupabaseService.cs` | Registrar InventoryService en facade |

---

## Permisos por Rol

| Accion | Direccion | Admin | Operacion | Coordinacion | Vendedor |
|--------|-----------|-------|-----------|--------------|----------|
| Ver inventario | Si | Si | Si | Si | No |
| Crear categorias | Si | Si | No | No | No |
| Editar categorias | Si | Si | No | No | No |
| Eliminar categorias | Si | No | No | No | No |
| Crear productos | Si | Si | Si | No | No |
| Editar productos | Si | Si | Si | No | No |
| Ajustar stock | Si | Si | Si | Si | No |

---

## Plan de Ejecucion por Fases

### Fase 6A: Mockup (EN PRUEBAS - pendiente feedback cliente)
- [x] Disenar mockup de pantalla principal con cards (InventoryWindow.xaml)
- [x] Disenar mockup de tabla de productos (CategoryDetailWindow.xaml)
- [x] Disenar mockup de alertas de stock bajo (filas amber, badges, KPI cards)
- [x] Disenar mockup de formularios (NewProductDialog, NewCategoryDialog)
- [x] Boton INVENTARIO en MainMenu con badge "EN PRUEBAS"
- [x] Toast notifications + confirmacion eliminacion inline
- [x] ComboBox custom estilo SupplierPendingView
- [x] Layout responsivo (MaxWidth centrado para pantallas grandes)
- [ ] Presentar al cliente para validacion
- [ ] Documentar feedback del cliente

### Fase 6B: Base de datos
- [ ] Crear script SQL con tablas, indexes, vistas, triggers
- [ ] Ejecutar en BD de desarrollo
- [ ] Crear modelos C# (3 archivos)

### Fase 6C: Servicio
- [ ] Implementar InventoryService
- [ ] Registrar en SupabaseService facade
- [ ] Tests basicos de CRUD

### Fase 6D: UI - Pantalla principal
- [ ] Crear InventoryWindow con WrapPanel de cards
- [ ] Implementar carga de categorias con resumen
- [ ] Dialog para crear/editar categoria
- [ ] Agregar boton en MainMenuWindow

### Fase 6E: UI - Productos
- [ ] Crear CategoryDetailWindow con DataGrid editable
- [ ] Implementar CRUD de productos
- [ ] Alertas visuales de stock bajo
- [ ] Busqueda y filtros

### Fase 6F: Movimientos y stock
- [ ] Registro de entradas/salidas
- [ ] Vista global de productos por pedir
- [ ] Historial de movimientos por producto

---

## Criterios de Aceptacion

1. El usuario puede crear categorias dinamicas con nombre y color
2. Las categorias se muestran como cards con conteo de productos y alertas
3. Al hacer clic en una card, se abre la tabla de productos de esa categoria
4. Los productos se pueden crear, editar y eliminar con interfaz tipo Excel
5. El sistema muestra alertas cuando el stock esta por debajo del minimo
6. Se registran automaticamente los movimientos de stock
7. La interfaz es consistente con el resto de la plataforma (mismos colores, fuentes, estilos)
8. Los permisos funcionan segun la matriz definida

---

## Notas

- Este modulo es completamente independiente del resto de la Fase 4
- El mockup es OBLIGATORIO antes de iniciar desarrollo
- Considerar en futuras fases: vincular consumo de inventario con ordenes de produccion
- Las unidades de medida pueden ser un catalogo configurable en el futuro
