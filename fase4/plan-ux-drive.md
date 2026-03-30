# Plan UX/UI - Rediseno IMA Drive

> **Fecha:** 30-Mar-2026
> **Archivo objetivo:** `SistemaGestionProyectos2/Views/DriveV2Window.xaml` + `.xaml.cs`
> **Progreso global:** 28/36 items (78%) - TODOS LOS 6 PUNTOS COMPLETADOS

---

## Punto 1: Animaciones de Carga
> Reemplazar spinner generico con skeleton loading + transiciones suaves

**Progreso: 4/7 -- CORE COMPLETADO (1E-1G descartados: skeleton+stagger reemplazan la necesidad)**

### 1A - Skeleton Cards (Ghost Cards) para Grid View
- [x] `MkGhostCard()`: Border 200px con thumbnail placeholder (120px) + 3 barras (nombre/meta/fecha)
- [x] Pulse animation (opacity 0.4↔1.0 en 800ms con SineEase, forever)
- [x] 8 ghost cards en WrapPanel via `ShowSkeletonGrid()`

### 1B - Skeleton Rows para List View
- [x] `MkGhostRow()`: Grid 4-col con icono 32px + barras de nombre/tipo/tamano/fecha
- [x] Misma pulse animation
- [x] 8 ghost rows en Border con CornerRadius via `ShowSkeletonList()`

### 1C - Reemplazar Spinner Central
- [x] `ShowSkeletonLoading()` llamado en LoadFolderFull y LoadRecentsInContent
- [x] Spinner del LoadingPanel conservado solo como fallback (ContextDownloadOverlay lo usa separadamente)
- [x] El contenido real reemplaza el skeleton automaticamente via RenderFolderUI/RenderContent

### 1D - Fade-in Escalonado (Staggered)
- [x] `ApplyStaggeredFadeIn(panel)`: delay de 30ms/card, max 12 stagger
- [x] Cada card: opacity 0→1 + translateY 12→0 en 200ms con QuadraticEase
- [x] Aplicado en RenderContent a todos los WrapPanels

### 1E-1G — DESCARTADOS
- El skeleton reemplaza completamente al spinner central
- La progress bar en top bar agrega complejidad sin valor (el skeleton ya da feedback suficiente)

---

## Punto 2: Dialogos de Confirmacion
> Unificar todos los dialogs, agregar animaciones, backdrop, botones con estados

**Progreso: 6/6 -- COMPLETADO**

### 2A - CornerRadius + Hover en botones del Confirm()
- [x] Botones cancel/ok refactorizados con `MkStyledBtn()`: CornerRadius=8, hover/pressed states completos
- [x] CtxCancelBtn: cambiado a Style=SecondaryButton (en P3E)

### 2B - Backdrop Dim
- [x] Window.Background cambiado a `#660F172A` (slate-900 con 40% opacity)

### 2C - Animacion de Entrada
- [x] Scale 0.95->1.0 + Opacity 0->1 en 200ms con CubicEase en el card Border

### 2D - Unificar Dialog Base
- [x] `MkStyledBtn()` helper reutilizable para todos los dialogs (Confirm, SyncDialog)
- [x] Colores de titulo/mensaje usan constantes frozen (TextPrimary, TextMuted)

### 2E - Selection Banner Botones
- [x] Implementado en P4C: PrimaryButton + SecondaryButton

### 2F - Width Dinamico
- [x] Botones usan MinWidth=100 en vez de Width fijo de 110px

---

## Punto 3: Botones - Coherencia de Estilos
> Asegurar que TODOS los botones de la app usen los 6 estilos definidos en XAML

**Progreso: 6/6 -- COMPLETADO**

### 3A - DestructiveButton: Pressed + Disabled State
- [x] Implementado en P4E: pressed=#FECACA, disabled=opacity 0.5

### 3B - (Fusionado con 3A)

### 3C - LinkButton: Disabled State
- [x] Foreground=#94A3B8, Cursor=Arrow cuando IsEnabled=False

### 3D - GhostButton
- [x] Nuevo estilo en XAML: bg=Transparent, fg=#64748B, hover=bg #F8FAFC + fg #475569, pressed=#F1F5F9

### 3E - Botones Inline del SyncDialog + CtxCancelBtn
- [x] SyncDialog: 3 botones refactorizados con `MkStyledBtn()` helper (CornerRadius=8, hover/pressed states)
- [x] CtxCancelBtn: cambiado a Style=SecondaryButton

### 3F - Transiciones Suaves (ColorAnimation)
- [x] PrimaryButton: hover-in 100ms, hover-out 150ms, pressed 50ms (via SolidColorBrush nombrado + Storyboard)
- [x] SecondaryButton: misma estructura de animación

---

## Punto 4: Paleta de Colores - Consolidacion
> Eliminar colores Material stray, unificar semantica

