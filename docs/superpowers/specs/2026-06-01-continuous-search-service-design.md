# Continuous Search Service — Design

**Date:** 2026-06-01
**Status:** Approved (pending spec review)

## Context

The search tool currently runs `query_index.py` as a **fresh subprocess per
query** (spawned by `mcp_server.py`). Two problems motivate this change:

1. **BM25 caching via pickle is unreliable.** The B2 fix persisted parsed nodes
   to `bm25.pkl` so the per-query process could rebuild BM25, but pickling the
   LlamaIndex nodes does not round-trip cleanly. We want to drop the pickle and
   keep BM25 **in memory only**.
2. **Everything is rebuilt on every query.** Loading the index, the embedding
   model, and the BM25 retriever per subprocess is wasteful. In-memory-only BM25
   is only viable if the process is **long-lived** — build the retrievers once,
   keep them warm, reuse them for every query.

The outcome: a continuously running **search service** (in Docker) that holds a
warm engine, and a thin MCP shim that forwards queries to it over HTTP.

## Decisions

- **Interface:** HTTP service (Flask) + thin MCP shim. `mcp_server.py` forwards
  to the service instead of spawning a subprocess.
- **Compute:** auto-detect — `device = "cuda" if torch.cuda.is_available() else "cpu"`.
- **Indexing:** out of scope for the service. `build_index.py` remains the
  separate, manual indexing step; the service assumes Qdrant is already populated.
- **BM25 node source:** reconstruct nodes from the **Qdrant collection at startup**
  (preserves original node IDs → dedup with the vector retriever still aligns; no
  source tree needed in the container; no pickle).
- **LLM:** the service does **not** use Ollama. `search_codebase` → `query()` is
  retrieval + embeddings only. `ask.py` (which uses the LLM) stays a separate CLI.
  No `/ask` endpoint (YAGNI).
- **Web framework:** Flask (single warm-singleton endpoint; one dependency).

## Architecture

```
build_index.py  ──(offline, manual)──>  Qdrant   (vectors + payload)
                                           │
search-service (container, always up)      │ scroll at startup
  startup: model_setup (auto device)       ▼
           load_index(from_vector_store) + reconstruct nodes from Qdrant
           → build vector(top6) + bm25(in-mem) + wide(top12) → warm singleton
  POST /search {"query": "..."} ──> query() on warm engine ──> JSON
  GET  /health

agent ──spawns──> mcp_server.py (shim) ──HTTP POST /search──> search-service
```

## Components and changes

### `query_index.py`
- **Remove** all BM25 pickle code: the `BM25_CACHE_PATH` constant, the
  `bm25_retriever_nodes_cache` global, the BM25 branch of `cache_warmup()`, and
  the pickle read/write inside `get_bm25_retriever()`.
- **Add** a node-loader that reconstructs all nodes from Qdrant (scroll the
  collection, rebuild each node via
  `llama_index.core.vector_stores.utils.metadata_dict_to_node(payload, text=payload.get("text"))`;
  fall back to a plain `TextNode(id_=point.id, text=payload["text"], metadata=...)`
  if reconstruction fails). `get_bm25_retriever()` builds
  `BM25Retriever.from_defaults(nodes=<reconstructed nodes>)` in memory.
- The existing `_engine` singleton (`get_engine()`) is unchanged and is what keeps
  the three retrievers warm across requests in the long-lived process.
- The embedding-cache pickle (`EMB_CACHE_PATH`) **stays**, with the R3 atomic write.

### `build_index.py`
- **Remove** the B2 node-pickle write (and the `BM25_CACHE_PATH` constant /
  `pickle` import if now unused). B4/B5 fixes stay.

### `service.py` *(new)*
- Flask app. On startup: import `model_setup`, call `get_engine()` to warm the
  retrievers, log readiness.
- `POST /search` — body `{"query": "..."}`; returns the `query()` JSON. Returns a
  400-style JSON error for missing/empty query, 500-style JSON error on failure.
- `GET /health` — returns `{"status": "ok"}` once the engine is warm.
- Runs via Flask's built-in server (single worker, so the warm singleton is shared)
  on `0.0.0.0:8000`.

