# Fase V3-B: Recientes & Actividad

**Estado:** PENDIENTE
**Prioridad:** 3
**Archivos clave:** `DriveV2Window.xaml.cs`, SQL nuevo, `DriveService.cs`

---

## Checklist de implementacion

### B1: Seccion "Recientes" en sidebar
- [ ] Agregar seccion en sidebar (debajo de navegacion, encima de storage)
- [ ] Header "Recientes" con icono de reloj
- [ ] Query: `drive_files ORDER BY uploaded_at DESC LIMIT 15`
- [ ] Cada item: icono tipo + nombre (truncado 25 chars) + fecha relativa
- [ ] Fecha relativa: "hace 5 min", "hace 2h", "ayer", "3 dias", ">7d: dd/MM"
- [ ] Clic: navegar a la carpeta contenedora + seleccionar archivo + abrir detalle
- [ ] Actualizar lista al subir un archivo nuevo (sin re-query, insertar al inicio)
- [ ] Tooltip: nombre completo + carpeta + tamano

### B2: Tabla drive_activity + insercion desde servicio
- [ ] Crear script SQL: `sql/drive_v3_activity.sql`
- [ ] CREATE TABLE drive_activity (ver README.md para esquema)
- [ ] CREATE INDEX idx_drive_activity_user
- [ ] CREATE INDEX idx_drive_activity_folder
- [ ] CREATE INDEX idx_drive_activity_recent
- [ ] Crear modelo `Models/Database/DriveActivityDb.cs`
- [ ] En DriveService: metodo `LogActivity(userId, action, targetType, targetId, targetName, folderId, metadata)`
- [ ] Insertar actividad en: Upload, Download, Rename, Delete, Move, Share
- [ ] No insertar en: navegacion de carpetas (demasiado ruido)

### B3: Feed de actividad en sidebar
- [ ] Seccion "Actividad" colapsable (debajo de Recientes)
- [ ] Toggle chevron para expandir/colapsar
- [ ] Query: `drive_activity ORDER BY created_at DESC LIMIT 10`
- [ ] Formato: iniciales del usuario (circulo 24px) + "Juan subio contrato.pdf" + "hace 30 min"
- [ ] Acciones legibles: "subio", "descargo", "renombro", "elimino", "movio", "compartio"
- [ ] Clic en item: navegar al archivo/carpeta (si existe)
- [ ] Items de archivos eliminados: texto tachado, sin navegacion
- [ ] Solo visible para: direccion, administracion, coordinacion

### B4: "Mis recientes" personales
- [ ] Registrar acciones 'download' y 'view' en drive_activity para el usuario actual
- [ ] Toggle en sidebar: "Mis recientes" (default) / "Todos"
- [ ] "Mis recientes": filtrar drive_activity WHERE user_id = current_user AND action IN ('download', 'view', 'upload')
- [ ] "Todos": mostrar todo (como B1)
- [ ] Guardar preferencia del toggle en memoria de sesion (no persistir)

---

## SQL

```sql
-- sql/drive_v3_activity.sql

CREATE TABLE IF NOT EXISTS drive_activity (
    id SERIAL PRIMARY KEY,
    user_id INTEGER REFERENCES users(id),
    action VARCHAR(20) NOT NULL,
    target_type VARCHAR(10) NOT NULL,
    target_id INTEGER NOT NULL,
    target_name VARCHAR(255),
    folder_id INTEGER REFERENCES drive_folders(id) ON DELETE SET NULL,
    metadata JSONB,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_drive_activity_user ON drive_activity(user_id, created_at DESC);
CREATE INDEX idx_drive_activity_folder ON drive_activity(folder_id, created_at DESC);
CREATE INDEX idx_drive_activity_recent ON drive_activity(created_at DESC);

-- Cleanup: eliminar actividad >90 dias (ejecutar periodicamente)
-- DELETE FROM drive_activity WHERE created_at < NOW() - INTERVAL '90 days';
```

## Modelo

```csharp
[Table("drive_activity")]
public class DriveActivityDb : BaseModel
{
    [PrimaryKey("id")] public int Id { get; set; }
    [Column("user_id")] public int? UserId { get; set; }
    [Column("action")] public string Action { get; set; }
    [Column("target_type")] public string TargetType { get; set; }
    [Column("target_id")] public int TargetId { get; set; }
    [Column("target_name")] public string TargetName { get; set; }
    [Column("folder_id")] public int? FolderId { get; set; }
    [Column("metadata")] public string Metadata { get; set; }
    [Column("created_at")] public DateTime? CreatedAt { get; set; }
}
```
