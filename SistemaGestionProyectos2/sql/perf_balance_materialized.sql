-- ============================================================
-- Performance: Materialized View for Balance
-- Replaces v_balance_completo (expensive view that recalculates everything)
-- Run this on Supabase SQL Editor
-- ============================================================

-- Step 1: Create materialized view from existing view
CREATE MATERIALIZED VIEW IF NOT EXISTS mv_balance_completo AS
    SELECT * FROM v_balance_completo;

-- Step 2: Create unique index for concurrent refresh
CREATE UNIQUE INDEX IF NOT EXISTS idx_mv_balance_pk
    ON mv_balance_completo(año, mes_numero);

-- Step 3: Function to refresh the materialized view
CREATE OR REPLACE FUNCTION refresh_balance_completo()
RETURNS void
LANGUAGE plpgsql
SECURITY DEFINER
AS $$
BEGIN
    REFRESH MATERIALIZED VIEW CONCURRENTLY mv_balance_completo;
END;
$$;

-- Step 4: Grant access
GRANT SELECT ON mv_balance_completo TO anon, authenticated;
GRANT EXECUTE ON FUNCTION refresh_balance_completo() TO authenticated;

-- USAGE NOTES:
-- 1. The app should query mv_balance_completo instead of v_balance_completo
-- 2. Call refresh_balance_completo() via RPC when the "Actualizar Datos" button is clicked
-- 3. The materialized view is refreshed CONCURRENTLY so it doesn't block reads
-- 4. Consider adding a cron job to refresh every 30 minutes if needed
