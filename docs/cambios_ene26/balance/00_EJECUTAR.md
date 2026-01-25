# Balance - Guía de Ejecución

**Fecha:** 26 de Enero 2026

---

## Estado Actual

| Requerimiento | Estado | Notas |
|---------------|--------|-------|
| **Req. 1:** Semáforo Ventas | ✅ IMPLEMENTADO | Solo código C# |
| **Req. 2:** Utilidad Mejorada | ⏳ PENDIENTE | Requiere cambios BD |

---

## Req. 1: Semáforo de Ventas

### Ya Implementado en:
- `Views/BalanceWindowPro.xaml.cs`
- Método: `AddSemaforoDataRow()`

### Lógica:
```
Umbral Amarillo = (Nómina + Gastos Fijos) × 1.1
Umbral Verde = Umbral Amarillo + $100,000

$0             → ROJO FUERTE
< Amarillo     → ROJO CLARO
< Verde        → AMARILLO
>= Verde       → VERDE
```

### Para Probar:
1. Compilar y ejecutar la aplicación
2. Ir al módulo Balance
3. La fila "Ventas Totales" debería mostrar colores según los umbrales

---

## Req. 2: Utilidad Aproximada Mejorada

### Orden de Ejecución SQL:

```
┌─────────────────────────────────────────────────────────────┐
│ PASO 1: Ejecutar 01_VERIFICAR_DIAS_ESTIMADOS.sql           │
│         (Verificar si columna existe)                       │
├─────────────────────────────────────────────────────────────┤
│ SI la columna NO existe:                                    │
│   PASO 2: Ejecutar 02_ALTER_DIAS_ESTIMADOS.sql             │
├─────────────────────────────────────────────────────────────┤
│ PASO 3: RESPONDER PREGUNTAS (abajo)                        │
├─────────────────────────────────────────────────────────────┤
│ PASO 4: Ejecutar 03_VISTA_UTILIDAD_MENSUAL.sql             │
│         (después de ajustar según respuestas)              │
├─────────────────────────────────────────────────────────────┤
│ PASO 5: Actualizar v_balance_completo                      │
│         (para usar nueva fórmula de utilidad)              │
└─────────────────────────────────────────────────────────────┘
```

---

## Preguntas a Responder

### Pregunta 1: ¿Qué fecha determina el mes de una venta?

| Opción | Descripción | Implicación |
|--------|-------------|-------------|
| **A** | `f_podate` (Fecha de PO) | Mes en que se recibió la orden |
| **B** | `f_invoicedate` (Fecha factura) | Mes en que se facturó |
| **C** | `f_paymentdate` (Fecha cobro) | Mes en que se cobró |

**Respuesta seleccionada:** `____________________`

---

### Pregunta 2: ¿Cómo manejar órdenes SIN días estimados?

| Opción | Descripción | Impacto |
|--------|-------------|---------|
| **A** | Excluir del cálculo | No afectan utilidad |
| **B** | Usar valor default (ej: 15 días) | Afectan parcialmente |
| **C** | Calcular automático (`f_estdelivery - f_podate`) | Requiere datos de entrega |

**Respuesta seleccionada:** `____________________`

---

### Pregunta 3: ¿Valor máximo de días permitido?

El límite evita errores por datos incorrectos.

| Opción | Descripción |
|--------|-------------|
| **A** | 30 días (1 mes máximo) |
| **B** | 60 días (2 meses) |
| **C** | 90 días (3 meses) |
| **D** | Sin límite |

**Respuesta seleccionada:** `____________________`

---

## Modificaciones Pendientes en Código

Después de implementar el SQL, se necesita:

1. **Actualizar `BalanceCompletoDb`** (modelo)
   - Agregar campos si la vista cambia

2. **Actualizar `BuildBalanceTable()`**
   - Mostrar nueva fórmula de utilidad
   - Opcional: mostrar desglose (gasto material, proporción)

3. **Agregar columna `dias_estimados` en Manejo de Órdenes**
   - Campo editable en `EditOrderWindow`
   - Columna visible en grid de órdenes

---

## Archivos Creados

| Archivo | Propósito |
|---------|-----------|
| `README.md` | Documentación completa |
| `00_EJECUTAR.md` | Esta guía |
| `01_VERIFICAR_DIAS_ESTIMADOS.sql` | Diagnóstico |
| `02_ALTER_DIAS_ESTIMADOS.sql` | Migración columna |
| `03_VISTA_UTILIDAD_MENSUAL.sql` | Nueva vista |

---

## Contacto

**Proyecto:** Sistema de Gestión IMA Mecatrónica
