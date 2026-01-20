# Plan de Extensión - Plataforma IMA v2.0

## Contexto del Proyecto

- **Tecnología:** WPF (.NET 8.0) + Supabase (PostgreSQL)
- **Patrón:** MVVM con INotifyPropertyChanged
- **Versión actual:** 1.0.13
- **Repositorio:** SistemaGestionProyectos2

---

## Resumen Ejecutivo

Esta extensión incluye:
- Reestructuración del sistema de roles y permisos
- Nuevas columnas de control de gastos en órdenes
- Mejoras en el módulo de Balance (semáforo, utilidad)
- Dos nuevos módulos: Calendario (RRHH) y Portal de Usuarios

---

## FASE 1: Gestión de Roles y Permisos

### Estructura de Roles

| Rol | Descripción | Permisos Base |
|-----|-------------|---------------|
| `direccion` | Rol principal (actual admin) | Todo + Portal Usuarios |
| `administracion` | Administrativo | Como admin SIN portal vendedores + Calendario |
| `proyectos` | Gestión de proyectos | Igual a coordinación (temporal) |
| `coordinacion` | Coordinación de órdenes | Acceso a órdenes, entra directo a "En Proceso" |
| `ventas` | Vendedores | Portal de vendedores, comisiones |

### Checklist Fase 1

- [ ] **1.1 Base de Datos**
  - [ ] Documentar tabla `users` actual
  - [ ] Modificar/agregar columna `role` para nuevos valores
  - [ ] Crear tabla `roles` si no existe (id, name, description, permissions)
  - [ ] Crear tabla `user_preferences` para guardar filtros por usuario
  - [ ] Script SQL de migración

- [ ] **1.2 Backend/Modelos**
  - [ ] Actualizar modelo `UserDb` con nuevos roles
  - [ ] Crear enum o constantes para roles
  - [ ] Actualizar `SupabaseService` con métodos de roles

- [ ] **1.3 UI - Login y Navegación**
  - [ ] Modificar `LoginWindow` para manejar nuevos roles
  - [ ] Actualizar `MainMenuWindow` con permisos por rol
  - [ ] Agregar título de departamento en header (Coordinación, Ventas, etc.)
  - [ ] Coordinación: redirigir a "En Proceso" al iniciar

- [ ] **1.4 Converters y Visibilidad**
  - [ ] Actualizar `AdminVisibilityConverter` para nuevos roles
  - [ ] Crear converters específicos si es necesario

---

## FASE 2: Columnas de Gastos en MANEJO DE ÓRDENES

### Nuevas Columnas

| Columna | Tipo | Descripción | Editable |
|---------|------|-------------|----------|
| `gasto_material` | decimal | Suma de facturas pagadas a proveedores para la orden | No (calculado) |
| `gasto_operativo` | decimal | Gastos para realizar la orden (con descripción) | Sí |
| `gasto_indirecto` | decimal | Valor ingresado manualmente | Sí |
| `dias_estimados` | int | Días estimados para completar la orden | Sí |

### Checklist Fase 2

- [ ] **2.1 Base de Datos**
  - [ ] Agregar columnas a tabla `orders`:
    - [ ] `gasto_operativo` (decimal, default 0)
    - [ ] `gasto_indirecto` (decimal, default 0)
    - [ ] `dias_estimados` (int, nullable)
  - [ ] Crear tabla `order_gastos_operativos` para detalle:
    - [ ] id, order_id, monto, descripcion, fecha, created_by
  - [ ] Crear tabla `audit_order_changes` para auditoría:
    - [ ] id, order_id, campo, valor_anterior, valor_nuevo, usuario, fecha
  - [ ] Crear vista o función para calcular `gasto_material` desde `supplier_expenses`

- [ ] **2.2 Backend/Modelos**
  - [ ] Actualizar modelo `OrderDb` con nuevas columnas
  - [ ] Crear modelo `OrderGastoOperativo`
  - [ ] Crear modelo `AuditOrderChange`
  - [ ] Métodos en `SupabaseService`:
    - [ ] `GetGastoMaterialByOrder(orderId)`
    - [ ] `AddGastoOperativo(orderId, monto, descripcion)`
    - [ ] `UpdateGastoIndirecto(orderId, monto)`
    - [ ] `LogOrderChange(orderId, campo, valorAnterior, valorNuevo)`

- [ ] **2.3 UI - OrdersManagementWindow**
  - [ ] Agregar columna "Gasto Material" (solo lectura, calculado)
  - [ ] Agregar columna "Gasto Operativo" (con botón para agregar/ver detalle)
  - [ ] Agregar columna "Gasto Indirecto" (editable inline)
  - [ ] Agregar columna "Días Est." (editable inline)
  - [ ] Modal o panel para gestionar gastos operativos con descripción

- [ ] **2.4 Persistencia de Filtros**
  - [ ] Guardar filtro seleccionado en `user_preferences`
  - [ ] Cargar filtro al iniciar (solo admin/direccion)

---

## FASE 3: Mejoras en BALANCE

### Semáforo de Ventas Mensuales

```
ROJO:     ventas = $0.00
AMARILLO: ventas > (nomina + gasto_fijo) * 1.1
VERDE:    ventas > (nomina + gasto_fijo) * 1.1 + 100,000
```

### Fórmula de Utilidad

```
Utilidad = ventas_mensuales - gasto_material - gasto_operativo - gasto_indirecto
```

