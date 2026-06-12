# Continuous Search Service — Design

**Date:** 2026-06-01
**Status:** ✅ Implemented 2026-06-12 (branch `phase-1-search-service`), with
one change: `mcp_server.py` became a real MCP server (official `mcp` SDK,
FastMCP/stdio) instead of keeping the line protocol. Container stack verified
with docker compose on 2026-06-13 (full runtime checklist below passed).

## Context

Today, every search runs `query_index.py` as a **new process per query**
(started by `mcp_server.py`). Two problems drive this change:

1. **BM25 caching with pickle is unreliable.** The B2 fix saved the parsed
   nodes to `bm25.pkl`, so each new process could rebuild BM25. But pickling
   LlamaIndex nodes does not restore them cleanly. We want to remove the
   pickle file and keep BM25 **only in memory**.
2. **Everything is rebuilt for every query.** Each process loads the index,
   the embedding model, and the BM25 retriever again. In-memory BM25 only
   works if the process **lives long**: build the retrievers once, keep them
   warm, reuse them for every query.

The result: a **search service** that runs all the time (in Docker) and holds
a warm engine, plus a thin MCP shim that forwards queries to it over HTTP.

## Decisions

- **Interface:** HTTP service (Flask) + thin MCP shim. `mcp_server.py`
  forwards to the service instead of starting a process.
- **Compute:** auto-detect — `device = "cuda" if torch.cuda.is_available() else "cpu"`.
- **Indexing:** not part of the service. `build_index.py` stays the separate,
  manual indexing step; the service expects Qdrant to be already filled.
- **BM25 node source:** rebuild the nodes from the **Qdrant collection at
  startup**. This keeps the original node IDs, so deduplication against the
  vector retriever still works. No source tree is needed in the container,
  and no pickle.
- **LLM:** the service does **not** use Ollama. `search_codebase` → `query()`
  is retrieval + embeddings only. `ask.py` (which uses the LLM) stays a
  separate CLI. No `/ask` endpoint (YAGNI).
- **Web framework:** Flask (one warm-singleton endpoint; one dependency).

## Architecture

```
build_index.py  ──(offline, manual)──>  Qdrant   (vectors + payload)
                                           │
search-service (container, always up)      │ scroll at startup
  startup: model_setup (auto device)       ▼
           load_index(from_vector_store) + rebuild nodes from Qdrant
           → build vector(top6) + bm25(in-mem) + wide(top12) → warm singleton
  POST /search {"query": "..."} ──> query() on warm engine ──> JSON
  GET  /health

agent ──starts──> mcp_server.py (shim) ──HTTP POST /search──> search-service
```

## Components and changes

### `query_index.py`
- **Remove** all BM25 pickle code: the `BM25_CACHE_PATH` constant, the
  `bm25_retriever_nodes_cache` global, the BM25 part of `cache_warmup()`,
  and the pickle read/write inside `get_bm25_retriever()`.
- **Add** a node loader that rebuilds all nodes from Qdrant: scroll the
  collection and rebuild each node with
  `llama_index.core.vector_stores.utils.metadata_dict_to_node(payload, text=payload.get("text"))`;
  if that fails, fall back to a plain
  `TextNode(id_=point.id, text=payload["text"], metadata=...)`.
  `get_bm25_retriever()` then builds
  `BM25Retriever.from_defaults(nodes=<rebuilt nodes>)` in memory.
- The existing `_engine` singleton (`get_engine()`) stays as it is. It is
  what keeps the three retrievers warm between requests in the long-lived
  process.
- The embedding-cache pickle (`EMB_CACHE_PATH`) **stays**, with the R3
  atomic write.

### `build_index.py`
- **Remove** the B2 node-pickle write (and the `BM25_CACHE_PATH` constant /
  `pickle` import, if then unused). The B4/B5 fixes stay.

### `service.py` *(new)*
- Flask app. On startup: import `model_setup`, call `get_engine()` to warm
  the retrievers, log that it is ready.
- `POST /search` — body `{"query": "..."}`; returns the `query()` JSON.
  Returns a 400-style JSON error for a missing/empty query, and a 500-style
  JSON error on failure.
- `GET /health` — returns `{"status": "ok"}` once the engine is warm.
- Runs on Flask's built-in server (one worker, so the warm singleton is
  shared) on `0.0.0.0:8000`.

