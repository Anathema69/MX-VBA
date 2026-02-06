# Resumen del Esquema de BD - IMA Mecatrónica

**Fecha de extracción:** 19 de Enero de 2026
**Servidor:** Supabase (PostgreSQL)
**Proyecto:** wjozxqldvypdtfmkamud

---

## Estadísticas Generales

| Métrica | Valor |
|---------|-------|
| Total de tablas | **28** |
| Total de funciones | **39** |
| Total de triggers | **43** |
| Total de vistas | **5** |
| Total de secuencias | **27** |

*Actualizado: 2026-02-06 - Triggers de gastos operativos y comision*

---

## Lista de Tablas

| Tabla | Columnas | Tamaño | Descripción |
|-------|----------|--------|-------------|
| `app_versions` | 13 | 144 kB | Control de versiones de la aplicación |
| `audit_log` | 10 | 32 kB | Log de auditoría general |
| `invoice_audit` | 7 | 16 kB | Auditoría de cambios en facturas |
| `invoice_status` | 5 | 24 kB | Catálogo de estados de factura |
| `order_gastos_indirectos` | 9 | - | Gastos indirectos por orden *(v2.0)* |
| `order_gastos_operativos` | 11 | - | Gastos operativos por orden (con f_commission_rate) *(v2.0, actualizada 2026-02-06)* |
| `order_history` | 10 | 272 kB | Histórico de cambios en órdenes |
| `order_status` | 5 | 24 kB | Catálogo de estados de orden |
| `t_balance_adjustments` | 10 | 24 kB | Ajustes manuales al balance |
| `t_client` | 13 | 80 kB | Clientes |
| `t_commission_rate_history` | 17 | 80 kB | Historial de cambios en comisiones |
| `t_contact` | 10 | 104 kB | Contactos de clientes |
| `t_expense` | 15 | 120 kB | Gastos/Cuentas por pagar |
| `t_expense_audit` | 28 | - | **Auditoría de gastos** *(Nueva)* |
| `t_fixed_expenses` | 9 | 24 kB | Gastos fijos mensuales |
| `t_fixed_expenses_history` | 9 | 32 kB | Historial de gastos fijos |
| `t_invoice` | 17 | 216 kB | Facturas |
| `t_order` | 24 | 184 kB | **Órdenes** (tabla core) |
| `t_order_deleted` | 21 | 80 kB | Auditoría de órdenes eliminadas |
| `t_overtime_hours` | 9 | 32 kB | Horas extras mensuales |
| `t_overtime_hours_audit` | 12 | 24 kB | Auditoría de horas extras |
| `t_payroll` | 19 | 32 kB | Nómina de empleados |
| `t_payroll_history` | 22 | 80 kB | Historial de cambios en nómina |
| `t_payrollovertime` | 10 | 56 kB | Relación nómina-horas extras |
| `t_supplier` | 10 | 72 kB | Proveedores |
| `t_vendor` | 9 | 64 kB | Vendedores |
| `t_vendor_commission_payment` | 13 | 112 kB | Pagos de comisiones |
| `users` | 10 | 96 kB | Usuarios del sistema |

---

## Vistas de BD

| Vista | Propósito |
|-------|-----------|
| `v_balance_completo` | Balance mensual completo (gastos + ingresos + utilidad) |
| `v_balance_gastos` | Desglose de gastos mensuales (nómina, fijos, variables) |
| `v_balance_ingresos` | Ingresos esperados y percibidos por mes |
| `v_income` | Detalle de ingresos por factura con estado |
| `v_expense_audit_report` | Reportes de auditoría de gastos *(Nueva)* |

---

## Diagrama de Alto Nivel

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              MÓDULOS DEL SISTEMA                            │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  USUARIOS & AUTH         ÓRDENES (CORE)           FACTURACIÓN              │
│  ────────────────        ──────────────           ───────────              │
│  • users                 • t_order                • t_invoice              │
│  • audit_log             • order_status           • invoice_status         │
│                          • order_history          • invoice_audit          │
│                          • t_order_deleted                                 │
│                                                                             │
│  CLIENTES                PROVEEDORES/GASTOS       COMISIONES               │
│  ────────                ─────────────────        ──────────               │
│  • t_client              • t_supplier             • t_vendor               │
│  • t_contact             • t_expense              • t_vendor_commission_   │
│                          • t_expense_audit          payment                │
│                          • order_gastos_           • t_commission_rate_     │
│                            operativos               history                │
│                          • order_gastos_                                   │
│                            indirectos                                      │
│                                                                             │
│  NÓMINA/RRHH             BALANCE/FINANZAS         SISTEMA                  │
│  ──────────              ───────────────          ───────                  │
│  • t_payroll             • t_fixed_expenses       • app_versions           │
│  • t_payroll_history     • t_fixed_expenses_      • t_balance_adjustments  │
│  • t_overtime_hours        history                                         │
│  • t_overtime_hours_     • v_balance_completo                              │
│    audit                 • v_balance_gastos                                │
│  • t_payrollovertime     • v_balance_ingresos                              │
│                          • v_income                                        │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Roles Actuales (Constraint en BD)

```sql
-- Constraint: users_role_check
CHECK (role IN ('admin', 'coordinator', 'salesperson'))
```

| Rol | Descripción |
|-----|-------------|
| `admin` | Acceso total al sistema |
| `coordinator` | Gestión de órdenes |
| `salesperson` | Portal de vendedores (comisiones) |

**IMPORTANTE para v2.0:** Este constraint deberá modificarse para soportar los nuevos roles:
- `direccion`
- `administracion`
- `proyectos`
- `coordinacion`
- `ventas`

---

## Secuencias (Auto-increment)

| Secuencia | Tabla |
|-----------|-------|
| `users_id_seq` | users |
| `t_order_f_order_seq` | t_order |
| `t_client_f_client_seq` | t_client |
| `t_invoice_f_invoice_seq` | t_invoice |
| `t_expense_f_expense_seq` | t_expense |
| `t_supplier_f_supplier_seq` | t_supplier |
| `t_vendor_f_vendor_seq` | t_vendor |
| `t_vendor_commission_payment_id_seq` | t_vendor_commission_payment |
| `t_payroll_f_payroll_seq` | t_payroll |
| ... (26 total) | |

---

## Hallazgos Importantes

### Tablas NO documentadas previamente:
1. `audit_log` - Log general de auditoría
2. `invoice_audit` - Auditoría específica de facturas
3. `t_balance_adjustments` - Ajustes manuales al balance
4. `t_overtime_hours` - Horas extras por mes
5. `t_overtime_hours_audit` - Auditoría de horas extras
6. `t_payrollovertime` - Relación nómina-horas extras

### Vistas NO documentadas previamente:
- Todas las 4 vistas son críticas para el módulo de Balance

### Funciones NO documentadas previamente:
- `f_balance_anual_horizontal` - Balance anual pivoteado
- `f_balance_completo_horizontal` - Balance completo pivoteado
- `get_payroll_at_date` - Nómina histórica por fecha
- `get_monthly_payroll_total` - Total nómina mensual
- Y muchas más (36 total)

---

## Próximos Pasos

Ver documento: `docs/cambios_ene26/ANALISIS_BRECHAS.md`
