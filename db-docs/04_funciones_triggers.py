"""Fase 4: Documentación de Funciones y Triggers."""
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

    # Funciones
    cur.execute("""
        SELECT
            p.proname AS function_name,
            pg_get_function_arguments(p.oid) AS arguments,
            pg_get_function_result(p.oid) AS return_type,
            l.lanname AS language,
            p.prosrc AS source_code,
            p.provolatile AS volatility,
            p.prosecdef AS security_definer,
            obj_description(p.oid, 'pg_proc') AS comment,
            pg_get_functiondef(p.oid) AS full_definition
        FROM pg_proc p
        JOIN pg_namespace n ON n.oid = p.pronamespace
        JOIN pg_language l ON l.oid = p.prolang
        WHERE n.nspname = 'public'
        ORDER BY p.proname;
    """)
    functions = cur.fetchall()

    # Triggers
    cur.execute("""
        SELECT
            t.tgname AS trigger_name,
            c.relname AS table_name,
            CASE t.tgtype & 2 WHEN 2 THEN 'BEFORE' ELSE 'AFTER' END AS timing,
            CASE t.tgtype & 28
                WHEN 4 THEN 'INSERT'
                WHEN 8 THEN 'DELETE'
                WHEN 12 THEN 'INSERT OR DELETE'
                WHEN 16 THEN 'UPDATE'
                WHEN 20 THEN 'INSERT OR UPDATE'
                WHEN 24 THEN 'DELETE OR UPDATE'
                WHEN 28 THEN 'INSERT OR UPDATE OR DELETE'
            END AS events,
            CASE t.tgtype & 1 WHEN 1 THEN 'FOR EACH ROW' ELSE 'FOR EACH STATEMENT' END AS scope,
            p.proname AS function_name,
            t.tgenabled AS enabled,
            obj_description(t.oid, 'pg_trigger') AS comment
        FROM pg_trigger t
        JOIN pg_class c ON c.oid = t.tgrelid
        JOIN pg_namespace n ON n.oid = c.relnamespace
        JOIN pg_proc p ON p.oid = t.tgfoid
        WHERE n.nspname = 'public'
            AND NOT t.tgisinternal
        ORDER BY c.relname, t.tgname;
    """)
    triggers = cur.fetchall()

    lines = []
    lines.append("# Funciones y Triggers - Base de Datos IMA Mecatrónica")
    lines.append(f"Generado: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    lines.append(f"Funciones: {len(functions)} | Triggers: {len(triggers)}")
    lines.append("")

    # Índice de funciones
    lines.append("## Índice de Funciones")
    lines.append("")

    # Categorizar funciones
    trigger_funcs = {t[5] for t in triggers}
    rpc_funcs = []
    trig_funcs_list = []
    util_funcs = []

    for f in functions:
        fname = f[0]
        if fname in trigger_funcs:
            trig_funcs_list.append(f)
        elif f[3] == 'sql' or f[2] != 'trigger':
            rpc_funcs.append(f)
        else:
            util_funcs.append(f)

    # Re-categorize: if return is trigger, it's a trigger func
    rpc_final = []
    for f in rpc_funcs:
        if f[2] == 'trigger':
            trig_funcs_list.append(f)
        else:
            rpc_final.append(f)

    # util_funcs are orphaned trigger functions (return trigger but no active trigger uses them)
    orphan_funcs = util_funcs

    lines.append(f"### Funciones RPC/Negocio ({len(rpc_final)})")
    for i, f in enumerate(rpc_final, 1):
        lines.append(f"{i}. [{f[0]}](#func-{f[0]})")
    lines.append("")

    lines.append(f"### Funciones de Trigger ({len(trig_funcs_list)})")
    for i, f in enumerate(trig_funcs_list, 1):
        lines.append(f"{i}. [{f[0]}](#func-{f[0]})")
    lines.append("")

    if orphan_funcs:
        lines.append(f"### Funciones Huérfanas - sin trigger activo ({len(orphan_funcs)})")
        for i, f in enumerate(orphan_funcs, 1):
            lines.append(f"{i}. [{f[0]}](#func-{f[0]})")
        lines.append("")

    lines.append("---")
    lines.append("")

    # Documentar funciones RPC
    lines.append("# Funciones RPC / Negocio")
    lines.append("")
    for f in rpc_final:
        fname, args, ret, lang, src, vol, secdef, comment, fulldef = f
        vol_map = {'v': 'VOLATILE', 'i': 'IMMUTABLE', 's': 'STABLE'}
        lines.append(f"## {fname}")
        if comment:
            lines.append(f"> {comment}")
        lines.append("")
        lines.append(f"- **Argumentos**: `{args if args else '(ninguno)'}`")
        lines.append(f"- **Retorna**: `{ret}`")
        lines.append(f"- **Lenguaje**: `{lang}`")
        lines.append(f"- **Volatilidad**: `{vol_map.get(vol, vol)}`")
        if secdef:
            lines.append(f"- **Security**: `SECURITY DEFINER`")
        lines.append("")

        lines.append("### Código Fuente")
        lines.append("")
        lines.append(f"```{lang}")
        lines.append(src.strip() if src else "-- Código no disponible")
        lines.append("```")
        lines.append("")
        lines.append("---")
        lines.append("")

    # Documentar funciones de trigger
    lines.append("# Funciones de Trigger")
    lines.append("")
    for f in trig_funcs_list:
        fname, args, ret, lang, src, vol, secdef, comment, fulldef = f
        lines.append(f"## {fname}")
        if comment:
            lines.append(f"> {comment}")
        lines.append("")
        lines.append(f"- **Retorna**: `{ret}`")
        lines.append(f"- **Lenguaje**: `{lang}`")

        # Triggers que usan esta función
        using_triggers = [t for t in triggers if t[5] == fname]
        if using_triggers:
            lines.append(f"- **Usado por triggers**:")
            for t in using_triggers:
                lines.append(f"  - `{t[0]}` en `{t[1]}` ({t[2]} {t[3]} {t[4]})")
        lines.append("")

        lines.append("### Código Fuente")
        lines.append("")
        lines.append(f"```{lang}")
        lines.append(src.strip() if src else "-- Código no disponible")
        lines.append("```")
        lines.append("")
        lines.append("---")
        lines.append("")

    # Funciones huérfanas (return trigger pero sin trigger activo)
    if orphan_funcs:
        lines.append("# Funciones Huérfanas (sin trigger activo)")
        lines.append("> Estas funciones retornan `trigger` pero ningún trigger activo las referencia.")
        lines.append("> Pueden ser funciones obsoletas o pendientes de conectar.")
        lines.append("")
        for f in orphan_funcs:
            fname, args, ret, lang, src, vol, secdef, comment, fulldef = f
            vol_map = {'v': 'VOLATILE', 'i': 'IMMUTABLE', 's': 'STABLE'}
            lines.append(f"## {fname}")
            if comment:
                lines.append(f"> {comment}")
            lines.append("")
            lines.append(f"- **Retorna**: `{ret}`")
            lines.append(f"- **Lenguaje**: `{lang}`")
            lines.append(f"- **Estado**: Sin trigger activo")
            lines.append("")

            lines.append("### Código Fuente")
            lines.append("")
            lines.append(f"```{lang}")
            lines.append(src.strip() if src else "-- Código no disponible")
            lines.append("```")
            lines.append("")
            lines.append("---")
            lines.append("")

    # Triggers agrupados por tabla
    lines.append("# Triggers por Tabla")
    lines.append("")

    triggers_by_table = {}
    for t in triggers:
        tbl = t[1]
        if tbl not in triggers_by_table:
            triggers_by_table[tbl] = []
        triggers_by_table[tbl].append(t)

    lines.append("## Resumen")
    lines.append("")
    lines.append("| Tabla | # Triggers | Triggers |")
    lines.append("|-------|-----------|----------|")
    for tbl in sorted(triggers_by_table.keys()):
        trigs = triggers_by_table[tbl]
        names = ", ".join(t[0] for t in trigs)
        lines.append(f"| `{tbl}` | {len(trigs)} | {names} |")
    lines.append("")

    for tbl in sorted(triggers_by_table.keys()):
        trigs = triggers_by_table[tbl]
        lines.append(f"### {tbl}")
        lines.append("")
        for tname, _, timing, events, scope, func, enabled, tcomment in trigs:
            en_str = "Habilitado" if enabled == 'O' else "Deshabilitado"
            lines.append(f"- **`{tname}`**: {timing} {events} {scope} -> `{func}()` [{en_str}]")
            if tcomment:
                lines.append(f"  > {tcomment}")
        lines.append("")

    cur.close()
    conn.close()

    out_path = os.path.join(os.path.dirname(__file__), "output", "04_funciones_triggers.md")
    with open(out_path, "w", encoding="utf-8") as f:
        f.write("\n".join(lines))
    total_funcs = len(rpc_final) + len(trig_funcs_list) + len(orphan_funcs)
    print(f"OK: {out_path}")
    print(f"   {total_funcs} funciones total ({len(rpc_final)} RPC + {len(trig_funcs_list)} trigger + {len(orphan_funcs)} huerfanas)")
    print(f"   {len(triggers)} triggers en {len(triggers_by_table)} tablas")

if __name__ == "__main__":
    main()