### Checklist Fase 3

- [ ] **3.1 Base de Datos**
  - [ ] Verificar/crear tabla `monthly_config`:
    - [ ] id, mes, año, nomina, gasto_fijo
  - [ ] Crear vista `v_balance_mensual` con cálculos

- [ ] **3.2 Backend**
  - [ ] Crear modelo `MonthlyConfig`
  - [ ] Método `GetMonthlyConfig(mes, año)`
  - [ ] Método `CalculateUtilidad(mes, año)`
  - [ ] Método `GetSemaforoColor(ventas, nomina, gastoFijo)`

- [ ] **3.3 UI - BalanceWindow**
  - [ ] Implementar colores de semáforo en celdas de ventas
  - [ ] Agregar fila de "Utilidad Aproximada"
  - [ ] Tooltip con detalle del cálculo
  - [ ] (Pendiente) Cambiar colores según plantilla del cliente

---

## FASE 4: Módulo Calendario (RRHH)

### Funcionalidades

- Registro diario de personal:
  - Asistencias
  - Retardos
  - Faltas
  - Vacaciones

### Acceso

- `direccion`: Sí
- `administracion`: Sí
- Otros: No

### Checklist Fase 4

- [ ] **4.1 Base de Datos**
  - [ ] Crear tabla `employees`:
    - [ ] id, nombre, departamento, puesto, fecha_ingreso, activo
  - [ ] Crear tabla `attendance_records`:
    - [ ] id, employee_id, fecha, tipo (asistencia/retardo/falta/vacaciones)
    - [ ] hora_entrada, hora_salida, notas, created_by
  - [ ] Crear tabla `vacation_balance`:
    - [ ] employee_id, año, dias_totales, dias_usados

- [ ] **4.2 Backend/Modelos**
  - [ ] Crear modelo `Employee`
  - [ ] Crear modelo `AttendanceRecord`
  - [ ] Crear modelo `VacationBalance`
  - [ ] Métodos CRUD en `SupabaseService`

- [ ] **4.3 UI**
  - [ ] Crear `CalendarWindow.xaml`:
    - [ ] Vista de calendario mensual
    - [ ] Lista de empleados
    - [ ] Registro rápido de asistencia
    - [ ] Filtros por tipo de registro
  - [ ] Crear `EmployeeManagementWindow.xaml` (CRUD empleados)
  - [ ] Agregar botón en `MainMenuWindow` (solo direccion/administracion)

---

## FASE 5: Portal de Usuarios

### Funcionalidades

- CRUD completo de usuarios
- Asignación de roles
- Asignación de credenciales (usuario/contraseña)

### Acceso

- `direccion`: Sí (exclusivo)

### Checklist Fase 5

- [ ] **5.1 Base de Datos**
  - [ ] Revisar tabla `users` existente
  - [ ] Agregar columnas si faltan:
    - [ ] `created_at`, `updated_at`, `created_by`
    - [ ] `last_login`, `is_active`

- [ ] **5.2 Backend**
  - [ ] Métodos en `SupabaseService`:
    - [ ] `GetAllUsers()`
    - [ ] `CreateUser(user)`
    - [ ] `UpdateUser(user)`
    - [ ] `DeleteUser(userId)` o `DeactivateUser(userId)`
    - [ ] `ResetPassword(userId, newPassword)`

- [ ] **5.3 UI**
  - [ ] Crear `UserManagementWindow.xaml`:
    - [ ] DataGrid con lista de usuarios
    - [ ] Formulario de creación/edición
    - [ ] Selector de rol
    - [ ] Botón para resetear contraseña
  - [ ] Agregar botón en `MainMenuWindow` (solo direccion)

---

## FASE 6: Ajustes Finales

### Checklist Fase 6

- [ ] **6.1 UI General**
  - [ ] Cambiar colores según plantilla del cliente (cuando la envíe)
  - [ ] Verificar consistencia visual en todos los módulos

- [ ] **6.2 Testing**
  - [ ] Probar cada rol con sus permisos
  - [ ] Verificar cálculos de Balance
  - [ ] Probar auditoría de cambios
  - [ ] Validar flujo completo de gastos en órdenes

- [ ] **6.3 Documentación**
  - [ ] Actualizar manual de usuario
  - [ ] Documentar nuevas tablas en BD
  - [ ] Release notes para v2.0

---

## Dependencias entre Fases

```
FASE 1 (Roles) ─────┬──► FASE 2 (Gastos Órdenes) ──► FASE 3 (Balance)
                    │
                    ├──► FASE 4 (Calendario)
                    │
                    └──► FASE 5 (Portal Usuarios)

                              FASE 6 (Ajustes Finales)
```

---

## Notas Importantes

1. **Portal de Proveedores** - Resumen por saldo ya está COMPLETO
2. **Plantilla de colores** - Pendiente de recibir del cliente
3. **Rol "proyectos"** - Por ahora igual a coordinación, puede cambiar
4. **Gasto Operativo** - Pendiente de reformular el concepto exacto

---

## Comando para Iniciar Chat Nuevo

```
Estoy trabajando en la extensión de la Plataforma IMA (WPF + Supabase).
Lee el archivo docs/cambios_ene26/PLAN_EXTENSION_v2.md para el contexto completo.
Empezaremos por la FASE 1: Gestión de Roles y Permisos.
Primero necesito que documentes la estructura actual de la BD.
```
