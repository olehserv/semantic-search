# Semantic Code Search

Hybrid semantic + keyword search over a .NET codebase, exposed to an AI agent
(e.g. Claude Code). Source files are indexed into a [Qdrant](https://qdrant.tech/)
vector database; natural-language questions are answered by combining vector
search, BM25 keyword search, and (optionally) a local LLM.

## How it works

1. **Index** (`build_index.py`) — reads `.cs`/`.csproj`/`.sln`/`.slnx` files,
   splits them into overlapping chunks, embeds them with a local HuggingFace
   model, stores the vectors in Qdrant, and writes the chunk nodes to
   `./.claude/cache/bm25.pkl` for keyword search.
2. **Retrieve + rank** (`query_index.py`) — runs dense (top-6), BM25, and wide
   dense (top-12) retrieval over three query variants, deduplicates, re-embeds
   the candidates, and re-ranks them with a weighted score (`ranking.py`).
   Prints clean JSON on stdout; all diagnostics go to stderr.
3. **Answer** (`ask.py`) — feeds the top snippets to a local LLM (Ollama),
   auto-detecting the question mode (locate / flow / debug / explain), and
   retries with more context if the first answer is insufficient.

`mcp_server.py` wraps stage 2 for agents over stdin/stdout. *Note: it speaks a
custom line protocol, not yet the real MCP standard — replacing it with an
official MCP server is Phase 1 of `docs/reviews/2026-06-11-production-readiness-plan.md`.*

| File | Role |
|------|------|
| `scripts/build_index.py` | Build/rebuild the Qdrant index (`--force` skips the replace prompt) |
| `scripts/query_index.py` | Hybrid retrieval + re-ranking; CLI prints JSON |
| `scripts/ranking.py` | Pure fusion/scoring math (numpy only, unit-tested) |
| `scripts/ask.py` | LLM-backed answers over the retrieved code |
| `scripts/mcp_server.py` | stdin/stdout search wrapper for agents |
| `scripts/qdrant.py` | Qdrant connection + container lifecycle |
| `scripts/model_setup.py` | Embedding model + LLM configuration |
| `eval/` | Retrieval-quality eval harness (see `eval/README.md`) |

## Requirements

- **Python 3.11+** — `pip install -r requirements.txt`
  (`requirements-dev.txt` adds pytest/ruff)
- **Docker** — Qdrant is auto-started via `docker compose`. Without the compose
  plugin, start it manually:
  `docker run -d --name qdrant-local -p 6333:6333 -v qdrant_storage:/qdrant/storage qdrant/qdrant`
- **GPU optional** — embeddings use CUDA when available, otherwise CPU
  (auto-detected in `model_setup.py`)
- **[Ollama](https://ollama.com/)** with a pulled model (default `llama3`) —
  only needed for `ask.py`

## Usage

The scripts are meant to live in your target project under `.claude/scripts/`
and run from that **project's root** (paths like `./.claude/cache/...` resolve
relative to the current directory).

```bash
# 1. Build the index (re-run whenever the code changes)
python .claude/scripts/build_index.py            # add --force to skip the prompt

# 2. Query — raw ranked JSON on stdout
python .claude/scripts/query_index.py "where is authentication handled"

# 3. Ask — LLM answer grounded in the retrieved code (needs Ollama)
python .claude/scripts/ask.py "how does the request pipeline work"
```

Agents send one JSON request per line to `mcp_server.py`:

```json
{"tool": "search_codebase", "input": {"query": "where is the DB context configured"}}
```

## Configuration

| Setting | Location |
|---------|----------|
| Embedding model / device, LLM model | `model_setup.py` |
| Qdrant host / port / collection name | `qdrant.py` |
| Indexed file types / excludes | `build_index.py` (`load_documents`) |
| Chunk size / overlap | `build_index.py` (`SentenceSplitter`) |
| Retrieval depth, fusion weights, query expansion | `query_index.py` / `ranking.py` |

## Notes

- **BM25 needs a build.** Until `build_index.py` has written
  `./.claude/cache/bm25.pkl`, queries fail with an error telling you to run it.
- **Embedding cache.** Query-time embeddings are cached in
  `./.claude/cache/embeddings.pkl`. Delete it if you switch embedding models —
  cached vectors are not invalidated automatically.
- **Project status.** Senior review, known issues, and the roadmap live in
  `docs/reviews/`; session-to-session state is tracked in `handoff.md`.
