# Documentación de Base de Datos - IMA Mecatrónica

## Propósito

Este directorio contiene la documentación completa del esquema de base de datos de producción en Supabase. Esta documentación es **prerequisito** antes de implementar la extensión v2.0.

---

## Proceso de Extracción

### Paso 1: Ejecutar scripts en Supabase

1. Ir a **Supabase Dashboard** → **SQL Editor**
2. Abrir el archivo `scripts/extract_schema.sql`
3. Ejecutar **cada sección por separado** (están numeradas)
4. Copiar los resultados

### Paso 2: Documentar resultados

Los resultados se organizan en los siguientes archivos:

| Sección del Script | Archivo de Documentación |
|--------------------|--------------------------|
| 1. Lista de tablas | `01_TABLAS.md` |
| 2. Columnas | `01_TABLAS.md` |
| 3. Foreign Keys | `02_RELACIONES_FK.md` |
| 4. Índices | `04_VISTAS_INDICES.md` |
| 5-7. Triggers/Funciones | `03_FUNCIONES_TRIGGERS.md` |
| 8. Vistas | `04_VISTAS_INDICES.md` |
| 9-10. Enums/Constraints | `01_TABLAS.md` |
| 11-12. Stats/Secuencias | `00_RESUMEN_ESQUEMA.md` |

---

## Estructura de Archivos

```
docs/BD-IMA/
├── README.md                       # Este archivo
├── 00_RESUMEN_ESQUEMA.md          # Vista general, estadísticas
├── 01_TABLAS.md                   # Todas las tablas con columnas
├── 02_RELACIONES_FK.md            # Foreign keys y diagrama
├── 03_FUNCIONES_TRIGGERS.md       # Lógica de BD
├── 04_VISTAS_INDICES.md           # Vistas e índices
└── scripts/
    └── extract_schema.sql         # Script de extracción
```

---

## Checklist de Documentación

- [x] Ejecutar Sección 1: Lista de tablas
- [x] Ejecutar Sección 2: Columnas por tabla
- [x] Ejecutar Sección 3: Foreign keys
- [x] Ejecutar Sección 4: Índices
- [x] Ejecutar Sección 5: Triggers
- [x] Ejecutar Sección 6-7: Funciones
- [x] Ejecutar Sección 8: Vistas
- [x] Ejecutar Sección 9-10: Enums y constraints (Sección 9 vacía - no hay enums)
- [x] Ejecutar Sección 11: Conteo de registros
- [x] Ejecutar Sección 12: Secuencias
- [x] Crear documento de resumen (00_RESUMEN_ESQUEMA.md)
- [x] Validar vs documentación existente
- [x] Identificar discrepancias (ANALISIS_BRECHAS.md)

---

## Siguiente Paso

COMPLETADO. Se crearon los siguientes documentos:

```
docs/cambios_ene26/MIGRACION_v2.sql        # Script SQL consolidado
docs/cambios_ene26/ANALISIS_BRECHAS.md     # Análisis de brechas
docs/cambios_ene26/CONSULTA_CLIENTE_FORMULAS.md  # Preguntas pendientes
```

### Orden de ejecución:
1. Revisar `CONSULTA_CLIENTE_FORMULAS.md` con el cliente
2. Ejecutar `MIGRACION_v2.sql` en ambiente de prueba
3. Validar que la aplicación funciona
4. Ejecutar en producción
5. Comenzar desarrollo de código por fases

---

## Fecha de Última Actualización

**19 de Enero de 2026** - Extracción completa del esquema de Supabase
