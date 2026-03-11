# Fase 4 - Plataforma IMA Mecatronica

**Aprobado por cliente:** 27-Feb-2026
**Version base:** v2.0.4
**Inicio desarrollo:** Pendiente

---

## Dashboard de Progreso

| Bloque | Nombre | Complejidad | Estado | Progreso |
|--------|--------|-------------|--------|----------|
| 1 | Cosmeticos pendientes Fase 3 | Baja | COMPLETADO | 7/7 |
| 2 | Optimizacion de rendimiento | Media | COMPLETADO | 6/6 |
| 3 | Portal Ventas + Storage | Media | COMPLETADO | 4/4 |
| 4 | Columna Ejecutor | Baja | COMPLETADO (falta deploy SQL) | 3/3 |
| 5 | Archivos (Drive IMA) | Alta | EN PROGRESO | 4/5 |
| 6 | Modulo Inventario | Alta | PENDIENTE | 0/4 |

**Progreso global:** 27/29 items (93%)

---

## Bloque 5 - Control de Pasos (Drive V2)

### Fases completadas
- [x] **5A** BD + Modelos (tablas, indexes, triggers, seed)
- [x] **5B** DriveService (CRUD carpetas, archivos, vinculacion, R2)
- [x] **5C** DriveV2Window UI + logica completa (rediseno basado en Figma)

### Completadas
- [x] **5-PERF** Optimizacion de rendimiento
  - [x] SQL RPCs: `get_folder_stats`, `get_folder_breadcrumb_full`, `get_orders_by_ids`
  - [x] Upload paralelo SemaphoreSlim(3)
  - [x] Cache de navegacion stale-while-revalidate (cache-hit ~9ms)
  - [x] Benchmark automatizado (cold/warm/cache-hit/back-nav)
  - [x] Deploy SQL RPCs en Supabase
  - [x] Pruebas con carpeta real 2026 (520MB, 1268 archivos, 111 carpetas)
  - [x] Script upload automatizado (`fase4/dirve_test/upload_to_drive.py`)
  - [x] Vinculacion automatica de 23 carpetas a ordenes (`fase4/dirve_test/link_folders_to_orders.py`)
- [x] **5D** Columna CARPETA en OrdersManagementWindow
  - [x] Icono PNG (`folder_off`/`folder_on`) en columna separada
  - [x] Clic en orden vinculada abre DriveV2Window directo en la carpeta
  - [x] Clic en orden sin vincular abre Drive en modo seleccion
  - [x] Modo seleccion bloquea carpetas ya vinculadas (opacity + badge VINCULADA)
  - [x] Batch load de linked folders en background
  - [x] Icono `folder_pin.png` como icono principal de carpetas en Drive

### Pendiente
- [ ] **5E** Polish
  - [ ] **Busqueda global: requiere ajuste y revision** (busqueda actual con ILike funciona pero falta UX: no navega al resultado al hacer clic, no muestra ubicacion del resultado)
  - [ ] Quitar botones temporales (Purgar R2, Benchmark)
  - [ ] Quitar auto-login temporal (appsettings.json)
  - [ ] Validacion nombres duplicados mejorada
  - [ ] Confirmar si Shot&Shot/Rack V2.1 necesita orden nueva (actualmente sin vincular)

---

## Orden de Ejecucion Recomendado

```
Bloque 2 (deploy SQL) ──> Bloque 1 ──> Bloque 4 ──> Bloque 3 ──> Bloque 5 ──> Bloque 6
   Ya hecho, deploy     UI rapido     BD + UI     UI + logica   Storage      Modulo nuevo
```

**Justificacion:**
1. **Bloque 2** primero porque ya esta desarrollado y solo falta ejecutar SQL en produccion
2. **Bloque 1** segundo porque son cambios cosmeticos rapidos que el cliente espera desde Fase 3
3. **Bloque 4** (Ejecutor) porque es un cambio acotado en BD + UI que no depende de infraestructura externa
4. **Bloque 3** (Portal Ventas) porque comparte logica con Bloque 5 (archivos por orden) y conviene resolver el upload de archivos primero en un contexto mas simple
5. **Bloque 5** (Carpetas) porque requiere decision arquitectonica sobre storage (Supabase Storage vs Cloudflare R2) y es la base para la gestion de archivos
6. **Bloque 6** (Inventario) al final porque es un modulo completamente nuevo y el mas complejo, ademas requiere mockup de validacion con el cliente antes de desarrollar

---

## Indice de Documentacion

| Archivo | Contenido |
|---------|-----------|
| [bloques/01-cosmeticos.md](bloques/01-cosmeticos.md) | Spec detallado: ajustes visuales pendientes Fase 3 |
| [bloques/02-rendimiento.md](bloques/02-rendimiento.md) | Spec: optimizacion de rendimiento (estado actual + deploy) |
| [bloques/03-portal-ventas.md](bloques/03-portal-ventas.md) | Spec: mejoras Portal Ventas + gestion archivos |
| [bloques/04-ejecutor.md](bloques/04-ejecutor.md) | Spec: nueva columna Ejecutor en ordenes |
| [bloques/05-carpetas.md](bloques/05-carpetas.md) | Spec original: carpetas en la nube por orden |
| [bloques/05-archivos-drive.md](bloques/05-archivos-drive.md) | Spec v2: modulo ARCHIVOS con Cloudflare R2 |
| [bloques/06-inventario.md](bloques/06-inventario.md) | Spec: modulo de inventario completo |
| [logs.md](logs.md) | Registro de implementacion y decisiones tecnicas |
| [bugs.md](bugs.md) | Bugs encontrados durante desarrollo |

---

## Consideraciones Tecnicas Globales

### Stack actual
- .NET 8.0 WPF + C# + Supabase (PostgreSQL)
- Patron: Singleton Facade + Repository + partial MVVM
- 30 Views, 13+ Services, 34 tablas BD

### Convenciones a mantener
- Nombres de tablas: `t_*` para entidades, `*_status` para catalogos, `*_history/*_audit` para auditoria
- Servicios: heredan de `BaseSupabaseService`, registrados en `SupabaseService` facade
- Views: code-behind con `SafeLoadAsync()`, `CancellationTokenSource`, `OnClosed()` cleanup
- Cache: usar `ServiceCache` para datos de baja frecuencia de cambio (TTL 5-30min)

### Dependencias entre bloques
- Bloque 3 y 5 comparten: infraestructura de storage en la nube
- Bloque 4 depende de: tabla `t_payroll` (para listado de nombres de nomina)
- Bloque 6 es completamente independiente

### Scripts SQL pendientes de deploy (Bloque 2)
- `sql/perf_indexes.sql` - 6 indexes de rendimiento
- `sql/perf_server_aggregations.sql` - Funciones RPC de agregacion
- `sql/perf_balance_materialized.sql` - Vista materializada para Balance
