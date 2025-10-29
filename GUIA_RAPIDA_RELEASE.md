# ⚡ Guía Rápida - Nueva Versión (Release)

**Para:** Zuri Dev
**Uso:** Cada vez que quieras lanzar una nueva versión del sistema

---

## 📋 Checklist Rápido (5 Pasos)

```
☐ 1. Actualizar versión en código
☐ 2. Compilar y publicar
☐ 3. Crear instalador
☐ 4. Subir a Supabase Storage
☐ 5. Registrar versión en BD
```

---

## 🚀 Paso a Paso

### 1️⃣ Actualizar Versión (2 archivos)

**Archivo 1:** `SistemaGestionProyectos2.csproj`
```xml
<PropertyGroup>
    <Version>1.0.X</Version>  <!-- Cambiar X -->
    <AssemblyVersion>1.0.X.0</AssemblyVersion>
    <FileVersion>1.0.X.0</FileVersion>
</PropertyGroup>
```

**Archivo 2:** `appsettings.json`
```json
{
  "Application": {
    "Version": "1.0.X"  // Cambiar X
  }
}
```

---

### 2️⃣ Compilar y Publicar

⚠️ **IMPORTANTE:** Antes de compilar, cierra TODAS las instancias de la aplicación:
```bash
# Cerrar todas las instancias
taskkill /F /IM SistemaGestionProyectos2.exe
```

```bash
cd SistemaGestionProyectos2
dotnet clean
dotnet publish SistemaGestionProyectos2.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false
```

✅ Se genera en: `bin/Release/net8.0-windows/win-x64/publish/` (166 MB aprox)

---

### 3️⃣ Crear Instalador

1. Abrir `installer.iss` en **Inno Setup Compiler**
2. Actualizar línea 3:
   ```pascal
   AppVersion=1.0.X  ; Cambiar X
   ```
3. Actualizar línea 7:
   ```pascal
   OutputBaseFilename=SistemaGestionProyectos-v1.0.X-Setup  ; Cambiar X
   ```
4. Click en **Compile** (o F9)

✅ Se genera en: `installer/SistemaGestionProyectos-v1.0.X-Setup.exe`

---

### 4️⃣ Subir a Supabase Storage

1. Ir a: https://supabase.com → Tu Proyecto → **Storage**
2. Bucket: `app-installers`
3. Crear carpeta: `releases/v1.0.X/`
4. Subir archivo: `SistemaGestionProyectos-v1.0.X-Setup.exe`
5. **Copiar URL pública** (clic derecho → Get URL)

**Ejemplo de URL:**
```
https://wjozxqldvypdtfmkamud.supabase.co/storage/v1/object/public/app-installers/releases/v1.0.X/SistemaGestionProyectos-v1.0.X-Setup.exe
```

---

### 5️⃣ Registrar en Base de Datos

1. Ir a: https://supabase.com → Tu Proyecto → **SQL Editor**
2. Ejecutar este SQL (reemplazar valores):

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
    '1.0.X',  -- ✏️ Cambiar X
    true,  -- Esta es la última versión
    false,  -- ⚠️ true solo si es OBLIGATORIA
    'URL_COPIADA_DEL_PASO_4',  -- ✏️ Pegar URL completa aquí
    50.0,  -- ✏️ Tamaño del archivo en MB (aprox)
    'Descripción breve de cambios',  -- ✏️ Explica qué cambió
    'Zuri Dev',
    '{"added": ["Feature 1"], "improved": ["Mejora 1"], "fixed": ["Bug 1"]}'::jsonb
);
```

**Ejemplo completo:**
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
    true,
    false,
    'https://wjozxqldvypdtfmkamud.supabase.co/storage/v1/object/public/app-installers/releases/v1.0.2/SistemaGestionProyectos-v1.0.2-Setup.exe',
    50.2,
    'Corrección de bugs y mejoras de rendimiento',
    'Zuri Dev',
    '{"added": [], "improved": ["Velocidad de carga"], "fixed": ["Error en login"]}'::jsonb
);
```

---

## ✅ Verificar que Funcionó

```sql
-- Ver última versión
SELECT version, is_latest, is_mandatory, file_size_mb, release_date
FROM app_versions
WHERE is_latest = true;
```

Debería mostrar tu nueva versión con `is_latest = true`.

---

## 🎉 ¡Listo!

Los clientes verán la actualización automáticamente al iniciar sesión.

---

## 🔄 ¿Qué pasa después?

1. Cliente abre la app
2. Inicia sesión
3. Ve notificación de nueva versión
4. Hace clic en "Actualizar ahora"
5. Se descarga e instala automáticamente
6. App se reinicia con nueva versión

---

## ⚠️ Importante

- **NO elimines versiones antiguas** de la tabla (solo marca `is_latest = false`)
- **Prueba localmente** antes de subir a Supabase
- **Verifica la URL** del instalador antes de registrar
- **Opcional vs Obligatoria:**
  - `is_mandatory = false` → Usuario puede postponer
  - `is_mandatory = true` → Usuario DEBE actualizar

---

## 🐛 Si algo falla

1. **URL del instalador incorrecta:**
   ```sql
   UPDATE app_versions
   SET download_url = 'URL_CORRECTA'
   WHERE version = '1.0.X';
   ```

2. **Versión no aparece como latest:**
   ```sql
   UPDATE app_versions
   SET is_latest = true
   WHERE version = '1.0.X';
   ```

3. **Desactivar una versión:**
   ```sql
   UPDATE app_versions
   SET is_active = false
   WHERE version = '1.0.X';
   ```

---

## 📞 ¿Dudas?

Ver documentación completa: `PROCESO_ACTUALIZACION_AUTOMATICA.md`

---

**Tiempo estimado:** 10-15 minutos por release

¡Actualizaciones automáticas FTW! 🚀
