# Row Level Security (RLS) - Base de Datos IMA Mecatrónica
Generado: 2026-02-26 22:32:06

## Estado de RLS por Tabla

RLS Habilitado: 0 tablas | RLS Deshabilitado: 34 tablas
Total policies: 0

| Tabla | RLS | Forced | # Policies |
|-------|-----|--------|------------|
| `app_versions` | OFF | NO | 0 |
| `audit_log` | OFF | NO | 0 |
| `invoice_audit` | OFF | NO | 0 |
| `invoice_status` | OFF | NO | 0 |
| `order_gastos_indirectos` | OFF | NO | 0 |
| `order_gastos_operativos` | OFF | NO | 0 |
| `order_history` | OFF | NO | 0 |
| `order_status` | OFF | NO | 0 |
| `t_attendance` | OFF | NO | 0 |
| `t_attendance_audit` | OFF | NO | 0 |
| `t_balance_adjustments` | OFF | NO | 0 |
| `t_client` | OFF | NO | 0 |
| `t_commission_rate_history` | OFF | NO | 0 |
| `t_contact` | OFF | NO | 0 |
| `t_expense` | OFF | NO | 0 |
| `t_expense_audit` | OFF | NO | 0 |
| `t_fixed_expenses` | OFF | NO | 0 |
| `t_fixed_expenses_history` | OFF | NO | 0 |
| `t_holiday` | OFF | NO | 0 |
| `t_invoice` | OFF | NO | 0 |
| `t_order` | OFF | NO | 0 |
| `t_order_deleted` | OFF | NO | 0 |
| `t_overtime_hours` | OFF | NO | 0 |
| `t_overtime_hours_audit` | OFF | NO | 0 |
| `t_payroll` | OFF | NO | 0 |
| `t_payroll_history` | OFF | NO | 0 |
| `t_payrollovertime` | OFF | NO | 0 |
| `t_supplier` | OFF | NO | 0 |
| `t_vacation` | OFF | NO | 0 |
| `t_vacation_audit` | OFF | NO | 0 |
| `t_vendor` | OFF | NO | 0 |
| `t_vendor_commission_payment` | OFF | NO | 0 |
| `t_workday_config` | OFF | NO | 0 |
| `users` | OFF | NO | 0 |

## Policies

No hay policies RLS definidas en el esquema public.

## Análisis de Seguridad

### Tablas sensibles sin RLS
> Estas tablas podrían beneficiarse de RLS basado en su contenido

- `audit_log`
- `t_expense`
- `t_expense_audit`
- `t_payroll`
- `t_payroll_history`
- `t_payrollovertime`
- `t_vendor_commission_payment`
- `users`
