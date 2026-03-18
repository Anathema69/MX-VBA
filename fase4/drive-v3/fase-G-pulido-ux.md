# Fase V3-G: Pulido UX & Cosmeticos

**Estado:** PENDIENTE
**Prioridad:** 7 (ultima fase)
**Archivos clave:** `DriveV2Window.xaml`, `DriveV2Window.xaml.cs`

---

## Checklist de implementacion

### G1: Atajos de teclado estilo Explorador de Windows
- [ ] `F2` → Renombrar seleccionado (invocar `RenFolder` o `RenFile`)
- [ ] `Delete` → Eliminar con confirmacion (invocar `DelFolder` o `DelFile`)
- [ ] `F5` → Refrescar carpeta actual (invocar `LoadCurrentFolder`)
- [ ] `Ctrl+N` → Nueva carpeta (invocar `NewFolder_Click`)
- [ ] `Ctrl+U` → Subir archivo (invocar `Upload_Click`)
- [ ] `Ctrl+F` → Focus en SearchBox
- [ ] `Ctrl+A` → Seleccionar todos los archivos (agregar todos a `_selectedFileIds`)
- [ ] `Backspace` → Volver a carpeta padre (invocar `GoBack`)
- [ ] `Enter` → Abrir carpeta o archivo seleccionado
- [ ] `Alt+Left` → Historial atras
- [ ] `Alt+Right` → Historial adelante
- [ ] `Escape` → Cancelar seleccion multiple / cerrar panel detalle
- [ ] Implementar todo en `OnKeyDown` override con switch:
  ```csharp
  protected override void OnKeyDown(KeyEventArgs e)
  {
      if (_isCreatingFolder) { base.OnKeyDown(e); return; }

      var ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
      var alt = Keyboard.Modifiers.HasFlag(ModifierKeys.Alt);

      switch (e.Key)
      {
          case Key.F2: RenameSelected(); e.Handled = true; break;
          case Key.Delete: DeleteSelected(); e.Handled = true; break;
          case Key.F5: _ = LoadCurrentFolder(); e.Handled = true; break;
          case Key.N when ctrl: NewFolder_Click(null, null); e.Handled = true; break;
          // ... etc
      }
      base.OnKeyDown(e);
  }
  ```
- [ ] Tooltip en botones del toolbar mostrando el atajo: "Nueva carpeta (Ctrl+N)"

### G2: Breadcrumb con dropdown de hermanos
- [ ] Al hacer clic en segmento de breadcrumb: ademas de navegar, mostrar dropdown
- [ ] Clic izquierdo: navegar (comportamiento actual)
- [ ] Clic derecho o hover+delay: mostrar Popup con carpetas hermanas
- [ ] Query: `GetChildFolders(parentId)` del segmento padre
- [ ] Popup: lista de carpetas con icono + nombre
- [ ] Clic en item del dropdown: navegar a esa carpeta
- [ ] Estilo: mismo estilo que context menu (CornerRadius=10, shadow)
- [ ] Highlight del item actual en la lista (bold, fondo gris claro)

### G3: Status bar informativa
- [ ] Reemplazar/mejorar la barra inferior actual
- [ ] Layout: `DockPanel` con 3 secciones:
  - Izquierda: conteo de elementos ("12 carpetas, 45 archivos")
  - Centro: estado de sync (Fase E) o info de seleccion
  - Derecha: almacenamiento total ("2.3 GB de 10 GB")
- [ ] Contextos:
  - Normal: "12 carpetas, 45 archivos — 234 MB"
  - Seleccion: "3 archivos seleccionados (1.2 MB)"
  - Busqueda: "8 resultados para 'contrato'"
  - Upload: "Subiendo 2 archivos..."
  - Sync: "Sincronizado" / "Sincronizando..." / "Sin conexion"
- [ ] Transiciones suaves entre estados

### G4: Empty states mejorados
- [ ] Carpeta vacia:
  - Icono grande de carpeta (64px, color muted)
  - Texto: "Esta carpeta esta vacia"
  - Subtexto: "Arrastra archivos aqui o usa los botones de arriba"
  - Botones inline: "Subir archivos" | "Nueva carpeta"
- [ ] Sin resultados de busqueda:
  - Icono de lupa (64px)
  - Texto: "No se encontraron resultados para '[query]'"
  - Boton: "Buscar en todo el Drive"
- [ ] Error de conexion:
  - Icono de nube tachada (64px)
  - Texto: "No se pudo conectar al servidor"
  - Boton: "Reintentar"
- [ ] Primer uso (root vacio):
  - Titulo: "Bienvenido a IMA Drive"
  - 3 pasos: "1. Crea una carpeta" → "2. Sube archivos" → "3. Vincula a una orden"
  - Boton: "Crear primera carpeta"

### G5: Animaciones de transicion
- [ ] Navegacion entre carpetas: fade out (100ms) → fade in (150ms)
  ```csharp
  var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(100));
  fadeOut.Completed += (s, e) =>
  {
      // Render new content
      RenderFolderUI();
      var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150));
      ContentHost.BeginAnimation(OpacityProperty, fadeIn);
  };
  ContentHost.BeginAnimation(OpacityProperty, fadeOut);
  ```
- [ ] Nuevo archivo subido: scale 0.8→1.0 + opacity 0→1 (200ms, EaseOut)
- [ ] Archivo eliminado: scale 1.0→0.8 + opacity 1→0 (200ms, EaseIn)
- [ ] Ghost cards upload: pulse sutil en el borde (color azul 0.3→0.7→0.3 loop)
- [ ] Panel de detalle: slide-in desde la derecha (200ms)
- [ ] Toast notifications: slide-in desde abajo (ya existe, verificar que sea suave)
- [ ] Importante: todas las animaciones deben ser cancelables (si el usuario navega rapido)
  ```csharp
  ContentHost.BeginAnimation(OpacityProperty, null); // cancel pending
  ```

---

## Notas

- Esta fase se ejecuta al final porque son mejoras de polish, no funcionalidad critica
- Cada item es independiente — se pueden implementar en cualquier orden
- Las animaciones deben ser sutiles (100-200ms max) — no queremos que la app se sienta lenta
- Los atajos de teclado son los que mas impacto tendran en usuarios avanzados
