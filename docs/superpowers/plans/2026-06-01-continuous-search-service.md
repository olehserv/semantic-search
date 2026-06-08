# Continuous Search Service Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the per-query subprocess with a long-lived Flask search service that holds warm vector/BM25/wide retrievers, drop the BM25 pickle cache (rebuild BM25 in-memory from Qdrant), and make `mcp_server.py` a thin HTTP forwarder.

**Architecture:** `build_index.py` stays the offline indexer. A new `service.py` (Flask, single worker) warms the `query_index` engine once at startup — including BM25 rebuilt in-memory from nodes reconstructed out of the Qdrant collection — and serves `POST /search`. `mcp_server.py` forwards stdin requests to the service over HTTP. The service runs in Docker alongside Qdrant; embeddings auto-detect CUDA/CPU.

**Tech Stack:** Python 3.11, LlamaIndex (core + qdrant + huggingface + bm25), Qdrant, Flask, Docker Compose, pytest.

---

## Notes for the implementer

- This repo's scripts live in `scripts/` and import each other by bare module name (`import qdrant`, `import model_setup`), so they run with `scripts/` on `sys.path` (CWD = `scripts/`, or the deployed `.claude/scripts/`).
- `query_index.py` redirects its module-level `print` to **stderr** on purpose — keep new diagnostic prints as `print(...)`; they will go to stderr automatically.
- Importing `query_index` triggers `import model_setup`, which loads the embedding model. The test `conftest.py` (Task 1) stubs `model_setup` so tests don't download/load the model. Do not remove that stub.
- Three test files are runnable in different environments:
  - `tests/test_mcp_server.py` — needs only `requests` + `pytest` (no ML stack).
  - `tests/test_service.py`, `tests/test_node_loader.py` — need `llama-index-core`, `qdrant-client`, `flask`, `pytest` installed (the `model_setup` stub avoids the embedding-model load).
- After every code change, run `python -m py_compile` on the touched files as a fast gate.

---

## Task 1: Test scaffolding + dependency manifests

**Files:**
- Create: `requirements.txt`
- Create: `requirements-dev.txt`
- Create: `tests/conftest.py`

- [ ] **Step 1: Create `requirements.txt`**

```text
llama-index-core
llama-index-vector-stores-qdrant
llama-index-embeddings-huggingface
llama-index-llms-ollama
llama-index-retrievers-bm25
qdrant-client
torch
numpy
requests
flask
```

- [ ] **Step 2: Create `requirements-dev.txt`**

```text
-r requirements.txt
pytest
```

- [ ] **Step 3: Create `tests/conftest.py`**

```python
import os
import sys
import types

# Make the scripts importable by bare module name (import qdrant, import service, ...).
SCRIPTS = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "scripts"))
if SCRIPTS not in sys.path:
    sys.path.insert(0, SCRIPTS)

# Stub model_setup so importing query_index/service does not download or load the
# HuggingFace embedding model during tests. query() is monkeypatched in service tests,
# and load_all_nodes does not use the embed model.
if "model_setup" not in sys.modules:
    sys.modules["model_setup"] = types.ModuleType("model_setup")
```

- [ ] **Step 4: Verify pytest collects with no tests yet**

Run: `cd /home/kali/p/semantic-search && python -m pytest -q`
Expected: exit code 5 / "no tests ran" (collection works, no errors).

- [ ] **Step 5: Commit**

```bash
git add requirements.txt requirements-dev.txt tests/conftest.py
git commit -m "chore: add dependency manifests and pytest scaffolding"
```

---

## Task 2: `qdrant.py` — env-driven host/port + autostart gate

**Files:**
- Modify: `scripts/qdrant.py`

- [ ] **Step 1: Make host/port env-driven**

Replace:

```python
QDRANT_HOST = "localhost"
QDRANT_PORT = 6333
```

with:

```python
QDRANT_HOST = os.getenv("QDRANT_HOST", "localhost")
QDRANT_PORT = int(os.getenv("QDRANT_PORT", "6333"))
```

