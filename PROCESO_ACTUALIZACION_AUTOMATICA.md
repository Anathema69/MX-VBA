# 🔄 Sistema de Actualizaciones Automáticas

**Versión del Sistema:** 1.0.1
**Fecha de Implementación:** 14 de octubre de 2025
**Desarrollado por:** Zuri Dev

---

## 📖 Tabla de Contenidos

1. [Resumen del Sistema](#resumen-del-sistema)
2. [Cómo Funciona](#cómo-funciona)
3. [Configuración Inicial](#configuración-inicial)
4. [Proceso de Release (Nueva Versión)](#proceso-de-release-nueva-versión)
5. [Tipos de Actualización](#tipos-de-actualización)
6. [Guía Rápida para Nuevas Versiones](#guía-rápida-para-nuevas-versiones)
7. [Troubleshooting](#troubleshooting)

---

## 🎯 Resumen del Sistema

A partir de la **versión 1.0.1**, el Sistema de Gestión de Proyectos incluye **actualización automática**.

### ✅ Ventajas

- **Para el Cliente:** Siempre tiene la última versión sin intervención manual
- **Para Ti:** No necesitas enviar instaladores manualmente cada vez
- **Para Ambos:** Actualizaciones rápidas y sin fricción

### 📌 Componentes

1. **Tabla `app_versions` en Supabase** - Almacena info de versiones
2. **Supabase Storage** - Hospeda los instaladores
3. **UpdateService.cs** - Servicio que verifica versiones
4. **UpdateAvailableWindow** - Ventana que notifica al usuario
5. **Verificación automática al login** - Chequea updates cada vez que un usuario inicia sesión

---

## 🔍 Cómo Funciona

```
┌─────────────────────────────────────────────────────────────┐
│  FLUJO DE ACTUALIZACIÓN                                      │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  1. Usuario abre la aplicación                              │
│  2. Inicia sesión exitosamente                              │
│  3. App verifica versión actual vs. Supabase (background)   │
│  4. Si hay nueva versión:                                    │
│     ├─→ Muestra ventana de actualización                     │
│     ├─→ Usuario hace clic en "Actualizar ahora"              │
│     ├─→ Descarga instalador desde Supabase Storage           │
│     ├─→ Ejecuta instalador                                   │
│     └─→ App se cierra, instalador actualiza                  │
│  5. Si NO hay actualización:                                 │
│     └─→ Continúa normalmente (sin notificación)             │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

---

## ⚙️ Configuración Inicial

### Paso 1: Configurar Supabase (SOLO UNA VEZ)

1. **Crear tabla `app_versions`:**
   - Abre Supabase Dashboard → SQL Editor
   - Ejecuta el script: `setup_auto_update_supabase.sql`
   - Esto crea la tabla y las funciones necesarias

2. **Configurar Supabase Storage:**
   - Ve a: Storage → Create Bucket
   - Nombre: `app-installers`
   - Configuración:
     - Public: ✅ YES
     - File size limit: 100 MB
     - Allowed MIME types: `application/x-msdownload`, `application/octet-stream`

3. **Configurar Políticas de Acceso (RLS):**
   - Ve a: Storage → Policies → app-installers
   - Crear política:
     - Name: `Public read access`
     - Policy type: `SELECT`
     - Target roles: `anon`, `authenticated`
     - Using expression: `true`

### Paso 2: Instalar Versión 1.0.1 en Clientes

- Esta es la **última instalación manual**
- Genera instalador v1.0.1 (con auto-update incluido)
- Envía a clientes para instalación
- A partir de aquí, todo será automático

---

## 🚀 Proceso de Release (Nueva Versión)

Cuando termines cambios y quieras lanzar una nueva versión (ej: 1.0.2):

### Paso 1: Actualizar Versión en el Código

```xml
<!-- SistemaGestionProyectos2.csproj -->
<PropertyGroup>
    <Version>1.0.2</Version>
    <AssemblyVersion>1.0.2.0</AssemblyVersion>
    <FileVersion>1.0.2.0</FileVersion>
</PropertyGroup>
```

```json
// appsettings.json
{
  "Application": {
    "Version": "1.0.2"
  }
}
```

### Paso 2: Compilar y Publicar

```bash
# Limpiar
dotnet clean

# Publicar (self-contained)
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=false
```

### Paso 3: Crear Instalador

1. Abre `installer.iss` en Inno Setup Compiler
2. Actualiza la versión:
   ```pascal
   AppVersion=1.0.2
   OutputBaseFilename=SistemaGestionProyectos-v1.0.2-Setup
   ```
3. Compile → Se genera `SistemaGestionProyectos-v1.0.2-Setup.exe`

### Paso 4: Subir a Supabase Storage

1. Ve a: Supabase Dashboard → Storage → app-installers
2. Crear carpeta: `releases/v1.0.2/`
3. Subir archivo: `SistemaGestionProyectos-v1.0.2-Setup.exe`
4. Copiar URL pública del archivo (clic derecho → Get URL)

**Ejemplo de URL:**
```
https://wjozxqldvypdtfmkamud.supabase.co/storage/v1/object/public/app-installers/releases/v1.0.2/SistemaGestionProyectos-v1.0.2-Setup.exe
```

### Paso 5: Registrar Versión en Base de Datos

Ejecuta este SQL en Supabase:

```sql
INSERT INTO app_versions (
    version,
    is_latest,
    is_mandatory,
    download_url,
    file_size_mb,
    release_notes,
    created_by,
    changelog
) VALUES (
    '1.0.2',
    true,  -- Esta es la última versión
    false,  -- No es obligatoria (cambiar a true si lo es)
    'https://wjozxqldvypdtfmkamud.supabase.co/storage/v1/object/public/app-installers/releases/v1.0.2/SistemaGestionProyectos-v1.0.2-Setup.exe',
    50.5,  -- Tamaño en MB
    'Descripción de cambios en esta versión',
    'Zuri Dev',
    '{"added": ["Nueva funcionalidad X"], "improved": ["Mejora Y"], "fixed": ["Bug Z"]}'::jsonb
);
```

### Paso 6: Verificar

```sql
-- Ver última versión registrada
SELECT version, is_latest, is_mandatory, file_size_mb, release_date
FROM app_versions
WHERE is_latest = true;
```

### Paso 7: ¡Listo!

- Los clientes recibirán notificación automáticamente al iniciar sesión
- No necesitas enviarles nada manualmente

---

## 🔄 Tipos de Actualización

### 1. Actualización Opcional (`is_mandatory = false`)

- Usuario ve notificación
- Puede hacer clic en **"Recordar después"**
- App continúa funcionando normalmente
- Verá la notificación en el próximo inicio de sesión

**Usar cuando:**
- Nuevas funcionalidades no críticas
- Mejoras de rendimiento
- Cambios cosméticos

### 2. Actualización Obligatoria (`is_mandatory = true`)

- Usuario ve notificación
- **NO puede cerrar** la ventana sin actualizar
- Debe actualizar para continuar usando la app

**Usar cuando:**
- Cambios críticos de seguridad
- Bugs graves que afectan funcionalidad core
- Cambios en la API de Supabase que rompen compatibilidad

Para hacer una actualización obligatoria:

```sql
UPDATE app_versions
SET is_mandatory = true
WHERE version = '1.0.2';
```

---

## ⚡ Guía Rápida para Nuevas Versiones

### Para versiones menores (1.0.x → 1.0.y):

```bash
# 1. Actualizar versión
# Editar: SistemaGestionProyectos2.csproj y appsettings.json

# 2. Compilar
dotnet clean
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=false

# 3. Crear instalador con Inno Setup
# Actualizar versión en installer.iss → Compile

# 4. Subir a Supabase Storage
# Dashboard → Storage → app-installers → releases/v1.0.X/

# 5. Registrar en DB (copiar URL del paso 4)
INSERT INTO app_versions (version, is_latest, is_mandatory, download_url, file_size_mb, release_notes, created_by)
VALUES ('1.0.X', true, false, 'URL_AQUI', XX.X, 'Notas', 'Zuri Dev');
```

---

## 🐛 Troubleshooting

### Problema: Clientes no ven notificación de actualización

**Posibles causas:**

1. **La versión no está marcada como `is_latest`**
   ```sql
   SELECT version, is_latest FROM app_versions ORDER BY release_date DESC LIMIT 5;
   ```
   Solución:
   ```sql
   UPDATE app_versions SET is_latest = true WHERE version = '1.0.2';
   ```

2. **El usuario tiene una versión más nueva**
   - Verifica que la versión en el código sea menor que la registrada

3. **Error de conexión a Supabase**
   - Revisa logs en: `%LocalAppData%\SistemaGestionProyectos\logs\`

### Problema: Descarga falla

**Posibles causas:**

1. **URL incorrecta**
   - Verifica que la URL sea pública y accesible
   - Prueba abrir la URL en un navegador

2. **Archivo no existe en Storage**
   - Ve a: Storage → app-installers → Verifica que el archivo esté ahí

3. **Problemas de permisos**
   - Verifica que el bucket sea público
   - Verifica que exista política de lectura pública

### Problema: Instalador no se ejecuta

**Posibles causas:**

1. **Antivirus bloqueando**
   - Agregar excepción en antivirus del cliente

2. **Usuario sin permisos de administrador**
   - El instalador requiere permisos de admin

3. **Archivo corrupto**
   - Volver a subir el archivo a Storage

---

## 📝 Changelog Example

Cuando registres una nueva versión, usa este formato de changelog:

```json
{
  "added": [
    "Módulo de reportes avanzados",
    "Exportación a Excel",
    "Búsqueda global"
  ],
  "improved": [
    "Velocidad de carga de órdenes (50% más rápido)",
    "Interfaz de usuario más intuitiva",
    "Logs más detallados"
  ],
  "fixed": [
    "Error al guardar clientes con RFC duplicado",
    "Bug en cálculo de comisiones",
    "Session timeout no respetaba configuración"
  ]
}
```

---

## 🎓 Mejores Prácticas

1. **Prueba localmente antes de subir:**
   - Instala la nueva versión en tu máquina
   - Verifica que funcione correctamente
   - Prueba el flujo de actualización (opcional)

2. **Versionado semántico:**
   - `MAJOR.MINOR.PATCH` (ej: 1.0.2)
   - MAJOR: Cambios incompatibles
   - MINOR: Nuevas funcionalidades compatibles
   - PATCH: Correcciones de bugs

3. **Release Notes claros:**
   - Explica qué cambió en lenguaje simple
   - Menciona si hay nuevas funcionalidades
   - Indica si se corrigieron bugs importantes

4. **Backups antes de actualizar:**
   - Supabase hace backups automáticos
   - Pero es buena práctica tener copias locales

---

## 📞 Soporte

Si tienes problemas con el sistema de actualizaciones:

1. Revisa los logs: `%LocalAppData%\SistemaGestionProyectos\logs\`
2. Verifica la tabla `app_versions` en Supabase
3. Contacta a Zuri Dev: WhatsApp o Workana

---

**¡Sistema de actualizaciones configurado y listo!** 🎉

*A partir de ahora, todas las actualizaciones serán automáticas. Solo necesitas compilar, subir a Supabase y registrar la versión.*
