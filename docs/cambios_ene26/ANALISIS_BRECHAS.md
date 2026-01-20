# Análisis de Brechas: BD Actual vs Extensión v2.0

**Fecha:** 19 de Enero de 2026
**Autor:** Documentación automática basada en extracción de Supabase
**Propósito:** Identificar todos los cambios de BD necesarios antes de implementar código

---

## Resumen Ejecutivo

| Categoría | Estado Actual | Requerido v2.0 | Brecha |
|-----------|---------------|----------------|--------|
| Roles de usuario | 3 roles | 5 roles | **MODIFICAR CONSTRAINT** |
| Columnas en t_order | 24 columnas | 27+ columnas | **AGREGAR 3+ COLUMNAS** |
| Tablas nuevas | 25 tablas | 28+ tablas | **CREAR 3+ TABLAS** |
| Vistas | 4 vistas | 5+ vistas | **CREAR/MODIFICAR** |
| Funciones | 36 funciones | 38+ funciones | **CREAR 2+ FUNCIONES** |

---

## 1. SISTEMA DE ROLES (CRÍTICO)

### Estado Actual

```sql
-- Constraint actual en tabla users:
CHECK (role IN ('admin', 'coordinator', 'salesperson'))
```

### Requerido por extensión

| Rol Actual | Rol Nuevo | Mapeo |
|------------|-----------|-------|
| `admin` | `direccion` | admin → direccion |
| (nuevo) | `administracion` | Crear nuevo |
| (nuevo) | `proyectos` | Crear nuevo (= coordinacion) |
| `coordinator` | `coordinacion` | Renombrar |
| `salesperson` | `ventas` | Renombrar |

### Script de Migración Requerido

```sql
-- 1. Eliminar constraint actual
ALTER TABLE users DROP CONSTRAINT users_role_check;

-- 2. Migrar datos existentes
UPDATE users SET role = 'direccion' WHERE role = 'admin';
UPDATE users SET role = 'coordinacion' WHERE role = 'coordinator';
UPDATE users SET role = 'ventas' WHERE role = 'salesperson';

-- 3. Crear nuevo constraint
ALTER TABLE users ADD CONSTRAINT users_role_check
CHECK (role IN ('direccion', 'administracion', 'proyectos', 'coordinacion', 'ventas'));

-- 4. Actualizar función get_vendors()
-- Actualmente filtra por role = 'salesperson', debe cambiar a 'ventas'
```

### Impacto en Código

- `AuthService.cs` - Verificación de roles
- `MainMenuWindow.xaml.cs` - Menú según rol
- `OrdersManagementView.xaml.cs` - Permisos
- Todos los servicios que verifican `role`

---

## 2. COLUMNAS NUEVAS EN t_order

### Estado Actual de t_order

```
f_order (PK), f_client, f_contact, f_quote, f_po, f_podate, f_estdelivery,
f_description, f_salesubtotal, f_saletotal, f_orderstat, f_expense,
actual_delivery, profit_amount, created_at, updated_at, created_by,
progress_percentage, order_percentage, invoiced, last_invoice_date,
f_salesman, updated_by, f_commission_rate
```

**Total actual: 24 columnas**

### Columnas Requeridas por extensión

| Columna | Tipo | Nullable | Default | Propósito |
|---------|------|----------|---------|-----------|
| `gasto_operativo` | NUMERIC(15,2) | YES | 0 | Gastos random editables |
| `gasto_indirecto` | NUMERIC(15,2) | YES | 0 | Valor ingresado manualmente |
| `dias_estimados` | INTEGER | YES | NULL | Días estimados para la orden |

### Script de Migración

```sql
-- Agregar nuevas columnas a t_order
ALTER TABLE t_order
ADD COLUMN gasto_operativo NUMERIC(15,2) DEFAULT 0,
ADD COLUMN gasto_indirecto NUMERIC(15,2) DEFAULT 0,
ADD COLUMN dias_estimados INTEGER DEFAULT NULL;

-- Agregar comentarios
COMMENT ON COLUMN t_order.gasto_operativo IS 'Gastos operativos editables con descripción';
COMMENT ON COLUMN t_order.gasto_indirecto IS 'Gastos indirectos ingresados manualmente';
COMMENT ON COLUMN t_order.dias_estimados IS 'Días estimados para completar la orden';
```

---

## 3. TABLAS NUEVAS REQUERIDAS

### 3.1 Tabla: order_gastos_operativos

Para almacenar el detalle de gastos operativos con descripción.

```sql
CREATE TABLE order_gastos_operativos (
    id SERIAL PRIMARY KEY,
    f_order INTEGER NOT NULL REFERENCES t_order(f_order) ON DELETE CASCADE,
    monto NUMERIC(15,2) NOT NULL,
    descripcion VARCHAR(255) NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    created_by INTEGER REFERENCES users(id)
);

CREATE INDEX idx_gastos_operativos_order ON order_gastos_operativos(f_order);

COMMENT ON TABLE order_gastos_operativos IS 'Detalle de gastos operativos por orden';
```

### 3.2 Tabla: user_preferences

Para guardar filtros y preferencias por usuario.

```sql
CREATE TABLE user_preferences (
    id SERIAL PRIMARY KEY,
    user_id INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    preference_key VARCHAR(100) NOT NULL,
    preference_value TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(user_id, preference_key)
);

CREATE INDEX idx_user_preferences_user ON user_preferences(user_id);

COMMENT ON TABLE user_preferences IS 'Preferencias y filtros guardados por usuario';
```

### 3.3 Tabla: attendance_records (Para Calendario RRHH)

