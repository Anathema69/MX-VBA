# Fase 4 — Plataforma IMA Mecatronica

> **Cliente:** IMA Mecatronica
> **Periodo:** Feb 2026 — Mar 2026
> **Version:** v2.0.4 → v2.3.1
> **Stack:** .NET 8.0 WPF + Supabase (PostgreSQL) + Cloudflare R2
> **Commits:** 35 | **Archivos:** 149+ | **Lineas:** +32,154

---

## Estado Final

```
 FASE 4 COMPLETADA ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ 100%

 ┌─────────┬──────────────────────────────────────┬───────────┐
 │ Bloque  │ Nombre                               │  Estado   │
 ├─────────┼──────────────────────────────────────┼───────────┤
 │   B1    │ Cosmeticos pendientes Fase 3         │ ✅ 7/7    │
 │   B2    │ Optimizacion de rendimiento          │ ✅ 6/6    │
 │   B3    │ Portal Ventas + Storage              │ ✅ 4/4    │
 │   B4    │ Columna Ejecutor                     │ ✅ 3/3    │
 │   B5    │ IMA Drive (Archivos en la nube)      │ ✅ 5/5    │
 │   B6    │ Modulo de Inventario                 │ ✅ 4/4    │
 ├─────────┼──────────────────────────────────────┼───────────┤
 │  EXTRA  │ Drive V3 (Preview/Recientes/Ops)     │ ✅ 23/23  │
 │  EXTRA  │ Mejoras post-produccion (7 items)    │ ✅ 7/7    │
 │  EXTRA  │ Sync carpetas + UI redesign          │ ✅ DONE   │
 └─────────┴──────────────────────────────────────┴───────────┘
```

---

## Modulos Entregados

### 1. IMA Drive — Gestion de Archivos en la Nube

```
 ┌──────────────────────────────────────────────────────────────┐
 │                      ARQUITECTURA                            │
 │                                                              │
 │   WPF UI (DriveV2Window)                                     │
 │      │                                                       │
 │      ├─── DriveService ──── Cloudflare R2 (blobs)            │
 │      │        │                                              │
 │      │        └──────────── Supabase PostgreSQL (metadatos)  │
 │      │                                                       │
 │      └─── FileWatcherService ── %LOCALAPPDATA%/IMA-Drive/    │
 │               (auto-sync)        (cache local + manifest)    │
 └──────────────────────────────────────────────────────────────┘
```

**Almacenamiento:** Cloudflare R2 (10GB free, sin costos de egress)

**Funcionalidades:**
- Navegacion de carpetas con breadcrumb, historial atras/adelante
- Vista cuadricula y lista con ordenamiento por nombre/tipo/tamano/fecha
- CRUD completo: crear, renombrar, mover, copiar, eliminar carpetas y archivos
- Upload multiple paralelo (5 simultaneos) con ghost cards de progreso
- Descarga individual, multiple y carpetas como ZIP
- Vinculacion de carpetas a ordenes de compra (columna CARPETA en Ordenes)
- Busqueda scoped (dentro de carpeta actual o global) con resultados agrupados
- Recientes (mis archivos / todos) con feed de actividad en sidebar
- Filtros por tipo: PDFs, Imagenes, CAD, Hojas de calculo, Videos
- Cortar/Copiar/Pegar (Ctrl+X/C/V), atajos tipo Explorer
- Indicador de almacenamiento global y cache local

**Open-in-Place (edicion nativa):**
- Doble clic abre con la app del sistema + auto-sync al guardar
- FileWatcher con debounce 2s, deteccion de conflictos, resolucion manual
- Badges de sincronizacion (verde=abierto, azul=syncing, check=synced, rojo=error)
- Deteccion de "Save As" en apps CAD (archivos nuevos en subdirectorio)

**Soporte CAD/CNC (13 extensiones):**

