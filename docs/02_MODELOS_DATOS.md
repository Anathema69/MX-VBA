# Modelos y Esquema de Base de Datos

## Diagrama Entidad-Relacion (ERD)

```mermaid
erDiagram
    users ||--o{ t_order : "crea/modifica"
    users ||--o| t_vendor : "es vendedor"
    t_client ||--o{ t_order : "tiene"
    t_client ||--o{ t_contact : "tiene"
    t_order ||--o{ t_invoice : "tiene"
    t_order ||--o{ t_expense : "tiene"
    t_order }o--|| order_status : "tiene"
    t_order }o--|| t_vendor : "asignado"
    t_invoice }o--|| invoice_status : "tiene"
    t_expense }o--|| t_supplier : "de"
    t_vendor ||--o{ vendor_commission_payments : "recibe"
    t_order ||--o{ vendor_commission_payments : "genera"
    t_order ||--o{ order_history : "registra"
    t_payroll ||--o{ t_payroll_history : "historial"
    t_fixed_expenses ||--o{ t_fixed_expenses_history : "historial"

    users {
        int id PK
        string username
        string email
        string password_hash
        string full_name
        string role
        boolean is_active
        datetime last_login
    }

    t_client {
        int f_client PK
        string f_name
        string f_address1
        string f_address2
        int f_credit
        string tax_id
        string phone
        string email
        boolean is_active
        datetime created_at
        datetime updated_at
        int created_by FK
        int updated_by FK
    }

    t_contact {
        int f_contact PK
        int f_client FK
        string f_contactname
        string f_email
        string f_phone
        string position
        boolean is_primary
        boolean is_active
    }

    t_order {
        int f_order PK
        string f_po
        string f_quote
        datetime f_podate
        int f_client FK
        int f_contact FK
        string f_description
        int f_salesman FK
        datetime f_estdelivery
        decimal f_salesubtotal
        decimal f_saletotal
        decimal f_expense
        int f_orderstat FK
        int progress_percentage
        int order_percentage
        decimal f_commission_rate
        int created_by FK
        int updated_by FK
        datetime created_at
        datetime updated_at
    }

    order_status {
        int f_orderstatus PK
        string f_name
        boolean is_active
        int display_order
    }

    t_invoice {
        int f_invoice PK
        int f_order FK
        string f_folio
        datetime f_invoicedate
        datetime f_receptiondate
        decimal f_subtotal
        decimal f_total
        int f_invoicestat FK
        datetime f_paymentdate
        datetime due_date
        string payment_method
        string payment_reference
        decimal balance_due
        datetime created_at
        datetime updated_at
        int created_by FK
    }

    invoice_status {
        int f_invoicestat PK
        string f_name
        boolean is_active
        int display_order
    }

    t_expense {
        int f_expense PK
        int f_supplier FK
        string f_description
        datetime f_expensedate
        decimal f_totalexpense
        datetime f_scheduleddate
        string f_status
        datetime f_paiddate
        string f_paymethod
        int f_order FK
        string expense_category
        datetime created_at
        datetime updated_at
        string created_by
    }

    t_supplier {
        int f_supplier PK
        string f_suppliername
        int f_credit
        string tax_id
        string phone
        string email
        string address
        boolean is_active
        datetime created_at
        datetime updated_at
    }

    t_vendor {
        int f_vendor PK
        string f_vendorname
        int f_user_id FK
        decimal f_commission_rate
        string f_phone
        string f_email
        boolean is_active
        datetime created_at
        datetime updated_at
    }

    vendor_commission_payments {
        int id PK
        int f_vendor FK
        int f_order FK
        decimal commission_amount
        string payment_status
        datetime payment_date
    }

    t_payroll {
        int f_payroll PK
        string f_employee
        string f_title
        datetime f_hireddate
        string f_range
        string f_condition
        datetime f_lastraise
        decimal f_sspayroll
        decimal f_weeklypayroll
        decimal f_socialsecurity
        string f_benefits
        decimal f_benefitsamount
        decimal f_monthlypayroll
        string employee_code
        boolean is_active
        datetime created_at
        datetime updated_at
        int created_by FK
        int updated_by FK
    }

    t_payroll_history {
        int id PK
        int f_payroll FK
        string f_employee
        string f_title
        decimal f_monthlypayroll
        datetime effective_date
        string change_type
        string change_summary
        int created_by FK
        datetime created_at
    }

    t_fixed_expenses {
        int id PK
        string expense_type
        string description
        decimal monthly_amount
        boolean is_active
        int created_by FK
        datetime created_at
        datetime updated_at
        datetime effective_date
    }

    t_fixed_expenses_history {
        int id PK
        int expense_id FK
        string description
        decimal monthly_amount
        datetime effective_date
        string change_type
        string change_summary
        int created_by FK
        datetime created_at
    }

    order_history {
        int id PK
        int f_order FK
        int user_id FK
        string action
        string field_name
        string old_value
        string new_value
        string change_description
        datetime changed_at
    }
```

