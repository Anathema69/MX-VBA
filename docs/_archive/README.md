# Docs Archivados

Este directorio contiene documentacion **historica congelada**. Ya no se actualiza.
No citar desde docs activos salvo referencia historica explicita.

## Contenido

### `BD-IMA/` (movido 2026-04-20)

Documentacion manual de la BD generada en enero 2026 a partir de un script SQL
de extraccion (`scripts/extract_schema.sql`, ya no usado). Archivo porque fue
**reemplazada por la fuente canonica auto-generada** en [`../../db-docs/output/`](../../db-docs/output/),
que se regenera desde Supabase en vivo con los 7 scripts Python de `db-docs/`.

Al momento de archivar, la BD tenia 33 tablas y 10 vistas segun `BD-IMA/`;
la realidad (2026-04-20) son 44 tablas y 15 vistas — por eso no vale la pena
mantenerla sincronizada manualmente.

Para consultas actuales de estructura de BD, **siempre ir a `db-docs/output/`**.
