# Project Handoff — Semantic Code Search

> Living document. Update the **Status log** and **Next actions** at the end
> of every working session, so anyone (human or agent) can continue the work
> without extra context.

**Last updated:** 2026-06-13 (task 3.1 — config layer)
**Repo state:** branch `phase-3-config-layer`. Merged into `main` so far:
PRs #1–#8 (Phase 0 through Phase 2, ending with task 2.6 — optional
cross-encoder re-ranker).

---

## What this project is

Hybrid semantic + keyword (BM25) code search over a .NET codebase. It uses
Qdrant and a local HuggingFace embedding model (`BAAI/bge-base-en-v1.5`),
with an optional Ollama Q&A layer. The goal is to give AI agents (Claude
Code) a `search_codebase` MCP tool. See `README.md` for usage.

## Current state (2026-06-11)

| Area | State |
|------|-------|
| Indexing (`scripts/build_index.py`) | Works. `--force` flag for scripts (Phase 0.4). C# files get code-aware chunks from `scripts/chunking.py` (task 2.4, tree-sitter); other files keep the `SentenceSplitter`. Crash-safe rebuild via a Qdrant alias swap (task 3.3, H6): builds into `<name>-<timestamp>`, swaps the alias, deletes the old one — no destructive delete-first. No incremental mode yet (task 3.4). |
| Search (`scripts/query_index.py` + `scripts/ranking.py`) | Works after a build. BM25 is rebuilt **in memory from Qdrant** (no `bm25.pkl` anymore); an empty collection gives a clear "run build_index.py" error. Scoring math in `ranking.py` (pure numpy, tested). Fusion is RRF over ranks since task 2.1 (H1 fixed; candidates cut after fusion, H2 fully closed — 2.2 found the cut never bites). Optional cross-encoder re-ranker (task 2.6) behind `CROSS_ENCODER_MODEL`, **off by default**. |
| Search service (`scripts/service.py`) | **New (Phase 1).** Flask, warm engine. Verified on host (first query 0.65 s, second 0.10 s — was ~30 s) **and in Docker** (2026-06-13): compose stack up, same top result + score as host, BM25 nodes loaded from Qdrant inside the container, warm queries 0.25 s, restart loads the model from the `hf_models` volume (no re-download), engine built once per process. |
| Q&A (`scripts/ask.py`) | Works with local Ollama (`llama3`). Small context budget, weak retry logic. |
| Agent integration (`scripts/mcp_server.py`, `.mcp.json`) | **Real MCP server (Phase 1).** Official `mcp` SDK, FastMCP, stdio; one tool `search_codebase` that forwards to the service over HTTP with a timeout. Verified end-to-end with an MCP stdio client: initialize → tools/list → tools/call returns ranked results; service down → structured `{"error", "hint"}`. Legacy `mcp.json` deleted. |
| Evaluation (`eval/`) | **Good.** Golden-set harness (Recall@k, MRR, nDCG@k), 34 passing unit tests (heavy tests skip without the ML stack), end-to-end demo. Demo checked 2026-06-11: 1.000 on all metrics for the 8 toy questions — the toy set is saturated. A real .NET project (LookingForMentor) sits in `eval/sample_real/` (committed to the repo since PR #3) with its own golden set `eval/golden_real.jsonl`; baseline reports live in `eval/baselines/`. |
| Dependencies | `requirements.txt` (pinned, now incl. `flask`, `mcp`) + `requirements-dev.txt`; `eval/requirements-eval.txt` points to the root file. Local `.venv/` gitignored. |
| CI | GitHub Actions `ci.yml`: ruff + py_compile + pytest (+ `requests`, so the MCP shim tests run); heavy tests skip without the ML stack. Plus `eval.yml` (task 2.5): a **manual** (`workflow_dispatch`) eval gate — Qdrant service container, CPU torch, sample-corpus eval, fails on regression vs the saved baseline, uploads the report artifact. |
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

- ~~`eval/run_demo.sh` can delete a real index~~ **Closed (task 3.1, H7).**
  `run_demo.sh` now `export`s a dedicated `QDRANT_COLLECTION=demo-eval` before
  building, overriding any value in the caller's environment, so the demo can
  only ever (re)build its own throwaway collection — never a real index such as
  `lfm`.
- ~~Changing the embedding model silently mixes old and new vectors~~
  **Closed (task 3.2, H3).** The cache is now SQLite keyed by
  `(model_name, text_hash)` (`scripts/embedding_cache.py`), so a different
  model simply misses and recomputes — vectors from two models can never be
  confused. The cache also has an LRU size limit and writes once per query.
- Importing `query_index` or `model_setup` does heavy work (model load) —
  tests replace `model_setup` with a fake in `tests/conftest.py`. The
  embedding cache no longer opens its database at import (it is built lazily
  on first query).
- The whole stack now runs compose-managed (project `ai-agent`):
  `docker compose -f scripts/docker-compose.yml -p ai-agent up -d`.
  Both containers use `restart: unless-stopped` and are left running.

## Next actions (in order)

1. **Review + merge the 2.6 PR** (branch `spike/cross-encoder-rerank`): optional
   cross-encoder re-ranker, off by default; `blend_cross_encoder()` + tests, new
   baseline, docs. Default behavior unchanged (gate: 0.912/0.716/0.760 reproduced).
2. **Continue Phase 3 (operations hardening).** Tasks 3.1 (config layer), 3.2
   (embedding cache v2), and 3.3 (safe index rebuild) are done; next up is 3.4
   (incremental indexing) — see the plan table in
   `docs/reviews/2026-06-11-production-readiness-plan.md`. Phase 2 is complete
   (all of 2.1–2.6 done; "done when" met: Recall@5 0.600 → 0.912, nDCG@5 0.343 →
   0.760, a report per change). Remaining Phase 3 work: incremental indexing,
   structured logging, the pipeline test suite, lazy init, the security pass,
   and ask.py cleanup.

## How to verify the project right now

```bash
.venv/bin/python -m pytest tests/ -q   # 77 tests (heavy ones skip without the ML stack)
.venv/bin/ruff check scripts/ eval/ tests/
bash eval/run_demo.sh                  # full e2e: venv + Qdrant + index + eval
```

## Status log

- **2026-06-13 (task 3.1 — config layer, H7)** — New `scripts/config.py`: a
  pydantic-settings `Settings` class + module singleton `settings` that reads all
  deployment config from env vars in one typed place (Qdrant host/port/collection,
  autostart, embedding + LLM model, cache path, rerank cut, cross-encoder model,
  service port, query-variant suffixes). Env var names kept bare (no prefix) so
  docker-compose and docs are unchanged; three fields use an explicit
  `validation_alias` (`QDRANT_COLLECTION`, `QUERY_VARIANT_SUFFIXES`, `PORT`).
  `QUERY_VARIANT_SUFFIXES` stays a `str` + a `query_variant_suffixes` property
  (pydantic would otherwise JSON-parse a list field and reject
  `implementation,.NET core backend`); the property reproduces the old inline
  split exactly. `qdrant.py`, `model_setup.py`, `query_index.py`, `service.py`
  now source their values from `settings` but keep their existing public names
  (e.g. `qdrant.COLLECTION_NAME`), so callers are untouched — minimal diff. Pure
  tuning constants (`RRF_K`, `CROSS_ENCODER_WEIGHT`, `MAX_CONTEXT_CHARS` — the
  last owned by task 3.9) deliberately left in place. `config.py` is import-cheap
  (only pydantic-settings, no torch/llama-index), so the conftest `model_setup`
  stub is unaffected. **H7 closed:** `eval/run_demo.sh` now `export`s
  `QDRANT_COLLECTION=demo-eval`, so the demo can never delete a real index.
  `pydantic`/`pydantic-settings` pinned in `requirements.txt`. New
  `tests/test_config.py` (defaults, env overrides, comma-split edge case); 64
  tests green, ruff clean. `QDRANT_AUTOSTART` is now a pydantic bool (`0`/`1` →
  `False`/`True`, same as the old `!= "1"` check). Eval numbers unchanged
  (consolidation only).

- **2026-06-13 (task 2.6 — optional cross-encoder re-ranker)** — Investigated and
  productionized **off by default**. `CROSS_ENCODER_MODEL` env var (empty = off):
  when set, the candidate pool is re-scored by a cross-encoder blended with RRF
  (`blend_cross_encoder()` in `ranking.py`, weight 0.85, unit-tested). Findings on
  the real `lfm` eval: `ms-marco-MiniLM-L-6-v2` + RRF beats the cosine default
  (Recall@5 0.912 → 0.925, MRR 0.716 → **0.751**, nDCG@5 0.760 → **0.782**) for
  ~1 s/query on CPU (vs ~0.16 s); `bge-reranker-base` is marginally better
  (0.938/0.747/0.786) but ~5.9 s/query — not worth it. **Pure cross-encoder
  (no RRF) is worse** (0.875/0.686/0.721): it drops the BM25/keyword signal, so it
  is always blended. Left off by default because the gain is modest and the CPU
  latency cost is real; the warm 0.16 s default is preserved. New baseline
  `eval/baselines/2026-06-13-real-cross-encoder.json`; `sentence-transformers`
  pinned in `requirements.txt`. 59 tests green, ruff clean. **Phase 2 complete.**
- **2026-06-13 (task 2.2 check + task 2.5 — eval CI)** — Two Phase 2 leftovers
  closed. **2.2:** the post-fusion candidate cut (`[:30]` in `query_index.py`) is
  now `RERANK_CANDIDATES` (env var, default 30). Swept 30/50/100/9999 on the real
  `lfm` index — **all identical** (Recall@5 0.912 / MRR 0.716 / nDCG@5 0.760),
  because the unique candidate pool per query is only ~16–23, always under 30. So
  the cut does not hurt recall (H2 fully closed); the knob stays for future
  retriever widening. No new baseline (numbers unchanged). **2.5:** new manual
  workflow `.github/workflows/eval.yml` (`workflow_dispatch`): Qdrant service
  container + CPU torch, indexes the sample corpus, runs the eval, fails on any
  drop below the saved sample baseline, uploads the report as an artifact. Sample
  corpus reproduces 1.000; 55 tests green, ruff clean. `eval/README.md` documents
  both. **Phase 2 is now complete** except the optional 2.6 (cross-encoder),
  which is deliberately not done.
- **2026-06-13 (task 2.4 — code-aware chunking)** — New `scripts/chunking.py`
  (tree-sitter + tree-sitter-c-sharp, pinned): C# files are cut on
  type/member borders, signatures stay with bodies, every chunk carries
  namespace/type/member metadata and a `// namespace …` header line; nested
  big types recurse, oversized members line-split, parse failures fall back
  to the old `SentenceSplitter` (all 238 real files parse — zero fallbacks).
  **Eval gate passed, biggest win so far:** real project Recall@5
  0.787 → **0.912**, MRR 0.526 → **0.716**, nDCG@5 0.585 → **0.760**
  (`2026-06-13-real-chunking.json`); sample corpus rebuilt, stays 1.000.
  Both collections re-indexed (330 nodes for `lfm`). 55 tests green.
- **2026-06-13 (task 2.3 — configurable query variants)** — The hard-coded
  `".NET core backend"` suffix is gone from `expand_query()` (H7 part);
  variants now come from the `QUERY_VARIANT_SUFFIXES` env var (default
  `implementation`). Eval: with
  `QUERY_VARIANT_SUFFIXES="implementation,.NET core backend"` the numbers
  reproduce task 2.1 exactly (0.787 / 0.526 / 0.585 —
  `2026-06-13-real-variants.json`); the generic default scores 0.738 /
  0.498 / 0.555 on this .NET corpus, so the suffix is a real domain hint —
  documented in `eval/README.md` that baseline comparisons must set it.
  Sample corpus stays 1.000 either way. 49 tests green.
- **2026-06-13 (task 2.1 — RRF fusion)** — Min-max score mixing replaced by
  Reciprocal Rank Fusion (finding H1): `rrf_scores()` in `ranking.py` fuses
  the 9 ranked lists (3 retrievers × 3 query variants, original query
  weighted 1.5) by rank only; `score_candidates()` now blends
  `alpha * cosine + (1 - alpha) * RRF`. The old `freq_boost` and
  `make_normalizer` are gone (RRF covers both), and the top-30 candidate cut
  happens after fusion instead of arrival order (most of H2). **Eval gate
  passed:** real project Recall@5 0.600 → **0.787**, MRR 0.263 → **0.526**,
  nDCG@5 0.343 → **0.585** (`eval/baselines/2026-06-13-real-rrf.json`);
  sample corpus stays 1.000. 47 tests green. Also: LookingForMentor is now
  committed to the repo (Oleh, PR #3 follow-up) — docs updated.
- **2026-06-13 (Phase 2 baselines)** — Real .NET project (LookingForMentor,
  ~238 .cs files, CQRS + Blazor) added by Oleh at `eval/sample_real/`
  (gitignored). `QDRANT_COLLECTION` env var added (default `demo`), so the
  real index lives in its own collection `lfm` and the demo stays safe.
  Wrote `eval/golden_real.jsonl` (40 questions; labels verified unique by
  file name). Indexed (261 docs → 335 nodes) and recorded baselines in
  `eval/baselines/`: **real project Recall@5 0.600, MRR 0.263, nDCG@5
  0.343** (`2026-06-13-real.json`); sample corpus still 1.000 on all
  metrics (`2026-06-13-sample.json`, saturated). These are the numbers
  Phase 2 tasks 2.1–2.4 must beat.
- **2026-06-13 (container verification)** — Compose plugin installed (v5.1.1)
  by Oleh; Phase 1's deferred step finished. Dockerfile fixed to install CPU
  torch (the PyPI default is the multi-GB CUDA build); obsolete `version:`
  removed from compose. Manually-run Qdrant replaced by the compose-managed
  stack; sample index rebuilt. Verified in the container: same search results
  as host, BM25 from Qdrant, warm 0.25 s queries, MCP e2e passes, restart
  uses the model volume (no re-download), engine built once per process.
  Stack left running (project `ai-agent`).
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
