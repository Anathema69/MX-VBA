"""
Upload local folder tree to IMA Drive (Supabase BD + Cloudflare R2)
Usage: venv/Scripts/python upload_to_drive.py

Recreates the full folder structure under the Drive root folder,
then uploads all files to R2 with metadata in BD.
"""

import os
import sys
import time
import json
import mimetypes
import requests
import boto3
from botocore.config import Config
from concurrent.futures import ThreadPoolExecutor, as_completed
from datetime import datetime

# ========================================
# CONFIG (from appsettings.json)
# ========================================
SUPABASE_URL = "https://wjozxqldvypdtfmkamud.supabase.co"
SUPABASE_KEY = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6Indqb3p4cWxkdnlwZHRmbWthbXVkIiwicm9sZSI6ImFub24iLCJpYXQiOjE3NTQ1OTQ1NzEsImV4cCI6MjA3MDE3MDU3MX0.n9QdmkQ7fWWLIZaz0x9RFDcYxD3TSAhP2imyf4o-3ok"

R2_ACCOUNT_ID = "de1e6bb4adfca3d3f7ce503e218dd70e"
R2_ACCESS_KEY = "9b1ea138629f71fcf035cd602102c4f8"
R2_SECRET_KEY = "8ab25a0311f249f9bd55f7377158efb6555757b3cf7c41d3af03070306b00c1e"
R2_BUCKET = "ima-drive"

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
LOCAL_ROOT = os.path.join(SCRIPT_DIR, "2026")
UPLOAD_USER_ID = 1  # admin user
MAX_PARALLEL_UPLOADS = 3

# ========================================
# SUPABASE REST HELPERS
# ========================================
HEADERS = {
    "apikey": SUPABASE_KEY,
    "Authorization": f"Bearer {SUPABASE_KEY}",
    "Content-Type": "application/json",
    "Prefer": "return=representation"
}

def sb_get(table, params=None):
    r = requests.get(f"{SUPABASE_URL}/rest/v1/{table}", headers=HEADERS, params=params)
    r.raise_for_status()
    return r.json()

def sb_post(table, data):
    r = requests.post(f"{SUPABASE_URL}/rest/v1/{table}", headers=HEADERS, json=data)
    r.raise_for_status()
    return r.json()

# ========================================
# R2 CLIENT
# ========================================
s3 = boto3.client(
    "s3",
    endpoint_url=f"https://{R2_ACCOUNT_ID}.r2.cloudflarestorage.com",
    aws_access_key_id=R2_ACCESS_KEY,
    aws_secret_access_key=R2_SECRET_KEY,
    config=Config(signature_version="s3v4"),
    region_name="auto"
)

# ========================================
# DRIVE OPERATIONS
# ========================================

def get_root_folder_id():
    """Get the ID of the root folder (IMA MECATRONICA)"""
    rows = sb_get("drive_folders", {"parent_id": "is.null", "select": "id,name"})
    if rows:
        return rows[0]["id"]
    raise Exception("No root folder found in drive_folders")

def get_child_folders(parent_id):
    """Get existing child folders of a parent"""
    rows = sb_get("drive_folders", {"parent_id": f"eq.{parent_id}", "select": "id,name"})
    return {r["name"]: r["id"] for r in rows}

def create_folder(name, parent_id):
    """Create a folder in BD and return its ID"""
    result = sb_post("drive_folders", {
        "name": name,
        "parent_id": parent_id,
        "created_by": UPLOAD_USER_ID
    })
    return result[0]["id"]

def get_existing_files(folder_id):
    """Get existing file names in a folder"""
    rows = sb_get("drive_files", {"folder_id": f"eq.{folder_id}", "select": "file_name"})
    return {r["file_name"] for r in rows}

def upload_file(local_path, folder_id):
    """Upload a file to R2 + create record in BD"""
    file_name = os.path.basename(local_path)
    file_size = os.path.getsize(local_path)
    timestamp = int(time.time())
    storage_path = f"{folder_id}/{timestamp}_{file_name}"
    content_type = mimetypes.guess_type(file_name)[0] or "application/octet-stream"

    # Upload to R2
    with open(local_path, "rb") as f:
        s3.put_object(
            Bucket=R2_BUCKET,
            Key=storage_path,
            Body=f,
            ContentType=content_type
        )

    # Create record in BD
    sb_post("drive_files", {
        "folder_id": folder_id,
        "file_name": file_name,
        "storage_path": storage_path,
        "file_size": file_size,
        "content_type": content_type,
        "uploaded_by": UPLOAD_USER_ID
    })
    return file_size

# ========================================
# RECURSIVE UPLOAD
# ========================================