(`os` is already imported.)

- [ ] **Step 2: Add the `QDRANT_AUTOSTART` gate to `ensure_qdrant()`**

Replace the entire `ensure_qdrant` function with:

```python
def ensure_qdrant():
    if get_Qdrant_client() is not None:
        return

    # Inside a container (QDRANT_AUTOSTART=0) we cannot run docker — just wait
    # for the Qdrant service (started via compose depends_on) to become reachable.
    if os.getenv("QDRANT_AUTOSTART", "1") != "1":
        for _ in range(30):
            if get_Qdrant_client() is not None:
                print("✅ Qdrant is ready")
                return
            time.sleep(1)
        raise RuntimeError("❌ Qdrant not reachable")

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

    for _ in range(15):
        if get_Qdrant_client() is not None:
            print("✅ Qdrant is ready")
            return
        time.sleep(1)

    raise RuntimeError("❌ Qdrant failed to start")
```

- [ ] **Step 3: Compile-check**

Run: `cd /home/kali/p/semantic-search && python -m py_compile scripts/qdrant.py`
Expected: no output (success).

- [ ] **Step 4: Commit**

```bash
git add scripts/qdrant.py
git commit -m "feat: make Qdrant host/port env-driven and gate container autostart"
```

---

## Task 3: `model_setup.py` — auto-detect compute device

**Files:**
- Modify: `scripts/model_setup.py`

- [ ] **Step 1: Auto-detect device**

Replace:

```python
Settings.embed_model = HuggingFaceEmbedding(
    model_name="BAAI/bge-base-en-v1.5",
    device="cuda"
)
```

with:

```python
import torch

device = "cuda" if torch.cuda.is_available() else "cpu"

Settings.embed_model = HuggingFaceEmbedding(
    model_name="BAAI/bge-base-en-v1.5",
    device=device
)
```

(Place the `import torch` with the other imports at the top of the file.)

- [ ] **Step 2: Compile-check**

Run: `cd /home/kali/p/semantic-search && python -m py_compile scripts/model_setup.py`
Expected: no output.

- [ ] **Step 3: Commit**

```bash
git add scripts/model_setup.py
git commit -m "feat: auto-detect cuda/cpu for the embedding model"
```

---

## Task 4: `query_index.py` — drop BM25 pickle, rebuild BM25 from Qdrant

**Files:**
- Modify: `scripts/query_index.py`
- Test: `tests/test_node_loader.py`

- [ ] **Step 1: Write the failing test for the node loader**

Create `tests/test_node_loader.py`:

```python
import types

import query_index


class FakeClient:
    """Returns successive (points, next_offset) tuples from scroll()."""

    def __init__(self, pages):
        self.pages = pages
        self.calls = 0

    def scroll(self, **kwargs):
        page = self.pages[self.calls]
        self.calls += 1
        return page


def _point(pid, text):
    # Payload without _node_content forces the metadata_dict_to_node fallback.
    return types.SimpleNamespace(id=pid, payload={"text": text})


def test_load_all_nodes_pages_and_preserves_ids():
    pages = [
        ([_point("a", "foo"), _point("b", "bar")], "offset-1"),
        ([_point("c", "baz")], None),
    ]
    client = FakeClient(pages)

    nodes = query_index.load_all_nodes(client, "col")

    assert [n.text for n in nodes] == ["foo", "bar", "baz"]
    assert [n.node_id for n in nodes] == ["a", "b", "c"]
    assert client.calls == 2
```

- [ ] **Step 2: Run it to verify it fails**

Run: `cd /home/kali/p/semantic-search && python -m pytest tests/test_node_loader.py -q`
Expected: FAIL — `AttributeError: module 'query_index' has no attribute 'load_all_nodes'`.

- [ ] **Step 3: Remove the BM25 pickle machinery**

In `scripts/query_index.py`:

