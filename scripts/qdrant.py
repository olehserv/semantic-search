from qdrant_client import QdrantClient
import subprocess
import requests
import time
import os

QDRANT_HOST = "localhost"
QDRANT_PORT = 6333

COLLECTION_NAME = "demo"

BASE_DIR = os.path.dirname(os.path.abspath(__file__))

compose_file = os.path.join(BASE_DIR, "docker-compose.yml")

def get_Qdrant_client():
    try:
        client = QdrantClient(host=QDRANT_HOST, port=QDRANT_PORT)
        client.get_collections()
        return client
    except Exception:
        # Connectivity probe — used in a retry loop, so stay quiet on failure.
        return None


def ensure_qdrant():
    if not get_Qdrant_client() is None:
        return

    print("🚀 Starting Qdrant container...")
    
    try:
        subprocess.run(
            ["docker", "compose", "-f", compose_file, "-p", "ai-agent", "up", "-d"],
            check=True
        )
    except (subprocess.CalledProcessError, FileNotFoundError):
        subprocess.run(
            ["docker-compose", "-f", compose_file, "-p", "ai-agent", "up", "-d"],
            check=True
        )

    # чекаємо поки підніметься
    for _ in range(15):
        if not get_Qdrant_client() is None:
            print("✅ Qdrant is ready")
            return
        time.sleep(1)

    raise RuntimeError("❌ Qdrant failed to start")

if __name__ == "__main__":
    ensure_qdrant()