stats = {
    "folders_created": 0,
    "folders_skipped": 0,
    "files_uploaded": 0,
    "files_skipped": 0,
    "bytes_uploaded": 0,
    "errors": []
}

def process_folder(local_dir, parent_id, depth=0):
    """Recursively create folders and upload files"""
    indent = "  " * depth
    dir_name = os.path.basename(local_dir)

    # Get or create this folder in BD
    existing = get_child_folders(parent_id)

    if dir_name in existing:
        folder_id = existing[dir_name]
        stats["folders_skipped"] += 1
        print(f"{indent}[skip] {dir_name}/ (ya existe, id={folder_id})")
    else:
        folder_id = create_folder(dir_name, parent_id)
        stats["folders_created"] += 1
        print(f"{indent}[new]  {dir_name}/ (id={folder_id})")

    # Get existing files in this folder to avoid duplicates
    existing_files = get_existing_files(folder_id)

    # Upload files in this directory
    entries = sorted(os.listdir(local_dir))
    files = [e for e in entries if os.path.isfile(os.path.join(local_dir, e))]
    subdirs = [e for e in entries if os.path.isdir(os.path.join(local_dir, e))]

    for f in files:
        if f in existing_files:
            stats["files_skipped"] += 1
            continue

        local_path = os.path.join(local_dir, f)
        try:
            size = upload_file(local_path, folder_id)
            stats["files_uploaded"] += 1
            stats["bytes_uploaded"] += size
            size_str = format_size(size)
            print(f"{indent}  [{stats['files_uploaded']}] {f} ({size_str})")
        except Exception as e:
            stats["errors"].append(f"{local_path}: {e}")
            print(f"{indent}  [ERR] {f}: {e}")

    # Recurse into subdirectories
    for d in subdirs:
        process_folder(os.path.join(local_dir, d), folder_id, depth + 1)

def format_size(b):
    for u in ["B", "KB", "MB", "GB"]:
        if b < 1024:
            return f"{b:.1f} {u}"
        b /= 1024
    return f"{b:.1f} TB"

# ========================================
# MAIN
# ========================================

def main():
    if not os.path.isdir(LOCAL_ROOT):
        print(f"ERROR: No se encontro {LOCAL_ROOT}")
        sys.exit(1)

    # Count what we're about to upload
    total_folders = sum(1 for _, dirs, _ in os.walk(LOCAL_ROOT) for _ in dirs)
    total_files = sum(1 for _, _, files in os.walk(LOCAL_ROOT) for _ in files)
    total_size = sum(
        os.path.getsize(os.path.join(root, f))
        for root, _, files in os.walk(LOCAL_ROOT)
        for f in files
    )

    print("=" * 60)
    print("IMA Drive - Upload automatizado")
    print("=" * 60)
    print(f"Origen:    {os.path.abspath(LOCAL_ROOT)}")
    print(f"Destino:   R2 bucket '{R2_BUCKET}'")
    print(f"Carpetas:  {total_folders}")
    print(f"Archivos:  {total_files}")
    print(f"Tamano:    {format_size(total_size)}")
    print("=" * 60)

    confirm = input("Continuar? (y/n): ").strip().lower()
    if confirm != "y":
        print("Cancelado.")
        return

    start = time.time()
    root_id = get_root_folder_id()
    print(f"\nRaiz del Drive: id={root_id}")
    print(f"Iniciando upload...\n")

    # The "2026" folder is the top-level to create under root
    process_folder(LOCAL_ROOT, root_id, depth=0)

    elapsed = time.time() - start

    print("\n" + "=" * 60)
    print("RESULTADO")
    print("=" * 60)
    print(f"Carpetas creadas:   {stats['folders_created']}")
    print(f"Carpetas existentes:{stats['folders_skipped']}")
    print(f"Archivos subidos:   {stats['files_uploaded']}")
    print(f"Archivos existentes:{stats['files_skipped']}")
    print(f"Datos subidos:      {format_size(stats['bytes_uploaded'])}")
    print(f"Errores:            {len(stats['errors'])}")
    print(f"Tiempo:             {elapsed:.1f}s")

    if stats["errors"]:
        print(f"\nErrores ({len(stats['errors'])}):")
        for e in stats["errors"]:
            print(f"  - {e}")

    # Save report
    report_path = os.path.join(SCRIPT_DIR, "upload_report.json")
    with open(report_path, "w") as f:
        json.dump({
            "timestamp": datetime.now().isoformat(),
            "source": os.path.abspath(LOCAL_ROOT),
            "stats": stats,
            "elapsed_seconds": round(elapsed, 1)
        }, f, indent=2)
    print(f"\nReporte guardado en: {report_path}")

if __name__ == "__main__":
    main()