a) Delete the BM25 cache path constant. Remove this line (keep `EMB_CACHE_PATH`):

```python
BM25_CACHE_PATH = "./.claude/cache/bm25.pkl"
```

b) Delete the BM25 nodes cache global. Remove:

```python
bm25_retriever_nodes_cache = None
```

c) In `cache_warmup()`, drop the BM25 branch and its global. The function becomes:

```python
def cache_warmup():
    global _embedding_cache
    if os.path.exists(EMB_CACHE_PATH):
        with open(EMB_CACHE_PATH, "rb") as f:
            print("[DEBUG] Loading EMB cache ...")
            _embedding_cache = pickle.load(f)
            print(f"[DEBUG] EMB cache size: {len(_embedding_cache)}")
```

- [ ] **Step 4: Add the node loader and rewrite `get_bm25_retriever`**

Add these imports near the top of `scripts/query_index.py` (with the other `llama_index` imports):

```python
from llama_index.core.schema import TextNode
from llama_index.core.vector_stores.utils import metadata_dict_to_node
```

Replace the entire `get_bm25_retriever` function with:

```python
def load_all_nodes(client, collection_name):
    """Reconstruct all nodes from the Qdrant collection (preserves node IDs)."""
    nodes = []
    offset = None
    while True:
        points, offset = client.scroll(
            collection_name=collection_name,
            with_payload=True,
            with_vectors=False,
            limit=256,
            offset=offset,
        )
        for p in points:
            payload = p.payload or {}
            try:
                node = metadata_dict_to_node(payload, text=payload.get("text"))
            except Exception:
                node = TextNode(id_=str(p.id), text=payload.get("text", ""))
            nodes.append(node)
        if offset is None:
            break
    return nodes


def get_bm25_retriever(index):
    # BM25 is in-memory only: rebuild it from the nodes stored in Qdrant each
    # time the engine is built (once per long-lived process).
    client = qdrant.get_Qdrant_client()
    nodes = load_all_nodes(client, qdrant.COLLECTION_NAME)
    print(f"[DEBUG] BM25 nodes loaded from Qdrant: {len(nodes)}")
    return BM25Retriever.from_defaults(nodes=nodes)
```

(`get_bm25_retriever` keeps its `index` parameter for call-site compatibility but no longer reads `index.docstore`.)

- [ ] **Step 5: Run the node-loader test to verify it passes**

Run: `cd /home/kali/p/semantic-search && python -m pytest tests/test_node_loader.py -q`
Expected: PASS.

- [ ] **Step 6: Confirm no BM25 pickle references remain and compile**

Run: `cd /home/kali/p/semantic-search && grep -n "bm25" scripts/query_index.py; python -m py_compile scripts/query_index.py`
Expected: only `get_bm25_retriever` / `BM25Retriever` / `bm25` retriever-variable references remain (no `BM25_CACHE_PATH`, no `bm25_retriever_nodes_cache`, no `pickle.dump`/`pickle.load` of BM25); compile succeeds.

- [ ] **Step 7: Commit**

```bash
git add scripts/query_index.py tests/test_node_loader.py
git commit -m "feat: rebuild BM25 in-memory from Qdrant, drop pickle cache"
```

---

## Task 5: `build_index.py` — remove the BM25 node-pickle write

**Files:**
- Modify: `scripts/build_index.py`

- [ ] **Step 1: Remove the pickle write block**

Delete the BM25 cache write that follows the "Index successfully built" log:

```python
    # Persist nodes for BM25: load_index() rebuilds the index from the Qdrant
    # vector store, which leaves index.docstore.docs empty, so query_index's
    # BM25 retriever has no nodes to work with. Write them here for it to load.
    os.makedirs(os.path.dirname(BM25_CACHE_PATH), exist_ok=True)
    with open(BM25_CACHE_PATH, "wb") as f:
        pickle.dump(nodes, f)
    print(f"[DEBUG] [{datetime.now()}] ✅ Wrote {len(nodes)} nodes to {BM25_CACHE_PATH}")
```

