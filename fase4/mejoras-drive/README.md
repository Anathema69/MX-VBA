# IMA Drive — Mejoras Post-Produccion

**Fecha inicio:** 2026-03-24
**Version base:** v2.2.0 (Drive V3-E completado)
**Estado:** EN PROGRESO

---

## Dashboard de Progreso

| # | Mejora | Severidad | Esfuerzo | Estado |
|---|--------|-----------|----------|--------|
| 1 | Actualizar mappings de extensiones (FIcon, FType, IsCadFile, GetContentType) | Alta | Bajo | ✅ HECHO |
| 2 | Filtro de basura en upload (~$, .db, .lck) + limpieza BD | Alta | Bajo | ✅ HECHO |
| 3 | Fallback OpenAs_RunDLL si Process.Start falla (.dwg y otros) | Alta | Bajo | ✅ HECHO |
| 4 | Sub-filtros CAD (Ensambles / Piezas / Planos / CNC) | Media | Medio | ✅ HECHO |
| 5 | Thumbnails CAD via Windows Shell (IShellItemImageFactory) | Media | Medio | ✅ HECHO |
| 6 | Descarga de contexto CAD (partes del ensamble) | Critica | Alto | ✅ HECHO |
| 7 | Ventana unica (Hide/Show MainMenu al abrir/cerrar modulo) | Media | Medio | ✅ HECHO |

**Progreso global:** 7/7 items completados

---

## Detalle por Mejora

### MEJORA-1: Actualizar mappings de extensiones

**Problema:** El 85% de archivos en BD (.ipt, .sldprt, .iam, .mcam, etc.) se muestran con icono generico y nombre ilegible ("Archivo IPT"). Extensiones como `.mcam`, `.mcx-7`, `.mcx-5`, `.sldasm` no estan mapeadas en ningun lado.

**Archivos modificados:**
- `Views/DriveV2Window.xaml.cs` → `FIcon()`, `FType()`, `FilterExtensions`
- `Services/Drive/DriveService.cs` → `IsCadFile()`, `GetContentType()`, `GetFileIcon()`

**Extensiones agregadas (basado en BD real):**

| Extension | Archivos en BD | Categoria | Subtipo | Icono |
|-----------|---------------|-----------|---------|-------|
| .ipt | 502 | CAD | Pieza | ruler |
| .iam | 97 | CAD | Ensamble | settings |
| .sldprt | 124 | CAD | Pieza | ruler |
| .sldasm | 27 | CAD | Ensamble | settings |
| .mcam | 80 | CNC | Programa | code |
| .mcx-7 | 77 | CNC | Programa | code |
| .mcx-5 | 3 | CNC | Programa | code |
| .igs | 52 | CAD | Modelo 3D | ruler |
| .jfif | 8 | Imagen | — | photo |

---

### MEJORA-2: Filtro de basura en upload

**Problema:** Usuarios suben archivos basura al seleccionar carpetas completas:
- `~$*.SLDPRT` (lock files de SolidWorks, 5-10 bytes)
- `Thumbs.db` (thumbnails de Windows)
- `.lck` (lock files de CAD)

**Archivos modificados:**
- `Views/DriveV2Window.xaml.cs` → `UploadFiles()` filtra antes de subir + toast informativo
- `Services/Drive/DriveService.cs` → `IsJunkFile()` metodo centralizado

**Reglas de filtrado:**
- Prefijo `~$` → lock file de Office/SolidWorks
- Prefijo `.` → archivo oculto de sistema
- Extension `.db` → Thumbs.db de Windows
- Extension `.lck` → lock file de CAD
- Extension `.tmp` / `.bak` → temporales

**Limpieza BD (SQL manual):**
```sql
-- Ejecutar manualmente despues de verificar
DELETE FROM drive_files
WHERE file_name LIKE '~$%'
   OR LOWER(file_name) LIKE '%.db'
   OR LOWER(file_name) LIKE '%.lck';
-- Nota: los blobs en R2 quedan huerfanos (costo negligible ~1KB)
```

---

### MEJORA-3: Fallback OpenAs_RunDLL

**Problema:** Si .dwg (u otra extension) no tiene programa predeterminado asociado en Windows, `Process.Start` lanza `Win32Exception` y el archivo no abre. El usuario solo ve "No se pudo abrir".

