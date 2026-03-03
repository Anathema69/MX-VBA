# Bloque 4: Columna "Ejecutor" en Ordenes

**Complejidad:** Baja
**Estado:** COMPLETADO (falta deploy SQL)
**Dependencias:** Tabla `t_payroll` (lista de empleados/nomina)

---

## Requerimientos del Cliente

1. Nueva columna editable "Ejecutor" en la tabla de ordenes
2. Selector multiple de nombres desde el listado de nomina
3. Permisos de edicion: Administracion, Operacion (proyectos), Coordinacion
4. Visible para todos los roles

---

## Analisis Tecnico

### Modelo de datos

**Opcion A: Campo texto simple en t_order**
- Agregar columna `ejecutores TEXT` a `t_order`
- Guardar nombres separados por coma
- Pros: Simple, sin tablas nuevas
- Contras: No relacional, dificil mantener consistencia si cambian nombres

**Opcion B: Tabla relacional (RECOMENDADA)**
- Nueva tabla `order_ejecutores` (many-to-many entre ordenes y empleados)
- Pros: Relacional, integridad referencial, consultas eficientes
- Contras: Requiere JOIN para mostrar nombres

### Esquema BD propuesto (Opcion B)

```sql
-- Tabla relacional orden-ejecutor
CREATE TABLE order_ejecutores (
    id SERIAL PRIMARY KEY,
    f_order INTEGER NOT NULL REFERENCES t_order(f_order) ON DELETE CASCADE,
    payroll_id INTEGER NOT NULL REFERENCES t_payroll(id) ON DELETE CASCADE,
    assigned_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    assigned_by INTEGER REFERENCES users(id),
    UNIQUE(f_order, payroll_id)  -- Evitar duplicados
);

-- Index para consultas rapidas
CREATE INDEX idx_order_ejecutores_order ON order_ejecutores(f_order);
CREATE INDEX idx_order_ejecutores_payroll ON order_ejecutores(payroll_id);

-- Vista helper para obtener nombres concatenados
CREATE OR REPLACE VIEW v_order_ejecutores AS
SELECT
    oe.f_order,
    STRING_AGG(p.f_name || ' ' || p.f_lastname, ', ' ORDER BY p.f_name) as ejecutores_nombre,
    ARRAY_AGG(oe.payroll_id) as ejecutores_ids
FROM order_ejecutores oe
JOIN t_payroll p ON oe.payroll_id = p.id
GROUP BY oe.f_order;
```

### Impacto en el modelo C#

**Nuevo modelo:**
```csharp
// Models/Database/OrderEjecutorDb.cs
public class OrderEjecutorDb
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("f_order")]
    public int OrderId { get; set; }

    [JsonPropertyName("payroll_id")]
    public int PayrollId { get; set; }

    [JsonPropertyName("assigned_at")]
    public DateTime AssignedAt { get; set; }

    [JsonPropertyName("assigned_by")]
    public int? AssignedBy { get; set; }
}
```

**Modificar OrderViewModel:**
```csharp
// Agregar propiedad
public string EjecutoresNombre { get; set; } = "";  // "Juan Perez, Maria Lopez"
public List<int> EjecutoresIds { get; set; } = new();
```

### Impacto en UI

**OrdersManagementWindow.xaml:**
- Nueva columna "EJECUTOR" despues de VENDEDOR
- Mostrar texto con nombres concatenados
- Ancho: ~180px
- Boton de edicion (icono lapiz) que abre popup/dialog de seleccion

**Popup de seleccion de ejecutores:**
- ListBox con CheckBoxes
- Items: todos los empleados activos de t_payroll
- Pre-seleccionados: ejecutores actuales de la orden
- Botones: Guardar / Cancelar
- Filtro de busqueda por nombre

**Visibilidad del boton de edicion por rol:**
```csharp
bool canEditEjecutor = currentRole is "direccion" or "administracion" or "proyectos" or "coordinacion";
```
- Todos ven la columna con los nombres
- Solo roles autorizados ven el boton de edicion

### Impacto en servicios

**OrderService.cs - Nuevos metodos:**
```csharp
// Obtener ejecutores de una orden
public async Task<List<OrderEjecutorDb>> GetEjecutores(int orderId, CancellationToken ct)

// Asignar ejecutores (reemplaza la lista completa)
public async Task SetEjecutores(int orderId, List<int> payrollIds, int assignedBy, CancellationToken ct)
```

**Modificar GetOrders/GetOrdersFiltered:**
- Hacer JOIN con v_order_ejecutores o hacer segunda query batch
- Evaluar impacto en rendimiento (idealmente usar la vista)

---

## Archivos a Crear

| Archivo | Tipo |
|---------|------|
| `Models/Database/OrderEjecutorDb.cs` | Modelo BD |
| `sql/bloque4_ejecutor.sql` | Script migracion BD |

## Archivos a Modificar

| Archivo | Cambio |
|---------|--------|
| `Models/DTOs/OrderViewModel.cs` | Agregar EjecutoresNombre, EjecutoresIds |
| `Views/OrdersManagementWindow.xaml` | Nueva columna + popup seleccion |
| `Views/OrdersManagementWindow.xaml.cs` | Handler para editar ejecutores |
| `Services/OrderService.cs` | GetEjecutores(), SetEjecutores() |
| `Services/SupabaseService.cs` | Exponer metodos de ejecutores en facade |

---

## Checklist de Implementacion

- [x] Crear script SQL para tabla order_ejecutores + vista + indexes
- [ ] Ejecutar script en BD (pendiente deploy)
- [x] Crear modelo OrderEjecutorDb
- [x] Agregar propiedades a OrderViewModel (EjecutoresNombre, EjecutoresIds)
- [x] Implementar GetEjecutores() en OrderService
- [x] Implementar SetEjecutores() en OrderService
- [x] Implementar GetEjecutoresNombresBatch() para carga optimizada
- [x] Integrar en GetOrders() para cargar ejecutores con ordenes (LoadEjecutoresBatch)
- [x] Agregar columna EJECUTOR en OrdersManagementWindow.xaml (160px, con boton editar)
- [x] Crear EjecutorSelectionDialog con checkboxes, busqueda, contador
- [x] Permisos: todos los roles que ven ordenes pueden editar ejecutores
- [x] Registrar en SupabaseService facade (3 metodos)
- [ ] QA: Probar asignacion, edicion, visualizacion por rol

---

## Criterios de Aceptacion

1. La columna "EJECUTOR" muestra los nombres de los asignados en cada orden
2. Al hacer clic en editar, se abre un selector con todos los empleados de nomina
3. Se pueden seleccionar multiples ejecutores
4. Solo Administracion, Operacion y Coordinacion pueden editar
5. Todos los roles pueden ver la columna
6. Los cambios se persisten correctamente en BD
7. No hay impacto significativo en rendimiento de carga de ordenes
