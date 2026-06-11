# Project Handoff — Semantic Code Search

> Living document. Update the **Status log** and **Next actions** at the end
> of every working session, so anyone (human or agent) can continue the work
> without extra context.

**Last updated:** 2026-06-11 (Phase 0 + refactoring + docs done)
**Repo state:** branch `phase-0-fixes`, pushed; PR open against `main`:
https://github.com/olehserv/semantic-search/pull/1

---

## What this project is

Hybrid semantic + keyword (BM25) code search over a .NET codebase. It uses
Qdrant and a local HuggingFace embedding model (`BAAI/bge-base-en-v1.5`),
with an optional Ollama Q&A layer. The goal is to give AI agents (Claude
Code) a `search_codebase` MCP tool. See `README.md` for usage.

## Current state (2026-06-11)

| Area | State |
|------|-------|
| Indexing (`scripts/build_index.py`) | Works. `--force` flag for scripts (Phase 0.4). Still destructive (deletes the collection first), no incremental mode. |
| Search (`scripts/query_index.py` + `scripts/ranking.py`) | Works after a build. A query before the first build now fails with a clear error instead of breaking `bm25.pkl` (C2 fixed, with regression tests). The scoring math is extracted to `ranking.py` (pure numpy, 10 unit tests). Still open for Phase 2: min-max fusion over mixed scales, and candidates cut before ranking. |
| Q&A (`scripts/ask.py`) | Works with local Ollama (`llama3`). Small context budget, weak retry logic. |
| Agent integration (`scripts/mcp_server.py`, `mcp.json`) | **Still not usable by real MCP clients** — custom line protocol, not JSON-RPC/MCP; new process per query (slow cold start). Real fix is Phase 1. |
| Evaluation (`eval/`) | **Good.** Golden-set harness (Recall@k, MRR, nDCG@k), 34 passing unit tests (heavy tests skip without the ML stack), end-to-end demo. Demo checked 2026-06-11: 1.000 on all metrics for the 8 toy questions. |
| Dependencies | `requirements.txt` (pinned) + `requirements-dev.txt` at the root; `eval/requirements-eval.txt` points to the root file (Phase 0.1). Local `.venv/` is 5.2 GB, gitignored. |
| CI | GitHub Actions (`.github/workflows/ci.yml`): ruff + py_compile + pytest, without the ML stack (heavy tests skip themselves). |
| Docs | `README.md` is current. The design for the long-running search service is approved but **not built**: `docs/superpowers/specs/2026-06-01-continuous-search-service-design.md` + plan in `docs/superpowers/plans/`. |

## Key documents (read these first)

- `docs/reviews/2026-06-11-architecture-review.md` — the full review;
  findings are numbered C1–C4 (critical), H1–H7 (high), M1–M11 (medium).
- `docs/reviews/2026-06-11-production-readiness-plan.md` — the work plan in
  4 phases, with "done when" criteria.

## Decisions already made (do not reopen)

1. **A long-running search service instead of one process per query** —
   approved design, 2026-06-01 (Flask, warm engine, BM25 rebuilt in memory
   from Qdrant, no BM25 pickle file). The implementation plan exists;
   execute it, do not redesign it.
2. **Search tuning must pass the eval** — any change to chunking, fusion, or
   ranking needs better numbers from `eval/run_eval.py` before merging.
3. **No LLM inside the service** — `search_codebase` is search only;
   `ask.py` stays a separate CLI.
4. **Device auto-detect** — `cuda` if available, else `cpu` (already done in
   `model_setup.py`).

## Known risks

- `eval/run_demo.sh` rebuilds the Qdrant collection named in
  `scripts/qdrant.py` (`COLLECTION_NAME = "demo"`) — **it will delete a real
  index** that uses the same name.
- If you change the embedding model without deleting
  `./.claude/cache/embeddings.pkl`, old and new vectors get mixed silently
  (finding H3).
- Importing `query_index` or `model_setup` does heavy work (model load,
  cache reads) — tests replace `model_setup` with a fake in
  `tests/conftest.py`.
- This machine has no `docker compose` / `docker-compose`. Start Qdrant by
  hand: `docker run -d --name qdrant-local -p 6333:6333 -v
  qdrant_storage:/qdrant/storage qdrant/qdrant`.

## Next actions (in order)

1. **Review + merge PR #1** (Phase 0 + refactoring + docs):
   https://github.com/olehserv/semantic-search/pull/1
2. **Phase 1.1** — rewrite `mcp_server.py` with the official `mcp` SDK
   (FastMCP, stdio); replace `mcp.json` with a correct `.mcp.json`; test from
   Claude Code.
3. **Phase 1.2** — build the long-running search service (the plan in
   `docs/superpowers/plans/` is the work order).
4. Then Phase 2 (RRF fusion, code-aware chunking, golden set → 30–50
   questions), with recorded eval baselines.

## How to verify the project right now

```bash
.venv/bin/python -m pytest tests/ -q   # 34 tests (heavy ones skip without the ML stack)
.venv/bin/ruff check scripts/ eval/ tests/
bash eval/run_demo.sh                  # full e2e: venv + Qdrant + index + eval
```

## Status log

- **2026-06-11 (docs language)** — All documentation rewritten in plain
  English (B2 level) at Oleh's request. Same content, simpler sentences.
- **2026-06-11 (refactor + docs)** — Pure scoring math extracted to
  `scripts/ranking.py` with 10 tests that pin the current behavior (34 tests
  total, green). PEP8 cleanups in `qdrant.py`, plus a clear error when docker
  compose is missing. Verified no behavior change with the e2e demo (all
  metrics 1.000, same as before). README rewritten (CUDA is no longer
  claimed as required; `ranking.py`, `--force`, and the manual Qdrant start
  are documented), eval README shortened, plan/review marked with Phase 0
  status.
- **2026-06-11 (PR)** — Branch pushed, PR #1 opened, CI green on the first
  run (both push and pull_request events).
- **2026-06-11 (Phase 0)** — Phase 0 done on branch `phase-0-fixes`:
  0.1 pinned dependency files, 0.2 C2 fix + 5 regression tests, 0.3 robust
  process start, 0.4 `--force` flag, 0.5 CI workflow + lint cleanup, 0.6 doc
  link fixes.
- **2026-06-11 (review)** — Senior architecture review done. C2 (BM25 cache
  break) confirmed by reproduction. Review + plan written in `docs/reviews/`,
  this handoff file created. No code changed.
- **2026-06-01** — Long-running search service: design approved + step plan
  written (`docs/superpowers/`). Not built yet.
- **(earlier)** — Eval harness added (`b4c8540`); first pipeline (`d8e3326`).
