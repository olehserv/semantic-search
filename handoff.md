# Project Handoff — Semantic Code Search

> Living document. Update the **Status log** and **Next actions** at the end
> of every working session, so anyone (human or agent) can continue the work
> without extra context.

**Last updated:** 2026-06-12 (Phase 1 done)
**Repo state:** branch `phase-1-search-service`, pushed; PR open against
`main`: https://github.com/olehserv/semantic-search/pull/2
(PR #1 / Phase 0 is merged.)

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
| Search (`scripts/query_index.py` + `scripts/ranking.py`) | Works after a build. BM25 is rebuilt **in memory from Qdrant** (no `bm25.pkl` anymore); an empty collection gives a clear "run build_index.py" error. Scoring math in `ranking.py` (pure numpy, tested). Still open for Phase 2: min-max fusion over mixed scales, and candidates cut before ranking. |
| Search service (`scripts/service.py`) | **New (Phase 1).** Flask, warm engine. Verified on host: first query 0.65 s, second 0.10 s (was ~30 s per query). `GET /health`, `POST /search`, JSON errors (400/500). Docker files written but container not yet verified (no compose plugin on this machine). |
| Q&A (`scripts/ask.py`) | Works with local Ollama (`llama3`). Small context budget, weak retry logic. |
| Agent integration (`scripts/mcp_server.py`, `.mcp.json`) | **Real MCP server (Phase 1).** Official `mcp` SDK, FastMCP, stdio; one tool `search_codebase` that forwards to the service over HTTP with a timeout. Verified end-to-end with an MCP stdio client: initialize → tools/list → tools/call returns ranked results; service down → structured `{"error", "hint"}`. Legacy `mcp.json` deleted. |
| Evaluation (`eval/`) | **Good.** Golden-set harness (Recall@k, MRR, nDCG@k), 34 passing unit tests (heavy tests skip without the ML stack), end-to-end demo. Demo checked 2026-06-11: 1.000 on all metrics for the 8 toy questions. |
| Dependencies | `requirements.txt` (pinned, now incl. `flask`, `mcp`) + `requirements-dev.txt`; `eval/requirements-eval.txt` points to the root file. Local `.venv/` gitignored. |
| CI | GitHub Actions: ruff + py_compile + pytest (+ `requests`, so the MCP shim tests run). Heavy tests skip without the ML stack. |
| Docs | `README.md` is current (service + MCP usage). The 2026-06-01 service design is **implemented** (status noted in the spec). |

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

1. **Review + merge PR #2** (Phase 1):
   https://github.com/olehserv/semantic-search/pull/2
2. **Verify the container** on a machine with the docker compose plugin:
   `cd scripts && docker compose up -d --build search-service`, then the
   checklist in the old work order (Task 9).
3. **Phase 2** (eval-gated): record baselines, then RRF fusion (2.1), cut
   after scoring (2.2), configurable query variants (2.3), code-aware
   chunking (2.4), golden set → 30–50 questions (2.5).

## How to verify the project right now

```bash
.venv/bin/python -m pytest tests/ -q   # 34 tests (heavy ones skip without the ML stack)
.venv/bin/ruff check scripts/ eval/ tests/
bash eval/run_demo.sh                  # full e2e: venv + Qdrant + index + eval
```

## Status log

- **2026-06-12 (Phase 1)** — Real MCP server (FastMCP/stdio) + long-running
  Flask search service built on branch `phase-1-search-service`. BM25 now
  rebuilt in memory from Qdrant; `bm25.pkl` and the legacy `mcp.json` are
  gone. Verified: 43 unit tests green, eval still 1.000 on all metrics,
  service answers in 0.10–0.65 s warm, MCP e2e (initialize / tools/list /
  tools/call + service-down error) passes. Docker files written; container
  verification deferred (no compose plugin here).
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
