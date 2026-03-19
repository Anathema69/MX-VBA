"""Fase 7: Generación de Diagrama ER en formato Mermaid."""
import os
from datetime import datetime
from dotenv import load_dotenv
import psycopg2

load_dotenv()

def get_conn():
    return psycopg2.connect(
        host=os.getenv("DB_HOST"),
        port=os.getenv("DB_PORT"),
        dbname=os.getenv("DB_NAME"),
        user=os.getenv("DB_USER"),
        password=os.getenv("DB_PASSWORD"),
    )

def main():
    conn = get_conn()
    cur = conn.cursor()

    # Tablas y sus columnas PK
    cur.execute("""
        SELECT
            t.table_name,
            kcu.column_name AS pk_column
        FROM information_schema.tables t
        LEFT JOIN information_schema.table_constraints tc
            ON tc.table_name = t.table_name AND tc.constraint_type = 'PRIMARY KEY'
            AND tc.table_schema = 'public'
        LEFT JOIN information_schema.key_column_usage kcu
            ON kcu.constraint_name = tc.constraint_name AND kcu.table_schema = 'public'
        WHERE t.table_schema = 'public' AND t.table_type = 'BASE TABLE'
        ORDER BY t.table_name;
    """)
    table_pks = {}
    for tname, pk in cur.fetchall():
        table_pks[tname] = pk

    # Todas las columnas por tabla (simplificado para ER)
    cur.execute("""
        SELECT table_name, column_name, udt_name, is_nullable
        FROM information_schema.columns
        WHERE table_schema = 'public'
            AND table_name IN (
                SELECT table_name FROM information_schema.tables
                WHERE table_schema = 'public' AND table_type = 'BASE TABLE'
            )
        ORDER BY table_name, ordinal_position;
    """)
    table_columns = {}
    for tname, col, udt, null in cur.fetchall():
        if tname not in table_columns:
            table_columns[tname] = []
        table_columns[tname].append((col, udt, null))

    # Foreign keys
    cur.execute("""
        SELECT
            tc.table_name AS source_table,
            kcu.column_name AS source_column,
            ccu.table_name AS target_table,
            ccu.column_name AS target_column
        FROM information_schema.table_constraints tc
        JOIN information_schema.key_column_usage kcu
            ON tc.constraint_name = kcu.constraint_name
            AND tc.table_schema = kcu.table_schema
        JOIN information_schema.constraint_column_usage ccu
            ON tc.constraint_name = ccu.constraint_name
            AND tc.table_schema = ccu.table_schema
        WHERE tc.table_schema = 'public'
            AND tc.constraint_type = 'FOREIGN KEY'
        ORDER BY tc.table_name;
    """)
    fks = cur.fetchall()

    # Nullable info for FK columns
    cur.execute("""
        SELECT table_name, column_name, is_nullable
        FROM information_schema.columns
        WHERE table_schema = 'public'
        ORDER BY table_name, column_name;
    """)
    col_nullable = {}
    for tname, col, null in cur.fetchall():
        col_nullable[(tname, col)] = (null == 'YES')

    lines = []
    lines.append("# Diagrama Entidad-Relación - Base de Datos IMA Mecatrónica")
    lines.append(f"Generado: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    lines.append("")

    # ---- DIAGRAMA COMPLETO ----
    lines.append("## Diagrama Completo")
    lines.append("")
    lines.append("```mermaid")
    lines.append("erDiagram")

    # Entidades con atributos (solo PK y FK para no sobrecargar)
    for tname in sorted(table_columns.keys()):
        cols = table_columns[tname]
        pk = table_pks.get(tname)
        fk_cols = {f[1] for f in fks if f[0] == tname}
        lines.append(f"    {tname} {{")
        for col, udt, null in cols:
            marker = "PK" if col == pk else ("FK" if col in fk_cols else "")
            safe_udt = udt.replace("(", "_").replace(")", "").replace(",", "_")
            lines.append(f"        {safe_udt} {col} {marker}".rstrip())
        lines.append("    }")

    lines.append("")

    # Relaciones
    for src_t, src_c, tgt_t, tgt_c in fks:
        nullable = col_nullable.get((src_t, src_c), True)
        # Mermaid ER cardinality
        if nullable:
            rel = "}o--||"  # many (optional) to one
        else:
            rel = "}|--||"  # many (required) to one
        label = src_c.replace("_id", "").replace("f_", "")
        lines.append(f"    {tgt_t} {rel} {src_t} : \"{label}\"")

    lines.append("```")
    lines.append("")

    # ---- DIAGRAMA SIMPLIFICADO (solo tablas principales) ----
    core_tables = {
        't_order', 't_client', 't_contact', 't_vendor', 't_invoice',
        't_expense', 't_payroll', 'users', 'order_status', 'invoice_status',
        't_supplier', 'order_history', 'order_gastos_operativos', 'order_gastos_indirectos',
        't_vendor_commission_payment'
    }

    lines.append("## Diagrama Simplificado (Tablas Core)")
    lines.append("")
    lines.append("```mermaid")
    lines.append("erDiagram")

    for tname in sorted(core_tables):
        if tname in table_columns:
            cols = table_columns[tname]
            pk = table_pks.get(tname)
            fk_cols = {f[1] for f in fks if f[0] == tname}
            lines.append(f"    {tname} {{")
            for col, udt, null in cols:
                marker = "PK" if col == pk else ("FK" if col in fk_cols else "")
                safe_udt = udt.replace("(", "_").replace(")", "").replace(",", "_")
                lines.append(f"        {safe_udt} {col} {marker}".rstrip())
            lines.append("    }")

    lines.append("")

    for src_t, src_c, tgt_t, tgt_c in fks:
        if src_t in core_tables and tgt_t in core_tables:
            nullable = col_nullable.get((src_t, src_c), True)
            if nullable:
                rel = "}o--||"
            else:
                rel = "}|--||"
            label = src_c.replace("_id", "").replace("f_", "")
            lines.append(f"    {tgt_t} {rel} {src_t} : \"{label}\"")

    lines.append("```")
    lines.append("")

    # ---- DIAGRAMA DE MÓDULOS ----
    lines.append("## Diagrama por Módulos Funcionales")
    lines.append("")

    modules = {
        "Ventas/Órdenes": ['t_order', 't_client', 't_contact', 't_vendor', 'order_status',
                           'order_history', 'order_gastos_operativos', 'order_gastos_indirectos',
                           't_order_deleted'],
        "Facturación": ['t_invoice', 'invoice_status', 'invoice_audit'],
        "Gastos": ['t_expense', 't_expense_audit', 't_fixed_expenses', 't_fixed_expenses_history'],
        "Nómina/RRHH": ['t_payroll', 't_payroll_history', 't_payrollovertime',
                        't_overtime_hours', 't_overtime_hours_audit',
                        't_attendance', 't_attendance_audit',
                        't_vacation', 't_vacation_audit', 't_holiday', 't_workday_config'],
        "Comisiones": ['t_vendor', 't_vendor_commission_payment', 't_commission_rate_history'],
        "Sistema": ['users', 'audit_log', 'app_versions', 't_supplier',
                    't_balance_adjustments'],
    }

    for mod_name, mod_tables in modules.items():
        lines.append(f"### {mod_name}")
        lines.append(f"Tablas: {', '.join(f'`{t}`' for t in mod_tables)}")
        lines.append("")

    cur.close()
    conn.close()

    out_path = os.path.join(os.path.dirname(__file__), "output", "07_diagrama_er.md")
    with open(out_path, "w", encoding="utf-8") as f:
        f.write("\n".join(lines))
    print(f"OK: {out_path}")
    print(f"   {len(table_columns)} entidades")
    print(f"   {len(fks)} relaciones")
    print(f"   3 diagramas: completo, simplificado, módulos")

if __name__ == "__main__":
    main()