```
 ┌─────────────┬─────────────────┬──────────┬───────────┐
 │  Subtipo    │  Extensiones    │  Color   │  Icono    │
 ├─────────────┼─────────────────┼──────────┼───────────┤
 │  Piezas     │ .ipt .sldprt    │ Morado   │ ruler     │
 │  Ensambles  │ .iam .sldasm    │ Teal     │ gear      │
 │  Planos     │ .dwg .dxf       │ Morado   │ ruler     │
 │  Modelos 3D │ .step .stp .igs │ Morado   │ ruler     │
 │  CNC        │ .mcam .mcx-*    │ Naranja  │ wrench    │
 └─────────────┴─────────────────┴──────────┴───────────┘
```

- Sub-filtros CAD en sidebar (Ensambles/Piezas/Planos/Modelos 3D/CNC)
- Descarga de contexto: al abrir ensamble, descarga todas las piezas de la carpeta
- Thumbnails via Windows Shell (si Inventor/SolidWorks instalado)
- Filtro de basura automatico (~$, .db, .lck, .tmp)
- Fallback "Abrir con..." si no hay programa asociado

**Sincronizacion de carpetas:**
- Drag-drop de carpeta desde Windows sincroniza arbol completo
- Deteccion de duplicados con opciones Sobrescribir/Omitir
- Creacion paralela por nivel (10 carpetas simultaneas)
- Overlay de progreso con barra, porcentaje y boton Cancelar
- Analisis instantaneo (2 queries bulk vs N)

**Base de datos:**
- 3 tablas: `drive_folders`, `drive_files`, `drive_activity`
- 6 triggers de auditoria automatica
- 7 RPCs optimizadas (breadcrumb, stats, busqueda, validaciones)

---

### 2. Modulo de Inventario

```
 ┌──────────────────────────────────────────────────────────┐
 │  INVENTARIO                                              │
 │                                                          │
 │  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐ │
 │  │Productos │  │ Por pedir│  │Categorias│  │  Valor   │ │
 │  │   142    │  │    8     │  │    8     │  │ $24,500  │ │
 │  └──────────┘  └──────────┘  └──────────┘  └──────────┘ │
 │                                                          │
 │  Sidebar              Detalle de productos               │
 │  ┌────────┐          ┌─────────────────────┐             │
 │  │ Torn.  │──────────│ Codigo │ Nombre │ Q │             │
 │  │ Cable. │          │ T-001  │ Torn M6│12 │             │
 │  │ Conect.│          │ T-002  │ Tuerca │ 0 │ ← ALERTA   │
 │  │ Herram.│          │ ...    │ ...    │.. │             │
 │  └────────┘          └─────────────────────┘             │
 └──────────────────────────────────────────────────────────┘
```

- 8 categorias pre-configuradas
- Creacion/edicion inline sin dialogos modales
- Alertas de stock bajo con indicadores visuales
- Filtros por ubicacion, stock bajo y busqueda
- Auditoria completa de cambios
- **BD:** 4 tablas, 3 vistas, 6 funciones RPC, 11 indices

---

### 3. Portal Ventas V2

- Dashboard vendedor con cards compactas y galeria de facturas
- Preview modal con zoom (50%-500%), pan, doble clic reset
- Boton "Liberar Orden" (cambia estado + comision a pending)
- Stepper visual de 3 pasos (Liberada → Revision → Pago)
- Galeria en panel admin con preview completo

---

### 4. Columna Ejecutor en Ordenes

- Asignacion de empleados de nomina a ordenes de compra
- Chips coloridos con iniciales en columna del DataGrid
- Dialogo de seleccion estilo Notion/Linear con busqueda

---

### 5. Plataforma — Ventana Unica

```
 ANTES                          AHORA
 ┌──────────┐ ┌──────────┐     ┌──────────┐
 │ MainMenu │ │ Nomina   │     │ Nomina   │  ← solo 1 ventana
 └──────────┘ └──────────┘     └──────────┘
     2 en taskbar                  1 en taskbar
                                   al cerrar → MainMenu reaparece
```