- [ ] **Step 2: Remove the now-unused constant and import**

Delete the constant block:

```python
# Must match query_index.BM25_CACHE_PATH. Duplicated rather than imported:
# importing query_index here would trigger its import-time side effects
# (cache warmup + a Qdrant connection).
BM25_CACHE_PATH = "./.claude/cache/bm25.pkl"
```

and remove `import pickle` (it is no longer used in this file).

- [ ] **Step 3: Confirm pickle is gone and compile**

Run: `cd /home/kali/p/semantic-search && grep -n "pickle\|BM25_CACHE_PATH" scripts/build_index.py; python -m py_compile scripts/build_index.py`
Expected: no matches; compile succeeds.

- [ ] **Step 4: Commit**

```bash
git add scripts/build_index.py
git commit -m "refactor: drop BM25 node pickle write from build_index"
```

---

## Task 6: `service.py` — Flask search service holding the warm engine

**Files:**
- Create: `scripts/service.py`
- Test: `tests/test_service.py`

- [ ] **Step 1: Write the failing tests**

Create `tests/test_service.py`:

```python
import service


def test_health_ok():
    client = service.app.test_client()
    resp = client.get("/health")
    assert resp.status_code == 200
    assert resp.get_json()["status"] == "ok"


def test_search_missing_query_is_400():
    client = service.app.test_client()
    resp = client.post("/search", json={})
    assert resp.status_code == 400
    assert "error" in resp.get_json()


def test_search_returns_query_result(monkeypatch):
    monkeypatch.setattr(service, "query", lambda q: {"sources": ["x.cs"], "context": []})
    client = service.app.test_client()
    resp = client.post("/search", json={"query": "where is auth"})
    assert resp.status_code == 200
    assert resp.get_json()["sources"] == ["x.cs"]


def test_search_error_is_500(monkeypatch):
    def boom(q):
        raise RuntimeError("kaboom")
    monkeypatch.setattr(service, "query", boom)
    client = service.app.test_client()
    resp = client.post("/search", json={"query": "x"})
    assert resp.status_code == 500
    assert "kaboom" in resp.get_json()["error"]
```

- [ ] **Step 2: Run to verify it fails**

Run: `cd /home/kali/p/semantic-search && python -m pytest tests/test_service.py -q`
Expected: FAIL — `ModuleNotFoundError: No module named 'service'`.

- [ ] **Step 3: Create `scripts/service.py`**

```python
import os

from flask import Flask, request, jsonify

from query_index import query, get_engine

app = Flask(__name__)


@app.route("/health")
def health():
    return jsonify({"status": "ok"})


@app.route("/search", methods=["POST"])
def search():
    data = request.get_json(silent=True) or {}
    q = data.get("query")
    if not q:
        return jsonify({"error": "missing 'query'"}), 400
    try:
        return jsonify(query(q))
    except Exception as e:
        return jsonify({"error": str(e)}), 500


def main():
    # Configure the embedding model + LLM, then warm the retrievers once so the
    # first real request does not pay the build cost.
    import model_setup  # noqa: F401  (import side effect: configures Settings)
    get_engine()
    app.run(host="0.0.0.0", port=int(os.getenv("PORT", "8000")))


if __name__ == "__main__":
    main()
```

(`model_setup` and `get_engine()` are deferred into `main()` so importing the app for tests does not load the embedding model or connect to Qdrant.)

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd /home/kali/p/semantic-search && python -m pytest tests/test_service.py -q`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add scripts/service.py tests/test_service.py
git commit -m "feat: add Flask search service holding the warm engine"
```

---

## Task 7: `mcp_server.py` — forward to the HTTP service

**Files:**
- Modify: `scripts/mcp_server.py`
- Test: `tests/test_mcp_server.py`

- [ ] **Step 1: Write the failing tests**

Create `tests/test_mcp_server.py`:

