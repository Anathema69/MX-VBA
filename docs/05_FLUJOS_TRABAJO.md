# Flujos de Trabajo y Procesos

## 1. Ciclo de Vida de una Orden

### Diagrama de Estados

```mermaid
stateDiagram-v2
    [*] --> CREADA: Nueva orden

    CREADA --> EN_PROCESO: Iniciar trabajo
    CREADA --> CANCELADA: Cancelar

    EN_PROCESO --> LIBERADA: Completar trabajo
    EN_PROCESO --> CANCELADA: Cancelar

    LIBERADA --> CERRADA: Factura recibida
    LIBERADA --> COMPLETADA: 100% facturado

    CERRADA --> COMPLETADA: Pago recibido

    COMPLETADA --> [*]
    CANCELADA --> [*]
```

### Descripcion de Estados

| Estado | Codigo | Descripcion | Acciones Permitidas |
|--------|:------:|-------------|---------------------|
| CREADA | 0 | Orden recien creada | Editar, Cancelar, Pasar a EN_PROCESO |
| EN PROCESO | 1 | Trabajo en progreso | Editar, Actualizar progreso, Cancelar |
| LIBERADA | 2 | Trabajo completado, lista para facturar | Crear facturas |
| CERRADA | 3 | Facturada y recibida por cliente | Registrar pagos |
| COMPLETADA | 4 | Totalmente pagada | Solo lectura |
| CANCELADA | 5 | Orden cancelada | Solo lectura |

### Flujo Completo

```mermaid
sequenceDiagram
    participant Admin
    participant Sistema
    participant Coord as Coordinador
    participant Cliente

    Admin->>Sistema: Crear Orden (PO, Cliente, Total)
    Sistema->>Sistema: Estado = CREADA (0)

    Coord->>Sistema: Iniciar trabajo
    Sistema->>Sistema: Estado = EN_PROCESO (1)

    loop Durante produccion
        Coord->>Sistema: Actualizar progreso %
    end

    Coord->>Sistema: Marcar como completado
    Sistema->>Sistema: Estado = LIBERADA (2)

    Admin->>Sistema: Crear Factura
    Sistema->>Cliente: Enviar factura

    Cliente->>Admin: Confirmar recepcion
    Admin->>Sistema: Registrar recepcion
    Sistema->>Sistema: Estado = CERRADA (3)

    Cliente->>Admin: Realizar pago
    Admin->>Sistema: Registrar pago
    Sistema->>Sistema: Estado = COMPLETADA (4)
```

---

## 2. Proceso de Facturacion

### Flujo de Creacion de Factura

```mermaid
flowchart TD
    A[Orden en estado LIBERADA] --> B{Puede facturar?}
    B -->|No| C[Mensaje: Solo ordenes 1-4]
    B -->|Si| D[Abrir InvoiceManagementWindow]

    D --> E[Ingresar datos de factura]
    E --> F[Folio, Fecha, Subtotal, Total]
    F --> G[Calcular fecha vencimiento]
    G --> H{Validar datos}

    H -->|Error| I[Mostrar errores]
    I --> E

    H -->|OK| J[Guardar factura]
    J --> K[Estado factura = PENDIENTE]

    K --> L{Total facturado = Total orden?}
    L -->|Si| M[Cambiar orden a LIBERADA 100%]
    L -->|No| N[Mantener estado orden]

    M --> O[Fin]
    N --> O
```

### Estados de Factura

```mermaid
stateDiagram-v2
    [*] --> PENDIENTE: Factura creada

    PENDIENTE --> ENVIADA: Enviar al cliente
    PENDIENTE --> VENCIDA: Pasar fecha vencimiento

    ENVIADA --> VENCIDA: Pasar fecha vencimiento
    ENVIADA --> PAGADA: Recibir pago

    VENCIDA --> PAGADA: Recibir pago

    PAGADA --> [*]
```

### Calculo de Fecha de Vencimiento

```csharp
// Se calcula automaticamente basado en los dias de credito del cliente
DueDate = InvoiceDate + ClientCreditDays
```

---

## 3. Gestion de Gastos

### Flujo de Gastos

