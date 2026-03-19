"""Test de conexión a la base de datos Supabase PostgreSQL."""
import os
from dotenv import load_dotenv
import psycopg2

load_dotenv()

def test_connection():
    try:
        conn = psycopg2.connect(
            host=os.getenv("DB_HOST"),
            port=os.getenv("DB_PORT"),
            dbname=os.getenv("DB_NAME"),
            user=os.getenv("DB_USER"),
            password=os.getenv("DB_PASSWORD"),
        )
        cur = conn.cursor()

        # Version de PostgreSQL
        cur.execute("SELECT version();")
        version = cur.fetchone()[0]
        print(f"Conectado: {version[:60]}...")

        # Contar tablas, vistas, funciones
        cur.execute("""
            SELECT
                (SELECT count(*) FROM information_schema.tables
                 WHERE table_schema = 'public' AND table_type = 'BASE TABLE') AS tables,
                (SELECT count(*) FROM information_schema.views
                 WHERE table_schema = 'public') AS views,
                (SELECT count(*) FROM information_schema.routines
                 WHERE routine_schema = 'public') AS functions;
        """)
        tables, views, functions = cur.fetchone()
        print(f"Tablas: {tables} | Vistas: {views} | Funciones: {functions}")

        # Listar tablas
        cur.execute("""
            SELECT table_name FROM information_schema.tables
            WHERE table_schema = 'public' AND table_type = 'BASE TABLE'
            ORDER BY table_name;
        """)
        print("\nTablas encontradas:")
        for row in cur.fetchall():
            print(f"  - {row[0]}")

        cur.close()
        conn.close()
        print("\nConexion OK - todo listo para documentar.")
        return True

    except Exception as e:
        print(f"ERROR de conexion: {e}")
        return False

if __name__ == "__main__":
    test_connection()
