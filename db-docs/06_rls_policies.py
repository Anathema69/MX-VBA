"""Fase 6: Documentación de Row Level Security (RLS) Policies."""
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

    # Tablas con RLS habilitado
    cur.execute("""
        SELECT
            c.relname AS table_name,
            c.relrowsecurity AS rls_enabled,
            c.relforcerowsecurity AS rls_forced
        FROM pg_class c
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = 'public' AND c.relkind = 'r'
        ORDER BY c.relname;
    """)
    tables = cur.fetchall()

    # Policies
    cur.execute("""
        SELECT
            pol.polname AS policy_name,
            c.relname AS table_name,
            CASE pol.polcmd
                WHEN 'r' THEN 'SELECT'
                WHEN 'a' THEN 'INSERT'
                WHEN 'w' THEN 'UPDATE'
                WHEN 'd' THEN 'DELETE'
                WHEN '*' THEN 'ALL'
            END AS command,
            CASE pol.polpermissive
                WHEN true THEN 'PERMISSIVE'
                ELSE 'RESTRICTIVE'
            END AS type,
            pg_get_expr(pol.polqual, pol.polrelid) AS using_expr,
            pg_get_expr(pol.polwithcheck, pol.polrelid) AS with_check_expr,
            ARRAY(
                SELECT rolname FROM pg_roles
                WHERE oid = ANY(pol.polroles)
            ) AS roles
        FROM pg_policy pol
        JOIN pg_class c ON c.oid = pol.polrelid
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = 'public'
        ORDER BY c.relname, pol.polname;
    """)
    policies = cur.fetchall()

    lines = []
    lines.append("# Row Level Security (RLS) - Base de Datos IMA Mecatrónica")
    lines.append(f"Generado: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    lines.append("")

    # Resumen RLS por tabla
    rls_on = [t for t in tables if t[1]]
    rls_off = [t for t in tables if not t[1]]

    lines.append("## Estado de RLS por Tabla")
    lines.append("")
    lines.append(f"RLS Habilitado: {len(rls_on)} tablas | RLS Deshabilitado: {len(rls_off)} tablas")
    lines.append(f"Total policies: {len(policies)}")
    lines.append("")

    lines.append("| Tabla | RLS | Forced | # Policies |")
    lines.append("|-------|-----|--------|------------|")
    for tname, rls_en, rls_force in tables:
        pol_count = sum(1 for p in policies if p[1] == tname)
        rls_str = "ON" if rls_en else "OFF"
        force_str = "SI" if rls_force else "NO"
        lines.append(f"| `{tname}` | {rls_str} | {force_str} | {pol_count} |")
    lines.append("")

    # Detalle de policies
    if policies:
        lines.append("## Detalle de Policies")
        lines.append("")

        policies_by_table = {}
        for p in policies:
            tbl = p[1]
            if tbl not in policies_by_table:
                policies_by_table[tbl] = []
            policies_by_table[tbl].append(p)

        for tbl in sorted(policies_by_table.keys()):
            pols = policies_by_table[tbl]
            lines.append(f"### {tbl}")
            lines.append("")
            for pname, _, cmd, ptype, using_e, check_e, roles in pols:
                roles_str = ", ".join(roles) if roles else "PUBLIC"
                lines.append(f"#### `{pname}`")
                lines.append(f"- **Comando**: {cmd}")
                lines.append(f"- **Tipo**: {ptype}")
                lines.append(f"- **Roles**: {roles_str}")
                if using_e:
                    lines.append(f"- **USING**: `{using_e}`")
                if check_e:
                    lines.append(f"- **WITH CHECK**: `{check_e}`")
                lines.append("")
            lines.append("---")
            lines.append("")
    else:
        lines.append("## Policies")
        lines.append("")
        lines.append("No hay policies RLS definidas en el esquema public.")
        lines.append("")

    # Tablas sin RLS que podrían necesitarlo
    lines.append("## Análisis de Seguridad")
    lines.append("")
    sensitive_prefixes = ('users', 'audit_log', 't_payroll', 't_expense', 't_vendor_commission')
    sensitive_no_rls = [t[0] for t in rls_off if any(t[0].startswith(p) for p in sensitive_prefixes)]
    if sensitive_no_rls:
        lines.append("### Tablas sensibles sin RLS")
        lines.append("> Estas tablas podrían beneficiarse de RLS basado en su contenido")
        lines.append("")
        for t in sensitive_no_rls:
            lines.append(f"- `{t}`")
        lines.append("")
    else:
        lines.append("Todas las tablas sensibles tienen RLS habilitado.")
        lines.append("")

    cur.close()
    conn.close()

    out_path = os.path.join(os.path.dirname(__file__), "output", "06_rls_policies.md")
    with open(out_path, "w", encoding="utf-8") as f:
        f.write("\n".join(lines))
    print(f"OK: {out_path}")
    print(f"   {len(rls_on)} tablas con RLS ON, {len(rls_off)} con RLS OFF")
    print(f"   {len(policies)} policies definidas")

if __name__ == "__main__":
    main()
