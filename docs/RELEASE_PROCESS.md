# Proceso de Release - Sistema de Gestión de Proyectos

**Última actualización:** 27 de Enero de 2026
**Último release documentado:** v2.0.2
**Último commit registrado:** `0357d8073eaaf181dbfdfe2791b48a9627d67c21`

---

## Resumen del Proceso

Para generar una nueva versión de la aplicación, se deben seguir estos pasos en orden:

1. Actualizar versión en todos los archivos
2. Publicar aplicación (self-contained)
3. Generar instalador
4. Actualizar script SQL
5. Commit y push
6. Subir instalador a Supabase Storage
7. Ejecutar script SQL en Supabase

---

## 1. Actualizar Versión en Todos los Archivos

**CRÍTICO:** La versión debe estar sincronizada en TODOS estos archivos para evitar bucles de actualización.

### Archivos a actualizar:

| Archivo | Ubicación de la versión | Ejemplo |
|---------|------------------------|---------|
| `.csproj` | `<Version>`, `<AssemblyVersion>`, `<FileVersion>` | `2.0.2`, `2.0.2.0` |
| `appsettings.json` | `Application.Version` | `"2.0.2"` |
| `installer.iss` | `AppVersion`, `OutputBaseFilename` | `2.0.2` |
| `update_app.sql` | `v_version` | `'2.0.2'` |

### Ejemplo de actualización en .csproj:

```xml
<!-- SistemaGestionProyectos2.csproj -->
<PropertyGroup>
    <Version>2.0.3</Version>
    <AssemblyVersion>2.0.3.0</AssemblyVersion>
    <FileVersion>2.0.3.0</FileVersion>
</PropertyGroup>
```

### Ejemplo de actualización en appsettings.json:

```json
{
  "Application": {
    "Name": "Sistema de Gestión de Proyectos",
    "Version": "2.0.3",
    "Environment": "Production"
  },
  "DevMode": {
    "Enabled": false,
    "AutoLogin": false
  }
}
```

### Ejemplo de actualización en installer.iss:

```iss
[Setup]
AppVersion=2.0.3
OutputBaseFilename=SistemaGestionProyectos-v2.0.3-Setup
```

---

## 2. Publicar Aplicación (Self-Contained)

La aplicación DEBE publicarse como **self-contained** para que el usuario NO necesite instalar .NET Runtime ni ninguna otra dependencia.

### Comando de publicación:

```bash
cd SistemaGestionProyectos2

dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o "bin/Release/net8.0-windows/win-x64/publish"
```

### Parámetros importantes:

| Parámetro | Valor | Descripción |
|-----------|-------|-------------|
| `-c` | `Release` | Configuración de compilación |
| `-r` | `win-x64` | Runtime de destino (Windows 64-bit) |
| `--self-contained` | `true` | **Incluye .NET Runtime** (no requiere instalación) |
| `-p:PublishSingleFile` | `false` | Múltiples archivos (requerido para WPF) |
| `-o` | `bin/Release/.../publish` | Carpeta de salida |

---

## 3. Generar Instalador con Inno Setup

