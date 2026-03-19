"""Fase 3: Documentación de Vistas."""
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

    # Vistas regulares
    cur.execute("""
        SELECT
            v.table_name AS view_name,
            v.view_definition,
            obj_description((quote_ident('public') || '.' || quote_ident(v.table_name))::regclass, 'pg_class') AS comment
        FROM information_schema.views v
        WHERE v.table_schema = 'public'
        ORDER BY v.table_name;
    """)
    views = cur.fetchall()

    # Columnas de cada vista
    def get_view_columns(view_name):
        cur.execute("""
            SELECT
                c.ordinal_position,
                c.column_name,
                c.data_type,
                c.udt_name,
                c.is_nullable
            FROM information_schema.columns c
            WHERE c.table_schema = 'public' AND c.table_name = %s
            ORDER BY c.ordinal_position;
        """, (view_name,))
        return cur.fetchall()

    # Vistas materializadas
    cur.execute("""
        SELECT
            c.relname AS matview_name,
            pg_get_viewdef(c.oid, true) AS definition,
            obj_description(c.oid, 'pg_class') AS comment
        FROM pg_class c
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = 'public' AND c.relkind = 'm'
        ORDER BY c.relname;
    """)
    matviews = cur.fetchall()

    # Columnas de vistas materializadas
    def get_matview_columns(mv_name):
        cur.execute("""
            SELECT
                a.attnum AS position,
                a.attname AS column_name,
                pg_catalog.format_type(a.atttypid, a.atttypmod) AS data_type,
                NOT a.attnotnull AS is_nullable
            FROM pg_attribute a
            JOIN pg_class c ON c.oid = a.attrelid
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = 'public' AND c.relname = %s
                AND a.attnum > 0 AND NOT a.attisdropped
            ORDER BY a.attnum;
        """, (mv_name,))
        return cur.fetchall()

    lines = []
    lines.append("# Documentación de Vistas - Base de Datos IMA Mecatrónica")
    lines.append(f"Generado: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    lines.append(f"Vistas regulares: {len(views)} | Vistas materializadas: {len(matviews)}")
    lines.append("")

    # Índice
    lines.append("## Índice")
    lines.append("")
    lines.append("### Vistas Regulares")
    for i, (name, _, _) in enumerate(views, 1):
        lines.append(f"{i}. [{name}](#{name})")
    lines.append("")
    if matviews:
        lines.append("### Vistas Materializadas")
        for i, (name, _, _) in enumerate(matviews, 1):
            lines.append(f"{i}. [{name}](#{name})")
        lines.append("")
    lines.append("---")
    lines.append("")

    # Vistas regulares
    for vname, vdef, vcomment in views:
        cols = get_view_columns(vname)
        lines.append(f"## {vname}")
        if vcomment:
            lines.append(f"> {vcomment}")
        lines.append(f"Tipo: Vista regular | Columnas: {len(cols)}")
        lines.append("")

        lines.append("### Columnas")
        lines.append("")
        lines.append("| # | Columna | Tipo | Nullable |")
        lines.append("|---|---------|------|----------|")
        for pos, col, dtype, udt, null in cols:
            tipo = udt if udt in ('uuid', 'timestamptz', 'jsonb', 'bool', 'int4', 'int8', 'float8', 'numeric', 'text', 'date') else dtype
            lines.append(f"| {pos} | `{col}` | `{tipo}` | {'NULL' if null == 'YES' else 'NOT NULL'} |")
        lines.append("")

        lines.append("### Definición SQL")
        lines.append("")
        lines.append("```sql")
        lines.append(vdef.strip() if vdef else "-- Definición no disponible")
        lines.append("```")
        lines.append("")

        # Tablas referenciadas (extraer de la definición)
        if vdef:
            tables_ref = set()
            for word in vdef.split():
                word_clean = word.strip('(),;').lower()
                if word_clean.startswith('t_') or word_clean.startswith('order_') or word_clean.startswith('invoice_') or word_clean == 'users':
                    tables_ref.add(word_clean)
            if tables_ref:
                lines.append("### Tablas Referenciadas")
                for t in sorted(tables_ref):
                    lines.append(f"- `{t}`")
                lines.append("")

        lines.append("---")
        lines.append("")

    # Vistas materializadas
    if matviews:
        lines.append("# Vistas Materializadas")
        lines.append("")
        for mvname, mvdef, mvcomment in matviews:
            cols = get_matview_columns(mvname)
            lines.append(f"## {mvname}")
            if mvcomment:
                lines.append(f"> {mvcomment}")
            lines.append(f"Tipo: Vista materializada | Columnas: {len(cols)}")
            lines.append("")

            lines.append("### Columnas")
            lines.append("")
            lines.append("| # | Columna | Tipo | Nullable |")
            lines.append("|---|---------|------|----------|")
            for pos, col, dtype, is_null in cols:
                lines.append(f"| {pos} | `{col}` | `{dtype}` | {'NULL' if is_null else 'NOT NULL'} |")
            lines.append("")

            lines.append("### Definición SQL")
            lines.append("")
            lines.append("```sql")
            lines.append(mvdef.strip() if mvdef else "-- Definición no disponible")
            lines.append("```")
            lines.append("")

            # Indexes de la matview
            cur.execute("""
                SELECT i.relname, pg_get_indexdef(ix.indexrelid)
                FROM pg_index ix
                JOIN pg_class t ON t.oid = ix.indrelid
                JOIN pg_class i ON i.oid = ix.indexrelid
                JOIN pg_namespace n ON n.oid = t.relnamespace
                WHERE n.nspname = 'public' AND t.relname = %s
                ORDER BY i.relname;
            """, (mvname,))
            mv_indexes = cur.fetchall()
            if mv_indexes:
                lines.append("### Indexes")
                for idx_name, idx_def in mv_indexes:
                    lines.append(f"- `{idx_name}`: `{idx_def}`")
                lines.append("")

            lines.append("---")
            lines.append("")

    cur.close()
    conn.close()

    out_path = os.path.join(os.path.dirname(__file__), "output", "03_vistas.md")
    with open(out_path, "w", encoding="utf-8") as f:
        f.write("\n".join(lines))
    print(f"OK: {out_path}")
    print(f"   {len(views)} vistas regulares")
    print(f"   {len(matviews)} vistas materializadas")

if __name__ == "__main__":
    main()