**Solucion:** Capturar `Win32Exception` y abrir el dialogo nativo de Windows "¿Con que app deseas abrir este archivo?". El usuario elige programa y lo marca como predeterminado.

**Archivo modificado:**
- `Views/DriveV2Window.xaml.cs` → `OpenFileInPlace()`

---

### MEJORA-4: Sub-filtros CAD (PENDIENTE)

**Problema:** El filtro "Archivos CAD" agrupa 10+ extensiones sin distincion. El cliente no puede filtrar solo ensambles (.iam, .sldasm) vs piezas (.ipt, .sldprt) vs programas CNC (.mcam, .mcx-*).

**Propuesta:**
```
▼ CAD (47)
  ├─ ⚙ Ensambles (8)    .iam .sldasm
  ├─ 📐 Piezas (25)     .ipt .sldprt
  ├─ 📏 Planos (10)     .dwg .dxf
  └─ ⚡ CNC (4)          .mcam .mcx-5 .mcx-7 .mcx-9
```

---

### MEJORA-5: Thumbnails CAD via Windows Shell (PENDIENTE)

**Problema:** Archivos CAD muestran icono generico. Windows Explorer muestra thumbnails 3D renderizados si el software esta instalado.

**Propuesta:** Usar `IShellItemImageFactory` (COM) para obtener el thumbnail que Windows ya genera, despues de descargar al cache local. Solo funciona si el usuario tiene SW/Inventor instalado.

**Alternativa minima:** Extraer thumbnail embebido de archivos .ipt/.sldprt (OLE structured storage).

---

### MEJORA-6: Descarga de contexto CAD (PENDIENTE)

**Problema:** Al abrir un ensamble (.iam) desde una carpeta, Inventor busca las partes por nombre. Si el usuario abrio partes de otro mes antes, Inventor puede mezclar partes de meses distintos.

**Propuesta:** Al abrir un ensamble, descargar TAMBIEN todas las partes de la misma carpeta Drive al mismo subdirectorio local, para que el software CAD encuentre todo junto.

---

### MEJORA-7: Ventana unica (PENDIENTE)

**Problema:** La app abre cada modulo como Window independiente. El taskbar muestra multiples entradas confusas.

**Propuesta (Opcion C — Hide/Show):** Al abrir un modulo, `MainMenuWindow.Hide()`. Al cerrar modulo, `MainMenuWindow.Show()`. Una sola ventana visible a la vez.

---

## Datos de la BD (24-Mar-2026)

**1,349 archivos** total en `drive_files`:

| Extension | Archivos | Tamano | Categoria |
|-----------|----------|--------|-----------|
| .ipt | 502 | 81 MB | CAD Pieza (Inventor) |
| .sldprt | 124 | 16 MB | CAD Pieza (SolidWorks) |
| .iam | 97 | 29 MB | CAD Ensamble (Inventor) |
| .step | 96 | 32 MB | Modelo 3D STEP |
| .stp | 82 | 20 MB | Modelo 3D STEP |
| .mcam | 80 | 132 MB | CNC (Mastercam) |
| .mcx-7 | 77 | 56 MB | CNC (Mastercam 7) |
| .mcx-9 | 55 | 39 MB | CNC (Mastercam 9) |
| .igs | 52 | 5 MB | Modelo 3D IGES |
| .sldasm | 27 | 83 MB | CAD Ensamble (SolidWorks) |
| .db | 23 | 1 MB | BASURA (Thumbs.db) |
| .pdf | 22 | 2 MB | Documento PDF |
| .lck | 11 | 176 KB | BASURA (lock file) |
| .jfif | 8 | 2 MB | Imagen JPEG |
| .txt | 4 | 504 B | Texto plano |
| .log | 4 | 81 KB | Registro |
| .mcx-5 | 3 | 19 MB | CNC (Mastercam 5) |
| .dxf | 1 | 21 KB | Plano DXF |
| .dwg | 1 | 406 KB | Plano AutoCAD |

**Basura detectada:** ~17 archivos `~$*` (lock files SolidWorks, 5-10 bytes) + 23 `.db` + 11 `.lck` = ~51 archivos basura
