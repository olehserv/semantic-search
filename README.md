# Semantic Code Search

A search tool for .NET codebases. It combines two kinds of search:
**semantic search** (search by meaning, using vectors) and **keyword search**
(BM25). An AI agent (for example Claude Code) can use it to ask questions
about the code in natural language. The vectors are stored in a
[Qdrant](https://qdrant.tech/) database.

## How it works

1. **Index** (`build_index.py`) — reads `.cs`/`.csproj`/`.sln`/`.slnx` files,
   cuts them into overlapping chunks, turns each chunk into a vector with a
   local HuggingFace model, and saves the vectors in Qdrant. It also saves the
   chunks to `./.claude/cache/bm25.pkl` so keyword search has data.
2. **Search + rank** (`query_index.py`) — for one question it runs three
   searches (vector top-6, BM25, vector top-12) with three variants of the
   question. Then it removes duplicates and gives each result a combined score
   (`ranking.py`). It prints clean JSON on stdout; all log messages go to
   stderr.
3. **Answer** (`ask.py`) — sends the best code chunks to a local LLM (Ollama)
   and returns a written answer. It detects the question type (locate / flow /
   debug / explain) and retries with more context if the first answer is too
   weak.

`mcp_server.py` lets agents use step 2 over stdin/stdout. *Note: it uses a
simple custom protocol, not the real MCP standard yet. The real MCP server is
Phase 1 of `docs/reviews/2026-06-11-production-readiness-plan.md`.*

| File | Role |
|------|------|
| `scripts/build_index.py` | Build or rebuild the Qdrant index (`--force` = no question asked) |
| `scripts/query_index.py` | Hybrid search + ranking; CLI prints JSON |
| `scripts/ranking.py` | The scoring math (only numpy, unit-tested) |
| `scripts/ask.py` | LLM answers based on the found code |
| `scripts/mcp_server.py` | stdin/stdout search wrapper for agents |
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

# 2. Search — prints ranked results as JSON
python .claude/scripts/query_index.py "where is authentication handled"

# 3. Ask — get an LLM answer (needs Ollama)
python .claude/scripts/ask.py "how does the request pipeline work"
```

Agents send one JSON request per line to `mcp_server.py`:

```json
{"tool": "search_codebase", "input": {"query": "where is the DB context configured"}}
```

## Configuration

| Setting | Where |
|---------|-------|
| Embedding model / device, LLM model | `model_setup.py` |
| Qdrant host / port / collection name | `qdrant.py` |
| Which file types are indexed | `build_index.py` (`load_documents`) |
| Chunk size / overlap | `build_index.py` (`SentenceSplitter`) |
| Search depth, score weights, query variants | `query_index.py` / `ranking.py` |

## Good to know

- **BM25 needs a build first.** Before `build_index.py` has written
  `./.claude/cache/bm25.pkl`, every query stops with an error that tells you
  to run the build.
- **Embedding cache.** Embeddings made at query time are cached in
  `./.claude/cache/embeddings.pkl`. If you change the embedding model, delete
  this file — old vectors are not removed automatically.
- **Project status.** The code review, known problems, and the roadmap are in
  `docs/reviews/`. The current state of the work is in `handoff.md`.
