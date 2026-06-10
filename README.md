# Semantic Code Search

A hybrid semantic + keyword search tool for a code base, exposed to an AI agent
(e.g. Claude Code) over MCP. It indexes a .NET project's source files into a
[Qdrant](https://qdrant.tech/) vector database and answers natural-language
questions about the code by combining vector search, BM25 keyword search, and a
local LLM.

## How it works

The pipeline has three stages:

1. **Index** (`build_index.py`) — reads source files (`.cs`, `.csproj`, `.slnx`),
   splits them into overlapping chunks, embeds each chunk with a local
   HuggingFace model, and stores the vectors in Qdrant. It also writes the chunk
   nodes to `./.claude/cache/bm25.pkl` so keyword search has data to work with.

2. **Retrieve + rank** (`query_index.py`) — for a query it runs **hybrid
   retrieval**:
   - dense vector search (semantic similarity),
   - BM25 keyword search,
   - a wider vector pass,
   then deduplicates, re-embeds candidates, and re-ranks them with a weighted
   score (embedding similarity + retriever score + frequency boost). Returns the
   top code snippets as JSON.

3. **Answer** (`ask.py`) — feeds the retrieved snippets to a local LLM (Ollama)
   with a prompt tuned for a senior .NET engineer, and returns a written answer.
   It auto-detects the question mode (locate / flow / debug / explain) and
   retries with more context if the first answer is insufficient.

`mcp_server.py` wraps stage 2 as an MCP tool (`search_codebase`) so an agent can
call it over stdin/stdout.

### Components

| File | Role |
|------|------|
| `build_index.py` | Build/rebuild the Qdrant index from source files |
| `query_index.py` | Hybrid retrieval + re-ranking; CLI prints JSON results |
| `ask.py` | LLM-backed natural-language answers over the retrieved code |
| `mcp_server.py` | MCP server exposing `search_codebase` to an agent |
| `qdrant.py` | Qdrant connection + Docker container lifecycle |
| `model_setup.py` | Configures the embedding model and the LLM |
| `docker-compose.yml` | Qdrant service definition |

## Requirements

- **Python 3.11+**
- **Docker** (for the Qdrant container; started automatically)
- **A CUDA GPU** — embeddings run on `cuda` (`model_setup.py`). Change `device`
  there if you need CPU.
- **[Ollama](https://ollama.com/)** running locally with a model pulled
  (default `llama3`; configurable via `LLM_MODEL` in `model_setup.py`).

### Python dependencies

```bash
pip install -r requirements.txt        # runtime (pinned)
pip install -r requirements-dev.txt    # + pytest/ruff for development
```

## Layout / deployment

The scripts are meant to live in your target project under `.claude/scripts/`
and run with the **current working directory set to that project's root**. Paths
are resolved relative to CWD:

- caches: `./.claude/cache/embeddings.pkl`, `./.claude/cache/bm25.pkl`
- source files indexed from: `./` (recursively)
- `mcp.json` invokes `python .claude/scripts/mcp_server.py`

The collection name and Qdrant host/port are set in `qdrant.py`
(`COLLECTION_NAME`, `QDRANT_HOST`, `QDRANT_PORT`).

## Usage

All commands run from your **project root** (where the `.cs` files live).

### 1. Build the index

```bash
python .claude/scripts/build_index.py
```

This starts Qdrant if needed, reads your source files, embeds them, and stores
them in Qdrant. If the collection already exists it asks before replacing it.
Re-run this whenever the code changes (and to enable BM25 — see Notes).

### 2. Query (raw JSON results)

```bash
python .claude/scripts/query_index.py "where is authentication handled"
```

Prints a JSON object with the answer summary, source files, and ranked code
snippets. Diagnostic logs go to **stderr**, so stdout is clean JSON.

### 3. Ask (LLM answer)

```bash
python .claude/scripts/ask.py "how does the request pipeline work"
```

Requires Ollama running. Returns a written explanation grounded in the retrieved
code.

### 4. Use from an agent (MCP)

`mcp.json` registers a `code-search` server. Once configured, the agent can call
the `search_codebase` tool, which runs `query_index.py` and returns structured
results. The server reads one JSON request per line on stdin, e.g.:

```json
{"tool": "search_codebase", "input": {"query": "where is the DB context configured"}}
```

## Configuration

| Setting | Location |
|---------|----------|
| Embedding model / device | `model_setup.py` (`HuggingFaceEmbedding`) |
| LLM model (Ollama) | `model_setup.py` (`LLM_MODEL`) |
| Qdrant host / port / collection | `qdrant.py` |
| Indexed file types / excludes | `build_index.py` (`load_documents`) |
| Chunk size / overlap | `build_index.py` (`SentenceSplitter`) |
| Retrieval depth / score weights | `query_index.py` (`query()` args, `build_query_engine`) |

## Notes

- **BM25 requires a (re)build.** The index is loaded from the Qdrant vector
  store, which leaves the in-memory document store empty. `build_index.py`
  persists the chunk nodes to `bm25.pkl` for keyword search — until a build has
  written that cache, queries fail with an error telling you to run
  `build_index.py`.
- **Embedding cache.** Query-time embeddings are cached in
  `./.claude/cache/embeddings.pkl` to speed up repeated runs.
- **GPU / Ollama assumptions.** Embeddings default to CUDA and answers default to
  a local Ollama server; adjust `model_setup.py` for other setups.