### `mcp_server.py`
- Replace `subprocess.run([...query_index.py...])` with an HTTP `POST` to
  `f"{SEARCH_SERVICE_URL}/search"` (`SEARCH_SERVICE_URL` from env, default
  `http://localhost:8000`).
- Keep the B6/B7 robustness: break on EOF, structured error on malformed JSON,
  explicit "unknown tool" error, and a structured error if the service is
  unreachable / returns non-200.

### `qdrant.py`
- `QDRANT_HOST = os.getenv("QDRANT_HOST", "localhost")` (and optionally
  `QDRANT_PORT` from env) so the container can reach the `qdrant` compose service.
- `ensure_qdrant()` gains a `QDRANT_AUTOSTART` env gate (default `"1"`): when `"0"`
  (set in the container) it **waits/retries** for Qdrant to become reachable and
  raises a clear error if it never does, instead of trying to run `docker` (which
  it cannot do inside the container).

### `model_setup.py`
- `device = "cuda" if torch.cuda.is_available() else "cpu"` (import `torch`). Keeps
  the Ollama LLM config for `ask.py`.

### `Dockerfile` *(new)* + `requirements.txt` *(new)* + `docker-compose.yml`
- `requirements.txt`: pin the runtime deps (llama-index core + qdrant + huggingface
  embeddings + bm25 retriever, `qdrant-client`, `torch`, `numpy`, `requests`,
  `flask`). CPU torch wheel by default.
- `Dockerfile`: Python base (CPU-capable), install `requirements.txt`, copy
  scripts, `CMD` runs `service.py`. GPU is opt-in (swap to a CUDA base image; the
  auto-detect code uses the GPU when the host exposes it).
- `docker-compose.yml`: add `search-service` (build from Dockerfile),
  `depends_on: [qdrant]`, ports `8000:8000`, env `QDRANT_HOST=qdrant`,
  `QDRANT_AUTOSTART=0`, and a volume for the HuggingFace model cache to avoid
  re-downloading the embedding model on each start. GPU reservation documented as
  an optional `deploy.resources` block.

## Data flow (query)

1. (offline) `build_index.py` populates the Qdrant collection.
2. `search-service` boots: configures the embedding model (auto device), connects
   to Qdrant (waits/retries), reconstructs nodes from Qdrant, builds vector/BM25/
   wide retrievers, holds them as the warm `_engine` singleton.
3. Agent spawns `mcp_server.py`; it reads a `search_codebase` request from stdin
   and `POST`s the query to the service.
4. Service runs `query()` on the warm engine and returns ranked snippets as JSON;
   the shim relays it back to the agent.

## Error handling

- **Service startup:** if Qdrant never becomes reachable, log a clear error and
  exit non-zero (compose `depends_on` + retry loop covers ordering).
- **/search:** missing/empty `query` → JSON error; exception in `query()` → JSON
  error with the message (logged server-side).
- **MCP shim:** service down / non-200 / bad JSON → structured `{"error": ...}`
  response (never hangs the agent); EOF and unknown tools handled as today.

## Testing / verification

- **Static (here):** `python -m py_compile scripts/*.py service.py`.
- **Shim unit (here):** drive `mcp_server.py` over stdin with `SEARCH_SERVICE_URL`
  pointed at a non-existent port to confirm it returns a structured error (no deps
  needed), plus the existing malformed-JSON / unknown-tool cases.
- **Runtime (user's machine):**
  1. `docker compose up -d qdrant`; run `build_index.py` to populate the collection.
  2. `docker compose up -d search-service`; `curl localhost:8000/health` → `ok`.
  3. `curl -XPOST localhost:8000/search -d '{"query":"where is auth handled"}'`
     returns ranked JSON, and **BM25-only keyword hits appear** (confirms BM25 is
     built from Qdrant, in-memory).
  4. Drive `mcp_server.py` (with the service up) and confirm a valid JSON result.
  5. Confirm the embedding model downloads once (cached in the volume) and that the
     engine is built once at startup, not per request.

## Out of scope (deferred)

- R2 (mixed score-scale normalization), R4 (unbounded caches — now even less
  relevant with a single warm process), R5 (re-embedding).
- Service-side `/ask` (LLM) endpoint.
- Automatic index building/refresh inside the service.
