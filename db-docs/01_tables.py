"""Documentación completa de estructura de tablas."""
import os
import json
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

def get_tables(cur):
    cur.execute("""
        SELECT table_name,
               obj_description((quote_ident(table_schema) || '.' || quote_ident(table_name))::regclass, 'pg_class') AS comment
        FROM information_schema.tables
        WHERE table_schema = 'public' AND table_type = 'BASE TABLE'
        ORDER BY table_name;
    """)
    return cur.fetchall()

def get_columns(cur, table):
    cur.execute("""
        SELECT
            c.ordinal_position,
            c.column_name,
            c.data_type,
            c.udt_name,
            c.character_maximum_length,
            c.numeric_precision,
            c.numeric_scale,
            c.is_nullable,
            c.column_default,
            c.is_identity,
            c.identity_generation,
            col_description((quote_ident('public') || '.' || quote_ident(%s))::regclass, c.ordinal_position) AS comment
        FROM information_schema.columns c
        WHERE c.table_schema = 'public' AND c.table_name = %s
        ORDER BY c.ordinal_position;
    """, (table, table))
    return cur.fetchall()

def get_primary_keys(cur, table):
    cur.execute("""
        SELECT kcu.column_name, tc.constraint_name
        FROM information_schema.table_constraints tc
        JOIN information_schema.key_column_usage kcu
            ON tc.constraint_name = kcu.constraint_name
            AND tc.table_schema = kcu.table_schema
        WHERE tc.table_schema = 'public'
            AND tc.table_name = %s
            AND tc.constraint_type = 'PRIMARY KEY'
        ORDER BY kcu.ordinal_position;
    """, (table,))
    return cur.fetchall()

def get_foreign_keys(cur, table):
    cur.execute("""
        SELECT
            kcu.column_name AS fk_column,
            ccu.table_name AS ref_table,
            ccu.column_name AS ref_column,
            tc.constraint_name,
            rc.update_rule,
            rc.delete_rule
        FROM information_schema.table_constraints tc
        JOIN information_schema.key_column_usage kcu
            ON tc.constraint_name = kcu.constraint_name
            AND tc.table_schema = kcu.table_schema
        JOIN information_schema.constraint_column_usage ccu
            ON tc.constraint_name = ccu.constraint_name
            AND tc.table_schema = ccu.table_schema
        JOIN information_schema.referential_constraints rc
            ON tc.constraint_name = rc.constraint_name
            AND tc.table_schema = rc.constraint_schema
        WHERE tc.table_schema = 'public'
            AND tc.table_name = %s
            AND tc.constraint_type = 'FOREIGN KEY'
        ORDER BY kcu.column_name;
    """, (table,))
    return cur.fetchall()

def get_unique_constraints(cur, table):
    cur.execute("""
        SELECT tc.constraint_name,
               string_agg(kcu.column_name, ', ' ORDER BY kcu.ordinal_position) AS columns
        FROM information_schema.table_constraints tc
        JOIN information_schema.key_column_usage kcu
            ON tc.constraint_name = kcu.constraint_name
            AND tc.table_schema = kcu.table_schema
        WHERE tc.table_schema = 'public'
            AND tc.table_name = %s
            AND tc.constraint_type = 'UNIQUE'
        GROUP BY tc.constraint_name
        ORDER BY tc.constraint_name;
    """, (table,))
    return cur.fetchall()

def get_check_constraints(cur, table):
    cur.execute("""
        SELECT tc.constraint_name,
               cc.check_clause
        FROM information_schema.table_constraints tc
        JOIN information_schema.check_constraints cc
            ON tc.constraint_name = cc.constraint_name
            AND tc.constraint_schema = cc.constraint_schema
        WHERE tc.table_schema = 'public'
            AND tc.table_name = %s
            AND tc.constraint_type = 'CHECK'
            AND cc.check_clause NOT LIKE '%%IS NOT NULL'
        ORDER BY tc.constraint_name;
    """, (table,))
    return cur.fetchall()

def get_indexes(cur, table):
    cur.execute("""
        SELECT
            i.relname AS index_name,
            ix.indisunique AS is_unique,
            ix.indisprimary AS is_primary,
            pg_get_indexdef(ix.indexrelid) AS index_def
        FROM pg_index ix
        JOIN pg_class t ON t.oid = ix.indrelid
        JOIN pg_class i ON i.oid = ix.indexrelid
        JOIN pg_namespace n ON n.oid = t.relnamespace
        WHERE n.nspname = 'public' AND t.relname = %s
        ORDER BY i.relname;
    """, (table,))
    return cur.fetchall()

def get_row_count(cur, table):
    cur.execute("""
        SELECT reltuples::bigint AS estimate
        FROM pg_class
        WHERE relname = %s;
    """, (table,))
    row = cur.fetchone()
    return row[0] if row else 0