### Requisito:
- Inno Setup 6 instalado (normalmente en `C:\Program Files (x86)\Inno Setup 6\`)

### Comando:

```bash
cd SistemaGestionProyectos2

"/c/Program Files (x86)/Inno Setup 6/ISCC.exe" installer.iss
```

### Resultado:
El instalador se genera en:
```
SistemaGestionProyectos2/installer/SistemaGestionProyectos-v{VERSION}-Setup.exe
```

### Tamaño esperado:
Aproximadamente **50 MB** (incluye .NET Runtime completo)

---

## 4. Actualizar Script SQL (update_app.sql)

### Obtener commits desde el último release:

```bash
git log --oneline 0357d8073eaaf181dbfdfe2791b48a9627d67c21..HEAD
```

### Estructura del update_app.sql:

```sql
DO $$
DECLARE
    v_version       VARCHAR := '2.0.3';  -- Nueva versión
    v_created_by    VARCHAR := 'Zuri Dev';
    v_file_size_mb  NUMERIC := 49.66;    -- Actualizar con tamaño real
    v_is_mandatory  BOOLEAN := true;

    v_release_notes TEXT := 'Versión 2.0.3 - Título del Release

MÓDULO AFECTADO:
- Descripción del cambio 1
- Descripción del cambio 2

CORRECCIONES:
- Bug fix 1
- Bug fix 2

MEJORAS:
- Mejora 1
- Mejora 2';
```

### Obtener tamaño exacto del instalador:

```bash
stat --printf="%s" installer/SistemaGestionProyectos-v2.0.3-Setup.exe | awk '{printf "%.2f\n", $1/1024/1024}'
```

---

## 5. Commit y Push

### Archivos a incluir en el commit:

```bash
git add SistemaGestionProyectos2/SistemaGestionProyectos2.csproj
git add SistemaGestionProyectos2/appsettings.json
git add SistemaGestionProyectos2/installer.iss
git add SistemaGestionProyectos2/sql/update_app.sql
```

### Formato de mensaje de commit:

```bash
git commit -m "release: Versión X.Y.Z - Descripción breve

- Cambio 1
- Cambio 2
- Cambio 3"
```

---

## 6. Subir Instalador a Supabase Storage

### Ruta en Supabase Storage:

```
app-installers/releases/v{VERSION}/SistemaGestionProyectos-v{VERSION}-Setup.exe
```

### Ejemplo para v2.0.3:

```
app-installers/releases/v2.0.3/SistemaGestionProyectos-v2.0.3-Setup.exe
```

### URL resultante:

```
https://wjozxqldvypdtfmkamud.supabase.co/storage/v1/object/public/app-installers/releases/v2.0.3/SistemaGestionProyectos-v2.0.3-Setup.exe
```

---

## 7. Ejecutar Script SQL en Supabase

1. Ir a Supabase Dashboard → SQL Editor
2. Copiar contenido de `sql/update_app.sql`
3. Ejecutar
4. Verificar con el SELECT final que muestra las últimas versiones

---

## Checklist de Release

```
[ ] Versión actualizada en .csproj (Version, AssemblyVersion, FileVersion)
[ ] Versión actualizada en appsettings.json
[ ] DevMode.Enabled = false
[ ] DevMode.AutoLogin = false
[ ] Versión actualizada en installer.iss
[ ] Aplicación publicada (self-contained)
[ ] Instalador generado con Inno Setup
[ ] Tamaño del instalador actualizado en update_app.sql
[ ] Release notes escritas en update_app.sql
[ ] Commit y push realizados
[ ] Instalador subido a Supabase Storage
[ ] Script SQL ejecutado en Supabase
[ ] Verificado que la app no pide actualización en bucle
```

---

## Troubleshooting

### Problema: La app siempre pide actualizar

**Causa:** Las versiones no están sincronizadas.

**Verificación:**
```bash
# Ver versión en .csproj
grep -E "Version|AssemblyVersion|FileVersion" SistemaGestionProyectos2.csproj

# Ver versión en appsettings.json
grep "Version" appsettings.json

# Ver versión en BD
SELECT version, is_latest FROM app_versions ORDER BY id DESC LIMIT 3;
```

**Solución:** Asegurar que TODAS las versiones coincidan exactamente.

### Problema: El instalador requiere .NET

**Causa:** No se publicó como self-contained.

**Solución:** Verificar que el comando de publicación incluye `--self-contained true`.

### Problema: Error al compilar instalador

**Causa:** Inno Setup no encuentra los archivos.

**Verificación:**
```bash
ls -la bin/Release/net8.0-windows/win-x64/publish/SistemaGestionProyectos2.exe
```

**Solución:** Asegurar que la publicación se realizó en la carpeta correcta.

---

## Historial de Releases

| Versión | Fecha | Commit Hash | Notas |
|---------|-------|-------------|-------|
| 2.0.2 | 2026-01-27 | `0357d8073eaaf181dbfdfe2791b48a9627d67c21` | Nueva fórmula utilidad, fix versión Assembly |
| 2.0.1 | 2026-01-27 | `d93ebff` | Gastos de órdenes en balance |
| 2.0.0 | 2026-01-26 | - | Auditoría de gastos |

---

## Referencias

- **Documentación BD:** `docs/BD-IMA/`
- **Inno Setup:** https://jrsoftware.org/isinfo.php
- **dotnet publish:** https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-publish
