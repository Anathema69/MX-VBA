"""
Link Drive folders to their corresponding orders in BD.
Run AFTER upload_to_drive.py has finished.

Usage: python fase4/dirve_test/link_folders_to_orders.py
"""

import requests
import json

# ========================================
# CONFIG
# ========================================
SUPABASE_URL = "https://wjozxqldvypdtfmkamud.supabase.co"
SUPABASE_KEY = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6Indqb3p4cWxkdnlwZHRmbWthbXVkIiwicm9sZSI6ImFub24iLCJpYXQiOjE3NTQ1OTQ1NzEsImV4cCI6MjA3MDE3MDU3MX0.n9QdmkQ7fWWLIZaz0x9RFDcYxD3TSAhP2imyf4o-3ok"

HEADERS = {
    "apikey": SUPABASE_KEY,
    "Authorization": f"Bearer {SUPABASE_KEY}",
    "Content-Type": "application/json",
    "Prefer": "return=representation"
}

# ========================================
# MAPPING: folder path -> order_id
# Path is relative to the "2026" folder in Drive
# Format: ("month_folder", "client_folder", "project_folder") -> order_id
# ========================================
FOLDER_ORDER_MAP = [
    # === ALTA CONFIANZA (enero) ===
    ("1. Enero", "Lennox",           "Mesa Prototipo Ensamble Hairpin",      1172),
    ("1. Enero", "Engicom",          "RFQ URGENTE",                          1173),
    ("1. Enero", "Lennox",           "Plantilla Termica Coil 25-1861g01",    1175),
    ("1. Enero", "Shot-Shot",        "Rack V2.0",                            1176),
    ("1. Enero", "Android",          "Mordazas",                             1183),
    ("1. Enero", "Lennox",           "Fixture Ruteo",                        1187),
    ("1. Enero", "CP Manufacturing", "Punzones 321007",                      1189),

    # === RESUELTOS POR ANALISIS PROFUNDO (dic 2025 / enero) ===
    ("1. Enero", "Android",          "PuntaLift",                            1169),
    ("1. Enero", "Lennox",           "Baleros y Pernos Blower",              1167),
    ("1. Enero", "Lennox",           "Fixture Matriz",                       1164),
    ("1. Enero", "Lennox",           "Fixtures NPI Kamino",                  1163),

    # === ALTA CONFIANZA (febrero) ===
    ("2. Febrero", "Lennox",         "Mordaza aluminio",                     1182),
    ("2. Febrero", "Lennox",         "Solera Gage",                          1180),
    ("2. Febrero", "Lennox",         "100275 Tapon",                         1192),
    ("2. Febrero", "Lennox",         "2.- Tornillo",                         1196),
    ("2. Febrero", "Lennox",         "3.- Angulo",                           1197),
    ("2. Febrero", "Lennox",         "Spring Plunger Chico",                 1190),
    ("2. Febrero", "Lennox",         "Plantilla Termica Coil 25-1861g01 V2.0", 1194),
    ("2. Febrero", "Engicom",        "Maquinados Miscelaneos",               1191),
    ("2. Febrero", "Engicom",        "RFQ Urgente",                          1198),

    # === RESUELTOS POR ANALISIS PROFUNDO (febrero) ===
    ("2. Febrero", "Lennox",         "Doblador 21\u00b0",                    1174),
    ("2. Febrero", "Android",        "Dedo Lean Cell",                       1185),
    ("2. Febrero", "Android",        "Punta Lift",                           1193),

    # === MARZO ===
    # Shot&Shot/Rack V2.1 NO se vincula: orden 1176 ya vinculada a Rack V2.0 (1:1)
    # Si se requiere, crear orden nueva para V2.1
]

# ========================================
# HELPERS
# ========================================

def sb_get(table, params=None):
    r = requests.get(f"{SUPABASE_URL}/rest/v1/{table}", headers=HEADERS, params=params)
    r.raise_for_status()
    return r.json()

def sb_patch(table, params, data):
    r = requests.patch(f"{SUPABASE_URL}/rest/v1/{table}", headers=HEADERS, params=params, json=data)
    r.raise_for_status()
    return r.json()

def find_folder_id(root_id, path_parts):
    """Navigate the folder tree to find the ID of a nested folder"""
    current_id = root_id
    for part in path_parts:
        children = sb_get("drive_folders", {
            "parent_id": f"eq.{current_id}",
            "name": f"eq.{part}",
            "select": "id,name"
        })
        if not children:
            return None, f"No se encontro '{part}' bajo parent_id={current_id}"
        current_id = children[0]["id"]
    return current_id, None

# ========================================
# MAIN
# ========================================

def main():
    print("=" * 65)
    print("IMA Drive - Vinculacion de carpetas a ordenes")
    print("=" * 65)
    print(f"Mappings a vincular: {len(FOLDER_ORDER_MAP)}")
    print()

    # Find root folder (IMA MECATRONICA)
    roots = sb_get("drive_folders", {"parent_id": "is.null", "select": "id,name"})
    if not roots:
        print("ERROR: No se encontro carpeta raiz")
        return
    root_id = roots[0]["id"]
    print(f"Raiz del Drive: id={root_id} ({roots[0]['name']})")

    # Find "2026" folder
    folder_2026 = sb_get("drive_folders", {
        "parent_id": f"eq.{root_id}",
        "name": "eq.2026",
        "select": "id,name"
    })
    if not folder_2026:
        print("ERROR: No se encontro carpeta '2026'. Ejecuta upload_to_drive.py primero.")
        return
    id_2026 = folder_2026[0]["id"]
    print(f"Carpeta 2026: id={id_2026}")
    print()

    linked = 0
    skipped = 0
    errors = []

    for month, client, project, order_id in FOLDER_ORDER_MAP:
        path = [month, client, project]
        label = f"{client}/{project}"

        # Find folder ID by navigating the tree
        folder_id, err = find_folder_id(id_2026, path)
        if err:
            errors.append(f"{label}: {err}")
            print(f"  [ERR]  {label:<45} {err}")
            continue

        # Check if already linked
        folder_data = sb_get("drive_folders", {
            "id": f"eq.{folder_id}",
            "select": "id,linked_order_id"
        })
        if folder_data and folder_data[0].get("linked_order_id"):
            existing = folder_data[0]["linked_order_id"]
            if existing == order_id:
                skipped += 1
                print(f"  [skip] {label:<45} ya vinculada a orden {order_id}")
                continue
            else:
                print(f"  [WARN] {label:<45} vinculada a {existing}, re-vinculando a {order_id}")

        # Link folder to order
        try:
            sb_patch("drive_folders",
                {"id": f"eq.{folder_id}"},
                {"linked_order_id": order_id})
            linked += 1
            print(f"  [OK]   {label:<45} -> orden {order_id} (folder_id={folder_id})")
        except Exception as e:
            errors.append(f"{label}: {e}")
            print(f"  [ERR]  {label:<45} {e}")

    print()
    print("=" * 65)
    print("RESULTADO")
    print("=" * 65)
    print(f"Vinculadas:   {linked}")
    print(f"Ya existian:  {skipped}")
    print(f"Errores:      {len(errors)}")

    if errors:
        print(f"\nErrores:")
        for e in errors:
            print(f"  - {e}")

    print()
    print("Carpetas SIN vincular (requieren asignacion manual):")
    print("  - Ene/Junco/Mesa Servicios")
    print("  - Mar/Engicom/RFQ-47337")
    print("  - Mar/Lennox/Fixture planta2")
    print("  - Mar/Lennox/valvula reversible")

if __name__ == "__main__":
    main()