def format_type(col):
    """Formato legible del tipo de dato."""
    data_type, udt_name, char_len, num_prec, num_scale = col[2], col[3], col[4], col[5], col[6]
    if udt_name == 'uuid':
        return 'uuid'
    if udt_name == 'timestamptz':
        return 'timestamptz'
    if udt_name == 'timestamp':
        return 'timestamp'
    if udt_name == 'jsonb':
        return 'jsonb'
    if udt_name == 'json':
        return 'json'
    if udt_name == 'bool':
        return 'boolean'
    if udt_name in ('int2', 'int4', 'int8'):
        names = {'int2': 'smallint', 'int4': 'integer', 'int8': 'bigint'}
        return names[udt_name]
    if udt_name == 'float8':
        return 'double precision'
    if udt_name == 'float4':
        return 'real'
    if udt_name == 'numeric' and num_prec:
        return f'numeric({num_prec},{num_scale or 0})'
    if data_type == 'character varying' and char_len:
        return f'varchar({char_len})'
    if data_type == 'character varying':
        return 'varchar'
    if data_type == 'character' and char_len:
        return f'char({char_len})'
    if data_type == 'ARRAY':
        return f'{udt_name}[]' if udt_name.startswith('_') else f'{udt_name}[]'
    if data_type == 'USER-DEFINED':
        return udt_name
    return data_type

def main():
    conn = get_conn()
    cur = conn.cursor()

    tables = get_tables(cur)
    lines = []
    lines.append(f"# Documentación de Tablas - Base de Datos IMA Mecatrónica")
    lines.append(f"Generado: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    lines.append(f"PostgreSQL 17.4 | Supabase | {len(tables)} tablas")
    lines.append("")

    # Indice
    lines.append("## Índice")
    lines.append("")
    for i, (tname, _) in enumerate(tables, 1):
        lines.append(f"{i}. [{tname}](#{tname})")
    lines.append("")
    lines.append("---")
    lines.append("")

    for tname, tcomment in tables:
        pk_cols = get_primary_keys(cur, tname)
        fk_cols = get_foreign_keys(cur, tname)
        unique_cons = get_unique_constraints(cur, tname)
        check_cons = get_check_constraints(cur, tname)
        indexes = get_indexes(cur, tname)
        columns = get_columns(cur, tname)
        row_est = get_row_count(cur, tname)

        pk_set = {r[0] for r in pk_cols}
        fk_map = {r[0]: (r[1], r[2], r[4], r[5]) for r in fk_cols}

        lines.append(f"## {tname}")
        if tcomment:
            lines.append(f"> {tcomment}")
        lines.append(f"Filas estimadas: ~{row_est:,}")
        lines.append("")

        # Columnas
        lines.append("### Columnas")
        lines.append("")
        lines.append("| # | Columna | Tipo | Nullable | Default | PK | FK | Comentario |")
        lines.append("|---|---------|------|----------|---------|----|----|------------|")

        for col in columns:
            pos = col[0]
            name = col[1]
            tipo = format_type(col)
            nullable = "NULL" if col[7] == 'YES' else "NOT NULL"
            default = col[8] if col[8] else ""
            # Limpiar defaults largos
            if default and len(default) > 40:
                default = default[:37] + "..."
            is_pk = "PK" if name in pk_set else ""
            fk_info = ""
            if name in fk_map:
                ref_t, ref_c, _, _ = fk_map[name]
                fk_info = f"-> {ref_t}.{ref_c}"
            comment = col[11] or ""
            lines.append(f"| {pos} | `{name}` | `{tipo}` | {nullable} | {default} | {is_pk} | {fk_info} | {comment} |")

        lines.append("")

        # Primary Key
        if pk_cols:
            pk_name = pk_cols[0][1]
            pk_columns = ", ".join(r[0] for r in pk_cols)
            lines.append(f"### Primary Key")
            lines.append(f"- `{pk_name}` ({pk_columns})")
            lines.append("")

        # Foreign Keys
        if fk_cols:
            lines.append("### Foreign Keys")
            for fk_column, ref_table, ref_col, cname, upd, dele in fk_cols:
                lines.append(f"- `{cname}`: `{fk_column}` -> `{ref_table}.{ref_col}` (ON UPDATE {upd}, ON DELETE {dele})")
            lines.append("")

        # Unique Constraints
        if unique_cons:
            lines.append("### Unique Constraints")
            for cname, cols in unique_cons:
                lines.append(f"- `{cname}` ({cols})")
            lines.append("")

        # Check Constraints
        if check_cons:
            lines.append("### Check Constraints")
            for cname, clause in check_cons:
                lines.append(f"- `{cname}`: `{clause}`")
            lines.append("")

        # Indexes
        if indexes:
            lines.append("### Indexes")
            for idx_name, is_unique, is_primary, idx_def in indexes:
                flags = []
                if is_primary:
                    flags.append("PRIMARY")
                if is_unique and not is_primary:
                    flags.append("UNIQUE")
                flag_str = f" [{', '.join(flags)}]" if flags else ""
                lines.append(f"- `{idx_name}`{flag_str}: `{idx_def}`")
            lines.append("")

        lines.append("---")
        lines.append("")

    cur.close()
    conn.close()

    output = "\n".join(lines)
    out_path = os.path.join(os.path.dirname(__file__), "output", "01_tablas.md")
    os.makedirs(os.path.dirname(out_path), exist_ok=True)
    with open(out_path, "w", encoding="utf-8") as f:
        f.write(output)

    print(f"Documentacion generada: {out_path}")
    print(f"{len(tables)} tablas documentadas.")

if __name__ == "__main__":
    main()
