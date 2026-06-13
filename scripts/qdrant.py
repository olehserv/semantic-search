from qdrant_client import QdrantClient
import subprocess
import time
import os

from config import settings

# Public names kept for callers (query_index.py, build_index.py); values now
# come from the central config layer (plan 3.1).
QDRANT_HOST = settings.qdrant_host
QDRANT_PORT = settings.qdrant_port

COLLECTION_NAME = settings.collection_name

BASE_DIR = os.path.dirname(os.path.abspath(__file__))

COMPOSE_FILE = os.path.join(BASE_DIR, "docker-compose.yml")

def get_qdrant_client():
    try:
        client = QdrantClient(host=QDRANT_HOST, port=QDRANT_PORT)
        client.get_collections()
        return client
    except Exception:
        # Connectivity probe — used in a retry loop, so stay quiet on failure.
        return None


def ensure_qdrant():
    if get_qdrant_client() is not None:
        return

    # Inside a container (QDRANT_AUTOSTART=0) we cannot run docker — just wait
    # for the Qdrant service (started via compose depends_on) to become
    # reachable.
    if not settings.qdrant_autostart:
        for _ in range(30):
            if get_qdrant_client() is not None:
                print("✅ Qdrant is ready")
                return
            time.sleep(1)
        raise RuntimeError(
            f"❌ Qdrant not reachable at {QDRANT_HOST}:{QDRANT_PORT} "
            "(QDRANT_AUTOSTART=0, so no container was started)"
        )

    print("🚀 Starting Qdrant container...")

    try:
        subprocess.run(
            ["docker", "compose", "-f", COMPOSE_FILE, "-p", "ai-agent", "up", "-d"],
            check=True
        )
    except (subprocess.CalledProcessError, FileNotFoundError):
        try:
            subprocess.run(
                ["docker-compose", "-f", COMPOSE_FILE, "-p", "ai-agent", "up", "-d"],
                check=True
            )
        except (subprocess.CalledProcessError, FileNotFoundError) as e:
            raise RuntimeError(
                "Could not start Qdrant: neither 'docker compose' nor "
                "'docker-compose' is available. Start it manually, e.g.:\n"
                f"  docker run -d --name qdrant-local -p {QDRANT_PORT}:6333 "
                "-v qdrant_storage:/qdrant/storage qdrant/qdrant"
            ) from e

    # wait until the container is up
    for _ in range(15):
        if get_qdrant_client() is not None:
            print("✅ Qdrant is ready")
            return
        time.sleep(1)

    raise RuntimeError("❌ Qdrant failed to start")

if __name__ == "__main__":
    ensure_qdrant()