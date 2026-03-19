# Fase V3-D: Compartir & Enlaces

**Estado:** COMPLETADO (18-Mar-2026) — D1/D2/D4 cancelados por cliente, solo D3 (ZIP) implementado
**Prioridad:** 5
**Archivos clave:** `DriveService.cs`, `DriveV2Window.xaml.cs`

---

## Checklist de implementacion

### D1: Generar enlace temporal (signed URL)
- [x] Nuevo metodo en DriveService: `GenerateShareLink(storagePath, expiration) → string`
- [x] Usar `S3Client.GetPreSignedURL()` (ya disponible en AWSSDK.S3)
- [x] Default expiracion: 24 horas
- [x] Context menu: "Compartir enlace" como nueva opcion
- [x] Al generar: copiar al clipboard automaticamente con `Clipboard.SetText(url)`
- [x] Toast: "Enlace copiado — expira en 24 horas"
- [x] Registrar en drive_activity: action='share'

### D2: Dialog de compartir
- [x] Window estilo IMA (WindowStyle=None, AllowsTransparency, CornerRadius)
- [x] Preview: icono tipo + nombre del archivo + tamano
- [x] Dropdown de expiracion: "1 hora", "24 horas", "7 dias", "30 dias"
- [x] Al cambiar expiracion: regenerar el link
- [x] TextBox readonly con el link generado
- [x] Boton "Copiar enlace" con feedback visual: texto cambia a "Copiado!" por 2s
- [x] Boton "Abrir en navegador" para verificar el enlace
- [x] Boton cerrar (X) en esquina superior derecha

### D3: Descargar carpeta como ZIP
- [x] Context menu en carpeta: "Descargar como ZIP"
- [x] Recopilar archivos recursivamente (reusar `CollectAllFilePaths` existente)
- [x] Validar limites: max 50 archivos, max 500MB total
- [x] Si excede: dialog "Esta carpeta contiene X archivos (Y MB). Continuar?"
- [x] SaveFileDialog para elegir donde guardar el ZIP
- [x] Descargar cada archivo de R2 en paralelo (SemaphoreSlim(3))
- [x] Crear ZIP con `System.IO.Compression.ZipArchive` (.NET 8 built-in)
- [x] Progress: porcentaje basado en archivos completados (no bytes)
- [x] Toast al finalizar: "ZIP descargado: Planos.zip (2.3 MB, 12 archivos)"
- [x] Si algun archivo falla: incluir nota en toast "11 de 12 archivos descargados"

### D4: Historial de enlaces (basico)
- [x] Filtrar drive_activity WHERE action='share' AND target_id=fileId
- [x] En panel de detalle del archivo: seccion "Enlaces compartidos"
- [x] Mostrar: fecha de creacion + expiracion + estado (activo/expirado)
- [x] Estado: calcular si `created_at + expiracion < now()` → "Expirado" (rojo) / "Activo" (verde)
- [x] Nota: R2 signed URLs no se pueden revocar; informar al usuario
- [x] Boton "Generar nuevo enlace" si el actual expiro

---

## Notas tecnicas

### Signed URL con R2
```csharp
public string GenerateShareLink(string storagePath, TimeSpan expiration)
{
    if (!_isStorageConfigured) return null;

    var request = new GetPreSignedUrlRequest
    {
        BucketName = _bucketName,
        Key = storagePath,
        Expires = DateTime.UtcNow.Add(expiration),
        Verb = HttpVerb.GET
    };

    // R2 requiere path-style access
    return _s3Client.GetPreSignedURL(request);
}
```

### ZIP en memoria vs disco
- Para carpetas pequenas (<50MB): crear ZIP en MemoryStream
- Para carpetas grandes: crear ZIP directo a disco (FileStream)
- `ZipArchive` de .NET 8 soporta ambos

### Limitaciones de signed URLs en R2
- No se pueden revocar individualmente
- La unica forma de invalidar TODOS los enlaces es rotar el SecretAccessKey
- Para el MVP, documentar esta limitacion
- Futuro: si se necesita revocacion, implementar un proxy/redirect con validacion
