# Guía: Configurar Ambiente de Staging (Pruebas)

**Propósito:** Probar la migración v2.0 sin afectar producción

---

## Paso 1: Crear Proyecto en Supabase

1. Ve a [supabase.com/dashboard](https://supabase.com/dashboard)
2. Click **"New Project"**
3. Configurar:
   - **Organization:** (tu organización existente)
   - **Name:** `ima-staging`
   - **Database Password:** Genera una segura y guárdala
   - **Region:** South America (São Paulo)
   - **Pricing Plan:** Free tier es suficiente para pruebas
4. Click **"Create new project"**
5. Esperar ~2 minutos

---

## Paso 2: Obtener Credenciales

En el proyecto `ima-staging`:

1. Ve a **Settings** (engranaje) → **API**
2. Copia estos valores:

```
Project URL: https://XXXXXX.supabase.co
anon public: eyJhbG... (token largo)
```

---

## Paso 3: Actualizar Configuración Local

1. Abre el archivo:
   ```
   SistemaGestionProyectos2\appsettings.staging.json
   ```

2. Reemplaza los valores:
   ```json
   {
     "Supabase": {
       "Url": "https://TU_PROYECTO_STAGING.supabase.co",  // ← Pegar Project URL
       "AnonKey": "TU_ANON_KEY_STAGING",                  // ← Pegar anon public
       "ServiceRoleKey": ""
     }
   }
   ```

3. Guarda el archivo

---

## Paso 4: Crear Estructura de BD en Staging

En el proyecto `ima-staging` de Supabase:

1. Ve a **SQL Editor**
2. Copia el contenido del archivo `schema_oficial.sql` que descargaste de producción
3. Ejecuta el script completo
4. Verifica que no haya errores

**Alternativa:** Puedes usar el archivo que ya tienes en:
```
docs/BD-IMA/scripts/schema_oficial.sql
```

---

## Paso 5: Crear Usuarios de Prueba

Ejecuta en SQL Editor de staging:

```sql
-- Habilitar pgcrypto
CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- Crear usuario admin de prueba
INSERT INTO users (username, email, password_hash, full_name, role, is_active)
VALUES (
    'admin_test',
    'admin_test@ima.com',
    crypt('Test2026!', gen_salt('bf', 11)),
    'Admin de Pruebas',
    'admin',
    true
) ON CONFLICT (username) DO NOTHING;

-- Crear usuario coordinador de prueba
INSERT INTO users (username, email, password_hash, full_name, role, is_active)
VALUES (
    'coord_test',
    'coord_test@ima.com',
    crypt('Test2026!', gen_salt('bf', 11)),
    'Coordinador de Pruebas',
    'coordinator',
    true
) ON CONFLICT (username) DO NOTHING;

-- Verificar usuarios creados
SELECT id, username, full_name, role, is_active FROM users;
```

**Credenciales de prueba:**
- Usuario: `admin_test` / Password: `Test2026!`
- Usuario: `coord_test` / Password: `Test2026!`

---

## Paso 6: (Opcional) Copiar Datos de Producción

Si quieres datos reales para probar, puedes exportar/importar tablas específicas.

### Exportar desde Producción:

En SQL Editor de **producción**:

```sql
-- Ver datos de clientes (para copiar manualmente algunos)
SELECT * FROM t_client WHERE is_active = true LIMIT 10;

-- Ver datos de proveedores
SELECT * FROM t_supplier WHERE is_active = true LIMIT 10;

-- Ver catálogos
SELECT * FROM order_status;
SELECT * FROM invoice_status;
```

### Importar a Staging:

Copia los INSERTs necesarios y ejecútalos en staging.

**Mínimo recomendado:**
- `order_status` (catálogo de estados)
- `invoice_status` (catálogo de estados de factura)
- 2-3 clientes de prueba
- 2-3 proveedores de prueba

---

## Paso 7: Cambiar la App a Staging

### Opción A: Usar el script batch

1. Abre terminal en la carpeta del proyecto
2. Ejecuta:
   ```cmd
   switch-environment.bat
   ```
3. Selecciona opción `2` (Staging)

### Opción B: Manual

1. Copia `appsettings.staging.json` sobre `appsettings.json`
2. O edita directamente `appsettings.json` con los valores de staging

---

## Paso 8: Probar Conexión

1. Ejecuta la aplicación
2. Intenta login con `admin_test` / `Test2026!`
3. Si funciona, estás conectado a staging

---

## Paso 9: Ejecutar Migración v2.0

Ahora puedes probar el script de migración:

1. En SQL Editor de **staging**
2. Abre `docs/cambios_ene26/MIGRACION_v2.sql`
3. Ejecuta **fase por fase** (no todo junto)
4. Verifica después de cada fase

---

## Paso 10: Probar la App con Cambios

1. La app debería seguir funcionando
2. Prueba login (ahora el rol será `administracion` en vez de `admin`)
3. Verifica que los menús aparezcan correctamente
4. Prueba crear/editar órdenes

---

## Regresar a Producción

Cuando termines las pruebas:

```cmd
switch-environment.bat
```

Selecciona opción `1` (Producción)

---

## Resumen de Archivos

| Archivo | Propósito |
|---------|-----------|
| `appsettings.json` | Configuración activa (la que usa la app) |
| `appsettings.production.json` | Backup de config de producción |
| `appsettings.staging.json` | Config de staging (editar con tus valores) |
| `switch-environment.bat` | Script para cambiar entre ambientes |

---

## Checklist

- [ ] Proyecto `ima-staging` creado en Supabase
- [ ] Credenciales copiadas a `appsettings.staging.json`
- [ ] Schema ejecutado en staging
- [ ] Usuarios de prueba creados
- [ ] Catálogos básicos insertados
- [ ] App conecta a staging correctamente
- [ ] Migración v2.0 ejecutada y probada
- [ ] Regresar a producción cuando termine