### `mcp_server.py`
- Replace `subprocess.run([...query_index.py...])` with an HTTP `POST` to
  `f"{SEARCH_SERVICE_URL}/search"` (`SEARCH_SERVICE_URL` from env, default
  `http://localhost:8000`).
- Keep the B6/B7 robustness: stop on EOF, structured error for bad JSON,
  explicit "unknown tool" error, and a structured error when the service is
  unreachable or returns non-200.

### `qdrant.py`
- `QDRANT_HOST = os.getenv("QDRANT_HOST", "localhost")` (and optionally
  `QDRANT_PORT` from env), so the container can reach the `qdrant` compose
  service.
- `ensure_qdrant()` gets a `QDRANT_AUTOSTART` env switch (default `"1"`).
  When it is `"0"` (set inside the container), the function **waits and
  retries** until Qdrant is reachable, and raises a clear error if it never
  is — instead of trying to run `docker` (which is impossible inside the
  container).

### `model_setup.py`
- `device = "cuda" if torch.cuda.is_available() else "cpu"` (import `torch`).
  Keeps the Ollama LLM config for `ask.py`.

### `Dockerfile` *(new)* + `requirements.txt` *(new)* + `docker-compose.yml`
- `requirements.txt`: pin the runtime deps (llama-index core + qdrant +
  huggingface embeddings + bm25 retriever, `qdrant-client`, `torch`,
  `numpy`, `requests`, `flask`). CPU torch wheel by default.
- `Dockerfile`: Python base image (CPU works), install `requirements.txt`,
  copy the scripts, `CMD` runs `service.py`. GPU is opt-in (switch to a CUDA
  base image; the auto-detect code uses the GPU when the host provides it).
- `docker-compose.yml`: add `search-service` (build from the Dockerfile),
  `depends_on: [qdrant]`, ports `8000:8000`, env `QDRANT_HOST=qdrant`,
  `QDRANT_AUTOSTART=0`, and a volume for the HuggingFace model cache so the
  embedding model is not downloaded on every start. The GPU reservation is
  documented as an optional `deploy.resources` block.

## Data flow (one query)

1. (offline) `build_index.py` fills the Qdrant collection.
2. `search-service` starts: sets up the embedding model (auto device),
   connects to Qdrant (waits/retries), rebuilds the nodes from Qdrant, builds
   the vector/BM25/wide retrievers, and keeps them as the warm `_engine`
   singleton.
3. The agent starts `mcp_server.py`; it reads a `search_codebase` request
   from stdin and `POST`s the query to the service.
4. The service runs `query()` on the warm engine and returns ranked snippets
   as JSON; the shim passes it back to the agent.

## Error handling

- **Service startup:** if Qdrant never becomes reachable, log a clear error
  and exit non-zero (compose `depends_on` + the retry loop cover the start
  order).
- **/search:** missing/empty `query` → JSON error; exception inside
  `query()` → JSON error with the message (also logged on the server).
- **MCP shim:** service down / non-200 / bad JSON → structured
  `{"error": ...}` response (it never hangs the agent); EOF and unknown
  tools are handled as today.

## Testing / verification

- **Static (here):** `python -m py_compile scripts/*.py service.py`.
- **Shim unit test (here):** drive `mcp_server.py` over stdin with
  `SEARCH_SERVICE_URL` pointing at a closed port and check that it returns a
  structured error (no extra deps needed), plus the existing bad-JSON /
  unknown-tool cases.
- **Runtime (user's machine):**
  1. `docker compose up -d qdrant`; run `build_index.py` to fill the collection.
  2. `docker compose up -d search-service`; `curl localhost:8000/health` → `ok`.
  3. `curl -XPOST localhost:8000/search -d '{"query":"where is auth handled"}'`
     returns ranked JSON, and **BM25-only keyword hits appear** (this proves
     BM25 was built from Qdrant, in memory).
  4. Drive `mcp_server.py` (with the service up) and check for a valid JSON
     result.
  5. Check that the embedding model downloads only once (cached in the
     volume), and that the engine is built once at startup, not per request.

## Out of scope (postponed)

- R2 (mixed score-scale normalization), R4 (caches without size limits — even
  less important with one warm process), R5 (re-embedding).
- An `/ask` (LLM) endpoint in the service.
- Automatic index building/refresh inside the service.
