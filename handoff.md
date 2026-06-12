# Project Handoff — Semantic Code Search

> Living document. Update the **Status log** and **Next actions** at the end
> of every working session, so anyone (human or agent) can continue the work
> without extra context.

**Last updated:** 2026-06-13 (task 2.4 — code-aware chunking)
**Repo state:** branch `phase-2-code-chunking`. Merged into `main` so far:
PR #1 (Phase 0), PR #2 (Phase 1), PR #3 (baselines), PR #4 (RRF, task 2.1),
PR #5 (query variants, task 2.3).

---

## What this project is

Hybrid semantic + keyword (BM25) code search over a .NET codebase. It uses
Qdrant and a local HuggingFace embedding model (`BAAI/bge-base-en-v1.5`),
with an optional Ollama Q&A layer. The goal is to give AI agents (Claude
Code) a `search_codebase` MCP tool. See `README.md` for usage.

## Current state (2026-06-11)

| Area | State |
|------|-------|
| Indexing (`scripts/build_index.py`) | Works. `--force` flag for scripts (Phase 0.4). C# files get code-aware chunks from `scripts/chunking.py` (task 2.4, tree-sitter); other files keep the `SentenceSplitter`. Still destructive (deletes the collection first), no incremental mode. |
| Search (`scripts/query_index.py` + `scripts/ranking.py`) | Works after a build. BM25 is rebuilt **in memory from Qdrant** (no `bm25.pkl` anymore); an empty collection gives a clear "run build_index.py" error. Scoring math in `ranking.py` (pure numpy, tested). Fusion is RRF over ranks since task 2.1 (H1 fixed; candidates cut after fusion, so most of H2 too). |
| Search service (`scripts/service.py`) | **New (Phase 1).** Flask, warm engine. Verified on host (first query 0.65 s, second 0.10 s — was ~30 s) **and in Docker** (2026-06-13): compose stack up, same top result + score as host, BM25 nodes loaded from Qdrant inside the container, warm queries 0.25 s, restart loads the model from the `hf_models` volume (no re-download), engine built once per process. |
| Q&A (`scripts/ask.py`) | Works with local Ollama (`llama3`). Small context budget, weak retry logic. |
| Agent integration (`scripts/mcp_server.py`, `.mcp.json`) | **Real MCP server (Phase 1).** Official `mcp` SDK, FastMCP, stdio; one tool `search_codebase` that forwards to the service over HTTP with a timeout. Verified end-to-end with an MCP stdio client: initialize → tools/list → tools/call returns ranked results; service down → structured `{"error", "hint"}`. Legacy `mcp.json` deleted. |
| Evaluation (`eval/`) | **Good.** Golden-set harness (Recall@k, MRR, nDCG@k), 34 passing unit tests (heavy tests skip without the ML stack), end-to-end demo. Demo checked 2026-06-11: 1.000 on all metrics for the 8 toy questions — the toy set is saturated. A real .NET project (LookingForMentor) sits in `eval/sample_real/` (committed to the repo since PR #3) with its own golden set `eval/golden_real.jsonl`; baseline reports live in `eval/baselines/`. |
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
  `scripts/qdrant.py` — **it will delete a real index** that uses the same
  name. The name is now env-driven (`QDRANT_COLLECTION`, default `demo`), so
  keep real indexes in their own collection (the LookingForMentor index uses
  `lfm`).
- If you change the embedding model without deleting
  `./.claude/cache/embeddings.pkl`, old and new vectors get mixed silently
  (finding H3).
- Importing `query_index` or `model_setup` does heavy work (model load,
  cache reads) — tests replace `model_setup` with a fake in
  `tests/conftest.py`.
- The whole stack now runs compose-managed (project `ai-agent`):
  `docker compose -f scripts/docker-compose.yml -p ai-agent up -d`.
  Both containers use `restart: unless-stopped` and are left running.

## Next actions (in order)

1. **Review + merge the code-aware chunking PR** (branch
   `phase-2-code-chunking`): eval gate passed with the biggest win so far;
   numbers in the status log.
2. **Phase 2 remaining** (eval-gated, one change per PR): check whether the
   post-re-rank cut still hurts (2.2 remainder), optional cross-encoder
   re-ranker (2.6). Phase 2's "done when" (clearly above baseline, report
   per change) is close — consider closing Phase 2 and starting Phase 3.
   Each change must beat the recorded numbers in
   `eval/baselines/`.

## How to verify the project right now

```bash
.venv/bin/python -m pytest tests/ -q   # 34 tests (heavy ones skip without the ML stack)
.venv/bin/ruff check scripts/ eval/ tests/
bash eval/run_demo.sh                  # full e2e: venv + Qdrant + index + eval
```

## Status log

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