```python
import mcp_server


def test_search_code_unreachable(monkeypatch):
    def boom(*a, **k):
        raise mcp_server.requests.RequestException("no route")
    monkeypatch.setattr(mcp_server.requests, "post", boom)

    out = mcp_server.search_code("hi")
    assert "error" in out
    assert "unreachable" in out["error"]


def test_search_code_success(monkeypatch):
    class Resp:
        status_code = 200
        def json(self):
            return {"sources": ["a.cs"]}
    monkeypatch.setattr(mcp_server.requests, "post", lambda *a, **k: Resp())

    assert mcp_server.search_code("hi") == {"sources": ["a.cs"]}


def test_search_code_non_200(monkeypatch):
    class Resp:
        status_code = 500
        text = "boom"
        def json(self):
            return {}
    monkeypatch.setattr(mcp_server.requests, "post", lambda *a, **k: Resp())

    out = mcp_server.search_code("hi")
    assert out["error"].startswith("search service returned 500")
```

- [ ] **Step 2: Run to verify it fails**

Run: `cd /home/kali/p/semantic-search && python -m pytest tests/test_mcp_server.py -q`
Expected: FAIL — `AttributeError: module 'mcp_server' has no attribute 'requests'` (or import error referencing the old subprocess version).

- [ ] **Step 3: Rewrite `scripts/mcp_server.py`**

```python
import json
import os
import sys

import requests

SEARCH_SERVICE_URL = os.getenv("SEARCH_SERVICE_URL", "http://localhost:8000")


def search_code(query):
    try:
        resp = requests.post(
            f"{SEARCH_SERVICE_URL}/search",
            json={"query": query},
            timeout=120,
        )
    except requests.RequestException as e:
        return {"error": f"search service unreachable: {e}"}

    if resp.status_code != 200:
        return {"error": f"search service returned {resp.status_code}", "body": resp.text}

    try:
        return resp.json()
    except ValueError:
        return {"error": "invalid JSON from search service", "body": resp.text}


def main():
    while True:
        try:
            line = input()
        except EOFError:
            break

        try:
            request = json.loads(line)
        except json.JSONDecodeError:
            print(json.dumps({"error": "invalid JSON request"}), flush=True)
            continue

        if request.get("tool") == "search_codebase":
            response = {"output": search_code(request["input"]["query"])}
        else:
            response = {"error": f"unknown tool: {request.get('tool')}"}

        print(json.dumps(response), flush=True)


if __name__ == "__main__":
    main()
```

- [ ] **Step 4: Run unit tests to verify they pass**

Run: `cd /home/kali/p/semantic-search && python -m pytest tests/test_mcp_server.py -q`
Expected: PASS (3 tests).

- [ ] **Step 5: Smoke-test the stdin loop end-to-end**

Run:
```bash
cd /home/kali/p/semantic-search && printf '%s\n' \
  'not json' \
  '{"tool":"bogus"}' \
  '{"tool":"search_codebase","input":{"query":"x"}}' \
  | SEARCH_SERVICE_URL=http://127.0.0.1:1 python scripts/mcp_server.py
```
Expected: three JSON lines — invalid-JSON error, unknown-tool error, and an `{"output": {"error": "search service unreachable: ..."}}` (port 1 is closed). No traceback; clean EOF exit.

- [ ] **Step 6: Commit**

```bash
git add scripts/mcp_server.py tests/test_mcp_server.py
git commit -m "feat: forward MCP search_codebase to the HTTP search service"
```

---

## Task 8: Docker packaging

**Files:**
- Create: `Dockerfile`
- Create: `.dockerignore`
- Modify: `scripts/docker-compose.yml`

- [ ] **Step 1: Create `.dockerignore`**

```text
.git
.venv
__pycache__
*.pyc
cache
.claude
docs
tests
scripts.zip
```

- [ ] **Step 2: Create `Dockerfile`**

