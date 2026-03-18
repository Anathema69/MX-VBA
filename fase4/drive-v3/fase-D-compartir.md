# Fase V3-D: Compartir & Enlaces

**Estado:** PENDIENTE
**Prioridad:** 5
**Archivos clave:** `DriveService.cs`, `DriveV2Window.xaml.cs`

---

## Checklist de implementacion

### D1: Generar enlace temporal (signed URL)
- [ ] Nuevo metodo en DriveService: `GenerateShareLink(storagePath, expiration) → string`
- [ ] Usar `S3Client.GetPreSignedURL()` (ya disponible en AWSSDK.S3)
- [ ] Default expiracion: 24 horas
- [ ] Context menu: "Compartir enlace" como nueva opcion
- [ ] Al generar: copiar al clipboard automaticamente con `Clipboard.SetText(url)`
- [ ] Toast: "Enlace copiado — expira en 24 horas"
- [ ] Registrar en drive_activity: action='share'

### D2: Dialog de compartir
- [ ] Window estilo IMA (WindowStyle=None, AllowsTransparency, CornerRadius)
- [ ] Preview: icono tipo + nombre del archivo + tamano
- [ ] Dropdown de expiracion: "1 hora", "24 horas", "7 dias", "30 dias"
- [ ] Al cambiar expiracion: regenerar el link
- [ ] TextBox readonly con el link generado
- [ ] Boton "Copiar enlace" con feedback visual: texto cambia a "Copiado!" por 2s
- [ ] Boton "Abrir en navegador" para verificar el enlace
- [ ] Boton cerrar (X) en esquina superior derecha

### D3: Descargar carpeta como ZIP
- [ ] Context menu en carpeta: "Descargar como ZIP"
- [ ] Recopilar archivos recursivamente (reusar `CollectAllFilePaths` existente)
- [ ] Validar limites: max 50 archivos, max 500MB total
- [ ] Si excede: dialog "Esta carpeta contiene X archivos (Y MB). Continuar?"
- [ ] SaveFileDialog para elegir donde guardar el ZIP
- [ ] Descargar cada archivo de R2 en paralelo (SemaphoreSlim(3))
- [ ] Crear ZIP con `System.IO.Compression.ZipArchive` (.NET 8 built-in)
- [ ] Progress: porcentaje basado en archivos completados (no bytes)
- [ ] Toast al finalizar: "ZIP descargado: Planos.zip (2.3 MB, 12 archivos)"
- [ ] Si algun archivo falla: incluir nota en toast "11 de 12 archivos descargados"

### D4: Historial de enlaces (basico)
- [ ] Filtrar drive_activity WHERE action='share' AND target_id=fileId
- [ ] En panel de detalle del archivo: seccion "Enlaces compartidos"
- [ ] Mostrar: fecha de creacion + expiracion + estado (activo/expirado)
- [ ] Estado: calcular si `created_at + expiracion < now()` → "Expirado" (rojo) / "Activo" (verde)
- [ ] Nota: R2 signed URLs no se pueden revocar; informar al usuario
- [ ] Boton "Generar nuevo enlace" si el actual expiro

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
