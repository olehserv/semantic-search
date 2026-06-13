# Production-Readiness Plan — Semantic Code Search

**Date:** 2026-06-11
**Status:** Phase 0 ✅ done (2026-06-11, merged). Phase 1 ✅ done
(branch `phase-1-search-service`, 2026-06-12; container stack verified with
compose on 2026-06-13). Phases 2–3 are open. Live status is in `/handoff.md`.
**Input:** `2026-06-11-architecture-review.md` (same directory) — the IDs
(C1…C4, H1…H7, M1…M11) point to findings in that document.
**Main rule:** every change to search quality must be checked with the eval
harness (`eval/run_eval.py`). Record the numbers before the change. Keep only
what makes the numbers better.

---

## Phase 0 — Quick fixes (small, do first) ✅ done

| # | Task | Fixes | Size |
|---|------|-------|------|
| 0.1 | Add root `requirements.txt` (pinned versions) + `requirements-dev.txt`. The content was already written in Task 1 of `docs/superpowers/plans/2026-06-01-continuous-search-service.md`. | H4 | S |
| 0.2 | Fix the BM25 broken-cache crash: in `get_bm25_retriever()`, never save an empty node list; if there are no nodes, raise a clear error that says to run `build_index.py`. Add a regression test. | C2 | S |
| 0.3 | `mcp_server.py`: use `sys.executable` and build the script path from `__file__`. (Temporary fix — Phase 1 rewrites this file anyway.) | C4 | S |
| 0.4 | `build_index.py`: add a `--force` / `-y` flag to skip the question; remove the `echo y` trick in `run_demo.sh`. | M5 | S |
| 0.5 | CI (GitHub Actions): run `pytest`, `ruff check`, and `py_compile` on every push. | H5 (part) | S |
| 0.6 | Fix the broken `TODO.md` links in `eval/README.md` / `run_eval.py` / `run_demo.sh`. | M9 | S |

**Done when:** a fresh clone + `pip install -r requirements.txt` + `pytest` is
green in CI; a first-run query fails with a helpful message instead of
breaking the cache.

## Phase 1 — Make the agent integration real (the core value)

| # | Task | Fixes | Size |
|---|------|-------|------|
| 1.1 | Rewrite `mcp_server.py` with the official `mcp` Python SDK (`FastMCP`, stdio transport): one `search_codebase(query: str)` tool with a typed schema. Replace the root `mcp.json` with a correct `.mcp.json` (`mcpServers` format). Test it end-to-end from Claude Code. | C1 | M |
| 1.2 | Build the **approved** long-running search service (`docs/superpowers/specs/2026-06-01-continuous-search-service-design.md`): a Flask `service.py` with a warm engine; BM25 rebuilt in memory from Qdrant at startup (the BM25 pickle file is removed); `qdrant.py` configured by env vars (`QDRANT_HOST`/`QDRANT_PORT`/`QDRANT_AUTOSTART`); Dockerfile + compose service. The step-by-step plan already exists in `docs/superpowers/plans/`. | C3, M2 (bm25), M10 | L |
| 1.3 | Connect the new MCP tool to the service (`POST /search`), with a clear error when the service is down. | C1+C3 | S |

**Done when:** Claude Code can list and call `search_codebase`; a second query
returns in well under one second (warm engine); `curl /health` works.

## Phase 2 — Search quality (checked with the eval)

Before starting: record the baseline numbers on the sample corpus **and** on
one real .NET project. Change one thing per eval run.

| # | Task | Fixes | Size |
|---|------|-------|------|
| 2.1 | Replace the min-max score mixing with **Reciprocal Rank Fusion** across the three retrievers (and across the question variants). | H1 | M |
| 2.2 | Stop cutting candidates before ranking (`ordered_nodes[:30]` in arrival order) — score first, then cut. | H2 | S |
| 2.3 | Make the query variants configurable; remove the fixed `".NET core backend"` suffix. | H7 (part) | S |
| 2.4 | Code-aware chunking for C# (tree-sitter or Roslyn: cut on type/method borders, keep signatures with bodies, add namespace/class metadata). Expected to be the biggest single quality win. | M8 | L |
| 2.5 | Grow `eval/golden.jsonl` to 30–50 questions on a real codebase (the eval README explains how). Add the eval as a manual CI job with a saved baseline report. | H5 (quality) | M |
| 2.6 | Optional, after measuring 2.1–2.4: a cross-encoder re-ranker (e.g. `BAAI/bge-reranker-base`) instead of the hand-made cosine re-rank. **Done (2026-06-13):** added `CROSS_ENCODER_MODEL` (off by default); MiniLM-L6 + RRF blend beats the cosine re-rank (MRR 0.716 → 0.751) but costs ~1 s/query on CPU, so it ships opt-in. Pure cross-encoder (no RRF) was worse. | — | M |