## Tablas Principales

### 1. users - Usuarios del Sistema

| Columna | Tipo | Descripcion |
|---------|------|-------------|
| id | int (PK) | Identificador unico |
| username | string | Nombre de usuario (login) |
| email | string | Correo electronico |
| password_hash | string | Hash BCrypt de la contrasena |
| full_name | string | Nombre completo |
| role | string | Rol del usuario (admin/coordinator/salesperson) |
| is_active | boolean | Estado activo/inactivo |
| last_login | datetime | Ultimo inicio de sesion |

**Roles disponibles:**
- `admin` - Administrador con acceso total
- `coordinator` - Coordinador con acceso limitado a ordenes
- `salesperson` - Vendedor con acceso solo a sus comisiones

### 2. t_order - Ordenes/Proyectos

| Columna | Tipo | Descripcion |
|---------|------|-------------|
| f_order | int (PK) | ID de la orden |
| f_po | string | Numero de Purchase Order |
| f_quote | string | Numero de cotizacion |
| f_podate | datetime | Fecha de la orden |
| f_client | int (FK) | Cliente asociado |
| f_contact | int (FK) | Contacto del cliente |
| f_description | string | Descripcion del proyecto |
| f_salesman | int (FK) | Vendedor asignado |
| f_estdelivery | datetime | Fecha estimada de entrega |
| f_salesubtotal | decimal | Subtotal de venta |
| f_saletotal | decimal | Total de venta |
| f_expense | decimal | Gastos asociados |
| f_orderstat | int (FK) | Estado de la orden |
| progress_percentage | int | Porcentaje de progreso (0-100) |
| order_percentage | int | Porcentaje de ordenacion |
| f_commission_rate | decimal | Tasa de comision |

### 3. order_status - Estados de Ordenes

| ID | Nombre | Descripcion |
|----|--------|-------------|
| 0 | CREADA | Orden recien creada |
| 1 | EN PROCESO | Orden en trabajo |
| 2 | LIBERADA | Orden lista para facturar |
| 3 | CERRADA | Facturada y recibida |
| 4 | COMPLETADA | Pagada completamente |
| 5 | CANCELADA | Orden cancelada |

```mermaid
stateDiagram-v2
    [*] --> CREADA
    CREADA --> EN_PROCESO: Iniciar trabajo
    EN_PROCESO --> LIBERADA: Completar trabajo
    LIBERADA --> CERRADA: Facturar y recibir
    CERRADA --> COMPLETADA: Pago recibido
    CREADA --> CANCELADA: Cancelar
    EN_PROCESO --> CANCELADA: Cancelar
```

### 4. t_invoice - Facturas

| Columna | Tipo | Descripcion |
|---------|------|-------------|
| f_invoice | int (PK) | ID de factura |
| f_order | int (FK) | Orden asociada |
| f_folio | string | Numero de folio |
| f_invoicedate | datetime | Fecha de facturacion |
| f_receptiondate | datetime | Fecha de recepcion |
| f_subtotal | decimal | Subtotal |
| f_total | decimal | Total |
| f_invoicestat | int (FK) | Estado de la factura |
| f_paymentdate | datetime | Fecha de pago |
| due_date | datetime | Fecha de vencimiento |
| payment_method | string | Metodo de pago |
| payment_reference | string | Referencia del pago |
| balance_due | decimal | Saldo pendiente |

### 5. invoice_status - Estados de Facturas

| ID | Nombre | Descripcion |
|----|--------|-------------|
| 1 | PENDIENTE | Factura pendiente |
| 2 | ENVIADA | Factura enviada al cliente |
| 3 | VENCIDA | Factura vencida |
| 4 | PAGADA | Factura pagada |

### 6. t_client - Clientes

