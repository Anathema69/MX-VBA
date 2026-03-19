"""Fase 5: Documentación completa de Indexes."""
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

    # Todos los indexes con detalles
    cur.execute("""
        SELECT
            t.relname AS table_name,
            i.relname AS index_name,
            ix.indisunique AS is_unique,
            ix.indisprimary AS is_primary,
            ix.indisvalid AS is_valid,
            pg_get_indexdef(ix.indexrelid) AS index_def,
            am.amname AS index_type,
            pg_relation_size(i.oid) AS index_size_bytes,
            pg_size_pretty(pg_relation_size(i.oid)) AS index_size,
            ix.indnatts AS num_columns,
            string_agg(a.attname, ', ' ORDER BY array_position(ix.indkey, a.attnum)) AS columns,
            pg_stat_get_numscans(i.oid) AS num_scans,
            pg_stat_get_tuples_returned(i.oid) AS tuples_returned,
            pg_stat_get_tuples_fetched(i.oid) AS tuples_fetched
        FROM pg_index ix
        JOIN pg_class t ON t.oid = ix.indrelid
        JOIN pg_class i ON i.oid = ix.indexrelid
        JOIN pg_namespace n ON n.oid = t.relnamespace
        JOIN pg_am am ON am.oid = i.relam
        LEFT JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = ANY(ix.indkey)
        WHERE n.nspname = 'public'
        GROUP BY t.relname, i.relname, ix.indisunique, ix.indisprimary,
                 ix.indisvalid, ix.indexrelid, am.amname, i.oid, ix.indnatts
        ORDER BY t.relname, i.relname;
    """)
    indexes = cur.fetchall()

    # Tamaño total
    total_size = sum(r[7] for r in indexes)

    # Tablas sin índices personalizados (solo PK)
    tables_only_pk = {}
    for idx in indexes:
        tbl = idx[0]
        if tbl not in tables_only_pk:
            tables_only_pk[tbl] = {'pk': 0, 'other': 0}
        if idx[3]:  # is_primary
            tables_only_pk[tbl]['pk'] += 1
        else:
            tables_only_pk[tbl]['other'] += 1

    lines = []
    lines.append("# Documentación de Indexes - Base de Datos IMA Mecatrónica")
    lines.append(f"Generado: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    lines.append(f"Total indexes: {len(indexes)} | Tamaño total: {pg_pretty(total_size)}")
    lines.append("")

    # Resumen por tipo
    pk_count = sum(1 for i in indexes if i[3])
    unique_count = sum(1 for i in indexes if i[2] and not i[3])
    regular_count = len(indexes) - pk_count - unique_count
    btree_count = sum(1 for i in indexes if i[6] == 'btree')

    lines.append("## Resumen")
    lines.append("")
    lines.append(f"| Tipo | Cantidad |")
    lines.append(f"|------|----------|")
    lines.append(f"| PRIMARY KEY | {pk_count} |")
    lines.append(f"| UNIQUE | {unique_count} |")
    lines.append(f"| Regular | {regular_count} |")
    lines.append(f"| **Total** | **{len(indexes)}** |")
    lines.append("")
    lines.append(f"Método dominante: btree ({btree_count}/{len(indexes)})")
    lines.append("")

    # Indexes por tabla
    lines.append("## Indexes por Tabla")
    lines.append("")

    current_table = None
    for tbl, idx_name, is_uniq, is_pk, is_valid, idx_def, idx_type, size_b, size_p, ncols, cols, scans, tret, tfet in indexes:
        if tbl != current_table:
            if current_table is not None:
                lines.append("")
            lines.append(f"### {tbl}")
            lines.append("")
            lines.append("| Index | Tipo | Columnas | Método | Tamaño | Scans | Valid |")
            lines.append("|-------|------|----------|--------|--------|-------|-------|")
            current_table = tbl

        tipo = "PK" if is_pk else ("UNIQUE" if is_uniq else "INDEX")
        valid = "OK" if is_valid else "INVALID"
        scans_str = f"{scans:,}" if scans else "0"
        lines.append(f"| `{idx_name}` | {tipo} | {cols} | {idx_type} | {size_p} | {scans_str} | {valid} |")

    lines.append("")

    # Top 10 indexes más usados
    lines.append("## Top 10 Indexes Más Usados")
    lines.append("")
    used = sorted([i for i in indexes if i[11] and i[11] > 0], key=lambda x: x[11], reverse=True)[:10]
    if used:
        lines.append("| # | Tabla | Index | Scans | Tuples Returned |")
        lines.append("|---|-------|-------|-------|-----------------|")
        for i, idx in enumerate(used, 1):
            lines.append(f"| {i} | `{idx[0]}` | `{idx[1]}` | {idx[11]:,} | {idx[12]:,} |")
    else:
        lines.append("No hay estadísticas de uso disponibles (pg_stat necesita actividad).")
    lines.append("")

    # Indexes no usados (posibles candidatos a eliminar)
    lines.append("## Indexes Sin Uso (posibles candidatos a revisión)")
    lines.append("> Indexes con 0 scans que no son PK ni UNIQUE")
    lines.append("")
    unused = [i for i in indexes if (i[11] is None or i[11] == 0) and not i[3] and not i[2]]
    if unused:
        lines.append("| Tabla | Index | Columnas | Tamaño |")
        lines.append("|-------|-------|----------|--------|")
        for idx in unused:
            lines.append(f"| `{idx[0]}` | `{idx[1]}` | {idx[10]} | {idx[8]} |")
    else:
        lines.append("Todos los indexes regulares tienen uso registrado.")
    lines.append("")

    # Top 10 indexes más grandes
    lines.append("## Top 10 Indexes por Tamaño")
    lines.append("")
    biggest = sorted(indexes, key=lambda x: x[7], reverse=True)[:10]
    lines.append("| # | Tabla | Index | Tamaño | Tipo |")
    lines.append("|---|-------|-------|--------|------|")
    for i, idx in enumerate(biggest, 1):
        tipo = "PK" if idx[3] else ("UNIQUE" if idx[2] else "INDEX")
        lines.append(f"| {i} | `{idx[0]}` | `{idx[1]}` | {idx[8]} | {tipo} |")
    lines.append("")

    # Definiciones completas
    lines.append("## Definiciones Completas")
    lines.append("")
    for tbl, idx_name, _, is_pk, _, idx_def, _, _, _, _, _, _, _, _ in indexes:
        if not is_pk:  # Skip PKs ya que son obvios
            lines.append(f"- `{idx_name}`: `{idx_def}`")
    lines.append("")

    cur.close()
    conn.close()

    out_path = os.path.join(os.path.dirname(__file__), "output", "05_indexes.md")
    with open(out_path, "w", encoding="utf-8") as f:
        f.write("\n".join(lines))
    print(f"OK: {out_path}")
    print(f"   {len(indexes)} indexes ({pk_count} PK, {unique_count} UNIQUE, {regular_count} regular)")
    print(f"   Tamaño total: {pg_pretty(total_size)}")
    print(f"   {len(unused)} indexes sin uso")

def pg_pretty(bytes_val):
    if bytes_val < 1024:
        return f"{bytes_val} bytes"
    elif bytes_val < 1024 * 1024:
        return f"{bytes_val / 1024:.1f} kB"
    else:
        return f"{bytes_val / (1024*1024):.1f} MB"

if __name__ == "__main__":
    main()