- 10 modulos migrados: Drive, Inventario, Ordenes, Ventas, Proveedores, Ingresos, Nomina, Balance, Calendario, Usuarios

---

### 6. Rediseno UX/UI

- **Paleta:** 8 tokens semanticos (Success, Warning, Danger, Info), Tailwind unificado
- **Skeleton loading:** ghost cards/rows con pulse animation
- **Staggered fade-in:** 30ms delay/card al renderizar contenido
- **Toast:** estilo light pill (fondo blanco, border color, boton cerrar X)
- **Confirm dialog:** backdrop dim + scale/fade animation + icono contextual
- **Cards:** hover scale 1.015 + shadow elevation
- **Botones:** ColorAnimation hover/pressed, nuevo GhostButton
- **Overlay progreso:** fondo dim + card blanca + barra + porcentaje + cancelar

---

## Rendimiento

```
 ┌──────────────────────────────────────────────────────────┐
 │  BENCHMARKS (Sao Paulo → CDMX)                          │
 │                                                          │
 │  Navegacion Drive (cold)         258ms                   │
 │  Navegacion Drive (cache-hit)      9ms  ↓ 96.6%         │
 │  Seleccion vendedor (Portal)       1ms  ↓ 99.7%         │
 │  Stats de 100 carpetas           1 query (antes 200)     │
 │  Breadcrumb                      1 CTE  (antes N seq.)   │
 │  Eliminar carpeta grande         2 queries (antes N rec.) │
 │  Analisis sync carpeta           2 queries (antes N)     │
 │  GetAllFilesFlat                 paginado (antes max 1000)│
 └──────────────────────────────────────────────────────────┘
```

---

## Bugs Corregidos

| Bug | Modulo | Descripcion |
|-----|--------|-------------|
| BUG-005 | Portal Proveedores | Boton eliminar visible en gastos pagados |
| BUG-006 | Calendario | Crash al guardar TimeSpan null |
| Smart Freshness | Global | Datos obsoletos en todos los modulos |
| Loop update | Plataforma | Bucle infinito pidiendo actualizar |
| Drag-drop | Drive | No aceptaba archivos despues de rediseno |
| Sync cancel | Drive | App se colgaba al cancelar (deadlock) |
| Delete folder | Drive | No respondia con cientos de archivos |
| Confirm dialog | Drive | Botones no funcionaban (WindowStyle.None) |
| Duplicados | Drive | Error 23505 silencioso en upload |
| File assoc | Drive | Process.Start fallaba con asociacion rota |

---

## Estructura del Repositorio

```
MX-VBA/
├── SistemaGestionProyectos2/           Codigo fuente (.NET 8 WPF)
│   ├── Helpers/                        ShellThumbnailHelper, WindowHelper
│   ├── Models/Database/                12 modelos Postgrest
│   ├── Models/DTOs/                    ViewModels, DriveDTOs, InventoryDTOs
│   ├── Services/                       14 servicios especializados
│   │   ├── Drive/                      DriveService + FileWatcherService
│   │   ├── Inventory/                  InventoryService
│   │   ├── Storage/                    StorageService (Supabase Storage)
│   │   └── Core/                       BaseService, Cache, DataChanged
│   ├── Views/                          24 ventanas WPF
│   ├── Tests/                          Stress tests, workflow tests
│   ├── ico-ima/                        7 iconos PNG
│   ├── sql/                            10 scripts BD
│   ├── installer.iss                   Config Inno Setup
│   └── appsettings.json                Config (R2, Supabase, DevMode)
├── db-docs/                            7 scripts Python autodoc BD
├── docs/                               Release process, manual instalacion
└── fase4/                              Documentacion Fase 4
    ├── README.md                       Este archivo
    ├── bloques/                         Specs por bloque (01-06)
    ├── drive-v3/                        Plan V3 (7 fases A-G)
    ├── mejoras-drive/                   Mejoras post-produccion (7/7)
    ├── Modulo de Inventario/            Mockup Figma + capturas
    ├── _capturas/                       Capturas del cliente
    ├── bugs.md                          Tracking de bugs
    ├── logs.md                          Log de implementacion
    └── plan-ux-drive.md                 Plan rediseno UX
```

