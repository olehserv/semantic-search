# Project Handoff — Semantic Code Search

> Living document. Update the **Status log** and **Next actions** at the end of
> every working session so anyone (human or agent) can pick the project up cold.

**Last updated:** 2026-06-11 (Phase 0 complete)
**Repo state:** branch `phase-0-fixes` (6 commits ahead of `main`), clean tree.
Awaiting decision: merge to `main` or open a PR.

---

## What this project is

Hybrid semantic + keyword (BM25) code search over a .NET codebase, backed by
Qdrant + a local HuggingFace embedding model (`BAAI/bge-base-en-v1.5`), with an
optional Ollama-powered Q&A layer, intended to be exposed to AI agents (Claude
Code) as an MCP tool. See `README.md` for usage.

## Current state (2026-06-11)

| Area | State |
|------|-------|
| Indexing (`scripts/build_index.py`) | Works. `--force` flag for non-interactive rebuilds (Phase 0.4). Still destructive (deletes collection first), no incremental mode. |
| Retrieval (`scripts/query_index.py`) | Works after a build. Pre-build queries now fail with an actionable error instead of poisoning `bm25.pkl` (C2 fixed, Phase 0.2, regression-tested). Fusion math still flawed (min-max over mixed scales); candidates truncated before re-rank — Phase 2. |
| Q&A (`scripts/ask.py`) | Works with local Ollama (`llama3`). Small context budget, brittle retry heuristic. |
| Agent integration (`scripts/mcp_server.py`, `mcp.json`) | **Still does not work with real MCP clients** — custom line protocol, not JSON-RPC/MCP; cold subprocess per query (now via `sys.executable` + `__file__`-relative path, Phase 0.3). Full fix is Phase 1. |
| Evaluation (`eval/`) | **Good.** Golden-set harness (Recall@k, MRR, nDCG@k), 22 passing unit tests (`.venv/bin/python -m pytest tests/ -q`; 5 skip without the ML stack), end-to-end demo (`bash eval/run_demo.sh`). Only 8 toy golden queries so far. |
| Dependencies | `requirements.txt` (pinned) + `requirements-dev.txt` at root; `eval/requirements-eval.txt` includes the root manifest (Phase 0.1). `.venv/` exists locally (5.2 GB, gitignored). |
| CI | GitHub Actions (`.github/workflows/ci.yml`): ruff + py_compile + pytest, no ML stack (heavy tests self-skip). Phase 0.5. |
| Docs | `README.md` solid. **Approved but UNIMPLEMENTED design** for a continuous search service: `docs/superpowers/specs/2026-06-01-continuous-search-service-design.md` + step-by-step plan in `docs/superpowers/plans/`. Several files reference a `TODO.md` that no longer exists. |

## Key review documents (read these first)

- `docs/reviews/2026-06-11-architecture-review.md` — full senior review;
  findings catalogued as C1–C4 (critical), H1–H7 (high), M1–M11 (medium).
- `docs/reviews/2026-06-11-production-readiness-plan.md` — prioritized
  4-phase execution plan with exit criteria.

## Key decisions already made (don't re-litigate)

1. **Long-lived search service over per-query subprocess** — approved design,
   2026-06-01 (Flask, warm engine, BM25 rebuilt in-memory from Qdrant scroll,
   no BM25 pickle). Implementation plan exists; execute it, don't redesign.
2. **Eval-gated retrieval tuning** — any change to chunking/fusion/ranking
   must show improvement on `eval/run_eval.py` metrics before merging.
3. **No LLM in the service** — `search_codebase` is retrieval-only; `ask.py`
   stays a separate CLI (YAGNI, per the design doc).
4. **Device auto-detect** — `cuda if available else cpu` (already implemented
   in `model_setup.py`).

## Known landmines

- `eval/run_demo.sh` rebuilds the Qdrant collection named in
  `scripts/qdrant.py` (`COLLECTION_NAME = "demo"`) — **it will wipe a real
  index** that uses the same name.
- Swapping the embedding model without deleting
  `./.claude/cache/embeddings.pkl` silently mixes vectors from two models
  (review finding H3).
- Importing `query_index` or `model_setup` has heavy side effects (model
  load, cache reads) — tests stub `model_setup` in `tests/conftest.py`.

## Next actions (in order — from the production-readiness plan)

1. **Merge/PR the `phase-0-fixes` branch** (Phase 0 is complete on it).
2. **Phase 1.1** — rewrite `mcp_server.py` on the official `mcp` SDK
   (FastMCP, stdio); replace `mcp.json` with a proper `.mcp.json`; verify from
   Claude Code.
3. **Phase 1.2** — implement the continuous-search-service design (the
   existing plan in `docs/superpowers/plans/` is the work order).
4. Then Phase 2 (RRF fusion, code-aware chunking, golden set → 30–50 queries),
   gated on recorded eval baselines.

## How to verify the project right now

```bash
.venv/bin/python -m pytest tests/ -q  # 22 tests (5 need the ML stack and skip elsewhere)
.venv/bin/ruff check scripts/ eval/ tests/
bash eval/run_demo.sh                 # full e2e: venv + Qdrant + index + eval
```

## Status log

- **2026-06-11 (later)** — Phase 0 executed on branch `phase-0-fixes` (Claude):
  0.1 pinned manifests, 0.2 C2 fix + 5 regression tests, 0.3 robust subprocess
  invocation, 0.4 `--force` flag, 0.5 CI workflow + lint cleanup, 0.6 doc
  reference fixes. 22 tests green, ruff clean. Not yet merged.
- **2026-06-11** — Senior architecture review completed (Claude). Confirmed
  C2 (BM25 cache poisoning) by reproduction. Produced review + production
  plan in `docs/reviews/`, created this handoff file. No code changed.
- **2026-06-01** — Continuous-search-service design approved + implementation
  plan written (`docs/superpowers/`). Not yet implemented.
- **(earlier)** — Eval harness added (`b4c8540`); initial pipeline (`d8e3326`).