```mermaid
flowchart TD
    A[Crear Gasto] --> B[Seleccionar Proveedor]
    B --> C[Ingresar descripcion y monto]
    C --> D[Establecer fecha programada]
    D --> E[Estado = PENDIENTE]

    E --> F{Llego fecha programada?}
    F -->|No| G[Esperar]
    G --> F
    F -->|Si| H[Notificar pago pendiente]

    H --> I{Realizar pago?}
    I -->|No| J{Paso fecha?}
    J -->|Si| K[Marcar como VENCIDO]
    K --> I
    J -->|No| H

    I -->|Si| L[Registrar pago]
    L --> M[Estado = PAGADO]
    M --> N[Registrar metodo y fecha]
    N --> O[Fin]
```

### Estados de Gastos

| Estado | Descripcion |
|--------|-------------|
| PENDIENTE | Gasto creado, pendiente de pago |
| PAGADO | Gasto pagado |
| VENCIDO | Paso la fecha programada sin pagar |

---

## 4. Proceso de Comisiones

### Calculo de Comisiones

```mermaid
flowchart TD
    A[Orden creada] --> B[Asignar vendedor]
    B --> C[Definir tasa de comision]
    C --> D[Crear registro en vendor_commission_payments]
    D --> E[Estado = draft]

    E --> F{Orden pagada 100%?}
    F -->|No| G[Esperar pagos]
    G --> F

    F -->|Si| H[Estado = pending]
    H --> I{Admin aprueba pago?}
    I -->|No| J[Esperar aprobacion]
    J --> I

    I -->|Si| K[Registrar pago]
    K --> L[Estado = paid]
    L --> M[Registrar fecha de pago]
    M --> N[Fin]
```

### Vista del Vendedor

```mermaid
graph LR
    subgraph "Dashboard Vendedor"
        A[Total Draft] --> D[Ordenes en proceso]
        B[Total Pendiente] --> E[Listo para cobrar]
        C[Total Pagado] --> F[Historico]
    end
```

---

## 5. Flujo de Balance Mensual

### Calculo de Balance

```mermaid
flowchart TD
    A[Seleccionar mes/ano] --> B[Obtener ingresos del mes]
    B --> C[Sumar facturas pagadas]

    A --> D[Obtener gastos del mes]
    D --> E[Sumar gastos pagados]

    A --> F[Obtener nomina efectiva]
    F --> G[Calcular total nomina mensual]

    A --> H[Obtener gastos fijos efectivos]
    H --> I[Calcular total gastos fijos]

    A --> J[Obtener horas extras]
    J --> K[Calcular costo overtime]

    C --> L[TOTAL INGRESOS]
    E --> M[TOTAL GASTOS VARIABLES]
    G --> N[TOTAL NOMINA]
    I --> O[TOTAL GASTOS FIJOS]
    K --> P[TOTAL OVERTIME]

    L --> Q[BALANCE = Ingresos - Gastos - Nomina - Fijos - Overtime]
    M --> Q
    N --> Q
    O --> Q
    P --> Q

    Q --> R{Balance positivo?}
    R -->|Si| S[Verde: Ganancia]
    R -->|No| T[Rojo: Perdida]
```

### Datos Efectivos por Fecha

El sistema usa fechas efectivas para calculos retroactivos:

```csharp
// Obtener nomina efectiva para una fecha especifica
public async Task<List<PayrollTable>> GetEffectivePayroll(DateTime effectiveDate)
{
    // Retorna la nomina vigente en esa fecha
}

// Obtener gastos fijos efectivos
public async Task<List<FixedExpenseTable>> GetEffectiveFixedExpenses(DateTime effectiveDate)
{
    // Retorna gastos fijos vigentes en esa fecha
}
```

---

## 6. Flujo de Actualizaciones

```mermaid
sequenceDiagram
    participant App
    participant US as UpdateService
    participant DB as Supabase
    participant User

    App->>US: CheckForUpdate()
    US->>DB: GET app_versions (ultima)
    DB-->>US: AppVersionDb

    US->>US: Comparar versiones

    alt Nueva version disponible
        US-->>App: (true, newVersion)
        App->>User: Mostrar UpdateAvailableWindow

        alt Actualizacion obligatoria
            User->>App: Descargar e instalar
            App->>US: DownloadUpdate()
            US-->>App: Instalador descargado
            App->>App: Ejecutar instalador
            App->>App: Cerrar aplicacion
        else Actualizacion opcional
            alt Usuario acepta
                User->>App: Descargar ahora
                App->>US: DownloadUpdate()
            else Usuario postpone
                User->>App: Recordar despues
                App->>App: Continuar normal
            end
        end
    else Ya actualizado
        US-->>App: (false, null)
    end
```