---

## Base de Datos

```
 ┌─────────────────────────────────────────────────────┐
 │  TABLAS: 39  │  VISTAS: 13  │  FUNCIONES RPC: 19   │
 │  TRIGGERS: 34│  INDICES: 106│  ROLES: 7             │
 └─────────────────────────────────────────────────────┘

 Tablas nuevas en Fase 4:
 ─────────────────────────
 order_ejecutores          (many-to-many ordenes ↔ empleados)
 order_files               (archivos de Portal Ventas)
 drive_folders             (arbol de carpetas, auto-referencial)
 drive_files               (metadatos de archivos)
 drive_activity            (log de actividad app-level)
 drive_audit               (auditoria trigger-level)
 inventory_categories      (categorias de inventario)
 inventory_products        (productos con stock)
 inventory_movements       (movimientos de stock)
 inventory_audit           (auditoria de inventario)
 app_versions              (control de versiones + auto-update)
```

---

## Releases

| Version | Fecha | Highlights |
|---------|-------|------------|
| v2.0.5 | Feb 2026 | Cosmeticos Fase 3 + rendimiento |
| v2.0.6 | Mar 2026 | Portal Ventas + Storage |
| v2.0.7 | Mar 2026 | IMA Drive v1 (Cloudflare R2) |
| v2.0.8 | Mar 2026 | Portal Proveedores + cache fix |
| v2.0.9 | Mar 2026 | Inventario mockup |
| v2.1.0 | Mar 2026 | Drive V3 (Preview, Recientes, Operaciones) |
| v2.1.1 | Mar 2026 | Open-in-Place + UX fixes |
| v2.2.0 | Mar 2026 | Inventario completo + Drive produccion |
| v2.3.0 | Mar 2026 | Mejoras CAD + ventana unica |
| v2.3.1 | Mar 2026 | Sync carpetas + rediseno UX/UI |

---

## Documentacion Tecnica

| Archivo | Contenido |
|---------|-----------|
| [bloques/01-cosmeticos.md](bloques/01-cosmeticos.md) | Ajustes visuales Fase 3 |
| [bloques/02-rendimiento.md](bloques/02-rendimiento.md) | Optimizacion de rendimiento |
| [bloques/03-portal-ventas.md](bloques/03-portal-ventas.md) | Portal Ventas + archivos |
| [bloques/04-ejecutor.md](bloques/04-ejecutor.md) | Columna Ejecutor |
| [bloques/05-archivos-drive.md](bloques/05-archivos-drive.md) | IMA Drive completo |
| [bloques/06-inventario.md](bloques/06-inventario.md) | Modulo Inventario |
| [drive-v3/README.md](drive-v3/README.md) | Plan Drive V3 (7 fases) |
| [mejoras-drive/README.md](mejoras-drive/README.md) | Mejoras post-produccion |
| [plan-ux-drive.md](plan-ux-drive.md) | Plan rediseno UX |
| [bugs.md](bugs.md) | Bugs y correcciones |
| [logs.md](logs.md) | Log de implementacion |

---

## Herramientas de Desarrollo

- **db-docs/**: 7 scripts Python para autodoc de BD (tablas, relaciones, vistas, funciones, indices, RLS, diagrama ER)
- **sql/verify_drive_integrity.sql**: 8 queries para verificar integridad R2 vs BD
- **sql/cleanup_drive_basura.sql**: Limpieza de archivos basura (~$, .db, .lck)
- **Diagnosticar** (boton dev en Drive): compara R2 vs BD, detecta huerfanos, ofrece limpieza
- **Instalador**: Inno Setup con certificado Authenticode y firma automatica
- **Auto-update**: Notificacion en-app con descarga desde GitHub Releases
