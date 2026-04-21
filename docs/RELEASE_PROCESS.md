# Proceso de Release

**Ultima actualizacion:** Abril 2026 (v2.3.3)
**Flujo actual:** GitHub Releases + tabla `app_versions` en Supabase
**Este documento reemplaza:** `../GUIA_RAPIDA_RELEASE.md` y `../fase4/PROCESO_ACTUALIZACION_AUTOMATICA.md` (ambos obsoletos, apuntaban a Supabase Storage).

---

## Resumen

```
 ┌──────────────────────────────────────────────────────────────┐
 │  1) Bump de version   (csproj + installer.iss + update_app)  │
 │  2) Publish            (dotnet publish self-contained)        │
 │  3) Instalador         (Inno Setup ISCC)                      │
 │  4) GitHub Release     (gh release create vX.Y.Z)             │
 │  5) Registrar en BD    (ejecutar update_app.sql en Supabase)  │
 └──────────────────────────────────────────────────────────────┘

 Resultado: la app de los clientes detecta la version nueva al
 iniciar sesion, descarga el instalador desde GitHub, lo ejecuta
 y relanza la app sin elevacion (schtasks /rl limited).
```

---

## 1. Bump de version (3 archivos)

La version autoritativa es `.csproj`. Cualquier lectura de "version actual" en la app proviene de `Assembly.GetName().Version`.

**Archivo 1 — `SistemaGestionProyectos2/SistemaGestionProyectos2.csproj`:**
```xml
<PropertyGroup>
    <Version>2.3.4</Version>
    <AssemblyVersion>2.3.4.0</AssemblyVersion>
    <FileVersion>2.3.4.0</FileVersion>
</PropertyGroup>
```

**Archivo 2 — `SistemaGestionProyectos2/installer.iss`:**
```pascal
[Setup]
AppVersion=2.3.4
OutputBaseFilename=SistemaGestionProyectos-v2.3.4-Setup
```

**Archivo 3 — `SistemaGestionProyectos2/sql/update_app.sql`:**
```sql
v_version       VARCHAR := '2.3.4';
v_file_size_mb  NUMERIC := 55.0;   -- actualizar con tamano real
v_is_mandatory  BOOLEAN := false;
v_min_version   VARCHAR := '2.2.0';
v_release_notes TEXT := 'Version 2.3.4 - Titulo
...';
v_changelog := '{ "Added":[...], "Improved":[...], "Fixed":[...] }'::jsonb;
```

> **Nota:** `appsettings.json.Application.Version` **ya no existe**. Antes estaba y se desincronizaba facilmente, pero el codigo nunca lo lee (confirmado en `App.xaml.cs:332-334` — la version viene del Assembly). Se elimino para simplificar el checklist de release.

---

## 2. Publicar la aplicacion (self-contained)

Cerrar instancias previas:
```bash
taskkill /F /IM SistemaGestionProyectos2.exe
```

Publish:
```bash
cd SistemaGestionProyectos2
dotnet clean
dotnet publish SistemaGestionProyectos2.csproj \
    -c Release \
    -r win-x64 \
    --self-contained true \
    -p:PublishSingleFile=false
```

Salida: `bin/Release/net8.0-windows/win-x64/publish/` (~166 MB).

Parametros clave:
- `--self-contained true` — incluye el runtime .NET 8, el cliente no necesita instalarlo.
- `-p:PublishSingleFile=false` — requerido para WPF (single-file rompe assemblies XAML).

La firma Authenticode se dispara automaticamente post-build por el target `SignAssembly` en `.csproj` si existe `ima-dev-cert.pfx`.

---

## 3. Generar el instalador (Inno Setup)

Requisito: Inno Setup 6 instalado en `C:\Program Files (x86)\Inno Setup 6\`.

Compilar:
```bash
"/c/Program Files (x86)/Inno Setup 6/ISCC.exe" installer.iss
```

Salida: `installer/SistemaGestionProyectos-vX.Y.Z-Setup.exe` (~50-55 MB).

El instalador:
- Instala el certificado `ima-dev-cert.pfx` en `TrustedPublisher` y `Root` (silencioso).
- Copia los binarios self-contained a `C:\Program Files\SistemaGestionProyectos\`.
- Crea accesos directos (grupo + escritorio).
- Ejecuta la app con `runasoriginaluser` al finalizar.

Obtener tamano exacto del instalador para `update_app.sql`:
```bash
stat --printf="%s" installer/SistemaGestionProyectos-v2.3.4-Setup.exe \
    | awk '{printf "%.2f\n", $1/1024/1024}'
