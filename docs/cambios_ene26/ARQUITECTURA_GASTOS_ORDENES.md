# Arquitectura de Gastos en Ordenes

**Version:** 2.3
**Fecha:** 25 de Enero 2026
**Modulo:** Gestion de Ordenes

---

## Indice

1. [Resumen General](#1-resumen-general)
2. [Tipos de Gastos](#2-tipos-de-gastos)
3. [Estructura de Base de Datos](#3-estructura-de-base-de-datos)
4. [Vista v_order_gastos](#4-vista-v_order_gastos)
5. [Flujo de Datos (App C#)](#5-flujo-de-datos-app-c)
6. [Como Agregar Formulas Futuras](#6-como-agregar-formulas-futuras)
7. [Ejemplos de Formulas](#7-ejemplos-de-formulas)
8. [Archivos del Proyecto](#8-archivos-del-proyecto)
9. [Guia de Modificacion](#9-guia-de-modificacion)

---

## 1. Resumen General

El sistema maneja tres tipos de gastos por orden:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           GASTOS POR ORDEN                                  │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  GASTO MATERIAL      = Suma de pagos a proveedores (PAGADOS)               │
│                        Fuente: t_expense (calculado en vista)              │
│                        Editable: NO (automatico)                            │
│                                                                             │
│  GASTO OPERATIVO     = Suma de gastos manuales + Formula (futura)          │
│                        Fuente: order_gastos_operativos -> t_order          │
│                        Editable: SI (solo Direccion)                        │
│                                                                             │
│  GASTO INDIRECTO     = Suma de gastos manuales + Formula (futura)          │
│                        Fuente: order_gastos_indirectos -> t_order          │
│                        Editable: SI (solo Direccion)                        │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Principio de Actualizacion

La **app C#** es responsable de actualizar `t_order.gasto_operativo` y `t_order.gasto_indirecto` despues de cada operacion CRUD en las tablas de detalle. La vista solo lee estos valores.

---

## 2. Tipos de Gastos

### 2.1 Gasto Material

| Caracteristica | Valor |
|----------------|-------|
| **Origen** | Tabla `t_expense` (facturas a proveedores) |
| **Calculo** | SUM(f_totalexpense) WHERE f_status = 'PAGADO' |
| **Actualizacion** | Automatica en vista (subquery) |
| **Editable** | No - depende de estado de facturas |
| **Visible para** | Rol Direccion |

### 2.2 Gasto Operativo

| Caracteristica | Valor |
|----------------|-------|
| **Origen** | Tabla `order_gastos_operativos` |
| **Calculo** | SUM(monto) de la tabla de detalle |
| **Actualizacion** | App C# actualiza `t_order.gasto_operativo` despues de CRUD |
| **Editable** | Si - CRUD inline en EditOrderWindow |
| **Visible para** | Rol Direccion |
| **Ejemplos** | Fletes, viaticos, materiales menores |

### 2.3 Gasto Indirecto

| Caracteristica | Valor |
|----------------|-------|
| **Origen** | Tabla `order_gastos_indirectos` |
| **Calculo** | SUM(monto) de la tabla de detalle |
| **Actualizacion** | App C# actualiza `t_order.gasto_indirecto` despues de CRUD |
| **Editable** | Si - CRUD inline en EditOrderWindow |
| **Visible para** | Rol Direccion |
| **Ejemplos** | Gastos administrativos, overhead, costos fijos prorrateados |

---

## 3. Estructura de Base de Datos

### 3.1 Tablas de Detalle

```sql
-- Gastos operativos manuales
CREATE TABLE order_gastos_operativos (
    id SERIAL PRIMARY KEY,
    f_order INTEGER NOT NULL REFERENCES t_order(f_order) ON DELETE CASCADE,
    monto NUMERIC(15,2) NOT NULL,
    descripcion VARCHAR(255) NOT NULL,
    fecha_gasto TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    created_by INTEGER,
    updated_at TIMESTAMP,
    updated_by INTEGER
);

-- Gastos indirectos manuales
CREATE TABLE order_gastos_indirectos (
    id SERIAL PRIMARY KEY,
    f_order INTEGER NOT NULL REFERENCES t_order(f_order) ON DELETE CASCADE,
    monto NUMERIC(15,2) NOT NULL,
    descripcion VARCHAR(255) NOT NULL,
    fecha_gasto TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    created_by INTEGER,
    updated_at TIMESTAMP,
    updated_by INTEGER
);
```

### 3.2 Columnas en t_order

```sql
-- Totales (actualizados por la app C# despues de cada CRUD)
ALTER TABLE t_order ADD COLUMN gasto_operativo NUMERIC(15,2) DEFAULT 0;
ALTER TABLE t_order ADD COLUMN gasto_indirecto NUMERIC(15,2) DEFAULT 0;
```

### 3.3 Diagrama de Relaciones

```
┌─────────────────┐       ┌──────────────────────────┐
│     t_order     │──1:N──│ order_gastos_operativos  │
│                 │       │  (detalle manual)        │
│  f_order (PK)   │       └──────────────────────────┘
│  gasto_operativo│                │
│  gasto_indirecto│                │ App C# suma y actualiza
│                 │                ▼
│                 │       ┌──────────────────────────┐
│                 │──1:N──│ order_gastos_indirectos  │
│                 │       │  (detalle manual)        │
│                 │       └──────────────────────────┘
│                 │
│                 │       ┌──────────────────────────┐
│                 │──1:N──│      t_expense           │
└─────────────────┘       │  (gasto_material)        │
                          │  Calculado en vista      │
                          └──────────────────────────┘
```

---

## 4. Vista v_order_gastos

### 4.1 Definicion Actual

```sql
CREATE OR REPLACE VIEW v_order_gastos AS
SELECT
    o.f_order,
    o.f_po,
    o.f_quote,
    o.f_podate,
    o.f_client,
    o.f_contact,
    o.f_description,
    o.f_salesman,
    o.f_estdelivery,
    o.f_salesubtotal,
    o.f_saletotal,
    o.f_orderstat,
    o.progress_percentage,
    o.order_percentage,
    o.f_commission_rate,
    o.created_by,
    o.created_at,
    o.updated_by,
    o.updated_at,
    -- Gastos de la orden (leidos de t_order)
    COALESCE(o.gasto_operativo, 0) AS gasto_operativo,
    COALESCE(o.gasto_indirecto, 0) AS gasto_indirecto,
    -- Gastos de proveedores (calculados de t_expense)
    COALESCE(g.gasto_material_pagado, 0) AS gasto_material,
    COALESCE(g.gasto_material_pendiente, 0) AS gasto_material_pendiente,
    COALESCE(g.total_gastos, 0) AS total_gastos_proveedor,
    COALESCE(g.num_facturas, 0) AS num_facturas_proveedor
FROM t_order o
LEFT JOIN (
    SELECT
        f_order,
        SUM(CASE WHEN f_status = 'PAGADO' THEN f_totalexpense ELSE 0 END) AS gasto_material_pagado,
        SUM(CASE WHEN f_status = 'PENDIENTE' THEN f_totalexpense ELSE 0 END) AS gasto_material_pendiente,
        SUM(f_totalexpense) AS total_gastos,
        COUNT(*) AS num_facturas
    FROM t_expense
    WHERE f_order IS NOT NULL
    GROUP BY f_order
) g ON o.f_order = g.f_order;
```

### 4.2 Columnas Expuestas

| Campo | Descripcion | Origen |
|-------|-------------|--------|
| `gasto_operativo` | Total gastos operativos | t_order (actualizado por app) |
| `gasto_indirecto` | Total gastos indirectos | t_order (actualizado por app) |
| `gasto_material` | Pagos a proveedores PAGADOS | Subquery t_expense |
| `gasto_material_pendiente` | Pagos a proveedores PENDIENTES | Subquery t_expense |

---

## 5. Flujo de Datos (App C#)

### 5.1 Diagrama de Flujo

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         FLUJO: AGREGAR GASTO OPERATIVO                      │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  Usuario                  App C#                         Base de Datos     │
│     │                       │                                 │            │
│     │ Click "+ Nuevo"       │                                 │            │
│     │──────────────────────>│                                 │            │
│     │                       │                                 │            │
│     │ Ingresa monto/desc    │                                 │            │
│     │──────────────────────>│                                 │            │
│     │                       │                                 │            │
│     │ Click "Guardar"       │                                 │            │
│     │──────────────────────>│                                 │            │
│     │                       │                                 │            │
│     │                       │ 1. INSERT order_gastos_operativos             │
│     │                       │────────────────────────────────>│            │
│     │                       │                                 │            │
│     │                       │ 2. SELECT SUM(monto) WHERE f_order = X       │
│     │                       │────────────────────────────────>│            │
│     │                       │                                 │            │
│     │                       │ 3. UPDATE t_order SET gasto_operativo = suma │
│     │                       │────────────────────────────────>│            │
│     │                       │                                 │            │
│     │ Actualiza UI          │                                 │            │
│     │<──────────────────────│                                 │            │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 5.2 Metodo en OrderService.cs

```csharp
// Despues de INSERT/UPDATE/DELETE en order_gastos_operativos:
private async Task ActualizarTotalGastoOperativo(int orderId)
{
    // 1. Calcular suma
    var gastos = await GetGastosOperativosByOrder(orderId);
    var total = gastos.Sum(g => g.Monto);

    // 2. Actualizar t_order
    var order = await GetOrderById(orderId);
    order.GastoOperativo = total;
    await UpdateOrder(order, userId);
}
```

### 5.3 Mismo patron para Gasto Indirecto

```csharp
private async Task ActualizarTotalGastoIndirecto(int orderId)
{
    var gastos = await GetGastosIndirectosByOrder(orderId);
    var total = gastos.Sum(g => g.Monto);

    var order = await GetOrderById(orderId);
    order.GastoIndirecto = total;
    await UpdateOrder(order, userId);
}
```

---

## 6. Como Agregar Formulas Futuras

Cuando se defina una formula tipo `(A + B/C) + suma_manual`, hay dos opciones:

### 6.1 Opcion A: Formula en la Vista (Recomendado)

Modificar `v_order_gastos` para calcular:

```sql
-- ANTES (solo manual):
COALESCE(o.gasto_indirecto, 0) AS gasto_indirecto,

-- DESPUES (manual + formula):
COALESCE(o.gasto_indirecto, 0) + (
    -- Formula: ejemplo (gasto_material * 10%) + (venta / 100)
    COALESCE(g.gasto_material_pagado * 0.10, 0) +
    COALESCE(o.f_saletotal / NULLIF(100, 0), 0)
) AS gasto_indirecto,
```

**Ventaja:** App C# no requiere cambios, solo lee el total de la vista.

### 6.2 Opcion B: Formula en la App C#

Calcular en `ActualizarTotalGastoIndirecto`:

```csharp
private async Task ActualizarTotalGastoIndirecto(int orderId)
{
    var gastos = await GetGastosIndirectosByOrder(orderId);
    var sumaManual = gastos.Sum(g => g.Monto);

    var order = await GetOrderById(orderId);

    // Formula: (gasto_material * 10%) + (venta / 100)
    var gastoMaterial = await GetGastoMaterial(orderId);
    var formula = (gastoMaterial * 0.10m) + (order.SaleTotal / 100m);

    order.GastoIndirecto = sumaManual + formula;
    await UpdateOrder(order, userId);
}
```

**Ventaja:** Mas control, logs detallados.
**Desventaja:** Requiere cambios en C# para cada formula.

### 6.3 Recomendacion

- **Formula simple y estatica:** Opcion A (vista SQL)
- **Formula compleja o dinamica:** Opcion B (app C#)

---

## 7. Ejemplos de Formulas

### 7.1 En SQL (Vista)

```sql
-- 5% del total de venta
COALESCE(o.f_saletotal * 0.05, 0)

-- 15% del gasto material
COALESCE(g.gasto_material_pagado * 0.15, 0)

-- Formula compuesta: (Material * 10%) + (Venta * 2%)
COALESCE(g.gasto_material_pagado * 0.10, 0) + COALESCE(o.f_saletotal * 0.02, 0)

-- Con division protegida
COALESCE(o.f_saletotal / NULLIF(o.f_salesubtotal, 0), 0)

-- Condicional
CASE
    WHEN COALESCE(o.f_saletotal, 0) > 50000
    THEN o.f_saletotal * 0.03
    ELSE o.f_saletotal * 0.05
END
```

### 7.2 En C#

```csharp
// 5% del total de venta
var formula = order.SaleTotal * 0.05m;

// 15% del gasto material
var formula = gastoMaterial * 0.15m;

// Formula compuesta
var formula = (gastoMaterial * 0.10m) + (order.SaleTotal * 0.02m);

// Con division protegida
var formula = order.SaleSubtotal != 0
    ? order.SaleTotal / order.SaleSubtotal
    : 0;
```

---

## 8. Archivos del Proyecto

### 8.1 Scripts SQL

| Archivo | Descripcion | Estado |
|---------|-------------|--------|
| `docs/cambios_ene26/GASTO_MATERIAL_VISTA.sql` | Vista original con gasto_material | Ejecutado |
| `docs/cambios_ene26/GASTO_INDIRECTO_SETUP.sql` | Tabla + columna + vista actualizada | Ejecutado |
| `docs/cambios_ene26/FIX_GASTOS_INDIRECTOS_AUDIT.sql` | Agregar updated_at, updated_by | Pendiente |

### 8.2 Modelos C#

| Archivo | Descripcion | Estado |
|---------|-------------|--------|
| `Models/Database/OrderGastoOperativoDb.cs` | Modelo gastos operativos | Creado |
| `Models/Database/OrderGastoIndirectoDb.cs` | Modelo gastos indirectos | Creado |
| `Models/Database/OrderDb.cs` | Columnas gasto_operativo, gasto_indirecto | Creado |
| `Models/Database/OrderGastosViewDb.cs` | Vista con ambos gastos | Creado |

### 8.3 Servicios

| Archivo | Metodos | Estado |
|---------|---------|--------|
| `Services/Orders/OrderService.cs` | CRUD GastosOperativos | Creado |
| | CRUD GastosIndirectos | Creado |
| | RecalcularGastoIndirecto | Creado |

### 8.4 Vistas UI

| Archivo | Descripcion | Estado |
|---------|-------------|--------|
| `Views/EditOrderWindow.xaml` | Seccion gastos operativos | Creado |
| | Seccion gastos indirectos | Creado |
| `Views/EditOrderWindow.xaml.cs` | Logica CRUD inline ambos | Creado |
| `Views/OrdersManagementWindow.xaml` | Columna G.Operativo | Creado |
| | Columna G.Indirecto | Creado |
| `Views/OrdersManagementWindow.xaml.cs` | Visibilidad por rol | Creado |

---

## 9. Guia de Modificacion

### 9.1 Para Implementar Gasto Indirecto (actual)

1. [x] Ejecutar `GASTO_INDIRECTO_SETUP.sql` en BD
2. [x] Crear `OrderGastoIndirectoDb.cs`
3. [x] Agregar metodos CRUD en `OrderService.cs`
4. [x] Agregar seccion UI en `EditOrderWindow.xaml`
5. [x] Agregar columna en `OrdersManagementWindow.xaml`
6. [ ] Probar CRUD completo

### 9.2 Para Agregar Formula (futuro)

1. [ ] Definir formula matematica clara
2. [ ] Decidir: SQL (vista) o C# (servicio)
3. [ ] Implementar formula
4. [ ] Probar con datos reales
5. [ ] Actualizar esta documentacion

### 9.3 Para Agregar Nuevo Tipo de Gasto

1. [ ] Crear tabla `order_gastos_NUEVO`
2. [ ] Agregar columna `gasto_NUEVO` en `t_order`
3. [ ] Actualizar vista `v_order_gastos`
4. [ ] Crear modelo `OrderGastoNuevoDb.cs`
5. [ ] Agregar metodos CRUD en `OrderService.cs`
6. [ ] Agregar seccion UI en `EditOrderWindow.xaml`
7. [ ] Actualizar esta documentacion

---

## Historial de Cambios

| Fecha | Version | Cambio |
|-------|---------|--------|
| 2026-01-25 | 1.0 | Creacion inicial con gasto_material |
| 2026-01-25 | 2.0 | Agregar gasto_operativo con CRUD inline |
| 2026-01-25 | 2.1 | Documentar arquitectura para gasto_indirecto |
| 2026-01-25 | 2.2 | Simplificar: app C# actualiza t_order despues de CRUD |
| 2026-01-25 | 2.3 | Agregar campos auditoria (updated_at, updated_by) a ambas tablas |

---

## Scripts SQL Ejecutar

### Orden de ejecucion:

```bash
# 1. Setup gasto indirecto (tabla + columna + vista)
psql -f docs/cambios_ene26/GASTO_INDIRECTO_SETUP.sql

# 2. Agregar campos de auditoria a order_gastos_indirectos
psql -f docs/cambios_ene26/FIX_GASTOS_INDIRECTOS_AUDIT.sql
```

---

**Desarrollado por:** Equipo IMA Mecatronica
**Repositorio:** github.com/Anathema69/MX-VBA
