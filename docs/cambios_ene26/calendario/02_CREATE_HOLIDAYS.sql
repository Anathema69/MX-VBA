-- ============================================
-- Módulo Calendario de Personal
-- Script 02: Tabla de Feriados y Días de Descanso
-- Fecha: 2026-01-25
-- ============================================

-- ============================================
-- 1. Tabla de Feriados
-- ============================================
CREATE TABLE IF NOT EXISTS t_holiday (
    id SERIAL PRIMARY KEY,
    holiday_date DATE NOT NULL UNIQUE,
    name VARCHAR(100) NOT NULL,
    description TEXT,
    is_mandatory BOOLEAN DEFAULT TRUE,     -- Feriado obligatorio por ley
    is_recurring BOOLEAN DEFAULT TRUE,     -- Se repite cada año
    recurring_month INTEGER,               -- Mes (1-12) si es recurrente
    recurring_day INTEGER,                 -- Día del mes si es recurrente
    recurring_rule VARCHAR(50),            -- Regla especial: 'FIRST_MONDAY_FEB', 'THIRD_MONDAY_MAR', etc.
    year INTEGER,                          -- Año específico (NULL si es recurrente)
    created_by INTEGER,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_by INTEGER,
    updated_at TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_holiday_date ON t_holiday(holiday_date);
CREATE INDEX IF NOT EXISTS idx_holiday_year ON t_holiday(EXTRACT(YEAR FROM holiday_date));

COMMENT ON TABLE t_holiday IS 'Días feriados oficiales y personalizados';
COMMENT ON COLUMN t_holiday.is_mandatory IS 'TRUE = Feriado obligatorio por ley';
COMMENT ON COLUMN t_holiday.is_recurring IS 'TRUE = Se repite cada año';
COMMENT ON COLUMN t_holiday.recurring_rule IS 'Regla para feriados móviles (ej: THIRD_MONDAY_MAR)';

-- ============================================
-- 2. Tabla de Configuración de Días de Descanso
-- ============================================
CREATE TABLE IF NOT EXISTS t_workday_config (
    id SERIAL PRIMARY KEY,
    day_of_week INTEGER NOT NULL CHECK (day_of_week BETWEEN 0 AND 6), -- 0=Domingo, 6=Sábado
    is_workday BOOLEAN DEFAULT TRUE,
    description VARCHAR(50),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP,
    UNIQUE(day_of_week)
);

COMMENT ON TABLE t_workday_config IS 'Configuración de días laborales de la semana';
COMMENT ON COLUMN t_workday_config.day_of_week IS '0=Domingo, 1=Lunes, ..., 6=Sábado';

-- ============================================
-- 3. Insertar configuración por defecto (L-V laborales, S-D descanso)
-- ============================================
INSERT INTO t_workday_config (day_of_week, is_workday, description)
VALUES
    (0, FALSE, 'Domingo'),
    (1, TRUE, 'Lunes'),
    (2, TRUE, 'Martes'),
    (3, TRUE, 'Miércoles'),
    (4, TRUE, 'Jueves'),
    (5, TRUE, 'Viernes'),
    (6, FALSE, 'Sábado')
ON CONFLICT (day_of_week) DO NOTHING;

-- ============================================
-- 4. Insertar feriados oficiales de México 2026
-- ============================================
INSERT INTO t_holiday (holiday_date, name, description, is_mandatory, is_recurring, recurring_month, recurring_day, recurring_rule)
VALUES
    -- Feriados fijos
    ('2026-01-01', 'Año Nuevo', 'Día de Año Nuevo', TRUE, TRUE, 1, 1, NULL),
    ('2026-02-05', 'Día de la Constitución', 'Aniversario de la Constitución Mexicana', TRUE, TRUE, 2, 5, NULL),
    ('2026-03-21', 'Natalicio de Benito Juárez', 'Aniversario del nacimiento de Benito Juárez', TRUE, TRUE, 3, 21, NULL),
    ('2026-05-01', 'Día del Trabajo', 'Día Internacional del Trabajo', TRUE, TRUE, 5, 1, NULL),
    ('2026-09-16', 'Día de la Independencia', 'Aniversario del Grito de Independencia', TRUE, TRUE, 9, 16, NULL),
    ('2026-11-20', 'Revolución Mexicana', 'Aniversario de la Revolución Mexicana', TRUE, TRUE, 11, 20, NULL),
    ('2026-12-25', 'Navidad', 'Día de Navidad', TRUE, TRUE, 12, 25, NULL),

    -- Feriados móviles 2026 (calculados)
    ('2026-02-02', 'Día de la Constitución (Observado)', 'Primer lunes de febrero', TRUE, FALSE, NULL, NULL, 'FIRST_MONDAY_FEB'),
    ('2026-03-16', 'Natalicio de Benito Juárez (Observado)', 'Tercer lunes de marzo', TRUE, FALSE, NULL, NULL, 'THIRD_MONDAY_MAR'),
    ('2026-11-16', 'Revolución Mexicana (Observado)', 'Tercer lunes de noviembre', TRUE, FALSE, NULL, NULL, 'THIRD_MONDAY_NOV'),

    -- Días festivos opcionales comunes
    ('2026-05-10', 'Día de las Madres', 'Día de las Madres en México', FALSE, TRUE, 5, 10, NULL),
    ('2026-12-12', 'Día de la Virgen de Guadalupe', 'Celebración religiosa', FALSE, TRUE, 12, 12, NULL),
    ('2026-12-24', 'Nochebuena', 'Víspera de Navidad', FALSE, TRUE, 12, 24, NULL),
    ('2026-12-31', 'Fin de Año', 'Víspera de Año Nuevo', FALSE, TRUE, 12, 31, NULL)
ON CONFLICT (holiday_date) DO NOTHING;

-- ============================================
-- 5. Función para generar feriados del próximo año
-- ============================================
CREATE OR REPLACE FUNCTION generate_holidays_for_year(target_year INTEGER)
RETURNS INTEGER AS $$
DECLARE
    inserted_count INTEGER := 0;
    holiday_rec RECORD;
BEGIN
    -- Copiar feriados recurrentes al nuevo año
    FOR holiday_rec IN
        SELECT name, description, is_mandatory, recurring_month, recurring_day
        FROM t_holiday
        WHERE is_recurring = TRUE
        AND recurring_month IS NOT NULL
        AND recurring_day IS NOT NULL
    LOOP
        INSERT INTO t_holiday (holiday_date, name, description, is_mandatory, is_recurring, recurring_month, recurring_day, year)
        VALUES (
            MAKE_DATE(target_year, holiday_rec.recurring_month, holiday_rec.recurring_day),
            holiday_rec.name,
            holiday_rec.description,
            holiday_rec.is_mandatory,
            FALSE,  -- No es recurrente, es instancia específica
            holiday_rec.recurring_month,
            holiday_rec.recurring_day,
            target_year
        )
        ON CONFLICT (holiday_date) DO NOTHING;

        inserted_count := inserted_count + 1;
    END LOOP;

    RETURN inserted_count;
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION generate_holidays_for_year IS 'Genera los feriados recurrentes para un año específico';

-- ============================================
-- 6. Función para verificar si una fecha es día laboral
-- ============================================
CREATE OR REPLACE FUNCTION is_workday(check_date DATE)
RETURNS BOOLEAN AS $$
DECLARE
    day_config RECORD;
    is_holiday BOOLEAN;
BEGIN
    -- Verificar si es feriado
    SELECT EXISTS(SELECT 1 FROM t_holiday WHERE holiday_date = check_date AND is_mandatory = TRUE)
    INTO is_holiday;

    IF is_holiday THEN
        RETURN FALSE;
    END IF;

    -- Verificar configuración del día de la semana
    SELECT is_workday INTO day_config.is_workday
    FROM t_workday_config
    WHERE day_of_week = EXTRACT(DOW FROM check_date);

    RETURN COALESCE(day_config.is_workday, TRUE);
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION is_workday IS 'Verifica si una fecha es día laboral (no feriado, no fin de semana)';

-- ============================================
-- 7. Verificar datos insertados
-- ============================================
SELECT
    holiday_date,
    name,
    CASE WHEN is_mandatory THEN 'Obligatorio' ELSE 'Opcional' END as tipo
FROM t_holiday
WHERE EXTRACT(YEAR FROM holiday_date) = 2026
ORDER BY holiday_date;