**Progreso: 5/5 -- COMPLETADO**

### 4A - Definir Tokens Semanticos
- [x] Agregar brushes semanticos en Window.Resources (8 tokens XAML + 8 constantes C# frozen)
- [x] Refactorizar los hardcoded colors en code-behind para usar estos tokens

### 4B - Corregir Toast Colors
- [x] Success: `#065F46` (emerald-800), Error: `#9F1239` (rose-800), Info: `#1E40AF` (blue-800)

### 4C - Eliminar Material Colors
- [x] Selection Banner: Material orange → Tailwind amber + reutiliza PrimaryButton/SecondaryButton
- [x] GreenOk eliminado, reemplazado por Success (#10B981)

### 4D - Unificar Verdes
- [x] Toggle recientes + ghost upload states: GreenOk → Success

### 4E - Unificar Rojos
- [x] DestructiveBrush: #DC2626 → #EF4444 (base), hover #FEE2E2, pressed #FECACA (nuevo), disabled opacity 0.5 (nuevo)

---

## Punto 5: Toast Notifications - Rediseno
> Cambiar de fondo oscuro solido a estilo "pill" light + mejorar animaciones

**Progreso: 3/5 -- CORE COMPLETADO (5D, 5E opcionales descartados por complejidad vs valor)**

### 5A - Rediseno Visual (Light Pill)
- [x] Fondo blanco + border izquierdo 4px con color semantico (Grid 3-col layout)
- [x] Icono y accent bar coloreados por tipo (success=#10B981, error=#EF4444, warning=#F59E0B, info=#3B82F6)
- [x] Texto oscuro (#0F172A) en lugar de blanco
- [x] Border exterior sutil #E2E8F0 + sombra suave

### 5B - Animacion Slide-Down + Fade
- [x] Entrada: TranslateY -20->0 + Opacity 0->1 en 250ms con CubicEase EaseOut
- [x] Salida: TranslateY 0->-10 + Opacity 1->0 en 200ms con CubicEase EaseIn
- [x] Helper `DismissToast()` reutilizable

### 5C - Boton de Cerrar (X)
- [x] Icono E711 con Foreground TextLight, Cursor=Hand
- [x] Click handler `ToastClose_Click`: detiene timer + ejecuta DismissToast()

### 5D - Toast Stack — DESCARTADO
- Complejidad alta vs uso real (las toasts se reemplazan rapidamente, rara vez hay 3+ simultaneas)

### 5E - Toast de Progreso — DESCARTADO
- El ContextDownloadOverlay ya cubre este caso de uso adecuadamente

---

## Punto 6: Micro-interacciones
> Agregar animaciones sutiles que den vida a la interfaz

**Progreso: 4/7 -- CORE COMPLETADO (6B, 6F, 6G descartados por bajo impacto visual vs complejidad)**

### 6A - Hover en File/Folder Cards (Scale + Shadow)
- [x] Folder cards + File cards: ScaleTransform 1.0→1.015 en 150ms enter, 1.0 en 200ms leave
- [x] Shadow elevada: BlurRadius=20, ShadowDepth=6, Opacity=0.10
- [x] Helper `AnimateScale()` reutilizable

### 6B - Checkbox Bounce — DESCARTADO
- Bajo impacto visual relativo al esfuerzo (keyframes complejos)

### 6C - Sidebar Chevron Rotation Animada
- [x] DoubleAnimation 90↔0 en 200ms con QuadraticEase EaseInOut

### 6D - Drag & Drop Overlay Animado
- [x] Enter: fade-in 200ms + ScaleTransform 0.9→1.0 en Grid central
- [x] Leave: fade-out 150ms

### 6E - Image Overlay Transición
- [x] Open: overlay fade-in 250ms + image ScaleTransform 0.9→1.0 en 300ms CubicEase
- [x] Close: `CloseImageOverlay()` con fade-out 200ms

### 6F, 6G — DESCARTADOS
- Breadcrumb y accent bar: polish minimo vs complejidad de implementacion

---

## Orden de Implementacion Recomendado

| Fase | Punto | Items | Razon |
|------|-------|-------|-------|
| 1 | P4 (Colores) | 4A-4E | Base: los tokens afectan todo lo demas |
| 2 | P3 (Botones) | 3A-3E | Estructura: corregir estilos base |
| 3 | P2 (Dialogos) | 2A-2F | Depende de P3 para reusar estilos |
| 4 | P5 (Toasts) | 5A-5C | Depende de P4 para colores semanticos |
| 5 | P1 (Skeleton) | 1A-1E | Feature nueva independiente |
| 6 | P6 (Micro) | 6A-6G | Polish final, depende de todo lo anterior |

> **Nota:** Dentro de cada punto, los items estan ordenados por dependencia.
> Items marcados como "opcional" o "evaluar" pueden descartarse sin afectar la coherencia del rediseno.