```sql
CREATE TABLE attendance_records (
    id SERIAL PRIMARY KEY,
    employee_id INTEGER NOT NULL REFERENCES t_payroll(f_payroll) ON DELETE CASCADE,
    record_date DATE NOT NULL,
    record_type VARCHAR(20) NOT NULL CHECK (record_type IN ('ASISTENCIA', 'RETARDO', 'FALTA', 'VACACION', 'PERMISO')),
    notes TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    created_by INTEGER REFERENCES users(id),
    UNIQUE(employee_id, record_date)
);

CREATE INDEX idx_attendance_employee ON attendance_records(employee_id);
CREATE INDEX idx_attendance_date ON attendance_records(record_date);
CREATE INDEX idx_attendance_type ON attendance_records(record_type);

COMMENT ON TABLE attendance_records IS 'Registro de asistencias, retardos, faltas y vacaciones';
```

---

## 4. FUNCIONES NUEVAS REQUERIDAS

### 4.1 Función: get_semaforo_color()

Para el semáforo del Balance.

```sql
CREATE OR REPLACE FUNCTION get_semaforo_color(
    p_ventas NUMERIC,
    p_nomina NUMERIC,
    p_gasto_fijo NUMERIC
) RETURNS TEXT AS $$
DECLARE
    v_umbral_amarillo NUMERIC;
    v_umbral_verde NUMERIC;
BEGIN
    -- Umbral amarillo: (nomina + gasto_fijo) * 1.1
    v_umbral_amarillo := (p_nomina + p_gasto_fijo) * 1.1;

    -- Umbral verde: umbral_amarillo + 100,000
    v_umbral_verde := v_umbral_amarillo + 100000;

    -- Determinar color
    IF p_ventas = 0 OR p_ventas IS NULL THEN
        RETURN 'ROJO';
    ELSIF p_ventas >= v_umbral_verde THEN
        RETURN 'VERDE';
    ELSIF p_ventas >= v_umbral_amarillo THEN
        RETURN 'AMARILLO';
    ELSE
        RETURN 'ROJO';
    END IF;
END;
$$ LANGUAGE plpgsql IMMUTABLE;

COMMENT ON FUNCTION get_semaforo_color IS 'Calcula color del semáforo según ventas vs gastos fijos+nómina';
```

### 4.2 Función: get_gasto_material_orden()

Para calcular el gasto material de una orden.

```sql
CREATE OR REPLACE FUNCTION get_gasto_material_orden(p_order_id INTEGER)
RETURNS NUMERIC AS $$
BEGIN
    RETURN COALESCE(
        (SELECT SUM(f_totalexpense)
         FROM t_expense
         WHERE f_order = p_order_id
         AND f_status = 'PAGADO'),
        0
    );
END;
$$ LANGUAGE plpgsql STABLE;

COMMENT ON FUNCTION get_gasto_material_orden IS 'Suma gastos pagados a proveedores para una orden';
```

---

## 5. MODIFICACIONES A VISTAS EXISTENTES

### Vista: v_balance_completo

La vista actual YA calcula `utilidad_aproximada` pero con fórmula diferente:
```sql
-- Actual:
utilidad_aproximada = ingresos_esperados - total_gastos
```

**Requerido por extensión:**
```sql
-- Nuevo (según PDF):
utilidad = ventas_mensuales - gasto_material - gasto_operativo - gasto_indirecto
```

**Decisión:** ¿Modificar la vista existente o crear una nueva `v_balance_completo_v2`?

---

## 6. RESUMEN DE CAMBIOS SQL REQUERIDOS

### Prioridad 1: Roles (Bloquea todo lo demás)
1. Modificar constraint `users_role_check`
2. Migrar datos de roles existentes
3. Actualizar función `get_vendors()`

### Prioridad 2: Estructura de Órdenes
4. Agregar columnas a `t_order`
5. Crear tabla `order_gastos_operativos`

### Prioridad 3: Preferencias de Usuario
6. Crear tabla `user_preferences`

### Prioridad 4: Calendario RRHH
7. Crear tabla `attendance_records`

### Prioridad 5: Funciones de Balance
8. Crear función `get_semaforo_color()`
9. Crear función `get_gasto_material_orden()`
10. Evaluar modificación de vistas de balance

---

## 7. PREGUNTAS PENDIENTES PARA EL CLIENTE

1. **Gasto Operativo:** El PDF dice "pendiente de reformular". ¿Cuál es la definición final?
   - ¿Es un solo valor por orden o múltiples líneas con descripción?
   - ¿Quién puede editarlo?

2. **Semáforo del Balance:**
   - ¿Los umbrales (1.1x y +100k) son configurables o fijos?
   - ¿Se aplica por mes o acumulado?

3. **Migración de Roles:**
   - ¿Todos los `admin` actuales pasan a `direccion`?
   - ¿O algunos deben ser `administracion`?

4. **Calendario RRHH:**
   - ¿Se basa en `t_payroll` (empleados de nómina) o necesita tabla separada de empleados?
   - ¿Hay integraciones con sistemas de checador?

5. **Plantilla de Colores:**
   - El PDF menciona "yo mando esa plantilla" - ¿Ya está disponible?

---

## 8. RECOMENDACIÓN

Antes de escribir código, ejecutar en este orden:

1. **Clarificar preguntas pendientes** con el cliente
2. **Crear script de migración** consolidado (`MIGRACION_v2.sql`)
3. **Ejecutar en ambiente de prueba** primero
4. **Validar que la aplicación funciona** con los cambios de BD
5. **Entonces** comenzar cambios de código por fases

---

## Archivos Relacionados

- `docs/BD-IMA/00_RESUMEN_ESQUEMA.md` - Estado actual de la BD
- `docs/cambios_ene26/PLAN_EXTENSION_v2.md` - Plan de implementación (borrador)
- `docs/cambios_ene26/extensión_workana.pdf` - Requerimientos originales
