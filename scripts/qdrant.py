"""Connect to Qdrant, and start its Docker container when it is not running.

Qdrant is the vector database: it stores the chunk vectors and finds the
nearest ones at search time. The rest of the code just calls `ensure_qdrant()`
to make sure it is up, then `get_qdrant_client()` to talk to it.
"""
from qdrant_client import QdrantClient, models
import subprocess
import time
import os
import re

from config import settings

# Public names kept for callers (query_index.py, build_index.py); values now
# come from the central config layer (plan 3.1).
QDRANT_HOST = settings.qdrant_host
QDRANT_PORT = settings.qdrant_port

COLLECTION_NAME = settings.collection_name

BASE_DIR = os.path.dirname(os.path.abspath(__file__))

COMPOSE_FILE = os.path.join(BASE_DIR, "docker-compose.yml")

def get_qdrant_client():
    """Return a connected Qdrant client, or None if Qdrant is not reachable.

    It calls get_collections() as a quick "are you really there?" check, because
    creating the client object alone does not open a connection.
    """
    try:
        client = QdrantClient(host=QDRANT_HOST, port=QDRANT_PORT)
        client.get_collections()
        return client
    except Exception:
        # Connectivity probe — used in a retry loop, so stay quiet on failure.
        return None


def ensure_qdrant():
    """Make sure Qdrant is running before we use it. There are two cases.

    1. Already up -> nothing to do.
    2. QDRANT_AUTOSTART is off (we are inside a container): we cannot run
       docker here, so just wait for the Qdrant service to become reachable.
    3. Otherwise (local dev): start the Qdrant container with docker-compose,
       then wait for it to answer.

    Raises RuntimeError if Qdrant never comes up.
    """
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

# --- Safe index rebuild via aliases (plan 3.3, finding H6) -------------------
#
# An *alias* is a second name that points at a real collection. The query side
# always talks to the alias name (e.g. "demo"); a rebuild creates a brand new
# timestamped collection, fills it, and only then re-points the alias at it.
# Searching never stops, and a crashed build cannot destroy the live index.

def new_collection_name(name):
    """Return a fresh, sortable name like "demo-20260613-141500" for a build.

    The exact "<name>-YYYYMMDD-HHMMSS" shape is also what `promote_collection`
    sweeps, so the two must agree (see the regex below)."""
    return f"{name}-{time.strftime('%Y%m%d-%H%M%S')}"


def _build_pattern(name):
    """Match ONLY our own timestamped builds of `name`, e.g. demo-20260613-141500.

    Anchored so a sibling collection like "demo-eval" (or its builds
    "demo-eval-20260613-141500") is never matched when `name` is "demo"."""
    return re.compile(rf"^{re.escape(name)}-\d{{8}}-\d{{6}}$")


def resolve_active_collection(client, name):
    """Return the real collection `name` currently resolves to, or None.

    `name` may be an alias (return its target collection) or a real collection
    (return itself). Used to decide whether build_index should prompt before
    replacing an existing index."""
    for a in client.get_aliases().aliases:
        if a.alias_name == name:
            return a.collection_name
    real = {c.name for c in client.get_collections().collections}
    return name if name in real else None


def promote_collection(client, alias_name, new_collection):
    """Point `alias_name` at `new_collection`, then delete the old index.

    Steps (the alias swap itself is atomic, so queries never see a gap):
      1. A real collection named `alias_name` (a legacy index from before
         aliases existed) blocks the alias name -> delete it first. This is the
         one-time migration; the new data already exists, so nothing is lost.
      2. Re-point the alias in a single update: delete the old alias entry (if
         any) and create it pointing at `new_collection`, in one atomic call.
      3. Sweep every leftover timestamped build of this name except the new one
         — that removes the previous index and any orphan from a crashed build.
    """
    aliases = {a.alias_name: a.collection_name for a in client.get_aliases().aliases}
    collections = {c.name for c in client.get_collections().collections}

    # 1. Free the alias name if a legacy real collection is sitting on it.
    if alias_name in collections and alias_name not in aliases:
        client.delete_collection(alias_name)

    # 2. Atomically (re)point the alias at the freshly built collection.
    ops = []
    if alias_name in aliases:
        ops.append(models.DeleteAliasOperation(
            delete_alias=models.DeleteAlias(alias_name=alias_name)
        ))
    ops.append(models.CreateAliasOperation(
        create_alias=models.CreateAlias(
            collection_name=new_collection, alias_name=alias_name
        )
    ))
    client.update_collection_aliases(change_aliases_operations=ops)

    # 3. Delete the previous target and any crashed-build orphans for this name.
    pattern = _build_pattern(alias_name)
    for name in collections:
        if name != new_collection and pattern.match(name):
            client.delete_collection(name)


if __name__ == "__main__":
    ensure_qdrant()