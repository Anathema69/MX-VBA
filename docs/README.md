# Documentacion Tecnica — IMA Mecatronica

**Version actual:** 2.3.3 (abril 2026)
**Stack:** .NET 8 WPF + Supabase (PostgreSQL) + Cloudflare R2 + Inno Setup
**Ultima fase cerrada:** Fase 4 (Feb-Mar 2026)

---

Este directorio contiene la documentacion tecnica interna. Para la vision global del producto (modulos, releases, diagramas de alto nivel) ver [../README.md](../README.md). Para el dashboard de Fase 4 ver [../fase4/README.md](../fase4/README.md).

## Indice

### Arquitectura y codigo
| Documento | Contenido |
|---|---|
| [01_ARQUITECTURA.md](./01_ARQUITECTURA.md) | Capas, patrones, estructura de carpetas, dependencias. |
| [03_SERVICIOS.md](./03_SERVICIOS.md) | Servicios especializados y sus metodos principales. Incluye Drive, FileWatcher, Inventory, Storage, auto-update UIPI/schtasks. |
| [04_ROLES_AUTENTICACION.md](./04_ROLES_AUTENTICACION.md) | 5 roles (direccion/administracion/proyectos/coordinacion/ventas), matriz de permisos, BCrypt, session timeout. |

### Datos
| Documento | Contenido |
|---|---|
| [02_MODELOS_DATOS.md](./02_MODELOS_DATOS.md) | Resumen semantico por modulo: 44 tablas, 15 vistas, 73 funciones. Relaciones clave. |
| [../db-docs/output/](../db-docs/output/) | **Fuente canonica auto-generada** desde Supabase en vivo (Python + psycopg2). 7 archivos: tablas, relaciones, vistas, funciones/triggers, indices, RLS, diagrama ER. |

Si hay conflicto entre `02_MODELOS_DATOS.md` y `db-docs/output/`, creer a `db-docs/output/`.

### Procesos y operaciones
| Documento | Contenido |
|---|---|
| [05_FLUJOS_TRABAJO.md](./05_FLUJOS_TRABAJO.md) | Ciclo de vida de ordenes, facturacion, gastos, balance, Drive (Open-in-Place), inventario, ejecutor, auto-update, timeout. |
| [FLUJO_COMISIONES.md](./FLUJO_COMISIONES.md) | Detalle draft/pending/paid + Portal Ventas V2 (preview, stepper, galeria). |
| [RELEASE_PROCESS.md](./RELEASE_PROCESS.md) | Proceso real de release (GitHub Releases + Supabase `app_versions`). Checklist + troubleshooting + UIPI. |

## Fuentes canonicas de verdad

| Pregunta | Donde mirar |
|---|---|
| Version actual | `SistemaGestionProyectos2/SistemaGestionProyectos2.csproj` (campo `<Version>`) |
| Configuracion Supabase / R2 | `SistemaGestionProyectos2/appsettings.json` (base, production, staging) |
| Estructura BD actual | `db-docs/output/*.md` (regenerable con los 7 scripts Python) |
| Roles y permisos reales | `Views/MainMenuWindow.xaml.cs` + `Views/OrdersManagementWindow.xaml.cs` (switches por `Role`) |
| Proceso de release | `docs/RELEASE_PROCESS.md` + `SistemaGestionProyectos2/sql/update_app.sql` |

Si estos docs (`01-05`) quedan desactualizados frente al codigo o BD, **prevalece el codigo/BD**. Los docs se actualizan explicitamente tras cambios grandes.

## Regenerar `db-docs/output/`

```bash
cd db-docs
./venv/Scripts/python.exe 01_tables.py
./venv/Scripts/python.exe 02_relaciones.py
./venv/Scripts/python.exe 03_vistas.py
./venv/Scripts/python.exe 04_funciones_triggers.py
./venv/Scripts/python.exe 05_indexes.py
./venv/Scripts/python.exe 06_rls_policies.py
./venv/Scripts/python.exe 07_diagrama_er.py
```

Credenciales en `db-docs/.env` (no commiteado). Regenera desde la BD en vivo.

---

**Desarrollado por:** Zuri Dev
**Cliente:** IMA Mecatronica
**Repositorio:** [github.com/Anathema69/MX-VBA](https://github.com/Anathema69/MX-VBA)
