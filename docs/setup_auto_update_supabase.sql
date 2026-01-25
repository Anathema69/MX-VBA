-- ============================================================
-- SCRIPT: Insertar nueva versión en app_versions
-- ============================================================
-- INSTRUCCIONES:
--   1. Subir el instalador a Supabase Storage ANTES de ejecutar
--      Ruta: app-installers/releases/v{VERSION}/SistemaGestionProyectos-v{VERSION}-Setup.exe
--   2. Modificar SOLO la sección "CONFIGURACIÓN" abajo
--   3. Ejecutar en Supabase SQL Editor
--   4. Verificar con el SELECT final
-- ============================================================


-- ============================================================
-- CONFIGURACIÓN DE LA NUEVA VERSIÓN
-- ============================================================
-- >> MODIFICAR SOLO ESTOS VALORES <<

DO $$
DECLARE
    -- ┌────────────────────────────────────────────────────────┐
    -- │                    DATOS BÁSICOS                       │
    -- └────────────────────────────────────────────────────────┘
    v_version       VARCHAR := '2.0.0';
    v_created_by    VARCHAR := 'Zuri Dev';
    v_file_size_mb  NUMERIC := 50.00;  -- Tamaño real del instalador (actualizar después de compilar)
    v_is_mandatory  BOOLEAN := true;   -- OBLIGATORIA: cambios en roles de BD
    v_min_version   VARCHAR := NULL;   -- NULL = cualquier versión puede actualizar

    -- ┌────────────────────────────────────────────────────────┐
    -- │                   RELEASE NOTES                        │
    -- └────────────────────────────────────────────────────────┘
    v_release_notes TEXT := 'Versión 2.0.0 - Actualización Mayor

⚠️ ACTUALIZACIÓN OBLIGATORIA
Esta versión incluye cambios en los roles de la base de datos.
Si no actualiza, no podrá iniciar sesión correctamente.

═══════════════════════════════════════════════════════════════
NUEVO MÓDULO: GESTIÓN DE USUARIOS
═══════════════════════════════════════════════════════════════

• Panel completo de administración de usuarios
• Crear, editar y desactivar cuentas de usuario
• Asignación de roles y permisos
• Visualización de estado de cuenta (activo/inactivo)
• Interfaz moderna con diseño minimalista

═══════════════════════════════════════════════════════════════
MÓDULO CALENDARIO DE PERSONAL v1.3
═══════════════════════════════════════════════════════════════

• Visualización de calendario mensual de personal
• Asignación de turnos y horarios
• Gestión de días libres y vacaciones
• Vista consolidada por empleado
• Exportación de datos

═══════════════════════════════════════════════════════════════
MÓDULO BALANCE v2.0 - REDISEÑO COMPLETO
═══════════════════════════════════════════════════════════════

DISEÑO VISUAL MINIMALISTA:
• Paleta de colores monocromática profesional
• Headers de sección con línea de acento colorida:
  - Gastos (Rosa), Ingresos (Verde), Ventas (Amarillo), Resultado (Azul)
• Columna Total Anual destacada con fondo azul claro
• Selector de año mejorado (más grande y visible)

SEMÁFORO DE VENTAS:
• Indicador visual dot + texto con color del semáforo
• Colores: Rojo (bajo), Amarillo (medio), Verde (meta alcanzada)
• Tooltip con detalles de umbrales y diferencias
• Leyenda integrada en la sección de ventas

RESALTADO MES ACTUAL:
• Header del mes con fondo azul e indicador dot
• Celdas del mes actual con fondo azul sutil

FILA DE UTILIDAD MEJORADA:
• Flecha indicadora (▲/▼) según valor positivo/negativo
• Colores dinámicos: verde (ganancia) / rojo (pérdida)
• KPI de Utilidad con borde dinámico según estado

EDICIÓN DE HORAS EXTRA:
• Click selecciona todo el contenido
• Solo entrada numérica (dígitos y punto decimal)
• Máximo 2 decimales, autocompletado (.00)
• Enter guarda, Escape cancela
• Protección contra texto pegado inválido

