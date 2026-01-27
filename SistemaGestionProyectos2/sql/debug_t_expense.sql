-- Verificar estructura actual de t_expense
SELECT
    column_name,
    data_type,
    is_nullable,
    column_default
FROM information_schema.columns
WHERE table_name = 't_expense'
ORDER BY ordinal_position;
