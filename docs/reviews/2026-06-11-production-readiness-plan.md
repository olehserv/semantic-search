# Production-Readiness Plan — Semantic Code Search

**Date:** 2026-06-11
**Status:** Phase 0 ✅ complete (branch `phase-0-fixes`, 2026-06-11) — including
the optional 0.3. Phases 1–3 open. Current state lives in `/handoff.md`.
**Input:** `2026-06-11-architecture-review.md` (same directory) — finding IDs
(C1…C4, H1…H7, M1…M11) refer to that document.
**Principle:** every retrieval-quality change must be validated by the eval
harness (`eval/run_eval.py`) — record a baseline first, keep only what moves
the metrics.

---

## Phase 0 — Stop the bleeding (small, do first)

| # | Task | Fixes | Effort |
|---|------|-------|--------|
| 0.1 | Add root `requirements.txt` (pinned) + `requirements-dev.txt`; consider `pyproject.toml`. The unexecuted Task 1 of `docs/superpowers/plans/2026-06-01-continuous-search-service.md` already specifies the contents. | H4 | S |
| 0.2 | Fix the BM25 poisoned-cache crash: in `get_bm25_retriever()`, never persist an empty node list; if no nodes are available, raise a clear error telling the user to run `build_index.py`. Add a regression test. | C2 | S |
| 0.3 | `mcp_server.py`: use `sys.executable` and resolve the script path from `__file__`. (Interim fix — the file is rewritten in Phase 1 anyway; skip if Phase 1 starts immediately.) | C4 | S |
| 0.4 | `build_index.py`: add `--force` / `-y` flag to skip the interactive prompt; remove the `echo y` workaround in `run_demo.sh`. | M5 | S |
| 0.5 | CI (GitHub Actions): `python -m pytest tests/ -q`, `ruff check`, `python -m py_compile scripts/*.py eval/*.py` on every push. | H5 (partly) | S |
| 0.6 | Restore the lost `TODO.md` or update `eval/README.md` / `run_eval.py` / `run_demo.sh` to point at the docs that exist. | M9 | S |

**Exit criteria:** fresh clone + `pip install -r requirements.txt` + `pytest`
green in CI; first-run query fails with a helpful message instead of poisoning
the cache.

## Phase 1 — Make the agent integration real (core value)

| # | Task | Fixes | Effort |
|---|------|-------|--------|
| 1.1 | Rewrite `mcp_server.py` using the official `mcp` Python SDK (`FastMCP`, stdio transport): a `search_codebase(query: str)` tool with a typed schema. Replace root `mcp.json` with a correct `.mcp.json` (`mcpServers` format). Verify end-to-end from Claude Code. | C1 | M |
| 1.2 | Implement the **approved** continuous-search-service design (`docs/superpowers/specs/2026-06-01-continuous-search-service-design.md`): Flask `service.py` with warm engine, BM25 rebuilt in-memory from a Qdrant scroll at startup (deletes the BM25 pickle entirely), env-driven `qdrant.py` (`QDRANT_HOST`/`QDRANT_PORT`/`QDRANT_AUTOSTART`), Dockerfile + compose service. The step-by-step plan already exists in `docs/superpowers/plans/`. | C3, M2 (bm25), M10 | L |
| 1.3 | Point the new MCP tool at the service (`POST /search`), with a structured error when the service is down. | C1+C3 integration | S |

**Exit criteria:** Claude Code lists and calls `search_codebase` successfully;
second query returns in well under a second (warm engine); `curl /health` works.

## Phase 2 — Retrieval quality (eval-gated)

Record the baseline on the sample corpus **and** on one real .NET project
before starting. One change per eval run.