| Columna | Tipo | Descripcion |
|---------|------|-------------|
| f_client | int (PK) | ID del cliente |
| f_name | string | Nombre del cliente |
| f_address1 | string | Direccion linea 1 |
| f_address2 | string | Direccion linea 2 |
| f_credit | int | Dias de credito |
| tax_id | string | RFC/Tax ID |
| phone | string | Telefono |
| email | string | Email |
| is_active | boolean | Estado activo |

### 7. t_expense - Gastos

| Columna | Tipo | Descripcion |
|---------|------|-------------|
| f_expense | int (PK) | ID del gasto |
| f_supplier | int (FK) | Proveedor |
| f_description | string | Descripcion |
| f_expensedate | datetime | Fecha del gasto |
| f_totalexpense | decimal | Monto total |
| f_scheduleddate | datetime | Fecha programada de pago |
| f_status | string | Estado (PENDIENTE/PAGADO) |
| f_paiddate | datetime | Fecha de pago |
| f_paymethod | string | Metodo de pago |
| f_order | int (FK) | Orden asociada (opcional) |
| expense_category | string | Categoria del gasto |

### 8. t_payroll - Nomina

| Columna | Tipo | Descripcion |
|---------|------|-------------|
| f_payroll | int (PK) | ID del empleado |
| f_employee | string | Nombre del empleado |
| f_title | string | Puesto |
| f_hireddate | datetime | Fecha de contratacion |
| f_range | string | Rango/Nivel |
| f_condition | string | Condicion laboral |
| f_lastraise | datetime | Ultimo aumento |
| f_sspayroll | decimal | Salario SS |
| f_weeklypayroll | decimal | Salario semanal |
| f_socialsecurity | decimal | Seguro social |
| f_benefits | string | Beneficios |
| f_benefitsamount | decimal | Monto beneficios |
| f_monthlypayroll | decimal | Salario mensual |
| employee_code | string | Codigo empleado |
| is_active | boolean | Estado activo |

### 9. t_vendor - Vendedores

| Columna | Tipo | Descripcion |
|---------|------|-------------|
| f_vendor | int (PK) | ID del vendedor |
| f_vendorname | string | Nombre del vendedor |
| f_user_id | int (FK) | Usuario asociado |
| f_commission_rate | decimal | Tasa de comision (%) |
| f_phone | string | Telefono |
| f_email | string | Email |
| is_active | boolean | Estado activo |

### 10. vendor_commission_payments - Pagos de Comisiones

| Columna | Tipo | Descripcion |
|---------|------|-------------|
| id | int (PK) | ID del pago |
| f_vendor | int (FK) | Vendedor |
| f_order | int (FK) | Orden asociada |
| commission_amount | decimal | Monto de comision |
| payment_status | string | Estado (draft/pending/paid) |
| payment_date | datetime | Fecha de pago |

## Mapeo ORM (Postgrest)

Los modelos utilizan atributos de Postgrest para el mapeo:

```csharp
[Table("t_order")]
public class OrderDb : BaseModel
{
    [PrimaryKey("f_order", shouldInsert: false)]
    public int Id { get; set; }

    [Column("f_po")]
    public string Po { get; set; }

    [Column("f_client")]
    public int? ClientId { get; set; }

    // ... otros campos
}
```

## ViewModels

### OrderViewModel
Modelo de vista para mostrar ordenes en el DataGrid:

```csharp
public class OrderViewModel
{
    public int Id { get; set; }
    public string OrderNumber { get; set; }     // f_po
    public DateTime OrderDate { get; set; }      // f_podate
    public string ClientName { get; set; }       // Lookup desde t_client
    public string VendorName { get; set; }       // Lookup desde t_vendor
    public string Description { get; set; }      // f_description
    public decimal Subtotal { get; set; }        // f_salesubtotal
    public decimal Total { get; set; }           // f_saletotal
    public decimal InvoicedAmount { get; set; }  // Suma de facturas
    public int Progress { get; set; }            // progress_percentage
    public string Status { get; set; }           // Lookup desde order_status
}
```

## Auditoría y Historial

El sistema implementa auditoria en varias tablas:

### order_history
Registra cambios en ordenes:
- `action`: Tipo de accion (CREATE, UPDATE, STATUS_CHANGE, DELETE)
- `field_name`: Campo modificado
- `old_value`: Valor anterior
- `new_value`: Nuevo valor
- `change_description`: Descripcion del cambio

### t_payroll_history / t_fixed_expenses_history
Registra cambios historicos con fechas efectivas para calculos retroactivos de balance.