═══════════════════════════════════════════════════════════════
MEJORAS EN MENÚ PRINCIPAL Y PERMISOS
═══════════════════════════════════════════════════════════════

• Interfaz de menú principal rediseñada
• Sistema de permisos por rol mejorado
• Visibilidad de opciones según rol del usuario
• Mejor organización de módulos

═══════════════════════════════════════════════════════════════
GESTIÓN DE ÓRDENES - FILTROS PERSISTENTES
═══════════════════════════════════════════════════════════════

• Filtro de órdenes persistente para rol administración
• Se recuerda el último filtro aplicado al volver al módulo
• Mejora en la experiencia de navegación';

    -- ════════════════════════════════════════════════════════
    -- NO MODIFICAR DEBAJO DE ESTA LÍNEA
    -- ════════════════════════════════════════════════════════
    v_download_url TEXT;
    v_changelog JSONB;
BEGIN
    -- Construir URL del instalador (patrón estándar)
    v_download_url := 'https://wjozxqldvypdtfmkamud.supabase.co/storage/v1/object/public/app-installers/releases/v'
                      || v_version || '/SistemaGestionProyectos-v' || v_version || '-Setup.exe';

    -- Changelog estructurado (opcional, para futuras implementaciones)
    v_changelog := '{
        "Added": [
            "Módulo Gestión de Usuarios completo",
            "Módulo Calendario de Personal v1.3",
            "Semáforo de ventas con indicadores visuales",
            "Resaltado de mes actual en Balance",
            "Fila de Utilidad con flechas indicadoras",
            "KPI de Utilidad con estilo dinámico",
            "Edición robusta de Horas Extra",
            "Filtros persistentes en Órdenes para rol administración"
        ],
        "Changed": [
            "Rediseño completo del módulo Balance (v2.0)",
            "Mejoras en UI del menú principal",
            "Sistema de permisos por rol mejorado",
            "Paleta de colores monocromática profesional"
        ],
        "Fixed": [
            "Corrección en visibilidad de opciones por rol",
            "Mejora en navegación entre módulos"
        ],
        "Security": [
            "Actualización de roles en base de datos (OBLIGATORIO)"
        ]
    }'::jsonb;

    -- ════════════════════════════════════════════════════════
    -- PASO 1: Marcar versiones anteriores como NO latest
    -- ════════════════════════════════════════════════════════
    UPDATE app_versions
    SET is_latest = false
    WHERE is_latest = true;

    RAISE NOTICE '✓ Versiones anteriores marcadas como is_latest = false';

    -- ════════════════════════════════════════════════════════
    -- PASO 2: Insertar nueva versión
    -- ════════════════════════════════════════════════════════
    INSERT INTO app_versions (
        version,
        release_date,
        is_latest,
        is_mandatory,
        download_url,
        file_size_mb,
        release_notes,
        min_version,
        created_by,
        is_active,
        downloads_count,
        changelog
    ) VALUES (
        v_version,
        NOW(),
        true,
        v_is_mandatory,
        v_download_url,
        v_file_size_mb,
        v_release_notes,
        v_min_version,
        v_created_by,
        true,
        0,
        v_changelog
    );

    RAISE NOTICE '✓ Nueva versión % insertada correctamente', v_version;
    RAISE NOTICE '✓ URL: %', v_download_url;
    RAISE NOTICE '✓ Tamaño: % MB', v_file_size_mb;
    RAISE NOTICE '⚠️ ACTUALIZACIÓN OBLIGATORIA: %', v_is_mandatory;
END $$;


-- ============================================================
-- VERIFICACIÓN: Mostrar últimas versiones
-- ============================================================
SELECT
    id,
    version,
    is_latest,
    is_mandatory,
    is_active,
    release_date::date as fecha,
    file_size_mb,
    downloads_count
FROM app_versions
ORDER BY id DESC
LIMIT 5;