**Done when:** Recall@5 and nDCG@5 are clearly above the recorded baseline;
every merged change has a before/after eval report.

## Phase 3 — Operations hardening

| # | Task | Fixes | Size |
|---|------|-------|------|
| 3.1 | Config layer (env vars, e.g. `pydantic-settings`): collection name, Qdrant host/port, model names, cache paths. Give the eval demo its own collection so `run_demo.sh` can never delete a real index. | H7 | M |
| 3.2 | Embedding cache v2: key = `(model_name, text_hash)`, size limit (LRU), one disk write per query, drop the cache when the model changes. Consider SQLite instead of pickle. | H3, M2, M7 | M |
| 3.3 | Safe index rebuild: build into `<collection>-<timestamp>`, switch a Qdrant alias on success, delete the old one. Rebuilds become crash-safe with zero downtime. | H6 | M |
| 3.4 | Incremental indexing: track file content hashes; re-embed only changed files; remove vectors of deleted files. **Done (2026-06-13):** `--incremental` flag stores a `file_hash` per file in the Qdrant payload (excluded from embedding, so vectors/eval are unchanged), diffs disk vs the live index, and updates it in place (re-embed changed/new, delete removed). In-place trade-off vs the 3.3 alias swap; full rebuild stays the crash-safe default. | M6 | L |
| 3.5 | Replace `print` logging with the `logging` module (levels, structure); remove the replaced `print` built-in. **Done (2026-06-13):** new `scripts/logging_setup.py` (`setup_logging()` → stderr, level from `LOG_LEVEL`, default INFO); all script diagnostics use `logging.getLogger(__name__)`; removed the `print=functools.partial(...)` shim in query_index.py (stdout stays pure JSON because logging defaults to stderr); ask.py's `DEBUG` flag dropped. Eval table + build prompt intentionally stay `print`. | M11 | S |
| 3.6 | Pipeline test suite: unit tests for fusion/ranking with fake retrievers, an integration test against a throwaway Qdrant container (testcontainers), an MCP protocol test. | H5 | L |
| 3.7 | Lazy initialization — no model loading or cache reading at import time (use init functions or lazy singletons). Then tests no longer need the `model_setup` fake. | M1 | M |
| 3.8 | Security pass: Qdrant API key for non-local use, no pickle loads left, request size limits on `/search`. | M2, M10 | M |
| 3.9 | `ask.py` cleanup: logs to stderr, larger context budget (count tokens, not chars), replace the phrase-matching quality check with a structured self-check or remove the retry. | M3, M4 | M |

**Done when:** the system can be deployed with `docker compose up` and env-only
config; index rebuilds never destroy data; logs are structured; the pipeline
is covered by tests in CI.

## Why this order

1. **Phase 0 first** — reproducible installs and CI make every later change
   checkable; the C2 fix is tiny and removes the worst first-run experience.
2. **Phase 1 before Phase 2** — tuning quality makes no sense while every
   query takes ~30 s to start and no real agent can call the tool. Phase 1 is
   also already designed and planned; it is pure execution.
3. **Phase 2 is eval-gated** — this is the project's own stated method
   ("a change is an improvement only if the metrics say so"), and the eval
   harness already exists.
4. **Phase 3 last** — most items (safe rebuild, incremental indexing) only
   matter after the Phase 1 service architecture exists.

## Out of scope (for now)

- Languages other than .NET (but the chunker in 2.4 should keep the language
  pluggable).
- Automatic index refresh inside the service (file watching) — revisit after 3.4.
- An `/ask` LLM endpoint in the service — the design doc already rejected it
  (YAGNI).
- Scaling past one service instance (the warm-singleton design assumes one
  worker; fine for an internal team tool).