| # | Task | Fixes | Effort |
|---|------|-------|--------|
| 2.1 | Replace min-max pooled normalization with **Reciprocal Rank Fusion** across the three retrievers (and across query expansions). | H1 | M |
| 2.2 | Stop truncating candidates before ranking (`ordered_nodes[:30]` in insertion order) — fuse first, then cut. | H2 | S |
| 2.3 | Make query expansion configurable; remove the hardcoded `".NET core backend"` suffix. | H7 (partly) | S |
| 2.4 | Code-aware chunking for C# (tree-sitter or Roslyn-based: split on type/method boundaries, keep signatures with bodies, attach namespace/class metadata). Expected to be the biggest single quality win. | M8 | L |
| 2.5 | Expand `eval/golden.jsonl` to 30–50 labelled queries on a real codebase (the eval README already prescribes how). Add the eval as a manual-trigger CI job with a stored baseline report. | H5 (quality side) | M |
| 2.6 | Optional, after 2.1–2.4 measure: cross-encoder re-ranker (e.g. `BAAI/bge-reranker-base`) instead of the hand-rolled cosine re-rank. | — | M |

**Exit criteria:** Recall@5 and nDCG@5 measurably above the recorded baseline;
every merged change has a before/after eval report.

## Phase 3 — Operational hardening

| # | Task | Fixes | Effort |
|---|------|-------|--------|
| 3.1 | Config layer (env vars, e.g. `pydantic-settings`): collection name, Qdrant host/port, embedding + LLM model names, cache paths. Separate collection for the eval demo so `run_demo.sh` can never destroy a real index. | H7 | M |
| 3.2 | Embedding cache v2: key by `(model_name, text_hash)`, bound with LRU, batch the disk write once per query, drop the cache file on model mismatch. Consider SQLite instead of pickle. | H3, M2, M7 | M |
| 3.3 | Blue-green index rebuild: build into `<collection>-<timestamp>`, flip a Qdrant alias on success, drop the old collection. Rebuild becomes crash-safe and zero-downtime. | H6 | M |
| 3.4 | Incremental indexing: track file content hashes; re-embed only changed files; delete vectors of removed files. | M6 | L |
| 3.5 | Replace `print`-logging with the `logging` module (structured, level-controlled); remove the shadowed `print` builtin. | M11 | S |
| 3.6 | Pipeline test suite: unit tests for fusion/ranking with fake retrievers, an integration test against a throwaway Qdrant container (testcontainers), MCP protocol-level test. | H5 | L |
| 3.7 | Lazy initialization — kill import-time model loading and cache warmup (init function or lazy singletons). Lets tests drop the `model_setup` stub. | M1 | M |
| 3.8 | Security pass: Qdrant API key support for non-local deployments, no remaining pickle loads, request size limits on `/search`. | M2, M10 | M |
| 3.9 | `ask.py` cleanup: stderr for diagnostics, raise `MAX_CONTEXT_CHARS` (token-based budget), replace substring-based insufficiency detection with a structured self-check or drop the retry. | M3, M4 | M |

**Exit criteria:** deployable via `docker compose up` with env-only config;
index rebuilds are non-destructive; logs are structured; pipeline covered by
tests in CI.

## Sequencing rationale

1. **Phase 0 before everything** — reproducible installs and CI make every
   later change verifiable; the C2 fix is tiny and removes the worst first-run
   experience.
2. **Phase 1 before Phase 2** — quality tuning is pointless while every query
   pays a ~30 s cold start and no real agent can call the tool at all. Phase 1
   is also already designed and planned; it is pure execution.
3. **Phase 2 is eval-gated** — this is the project's own stated methodology
   ("a change is an improvement only if the metrics say so") and the eval
   harness already exists.
4. **Phase 3 hardening last** — most items (blue-green, incremental indexing)
   only matter once the service architecture from Phase 1 is in place.

## Explicitly out of scope (for now)

- Multi-language support beyond .NET (the chunker work in 2.4 should keep the
  language pluggable, though).
- Automatic index refresh inside the service (file watching) — revisit after 3.4.
- Service-side `/ask` LLM endpoint — the design doc already ruled it YAGNI.
- Scaling beyond a single service instance (the warm-singleton design assumes
  one worker; fine for a per-team internal tool).
