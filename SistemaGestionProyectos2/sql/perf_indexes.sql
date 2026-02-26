-- ============================================================
-- Performance: Database indexes for common query patterns
-- Run this on Supabase SQL Editor
-- ============================================================

-- Orders by date (most common sort)
CREATE INDEX IF NOT EXISTS idx_order_podate
    ON t_order(f_podate DESC);

-- Orders filtered by status + date (OrdersManagement filter)
CREATE INDEX IF NOT EXISTS idx_order_status_podate
    ON t_order(f_orderstat, f_podate DESC);

-- Pending expenses (SupplierPending view)
CREATE INDEX IF NOT EXISTS idx_expense_status_scheduled
    ON t_expense(f_status, f_scheduleddate)
    WHERE f_status = 'PENDIENTE';

-- Invoices by order (InvoiceManagement, PendingIncomes)
CREATE INDEX IF NOT EXISTS idx_invoice_order
    ON t_invoice(f_order);

-- Active payroll employees (PayrollManagement)
CREATE INDEX IF NOT EXISTS idx_payroll_active
    ON t_payroll(is_active)
    WHERE is_active = true;

-- Active contacts by client (ClientManagement)
CREATE INDEX IF NOT EXISTS idx_contact_client_active
    ON t_contact(f_client)
    WHERE is_active = true;

-- Fixed expenses history lookup (eliminates 380K seq scans in balance view)
CREATE INDEX IF NOT EXISTS idx_fixed_expenses_history_lookup
    ON t_fixed_expenses_history(expense_id, effective_date DESC)
    WHERE change_type NOT IN ('DEACTIVATED', 'DELETED');

-- Users active lookup (reduces 21K seq scans on login/user queries)
CREATE INDEX IF NOT EXISTS idx_users_active
    ON users(is_active)
    WHERE is_active = true;
