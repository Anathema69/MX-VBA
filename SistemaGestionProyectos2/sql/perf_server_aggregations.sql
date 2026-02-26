-- ============================================================
-- Performance: Server-side aggregation functions
-- Replaces client-side C# filtering with SQL aggregations
-- ============================================================

-- 1. Expense stats by status (replaces GetExpensesStatsByStatus client-side grouping)
CREATE OR REPLACE FUNCTION get_expense_stats_by_status()
RETURNS TABLE(status TEXT, total NUMERIC)
LANGUAGE sql STABLE
AS $$
    SELECT f_status, COALESCE(SUM(f_totalexpense), 0)
    FROM t_expense
    GROUP BY f_status;
$$;

-- 2. Monthly payroll total (replaces GetMonthlyPayrollTotal client-side sum)
CREATE OR REPLACE FUNCTION get_monthly_payroll_total()
RETURNS NUMERIC
LANGUAGE sql STABLE
AS $$
    SELECT COALESCE(SUM(f_monthlypayroll), 0)
    FROM t_payroll
    WHERE is_active = true;
$$;

-- 3. Expense statistics (replaces GetExpenseStatistics 6 client-side aggregations)
CREATE OR REPLACE FUNCTION get_expense_statistics()
RETURNS JSON
LANGUAGE sql STABLE
AS $$
    SELECT json_build_object(
        'TotalExpenses', COALESCE(SUM(f_totalexpense), 0),
        'PendingExpenses', COALESCE(SUM(CASE WHEN f_status = 'PENDIENTE' THEN f_totalexpense ELSE 0 END), 0),
        'PaidExpenses', COALESCE(SUM(CASE WHEN f_status = 'PAGADO' THEN f_totalexpense ELSE 0 END), 0),
        'OverdueExpenses', COALESCE(SUM(CASE WHEN f_status = 'PENDIENTE' AND f_scheduleddate < NOW() THEN f_totalexpense ELSE 0 END), 0),
        'ExpenseCount', COUNT(*),
        'AverageExpense', COALESCE(AVG(f_totalexpense), 0)
    )
    FROM t_expense;
$$;
