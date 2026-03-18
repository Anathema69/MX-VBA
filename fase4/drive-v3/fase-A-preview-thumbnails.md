# Fase V3-A: Preview & Thumbnails

**Estado:** PENDIENTE
**Prioridad:** 1 (primera fase a implementar)
**Archivos clave:** `DriveV2Window.xaml.cs`, `DriveService.cs`

---

## Checklist de implementacion

### A1: Preview de imagenes en panel de detalle
- [ ] Modificar `ShowDetail()` en DriveV2Window.xaml.cs
- [ ] Detectar si el archivo es imagen (reusar `IsImageFile()` del DriveService)
- [ ] Descargar via R2 signed URL (no el blob completo)
- [ ] Crear `BitmapImage` con `DecodePixelWidth=300` (control de memoria)
- [ ] Mostrar en `Image` control reemplazando el icono generico
- [ ] Loading spinner mientras descarga
- [ ] Cache en memoria: `Dictionary<int, BitmapImage>` con max 20 entries
- [ ] Eviction LRU cuando se supera el limite
- [ ] Clic en preview → overlay fullscreen (Border con fondo negro 0.85 opacity, Image Stretch=Uniform)
- [ ] Escape o clic fuera cierra el overlay
- [ ] Flechas izq/derecha para navegar entre imagenes de la misma carpeta

### A2: Preview de PDFs
- [ ] Evaluar WebView2 vs abrir en navegador (decision de equipo)
- [ ] Si WebView2: agregar NuGet `Microsoft.Web.WebView2`
- [ ] Si WebView2: embeber control en panel de detalle
- [ ] Si navegador: boton "Ver PDF" que descarga a temp y abre con `Process.Start`
- [ ] Descargar a `%TEMP%/IMA-Drive/preview/{fileId}_{fileName}`
- [ ] No re-descargar si ya existe y `uploaded_at` no cambio

### A3: Preview de texto plano
- [ ] Detectar extensiones: .txt, .csv, .log, .json, .xml, .md, .ini, .cfg
- [ ] Descargar primeros 100KB del archivo
- [ ] Mostrar en TextBox readonly, FontFamily=Consolas, FontSize=12
- [ ] ScrollViewer vertical
- [ ] Si archivo > 100KB: nota "Mostrando primeros 100KB"

### A4: Thumbnails en grid
- [ ] En `MkFileCard()`: detectar imagen → placeholder + carga async
- [ ] `LoadThumbnailAsync(int fileId, Image targetControl)`
- [ ] Check cache disco: `%TEMP%/IMA-Drive/thumbs/{fileId}.jpg`
- [ ] Si no existe: descargar de R2, redimensionar a 160x120, guardar como JPEG q80
- [ ] `SemaphoreSlim(5)` para limitar descargas paralelas
- [ ] Al completar: `Dispatcher.Invoke()` para actualizar el Image control
- [ ] Cancelar descargas pendientes al navegar a otra carpeta (`_cts.Cancel()`)
- [ ] Redimensionar con `TransformedBitmap` o `BitmapImage.DecodePixelWidth`

### A5: Preview Office (basica)
- [ ] Para .docx/.xlsx/.pptx: icono grande del tipo (Word/Excel/PowerPoint)
- [ ] Colores: Word=#2B579A, Excel=#217346, PowerPoint=#D24726
- [ ] Boton prominente "Abrir con [App]"
- [ ] Mostrar metadatos: tamano, fecha, tipo
- [ ] (Futuro) Extraer info: paginas/hojas con OpenXML SDK — fuera de alcance ahora

### A6: Preview CAD (placeholder)
- [ ] Para extensiones CAD: icono grande de engranaje/plano tecnico
- [ ] Texto: "Vista previa no disponible para archivos CAD"
- [ ] Boton "Abrir con aplicacion CAD"
- [ ] Color del icono: #FF6F00 (naranja ingenieria)

---

## Notas de implementacion

### Signed URL para preview (evitar descargar blob completo a memoria)
```csharp
// En DriveService.cs — nuevo metodo
public string GetPreviewUrl(string storagePath, int expirationMinutes = 15)
{
    var request = new GetPreSignedUrlRequest
    {
        BucketName = _bucketName,
        Key = storagePath,
        Expires = DateTime.UtcNow.AddMinutes(expirationMinutes),
        Verb = HttpVerb.GET
    };
    return _s3Client.GetPreSignedURL(request);
}
```

### Thumbnail resize
```csharp
private async Task<BitmapImage> CreateThumbnailAsync(byte[] imageBytes, int maxWidth = 160)
{
    return await Task.Run(() =>
    {
        var bi = new BitmapImage();
        bi.BeginInit();
        bi.StreamSource = new MemoryStream(imageBytes);
        bi.DecodePixelWidth = maxWidth;
        bi.CacheOption = BitmapCacheOption.OnLoad;
        bi.EndInit();
        bi.Freeze(); // required for cross-thread access
        return bi;
    });
}
```

### Cache LRU (in-memory para previews)
```csharp
private readonly LinkedList<int> _previewLru = new();
private readonly Dictionary<int, (BitmapImage img, LinkedListNode<int> node)> _previewCache = new();
private const int MaxPreviewCache = 20;

private void CachePreview(int fileId, BitmapImage img)
{
    if (_previewCache.ContainsKey(fileId))
    {
        _previewLru.Remove(_previewCache[fileId].node);
    }
    else if (_previewCache.Count >= MaxPreviewCache)
    {
        var oldest = _previewLru.Last!.Value;
        _previewLru.RemoveLast();
        _previewCache.Remove(oldest);
    }
    var node = _previewLru.AddFirst(fileId);
    _previewCache[fileId] = (img, node);
}
```
