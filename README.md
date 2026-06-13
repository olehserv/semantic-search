# Semantic Code Search

A search tool for .NET codebases. It combines two kinds of search:
**semantic search** (search by meaning, using vectors) and **keyword search**
(BM25). An AI agent (for example Claude Code) can use it to ask questions
about the code in natural language. The vectors are stored in a
[Qdrant](https://qdrant.tech/) database.

## How it works

1. **Index** (`build_index.py`) — reads `.cs`/`.csproj`/`.sln`/`.slnx` files,
   cuts them into overlapping chunks, turns each chunk into a vector with a
   local HuggingFace model, and saves the vectors (with the chunk text) in
   Qdrant.
2. **Search + rank** (`query_index.py`) — for one question it runs three
   searches (vector top-6, BM25, vector top-12) with three variants of the
   question. Then it removes duplicates and gives each result a combined score
   (`ranking.py`). BM25 is built in memory from the Qdrant collection.
3. **Serve** (`service.py`) — a long-running Flask service that loads the
   model and builds the search engine **once**, then answers every
   `POST /search` from the warm engine in well under a second.
4. **Agent access** (`mcp_server.py`) — a real MCP server (official `mcp`
   SDK, stdio). It gives agents one tool, `search_codebase`, and forwards
   each call to the service over HTTP.
5. **Answer** (`ask.py`) — sends the best code chunks to a local LLM (Ollama)
   and returns a written answer. It detects the question type (locate / flow /
   debug / explain) and retries with more context if the first answer is too
   weak.

| File | Role |
|------|------|
| `scripts/build_index.py` | Build or rebuild the Qdrant index (`--force` = no question asked) |
| `scripts/query_index.py` | Hybrid search + ranking; CLI prints JSON |
| `scripts/ranking.py` | The scoring math (only numpy, unit-tested) |
| `scripts/service.py` | Long-running search service with the warm engine |
| `scripts/mcp_server.py` | MCP server (stdio) — exposes `search_codebase` to agents |
| `scripts/ask.py` | LLM answers based on the found code |
| `scripts/qdrant.py` | Qdrant connection + starting the container |
| `scripts/model_setup.py` | Sets up the embedding model and the LLM |
| `eval/` | Search-quality measurement (see `eval/README.md`) |

## What you need

- **Python 3.11+** — `pip install -r requirements.txt`
  (`requirements-dev.txt` also installs pytest/ruff)
- **Docker** — Qdrant starts automatically with `docker compose`. If you do
  not have the compose plugin, start it by hand:
  `docker run -d --name qdrant-local -p 6333:6333 -v qdrant_storage:/qdrant/storage qdrant/qdrant`
- **GPU is optional** — the code uses CUDA when it exists, otherwise CPU
  (automatic, see `model_setup.py`)
- **[Ollama](https://ollama.com/)** with a model (default `llama3`) — only
  needed for `ask.py`

## How to use it

Put the scripts in your target project under `.claude/scripts/` and run them
from that **project's root folder**. Paths like `./.claude/cache/...` are
relative to the current directory.

```bash
# 1. Build the index (run again after the code changes)
python .claude/scripts/build_index.py            # --force = replace without asking

# 2. Start the search service (loads the model once, then stays warm)
python .claude/scripts/service.py                # serves on localhost:8000

# 3. Search over HTTP — ranked results as JSON
curl -XPOST localhost:8000/search -H 'Content-Type: application/json' \
     -d '{"query":"where is authentication handled"}'

# (One-off search without the service also works, but pays a cold start:)
python .claude/scripts/query_index.py "where is authentication handled"

# 4. Ask — get an LLM answer (needs Ollama)
python .claude/scripts/ask.py "how does the request pipeline work"
```

### Use it from Claude Code (MCP)

This repo has a `.mcp.json` that registers the `code-search` MCP server.
With the service running, Claude Code can call the `search_codebase` tool
directly. If the service is down, the tool returns a clear error with the
command to start it.

To use it from **another project**: copy the scripts to
`.claude/scripts/`, and add to that project's `.mcp.json`:

```json
{
  "mcpServers": {
    "code-search": {
      "command": "python3",
      "args": [".claude/scripts/mcp_server.py"]
    }
  }
}
```

The `command` must be a Python that has `mcp` and `requests` installed (the
shim needs nothing else — no torch, no llama-index).

## Configuration

Deployment config is centralized in `scripts/config.py` (pydantic-settings) and
read from environment variables — one typed place instead of scattered
`os.getenv` calls. Defaults match the values the project ships with.

| Setting | Env var | Default |
|---------|---------|---------|
| Qdrant host | `QDRANT_HOST` | `localhost` |
| Qdrant port | `QDRANT_PORT` | `6333` |
| Qdrant collection | `QDRANT_COLLECTION` | `demo` |
| Qdrant autostart (Docker) | `QDRANT_AUTOSTART` | `1` (on) |
| Embedding model | `EMBED_MODEL` | `BAAI/bge-base-en-v1.5` |
| LLM model (ask.py) | `LLM_MODEL` | `llama3` |
| Embedding cache path | `EMB_CACHE_PATH` | `./.claude/cache/embeddings.pkl` |
| Re-rank candidate cut | `RERANK_CANDIDATES` | `30` |
| Cross-encoder re-ranker | `CROSS_ENCODER_MODEL` | `""` (off) |
| Service port | `PORT` | `8000` |
| Query variants | `QUERY_VARIANT_SUFFIXES` | `implementation` (comma-separated; add domain terms, e.g. `implementation,.NET core backend`) |

| Other | Where |
|-------|-------|
| Embedding device (CPU/GPU auto) | `model_setup.py` |
| Which file types are indexed | `build_index.py` (`load_documents`) |
| Chunking | `chunking.py` — C# files are cut on type/method borders with tree-sitter (namespace/class metadata on every chunk); other files use the `SentenceSplitter` in `build_index.py` |
| Search depth, score weights | `query_index.py` / `ranking.py` |

## Good to know

- **BM25 needs a build first.** If the Qdrant collection is empty, the search
  stops with an error that tells you to run `build_index.py`.
- **Docker image.** `Dockerfile` + the `search-service` entry in
  `scripts/docker-compose.yml` run the service in a container. Qdrant access
  is configurable with `QDRANT_HOST` / `QDRANT_PORT` / `QDRANT_AUTOSTART` env
  vars. `QDRANT_COLLECTION` (default `demo`) picks the collection, so two
  indexed codebases can live side by side in one Qdrant.
- **Embedding cache.** Embeddings made at query time are cached in
  `./.claude/cache/embeddings.pkl`. If you change the embedding model, delete
  this file — old vectors are not removed automatically.
- **Project status.** The code review, known problems, and the roadmap are in
  `docs/reviews/`. The current state of the work is in `handoff.md`.
