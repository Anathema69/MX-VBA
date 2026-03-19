"""Fase 2: Documentación de relaciones (Foreign Keys) entre tablas."""
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

    # Todas las FK
    cur.execute("""
        SELECT
            tc.constraint_name,
            tc.table_name AS source_table,
            kcu.column_name AS source_column,
            ccu.table_name AS target_table,
            ccu.column_name AS target_column,
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
            AND tc.constraint_type = 'FOREIGN KEY'
        ORDER BY tc.table_name, kcu.column_name;
    """)
    fks = cur.fetchall()

    # Tablas sin FK (islas)
    cur.execute("""
        SELECT table_name FROM information_schema.tables
        WHERE table_schema = 'public' AND table_type = 'BASE TABLE'
        ORDER BY table_name;
    """)
    all_tables = {r[0] for r in cur.fetchall()}
    tables_with_fk = {r[1] for r in fks} | {r[3] for r in fks}
    isolated = sorted(all_tables - tables_with_fk)

    # Contar referencias entrantes por tabla
    ref_count = {}
    for fk in fks:
        target = fk[3]
        ref_count[target] = ref_count.get(target, 0) + 1

    lines = []
    lines.append("# Relaciones entre Tablas - Base de Datos IMA Mecatrónica")
    lines.append(f"Generado: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    lines.append(f"Total Foreign Keys: {len(fks)}")
    lines.append("")

    # Resumen
    lines.append("## Resumen de Conectividad")
    lines.append("")
    lines.append("| Tabla | FK Salientes | FK Entrantes | Rol |")
    lines.append("|-------|-------------|-------------|-----|")

    outgoing = {}
    incoming = {}
    for fk in fks:
        outgoing[fk[1]] = outgoing.get(fk[1], 0) + 1
        incoming[fk[3]] = incoming.get(fk[3], 0) + 1

    for t in sorted(all_tables):
        out_c = outgoing.get(t, 0)
        in_c = incoming.get(t, 0)
        if out_c == 0 and in_c == 0:
            role = "Aislada"
        elif in_c > 3:
            role = "Tabla Central"
        elif out_c > 0 and in_c == 0:
            role = "Hoja (solo referencia)"
        elif out_c == 0 and in_c > 0:
            role = "Catálogo/Lookup"
        else:
            role = "Intermedia"
        lines.append(f"| `{t}` | {out_c} | {in_c} | {role} |")
    lines.append("")

    # Lista completa de FK
    lines.append("## Todas las Foreign Keys")
    lines.append("")
    lines.append("| # | Constraint | Origen | Columna | Destino | Columna | ON UPDATE | ON DELETE |")
    lines.append("|---|-----------|--------|---------|---------|---------|-----------|-----------|")
    for i, (cname, src_t, src_c, tgt_t, tgt_c, upd, dele) in enumerate(fks, 1):
        lines.append(f"| {i} | `{cname}` | `{src_t}` | `{src_c}` | `{tgt_t}` | `{tgt_c}` | {upd} | {dele} |")
    lines.append("")

    # Agrupado por tabla destino (quién depende de quién)
    lines.append("## Dependencias por Tabla Destino")
    lines.append("")
    targets = {}
    for fk in fks:
        tgt = fk[3]
        if tgt not in targets:
            targets[tgt] = []
        targets[tgt].append(fk)

    for tgt in sorted(targets.keys()):
        refs = targets[tgt]
        lines.append(f"### {tgt} ({len(refs)} referencias entrantes)")
        for _, src_t, src_c, _, tgt_c, upd, dele in refs:
            lines.append(f"- `{src_t}.{src_c}` -> `{tgt}.{tgt_c}` (ON DELETE {dele})")
        lines.append("")

    # Tablas aisladas
    if isolated:
        lines.append("## Tablas Aisladas (sin FK)")
        lines.append("")
        for t in isolated:
            lines.append(f"- `{t}`")
        lines.append("")

    # Cadenas de CASCADE
    lines.append("## Cascadas de Eliminación")
    lines.append("> Tablas donde ON DELETE CASCADE puede causar eliminaciones en cadena")
    lines.append("")
    cascades = [(c, s, sc, t, tc) for c, s, sc, t, tc, u, d in fks if d == 'CASCADE']
    if cascades:
        lines.append("| Origen | Columna | Destino | Columna | Constraint |")
        lines.append("|--------|---------|---------|---------|------------|")
        for cname, src, srcc, tgt, tgtc in cascades:
            lines.append(f"| `{src}` | `{srcc}` | `{tgt}` | `{tgtc}` | `{cname}` |")
    else:
        lines.append("No hay cascadas de eliminación configuradas.")
    lines.append("")

    cur.close()
    conn.close()

    out_path = os.path.join(os.path.dirname(__file__), "output", "02_relaciones.md")
    os.makedirs(os.path.dirname(out_path), exist_ok=True)
    with open(out_path, "w", encoding="utf-8") as f:
        f.write("\n".join(lines))
    print(f"OK: {out_path}")
    print(f"   {len(fks)} foreign keys documentadas")
    print(f"   {len(isolated)} tablas aisladas")
    print(f"   {len(cascades)} cascadas ON DELETE")

if __name__ == "__main__":
    main()