```dockerfile
FROM python:3.11-slim

# Cache the HuggingFace model under a mounted volume to avoid re-downloading.
ENV PYTHONUNBUFFERED=1 \
    HF_HOME=/models

WORKDIR /app
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY scripts/ ./scripts/

WORKDIR /app/scripts
CMD ["python", "service.py"]
```

(GPU is opt-in: switch the base image to an `nvidia/cuda` Python image and install a CUDA torch wheel; the auto-detect code in `model_setup.py` then uses the GPU when the host exposes it via `--gpus`.)

- [ ] **Step 3: Add the `search-service` to `scripts/docker-compose.yml`**

Add this service under `services:` (sibling of `qdrant`):

```yaml
  search-service:
    build:
      context: ..
      dockerfile: Dockerfile
    container_name: search-service
    depends_on:
      - qdrant
    ports:
      - "8000:8000"
    environment:
      - QDRANT_HOST=qdrant
      - QDRANT_PORT=6333
      - QDRANT_AUTOSTART=0
      - HF_HOME=/models
    volumes:
      - hf_models:/models
    restart: unless-stopped
```

And add the named volume under the existing `volumes:` block:

```yaml
  hf_models:
```

- [ ] **Step 4: Validate the compose file**

Run: `cd /home/kali/p/semantic-search/scripts && docker compose -f docker-compose.yml config >/dev/null && echo OK`
Expected: `OK` (compose syntax valid). If Docker is unavailable in this environment, skip and validate on the target machine.

- [ ] **Step 5: Commit**

```bash
git add Dockerfile .dockerignore scripts/docker-compose.yml
git commit -m "feat: containerize the search service via docker-compose"
```

---

## Task 9: Final verification

**Files:** none (verification only)

- [ ] **Step 1: Compile every script**

Run: `cd /home/kali/p/semantic-search && python -m py_compile scripts/*.py`
Expected: no output.

- [ ] **Step 2: Run the full test suite**

Run: `cd /home/kali/p/semantic-search && python -m pytest -q`
Expected: all tests pass (node loader, service, mcp_server). Requires `llama-index-core`, `qdrant-client`, `flask`, `requests`, `pytest` installed.

- [ ] **Step 3: Runtime checklist (target machine with Docker + Qdrant + deps)**

Document and run:
1. `cd scripts && docker compose up -d qdrant`
2. From the project root, `python scripts/build_index.py` to populate the collection.
3. `cd scripts && docker compose up -d --build search-service`
4. `curl -s localhost:8000/health` → `{"status":"ok"}`.
5. `curl -s -XPOST localhost:8000/search -H 'Content-Type: application/json' -d '{"query":"where is authentication handled"}'` → ranked JSON; confirm keyword-only matches appear (BM25 built from Qdrant, in-memory).
6. Drive `mcp_server.py` with the service up:
   `printf '%s\n' '{"tool":"search_codebase","input":{"query":"db context"}}' | python scripts/mcp_server.py` → one `{"output": {...}}` JSON line.
7. Restart `search-service`; confirm the embedding model loads from the `hf_models` volume (no re-download) and the engine is built once at startup, not per request.

- [ ] **Step 4: Final commit (if any docs/notes updated)**

```bash
git add -A
git commit -m "docs: continuous search service verification notes" || true
```

---

## Self-review notes (for the author)

- **Spec coverage:** pickle removal (Task 4/5), in-memory BM25 from Qdrant (Task 4), warm singleton service (Task 6), MCP HTTP shim (Task 7), auto-device (Task 3), env-driven Qdrant + autostart gate (Task 2), Docker + compose + requirements (Task 1/8). All spec sections mapped.
- **Deferred (per spec):** R2/R4/R5, service `/ask` endpoint, in-service index build — intentionally not in any task.
- **Type/name consistency:** `load_all_nodes(client, collection_name)`, `get_bm25_retriever(index)`, `search_code(query)`, `SEARCH_SERVICE_URL`, `service.app`, `service.query` used consistently across tasks and tests.