---

## 7. Gestion de Clientes

### Flujo CRUD de Clientes

```mermaid
flowchart TD
    A[Abrir ClientManagementWindow] --> B[Cargar lista de clientes]

    B --> C{Accion?}

    C -->|Nuevo| D[Abrir NewClientWindow]
    D --> E[Ingresar datos cliente]
    E --> F{Validar datos}
    F -->|Error| G[Mostrar errores]
    G --> E
    F -->|OK| H[Guardar cliente]
    H --> I[Crear contacto principal]
    I --> B

    C -->|Editar| J[Seleccionar cliente]
    J --> K[Abrir editor]
    K --> L[Modificar datos]
    L --> M{Validar}
    M -->|OK| N[Actualizar]
    N --> B

    C -->|Desactivar| O[Confirmar desactivacion]
    O --> P[SoftDelete: is_active = false]
    P --> B

    C -->|Ver Contactos| Q[Abrir lista contactos]
    Q --> R[Gestionar contactos del cliente]
    R --> B
```

---

## 8. Timeout de Sesion

```mermaid
sequenceDiagram
    participant User as Usuario
    participant App
    participant STS as SessionTimeoutService
    participant Banner

    Note over STS: Timer cada 1 segundo

    loop Monitoreo activo
        STS->>STS: CheckInactivity()

        alt Actividad detectada
            User->>App: Mouse/Teclado
            App->>STS: ResetTimer()
            STS->>STS: _warningShown = false
            STS->>Banner: Hide()
        else 13 minutos sin actividad
            STS->>App: OnWarning
            App->>Banner: Show()
            Banner->>User: "Sesion cerrara en 2 min"
        else 15 minutos sin actividad
            STS->>App: OnTimeout
            App->>App: ForceLogout()
            App->>User: Mostrar LoginWindow
            App->>User: "Sesion cerrada por inactividad"
        end
    end
```

---

## 9. Ingresos Pendientes

### Flujo de Consulta

```mermaid
flowchart TD
    A[Abrir PendingIncomesView] --> B[Cargar facturas con status != PAGADA]

    B --> C[Agrupar por cliente]
    C --> D[Calcular totales por cliente]

    D --> E[Mostrar lista de clientes]
    E --> F{Seleccionar cliente?}

    F -->|Si| G[Abrir PendingIncomesDetailView]
    G --> H[Mostrar facturas del cliente]
    H --> I[Detalle por orden/factura]

    I --> J{Registrar pago?}
    J -->|Si| K[Abrir dialogo de pago]
    K --> L[Ingresar fecha y metodo]
    L --> M[Actualizar factura]
    M --> N[Recalcular estado orden]
    N --> B

    J -->|No| O[Volver a lista]
    O --> E

    F -->|No| P[Fin]
```

---

## Diagrama de Navegacion General

```mermaid
graph TB
    subgraph "Login"
        LW[LoginWindow]
    end

    subgraph "Admin Flow"
        MM[MainMenuWindow]
        OMW[OrdersManagementWindow]
        CMW[ClientManagementWindow]
        EMW[ExpenseManagementWindow]
        PMV[PayrollManagementView]
        BWP[BalanceWindowPro]
        VCW[VendorCommissionsWindow]
        PIV[PendingIncomesView]
    end

    subgraph "Coordinator Flow"
        OMW2[OrdersManagementWindow]
    end

    subgraph "Salesperson Flow"
        VD[VendorDashboard]
    end

    subgraph "Sub-ventanas"
        NOW[NewOrderWindow]
        EOW[EditOrderWindow]
        IMW[InvoiceManagementWindow]
        NCW[NewClientWindow]
        SMW[SupplierManagementWindow]
        EEW[EmployeeEditWindow]
        PIDV[PendingIncomesDetailView]
    end

    LW -->|admin| MM
    LW -->|coordinator| OMW2
    LW -->|salesperson| VD

    MM --> OMW
    MM --> CMW
    MM --> EMW
    MM --> PMV
    MM --> BWP
    MM --> VCW
    MM --> PIV

    OMW --> NOW
    OMW --> EOW
    OMW --> IMW
    OMW --> CMW

    CMW --> NCW

    EMW --> SMW

    PMV --> EEW

    PIV --> PIDV

    OMW2 --> EOW
```