```

---

## 4. Publicar en GitHub Releases

```bash
gh release create v2.3.4 \
    installer/SistemaGestionProyectos-v2.3.4-Setup.exe \
    --title "v2.3.4" \
    --notes "Resumen corto de la version"
```

La URL del asset queda en:
```
https://github.com/Anathema69/MX-VBA/releases/download/v2.3.4/SistemaGestionProyectos-v2.3.4-Setup.exe
```

`update_app.sql` construye esa URL automaticamente a partir de `v_version`.

---

## 5. Registrar la version en Supabase

1. Supabase Dashboard → SQL Editor.
2. Pegar el contenido de `SistemaGestionProyectos2/sql/update_app.sql` (ya con `v_version` actualizado en el paso 1).
3. Run.

El script hace:
```sql
UPDATE app_versions SET is_latest = false WHERE is_latest = true;
INSERT INTO app_versions (...) VALUES (v_version, NOW(), true, v_is_mandatory, v_download_url, ...);
```

4. Verificar con el SELECT final del script:
```sql
SELECT id, version, is_latest, is_active, release_date::date, file_size_mb
FROM app_versions ORDER BY id DESC LIMIT 5;
```

---

## Checklist de release

```
[ ] 1. .csproj: Version, AssemblyVersion, FileVersion actualizados
[ ] 2. installer.iss: AppVersion y OutputBaseFilename
[ ] 3. sql/update_app.sql: v_version, v_file_size_mb, v_release_notes, v_changelog
[ ] 4. DevMode.Enabled = false en appsettings.json (verificar)
[ ] 5. taskkill de instancias previas
[ ] 6. dotnet clean + dotnet publish self-contained
[ ] 7. ISCC.exe installer.iss -> .exe generado en installer/
[ ] 8. Probar instalador localmente (doble clic, login, navegar 1-2 modulos)
[ ] 9. Commit de los 3 archivos + sql/update_app.sql
[ ] 10. gh release create vX.Y.Z installer.exe --title --notes
[ ] 11. Ejecutar update_app.sql en Supabase SQL Editor
[ ] 12. Verificar: SELECT ... WHERE is_latest = true devuelve la nueva
[ ] 13. (opcional) Login con otro equipo y ver que se ofrece la actualizacion
```

---

## Tipos de actualizacion

### Opcional (`is_mandatory = false`)
El usuario ve `UpdateAvailableWindow` con boton "Recordar despues". Puede postergar. Se vuelve a ofrecer en el proximo login.

**Usar para:** features nuevas no criticas, mejoras de performance, cambios cosmeticos.

### Obligatoria (`is_mandatory = true`)
El usuario **no puede cerrar** la ventana sin actualizar.

**Usar para:** fixes de seguridad, bugs criticos que impiden el funcionamiento core, cambios incompatibles de API.

Conversion ex-post:
```sql
UPDATE app_versions SET is_mandatory = true WHERE version = '2.3.4';
```

---

## Flujo de auto-update en el cliente

Tras login exitoso, `App.xaml.cs` llama `CheckForUpdatesAsync()` (una vez por sesion, guard `_updateCheckDone`). Ver [03_SERVICIOS.md](./03_SERVICIOS.md#updateservice-con-fixes-de-abril-2026) y [05_FLUJOS_TRABAJO.md](./05_FLUJOS_TRABAJO.md#12-auto-update-con-fix-uipischtasks-abril-2026) para el flujo completo.

### Mecanismo post-update — schtasks /rl limited (fix abril 2026)

Problema historico: tras el auto-update la app quedaba relanzada con integridad alta (heredada del instalador elevado con UAC), y Windows UIPI bloqueaba drag-drop desde el Explorador hacia IMA Drive. Cinco commits iterativos:

| Commit | Fix |
|---|---|
| `4c93493` | Restaurar handlers drag-drop en XAML (`AddHandler` en code-behind se optimiza fuera en Release). |
| `bcd58e6` | Diagnosticar que la app lanzaba elevada tras auto-update. |
| `3d38fff` | Quitar `Verb=runas` del auto-update. |
| `0bdc11c` | Relanzar via script auxiliar .bat. |
| `d44710d` | Usar `schtasks /rl limited` — unica forma real de des-elevar en Windows. |

El script auxiliar que genera `UpdateService.InstallUpdate` hace:

```bat
taskkill /F /IM SistemaGestionProyectos2.exe
start /wait "" "%TEMP%\SistemaGestionProyectos-vX.Y.Z-Setup.exe" /silent
schtasks /create /tn "<taskName>" /tr "\"%APPEXE%\"" /sc once /st 00:00 /f /rl limited >nul 2>&1
schtasks /run /tn "<taskName>" >nul 2>&1
schtasks /delete /tn "<taskName>" /f >nul 2>&1
```

- `/rl limited` = Run Level LIMITED = integridad media, sin UAC.
- Solo `schtasks` y Shell COM (`IShellDispatch2.ShellExecute`) pueden des-elevar un proceso hijo. `Process.Start` hereda el token elevado del padre.

**Regla al tocar auto-update:** no restaurar `Verb=runas`, no usar `Process.Start` del instalador en modo elevado seguido de relanzar la app por el mismo camino. El flujo debe terminar con schtasks.

---

## Troubleshooting

### La app pide actualizar en bucle
- Verificar que `.csproj.Version` coincida con `app_versions.version` (la fila `is_latest=true`).
- En BD:
  ```sql
  SELECT version, is_latest, is_mandatory, release_date::date
  FROM app_versions WHERE is_latest = true;
  ```
- En codigo: `grep -E "Version|AssemblyVersion|FileVersion" SistemaGestionProyectos2.csproj`.
- `IsNewerVersion` compara semantica (Major.Minor.Build). Si `.csproj` va en 2.3.3 y BD dice 2.3.4 -> pide actualizar. Correcto.

### Drag-drop no funciona tras actualizar
Sintomas: el cursor no muestra icono de copia, o al soltar archivos no pasa nada.

1. Confirmar que la app NO corre elevada:
   ```powershell
   Get-Process SistemaGestionProyectos2 | Select-Object Name, @{N='Elevated';E={$_.StartInfo.Verb -eq 'runas'}}
   ```
   Mejor: Task Manager -> columna "Elevated".
2. Si esta elevada, cerrar y relanzar con doble clic normal.
3. Si persiste, revisar que los commits del fix UIPI estan presentes:
   ```bash
   git log --oneline | grep -iE "uipi|runas|schtasks|drag-drop"
   ```

### El instalador requiere .NET Runtime
Causa: no se publico self-contained. Verificar `--self-contained true` en el comando de publish.

### Inno Setup no encuentra binarios
Verificar que `bin/Release/net8.0-windows/win-x64/publish/SistemaGestionProyectos2.exe` exista antes de correr ISCC.

### URL del instalador incorrecta en BD
Correccion rapida:
```sql
UPDATE app_versions
SET download_url = 'https://github.com/Anathema69/MX-VBA/releases/download/v2.3.4/SistemaGestionProyectos-v2.3.4-Setup.exe'
WHERE version = '2.3.4';
```

### Desactivar una version problematica
```sql
UPDATE app_versions SET is_active = false, is_latest = false WHERE version = '2.3.4';
UPDATE app_versions SET is_latest = true WHERE version = '2.3.3';
```

---

## Historial de releases reciente

| Version | Fecha | Highlights |
|---|---|---|
| v2.3.3 | Abril 2026 | Fix drag-drop intermitente + tests automatizados |
| v2.3.2 | Abril 2026 | Fix drag-drop desde Ordenes (race condition) |
| v2.3.1 | Marzo 2026 | Sincronizacion de carpetas + UI mejorada |
| v2.3.0 | Marzo 2026 | IMA Drive mejoras CAD + ventana unica |
| v2.2.0 | Marzo 2026 | Modulo Inventario + IMA Drive produccion |
| v2.1.0 | Marzo 2026 | Drive V3 F+G + Modo Produccion |
| v2.0.9 | Marzo 2026 | Modulo Inventario (mockup) |

Historial completo: `git log --oneline --grep "^release:"`

---

## Referencias

- [03_SERVICIOS.md#updateservice](./03_SERVICIOS.md#updateservice-con-fixes-de-abril-2026) — detalle del `UpdateService` y sus metodos.
- [05_FLUJOS_TRABAJO.md#12-auto-update](./05_FLUJOS_TRABAJO.md#12-auto-update-con-fix-uipischtasks-abril-2026) — diagrama del flujo.
- `SistemaGestionProyectos2/sql/update_app.sql` — script actual.
- `SistemaGestionProyectos2/installer.iss` — config Inno Setup.
- `SistemaGestionProyectos2/build-release.bat` — automatizacion opcional (build + sign).
- Inno Setup: https://jrsoftware.org/isinfo.php
- `dotnet publish`: https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-publish
