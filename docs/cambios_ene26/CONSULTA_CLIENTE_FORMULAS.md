# Consulta al Cliente: Definición de Fórmulas y Gastos

**Fecha:** 19 de Enero de 2026
**Propósito:** Clarificar conceptos antes de implementar

---

## 1. GASTO OPERATIVO (Pendiente de Reformular)

El PDF menciona "Gasto Operativo" como una columna editable en Manejo de Órdenes, pero indica "pendiente de reformular".

### Preguntas para el Cliente:

**1.1 ¿Qué tipo de gastos incluye "Gasto Operativo"?**
- [ ] Pagos sin factura (efectivo, transferencias informales)
- [ ] Gastos de transporte/logística
- [ ] Gastos de herramientas o consumibles
- [ ] Comisiones especiales
- [ ] Otros: _________________

**1.2 ¿Es un solo valor por orden o múltiples líneas?**
- [ ] **Opción A:** Un solo campo numérico editable (simple)
- [ ] **Opción B:** Múltiples líneas con descripción cada una (detallado)

**Ejemplo Opción A:**
```
Orden PO-2025-001
├── Gasto Material: $50,000 (calculado de facturas)
├── Gasto Operativo: $5,000 (un solo valor)
└── Gasto Indirecto: $2,000 (un solo valor)
```

**Ejemplo Opción B:**
```
Orden PO-2025-001
├── Gasto Material: $50,000 (calculado de facturas)
├── Gastos Operativos:
│   ├── Transporte materiales: $2,000
│   ├── Herramienta especial: $1,500
│   └── Pago a contratista: $1,500
│   └── TOTAL: $5,000
└── Gasto Indirecto: $2,000
```

**Propuesta:** Implementamos Opción B (detallado) porque permite auditoría y trazabilidad.

**1.3 ¿Quién puede editar estos gastos?**
- [ ] Solo Administración
- [ ] Solo Dirección
- [ ] Administración y Dirección
- [ ] Coordinación también puede
- [ ] Todos los roles

---

## 2. GASTO INDIRECTO

**2.1 ¿Qué tipo de gastos incluye "Gasto Indirecto"?**
- [ ] Porcentaje de nómina asignado a la orden
- [ ] Porcentaje de gastos fijos asignado a la orden
- [ ] Valor fijo ingresado manualmente
- [ ] Cálculo automático basado en días estimados
- [ ] Otros: _________________

**2.2 ¿Cómo se calcula o ingresa?**
- [ ] **Manual:** El usuario ingresa el valor directamente
- [ ] **Automático:** Se calcula como % de (nómina + gastos fijos) según días estimados
- [ ] **Híbrido:** Se sugiere automático pero permite editar

**Si es automático, ¿cuál es la fórmula?**
```
Ejemplo: gasto_indirecto = (nomina_mensual + gastos_fijos) * (dias_estimados / 30)
```

---

## 3. FÓRMULA DE UTILIDAD (Balance)

El PDF dice:
> "Utilidad = ventas_mensuales - sum(gasto_material, Gasto_Operativo, gasto_indirecto)"

### Confirmación:

**3.1 ¿Esta fórmula es por ORDEN o por MES?**
- [ ] **Por Orden:** Cada orden muestra su utilidad individual
- [ ] **Por Mes:** El Balance muestra utilidad mensual consolidada
- [ ] **Ambos:** Se muestra en ambos lugares

**3.2 ¿Qué se considera "ventas_mensuales" en el Balance?**
- [ ] Total de órdenes con fecha de PO en ese mes (`f_podate`)
- [ ] Total de facturas emitidas en ese mes (`f_invoicedate`)
- [ ] Total de facturas pagadas en ese mes (`f_paymentdate`)
- [ ] Total de facturas con fecha de recepción en ese mes

**Actualmente el sistema usa:** `f_podate` para ventas totales en `v_balance_ingresos`

---

## 4. SEMÁFORO DEL BALANCE

### Fórmula confirmada:
```
ROJO:     ventas = 0
AMARILLO: ventas >= (nomina + gasto_fijo) * 1.1
VERDE:    ventas >= (nomina + gasto_fijo) * 1.1 + 100,000
```

### Confirmaciones adicionales:

**4.1 ¿El semáforo se aplica a cada mes individualmente?**
- [x] Sí, cada celda de mes tiene su propio color
- [ ] No, es un indicador global

**4.2 ¿Los umbrales (1.1 y 100k) son los correctos?**
- [x] Sí, usar esos valores
- [ ] No, los valores correctos son: _________________

**4.3 ¿Debe haber un "naranja" intermedio entre rojo y amarillo?**
- [ ] No, solo rojo/amarillo/verde
- [ ] Sí, agregar naranja para: _________________

---

## 5. DÍAS ESTIMADOS

**5.1 ¿Para qué se usa este campo además de mostrarlo?**
- [ ] Solo informativo
- [ ] Para calcular gasto indirecto proporcional
- [ ] Para alertas de órdenes retrasadas
- [ ] Para reportes de productividad

**5.2 ¿Se calcula automáticamente o es manual?**
- [ ] Manual (el usuario lo ingresa)
- [ ] Sugerido automáticamente basado en tipo de orden
- [ ] Otro: _________________

---

## 6. PLANTILLA DE COLORES

El PDF menciona: "Cambiar todos los colores a como se indique con una plantilla, yo mando esa plantilla"

**6.1 ¿Ya está disponible la plantilla de colores?**
- [ ] Sí, adjuntar o enviar
- [ ] No, enviaré después

**6.2 ¿Qué elementos deben cambiar de color?**
- [ ] Semáforo del Balance
- [ ] Estados de órdenes
- [ ] Estados de facturas
- [ ] Botones y UI general
- [ ] Todos los anteriores

---

## Resumen de Respuestas Requeridas

| # | Pregunta | Respuesta |
|---|----------|-----------|
| 1.1 | Tipos de gasto operativo | |
| 1.2 | Un valor o múltiples líneas | |
| 1.3 | Quién puede editar | |
| 2.1 | Tipos de gasto indirecto | |
| 2.2 | Manual o automático | |
| 3.1 | Utilidad por orden o mes | |
| 3.2 | Qué son ventas mensuales | |
| 4.1 | Semáforo por mes | Sí |
| 4.2 | Umbrales correctos | Sí (1.1 y 100k) |
| 5.1 | Uso de días estimados | |
| 5.2 | Manual o automático | |
| 6.1 | Plantilla disponible | |

---

## Implementación Propuesta (Mientras esperamos respuestas)

Procederemos con:

1. **Gasto Operativo:** Implementar con detalle (múltiples líneas + descripción)
   - Más flexible, se puede simplificar después

2. **Gasto Indirecto:** Campo manual editable
   - Si después se requiere fórmula automática, se agrega

3. **Días Estimados:** Campo manual
   - Se puede agregar cálculo sugerido después

4. **Semáforo:** Con la fórmula indicada y umbrales configurables en BD

¿Está de acuerdo con esta aproximación?
