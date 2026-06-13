# 🔍 Semantic Code Search

Ask your .NET codebase questions in plain English — and get the right files back. 🎯

It mixes two kinds of search so you get the best of both:

- 🧠 **Semantic search** — finds code by *meaning* (using vectors), so "where do we check the password?" matches `LoginService` even without those exact words.
- 🔑 **Keyword search (BM25)** — finds *exact* matches like a class or method name.

An AI agent (like **Claude Code**) can use it as a tool to explore your code for you. Vectors live in a [Qdrant](https://qdrant.tech/) database. 🗂️

> 🆕 New to the ideas here (embeddings, cosine similarity, BM25, RRF, re-ranking)?
> Start with [`docs/CONCEPTS.md`](docs/CONCEPTS.md) — it explains each one in plain English with analogies.

---

## 📦 What you need

| | |
|---|---|
| 🐍 **Python 3.11+** | `pip install -r requirements.txt` (use `requirements-dev.txt` for tests/lint too) |
| 🐳 **Docker** | Qdrant starts itself via `docker compose`. No compose plugin? Start it by hand (see below). |
| ⚡ **GPU** | Optional — uses CUDA if present, otherwise CPU. Fully automatic. |
| 💬 **[Ollama](https://ollama.com/)** | Only for `ask.py` (the written-answer feature). Default model: `llama3`. |

No compose plugin? Run Qdrant directly:

```bash
docker run -d --name qdrant-local -p 6333:6333 -v qdrant_storage:/qdrant/storage qdrant/qdrant
```

---

## 🚀 Quick start

Copy the scripts into your project under `.claude/scripts/`, then run them **from your project's root folder** (paths like `./.claude/cache/...` are relative to where you run them).

### 1️⃣ Build the index

Turn your code into searchable vectors. Run this once to start, and again when the code changes.

```bash
python .claude/scripts/build_index.py              # asks before replacing an existing index
python .claude/scripts/build_index.py --force      # replace without asking (good for scripts/CI)
python .claude/scripts/build_index.py --incremental # only re-do changed / new / deleted files (fast)
```

> 🔁 **Full build vs `--incremental`** — A full build is the safe default: it builds a brand-new index and only swaps it in when it finishes, so a crash can never destroy your live index. `--incremental` is much faster for small changes — it re-embeds only the files you touched and drops files you deleted. Use a full build after big changes; use `--incremental` day-to-day. (If an incremental run is interrupted, just run it again.)

### 2️⃣ Start the search service

Loads the model **once** and stays warm, so every search after the first is fast (well under a second). ⚡

```bash
python .claude/scripts/service.py        # serves on http://localhost:8000
```

### 3️⃣ Search 🔎

```bash
curl -XPOST localhost:8000/search -H 'Content-Type: application/json' \
     -d '{"query":"where is authentication handled"}'
```

You get back ranked results as JSON — `sources` (the matching files) and `context` (each snippet with its score and rank):

```json
{
  "answer": "Top 8 relevant code snippets retrieved. See context for details.",
  "sources": ["LoginService.cs", "LoginUserCommand.cs"],
  "context": [
    {
      "rank": 1,
      "file": "LoginService.cs",
      "path": "src/Auth/LoginService.cs",
      "score": 0.873,
      "text": "public bool VerifyPassword(string user, string pw) { ... }"
    }
  ]
}
```

(The `text` field is the code snippet, trimmed to ~800 characters.)

> 💡 No service running? A one-off search still works — it just pays a slow cold start each time:
> ```bash
> python .claude/scripts/query_index.py "where is authentication handled"
> ```

### 4️⃣ (Optional) Ask for a written answer 💬

Sends the best snippets to a local LLM (Ollama) and writes an answer in words.

```bash
python .claude/scripts/ask.py "how does the request pipeline work"
```

---

## 🤖 Use it from Claude Code (MCP)

This repo ships a `.mcp.json` that registers a `code-search` MCP server. With the service running (step 2 above), Claude Code can call the **`search_codebase`** tool directly. If the service is down, the tool replies with a clear error telling you how to start it. ✅

To use it in **another project**, copy the scripts to `.claude/scripts/` and add this to that project's `.mcp.json`:

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

That `command` just needs a Python with `mcp` and `requests` installed — the MCP shim is light (no torch, no llama-index). 🪶

---

## 🛠️ How it works (under the hood)

```text
  📥 INDEX  (run once, and again when code changes)
     .cs / .csproj / .sln ──► build_index.py ──► 🗂️  Qdrant
                                                  (vectors + chunk text)

  🔎 SEARCH  (every question)
     Claude Code ─MCP─► mcp_server.py ─HTTP─► service.py ──► 🗂️  Qdrant
                                              (warm engine:       │
                                               query_index +      ▼
                                               ranking)     🎯 ranked JSON

  🗣️ ASK  (optional)
     ask.py ──► search ──► 💬 Ollama ──► written answer
```

1. **📥 Index** (`build_index.py`) — reads `.cs`/`.csproj`/`.sln`/`.slnx` files, cuts them into chunks, turns each chunk into a vector with a local HuggingFace model, and stores the vectors (plus the text) in Qdrant.
2. **🔎 Search + rank** (`query_index.py`) — runs three searches (vector top-6, BM25, vector top-12) across a few variants of your question, removes duplicates, and gives each result a combined score (`ranking.py`). BM25 is rebuilt in memory from Qdrant.
3. **🔥 Serve** (`service.py`) — a long-running Flask service that builds the engine **once** and answers every `POST /search` from the warm engine.
4. **🤝 Agent access** (`mcp_server.py`) — a real MCP server (official `mcp` SDK, stdio) that gives agents the `search_codebase` tool and forwards each call to the service.
5. **🗣️ Answer** (`ask.py`) — sends the top snippets to a local LLM and returns a written answer, trimmed to a token budget (`MAX_CONTEXT_TOKENS`).

| File | Role |
|------|------|
| `scripts/build_index.py` | Build/rebuild the index (`--force` = no prompt; `--incremental` = changed files only) |
| `scripts/query_index.py` | Hybrid search + ranking; the CLI prints JSON |
| `scripts/ranking.py` | The scoring math (pure numpy, unit-tested) |
| `scripts/service.py` | Long-running search service with the warm engine |
| `scripts/mcp_server.py` | MCP server (stdio) — exposes `search_codebase` to agents |
| `scripts/ask.py` | Written LLM answers from the found code |
| `scripts/qdrant.py` | Qdrant connection + starting the container |
| `scripts/model_setup.py` | Loads the embedding model and the LLM (lazily) |
| `eval/` | Search-quality measurement (see `eval/README.md`) |

---

## 🔧 Configuration

All settings live in one typed place — `scripts/config.py` (pydantic-settings) — and come from environment variables. The defaults below are what ships, so it works out of the box. 👇

| Setting | Env var | Default |
|---------|---------|---------|
| Qdrant host | `QDRANT_HOST` | `localhost` |
| Qdrant port | `QDRANT_PORT` | `6333` |
| Qdrant collection | `QDRANT_COLLECTION` | `demo` |
| Qdrant autostart (Docker) | `QDRANT_AUTOSTART` | `1` (on) |
| Qdrant API key | `QDRANT_API_KEY` | `""` (off; set it for a secured/remote Qdrant) |
| Qdrant HTTPS | `QDRANT_HTTPS` | `0` (set `1` for TLS/remote Qdrant, e.g. Qdrant Cloud) |
| Embedding model | `EMBED_MODEL` | `BAAI/bge-base-en-v1.5` |
| LLM model (ask.py) | `LLM_MODEL` | `llama3` |
| Embedding cache path | `EMB_CACHE_PATH` | `./.claude/cache/embeddings.db` |
| Re-rank candidate cut | `RERANK_CANDIDATES` | `30` |
| Retriever-results cache size | `RETRIEVE_CACHE_SIZE` | `256` (LRU cap on the warm per-query cache) |
| Cross-encoder re-ranker | `CROSS_ENCODER_MODEL` | `""` (off) |
| Service port | `PORT` | `8000` |
| Max `/search` body size | `MAX_CONTENT_LENGTH` | `65536` bytes (bigger → HTTP 413) |
| ask.py context budget | `MAX_CONTEXT_TOKENS` | `4000` (tokens of code sent to the LLM) |
| Query variants | `QUERY_VARIANT_SUFFIXES` | `implementation` (comma-separated; add domain terms, e.g. `implementation,.NET core backend`) |
| Log level | `LOG_LEVEL` | `INFO` (set `DEBUG` for per-step traces; logs go to stderr) |

A few things that live in code, not env vars:

| Thing | Where |
|-------|-------|
| Embedding device (CPU/GPU auto) | `model_setup.py` |
| Which file types are indexed | `build_index.py` (`load_documents`) |
| Chunking | `chunking.py` (C# cut on type/method borders via tree-sitter; other files use `SentenceSplitter`) |
| Search depth, score weights | `query_index.py` / `ranking.py` |

---

## 💡 Good to know

- 🏗️ **Build before you search.** If the Qdrant collection is empty, search stops with a clear "run `build_index.py` first" message.
- 🐳 **Run it in Docker.** `Dockerfile` + the `search-service` entry in `scripts/docker-compose.yml` run the service in a container. Two codebases can share one Qdrant by using different `QDRANT_COLLECTION` names.
- 🧠 **The embedding cache is safe to keep.** Query-time embeddings are cached in `./.claude/cache/embeddings.db` (SQLite), keyed by `(model name, text)`. Change the embedding model and it simply recomputes — no stale vectors, no manual cleanup needed.
- 📋 **Project status & history.** The code review, decisions, and roadmap are in `docs/reviews/`; the live state of the work is in [`handoff.md`](handoff.md).
