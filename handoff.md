# Project Handoff — Semantic Code Search

> Living document. Update the **Status log** and **Next actions** at the end
> of every working session, so anyone (human or agent) can continue the work
> without extra context.

**Last updated:** 2026-06-13 (task 3.8 — security pass)
**Repo state:** branch `phase-3-security-pass`. Merged into `main` so far:
PRs #1–#16 (Phase 0 through Phase 2, plus Phase 3 tasks 3.1–3.7).

---

## What this project is

Hybrid semantic + keyword (BM25) code search over a .NET codebase. It uses
Qdrant and a local HuggingFace embedding model (`BAAI/bge-base-en-v1.5`),
with an optional Ollama Q&A layer. The goal is to give AI agents (Claude
Code) a `search_codebase` MCP tool. See `README.md` for usage.

## Current state (2026-06-11)

| Area | State |
|------|-------|
| Indexing (`scripts/build_index.py`) | Works. `--force` flag for scripts (Phase 0.4). C# files get code-aware chunks from `scripts/chunking.py` (task 2.4, tree-sitter); other files keep the `SentenceSplitter`. Crash-safe rebuild via a Qdrant alias swap (task 3.3, H6): builds into `<name>-<timestamp>`, swaps the alias, deletes the old one — no destructive delete-first. **Incremental mode (task 3.4, M6):** `--incremental` stores a `file_hash` per file (in the Qdrant payload, excluded from embedding) and updates the live index in place — re-embeds only changed/new files, deletes removed files' vectors, falls back to a full build when no index exists. |
| Search (`scripts/query_index.py` + `scripts/ranking.py`) | Works after a build. BM25 is rebuilt **in memory from Qdrant** (no `bm25.pkl` anymore); an empty collection gives a clear "run build_index.py" error. Scoring math in `ranking.py` (pure numpy, tested). Fusion is RRF over ranks since task 2.1 (H1 fixed; candidates cut after fusion, H2 fully closed — 2.2 found the cut never bites). Optional cross-encoder re-ranker (task 2.6) behind `CROSS_ENCODER_MODEL`, **off by default**. |
| Search service (`scripts/service.py`) | **New (Phase 1).** Flask, warm engine. Verified on host (first query 0.65 s, second 0.10 s — was ~30 s) **and in Docker** (2026-06-13): compose stack up, same top result + score as host, BM25 nodes loaded from Qdrant inside the container, warm queries 0.25 s, restart loads the model from the `hf_models` volume (no re-download), engine built once per process. |
| Q&A (`scripts/ask.py`) | Works with local Ollama (`llama3`). Small context budget, weak retry logic. |
| Agent integration (`scripts/mcp_server.py`, `.mcp.json`) | **Real MCP server (Phase 1).** Official `mcp` SDK, FastMCP, stdio; one tool `search_codebase` that forwards to the service over HTTP with a timeout. Verified end-to-end with an MCP stdio client: initialize → tools/list → tools/call returns ranked results; service down → structured `{"error", "hint"}`. Legacy `mcp.json` deleted. |
| Evaluation (`eval/`) | **Good.** Golden-set harness (Recall@k, MRR, nDCG@k), 34 passing unit tests (heavy tests skip without the ML stack), end-to-end demo. Demo checked 2026-06-11: 1.000 on all metrics for the 8 toy questions — the toy set is saturated. A real .NET project (LookingForMentor) sits in `eval/sample_real/` (committed to the repo since PR #3) with its own golden set `eval/golden_real.jsonl`; baseline reports live in `eval/baselines/`. |
| Dependencies | `requirements.txt` (pinned, now incl. `flask`, `mcp`) + `requirements-dev.txt`; `eval/requirements-eval.txt` points to the root file. Local `.venv/` gitignored. |
| CI | GitHub Actions `ci.yml`: ruff + py_compile + pytest (+ `requests`, so the MCP shim tests run); heavy tests skip without the ML stack. Plus `eval.yml` (task 2.5): a **manual** (`workflow_dispatch`) eval gate — Qdrant service container, CPU torch, sample-corpus eval, fails on regression vs the saved baseline, uploads the report artifact. Plus `tests-full.yml` (task 3.6): a **manual** (`workflow_dispatch`) job — CPU torch + `requirements-dev` (incl. testcontainers) + Docker — that runs the WHOLE pytest suite, including the pipeline / MCP-protocol / real-Qdrant integration tests that skip on the fast push CI. |
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
- ~~Importing `query_index` or `model_setup` does heavy work (model load) —
  tests replace `model_setup` with a fake in `tests/conftest.py`.~~ **Closed
  (task 3.7, M1).** Importing the pipeline configures no models and imports no
  torch: the work moved into `model_setup.setup_models()`, called lazily from
  the entry points (`build_query_engine`, `build_index`, `service.main`,
  `ask`). The conftest fake is gone. The embedding cache was already lazy.
- The whole stack now runs compose-managed (project `ai-agent`):
  `docker compose -f scripts/docker-compose.yml -p ai-agent up -d`.
  Both containers use `restart: unless-stopped` and are left running.

## Next actions (in order)

1. **Continue Phase 3 (operations hardening).** Tasks 3.1 (config layer), 3.2
   (embedding cache v2), 3.3 (safe index rebuild), 3.4 (incremental indexing),
   3.5 (structured logging), 3.6 (pipeline test suite), 3.7 (lazy init), and
   3.8 (security pass) are done; next up is **3.9 (ask.py cleanup** — logs to
   stderr [already done in 3.5], a larger context budget that counts tokens not
   chars, and replacing the phrase-matching quality check with a structured
   self-check or removing the retry), the **last Phase 3 item** — see the plan
   table in `docs/reviews/2026-06-11-production-readiness-plan.md`. Phase 2 is
   complete (all of 2.1–2.6 done; "done when" met: Recall@5 0.600 → 0.912,
   nDCG@5 0.343 → 0.760, a report per change).

## How to verify the project right now

```bash
.venv/bin/python -m pytest tests/ -q   # 100 tests with the full stack; heavy ones skip without it
.venv/bin/ruff check scripts/ eval/ tests/
bash eval/run_demo.sh                  # full e2e: venv + Qdrant + index + eval

# The pipeline (3.6) tests: query() with fake retrievers, the MCP protocol
# round-trip, and a real-Qdrant integration test (needs Docker; testcontainers
# spins its own qdrant/qdrant — skips cleanly without Docker):
.venv/bin/pip install -r requirements-dev.txt
.venv/bin/python -m pytest tests/test_pipeline_query.py tests/test_mcp_protocol.py \
    tests/test_integration_qdrant.py -v
```

## Status log

- **2026-06-13 (task 3.8 — security pass, M2 + M10)** — Three parts.
  **(1) Qdrant auth (M10):** new `QDRANT_API_KEY` (default `""` = unauthenticated,
  the local default) and `QDRANT_HTTPS` (default `0`) config fields; `qdrant.py`
  passes `api_key=… or None` and `https=…` to the single `QdrantClient` in
  `get_qdrant_client()`, so build/query/service all reach a secured/remote Qdrant
  (e.g. Qdrant Cloud) with no other change. **(2) Request-size limit (M10):**
  `service.py` sets `app.config["MAX_CONTENT_LENGTH"]` (new `MAX_CONTENT_LENGTH`
  config, default 64 KB) — Flask returns **413** for oversize bodies before
  parsing/embedding — and `/search` now rejects a missing **or non-string** query
  with **400**. **(3) No pickle loads (M2):** verified there are none (BM25 is
  rebuilt from Qdrant, the cache is SQLite from 3.2); added
  `tests/test_no_pickle.py` to keep it that way — it greps `scripts/`+`eval/` for
  `pickle.load`/`torch.load`/`joblib`/`dill`/`cloudpickle` and fails if any
  reappear. New `tests/test_qdrant_client.py` (auth kwargs reach the client) +
  `test_service.py` 413/400 cases + `test_config.py` field cases. **105 tests
  green, ruff clean**, demo eval unchanged at 1.000 (default path = no key, plain
  http). Docker compose stays unauthenticated for local dev (the key is opt-in
  via env for remote deploys). Also fixed a README drift: `EMB_CACHE_PATH`
  default is `.db` (SQLite), not `.pkl`.

- **2026-06-13 (task 3.7 — lazy initialization, M1)** — Moved the heavy
  model setup out of import time. `scripts/model_setup.py` is now a single
  guarded `setup_models()` function (imports moved inside; `import torch` only
  runs when the embed model actually needs building). It is idempotent and a
  **no-op for whichever of `Settings.embed_model` / `Settings.llm` is already
  set**, so a test's `MockEmbedding` is never overridden or re-downloaded. The
  side-effect `import model_setup  # noqa: F401` lines became plain imports +
  an explicit `model_setup.setup_models()` call at each lazy entry point:
  `build_query_engine()` (covers all of `query()`), `build_index()` +
  `build_index_incremental()`, `service.main()`, and `ask()`. The
  `tests/conftest.py` **model_setup fake is removed** — importing the pipeline
  no longer loads a model (verified: `import query_index` leaves
  `Settings._embed_model is None` and `torch` not in `sys.modules`). New
  `tests/test_model_setup.py` locks the no-override + idempotency guards. **100
  tests green, ruff clean**, demo eval unchanged at 1.000, integration test
  still runs on `MockEmbedding`. No production behavior change — same models,
  same device auto-detect, just lazy.

- **2026-06-13 (task 3.6 — pipeline test suite, H5)** — Closed the three
  pipeline-coverage gaps; **no production code changed**, tests + one dev dep
  only. (1) `tests/test_pipeline_query.py` — the full `query()` orchestration
  (variants → 3 retrievers → RRF → candidate cut → cosine re-rank → top_k JSON)
  with **fake retrievers** returning real `NodeWithScore` objects and a fake
  embed model/cache; asserts JSON shape, rank order, the candidate cut, and that
  a bm25-only node survives fusion (proves all three lists fuse). (2)
  `tests/test_mcp_protocol.py` — a real initialize / tools-list / tools-call
  round-trip over the MCP SDK's in-memory transport
  (`create_connected_server_and_client_session`), with `requests.post` faked;
  also checks the service-down error propagates as a normal result. (3)
  `tests/test_integration_qdrant.py` — **testcontainers** starts a throwaway
  `qdrant/qdrant` container and runs the real `build_index` → `query` round-trip
  using llama-index's `MockEmbedding` (no 400 MB model; BM25 carries the keyword
  match); skips cleanly when Docker/testcontainers is absent. `testcontainers`
  pinned in `requirements-dev.txt`. The pure ranking math was already covered by
  `test_ranking.py` (left as is). **CI (per Oleh): keep push CI fast, run the
  heavy suite manually** — `ci.yml` is unchanged (the three new tests skip there,
  like the other ML-stack tests); new `tests-full.yml` (`workflow_dispatch`,
  CPU torch + `requirements-dev` + Docker) runs the WHOLE suite incl. the
  integration test. **97 tests green locally, ruff clean**, demo eval unchanged
  at 1.000. Note: set Settings via the backing field `Settings._embed_model` —
  assigning `Settings.embed_model` goes through a getter that resolves the
  OpenAI default when unset.

- **2026-06-13 (task 3.5 — structured logging, M11)** — Replaced bare `print`
  diagnostics with the stdlib `logging` module across `scripts/`. New
  `scripts/logging_setup.py` with `setup_logging()` — one `logging.basicConfig`
  to **stderr**, level from a new `LOG_LEVEL` config field (default INFO;
  `LOG_LEVEL=DEBUG` shows the per-step traces), structured format
  (`time level name: message`). Entry points (`build_index`, `query_index`,
  `ask`, `qdrant` `__main__`s + `service.main()`) call it once; every module
  uses `logger = logging.getLogger(__name__)` and lazy `%`-args. Levels: the old
  `[DEBUG] [datetime] …` traces → `logger.debug` (the embedded timestamps are
  gone — the format adds the time), milestones/status → `logger.info`,
  recoverable problems ("cache failed", "could not clean up") → `logger.warning`.
  **Removed the M11 "replaced built-in":** `query_index.py` no longer does
  `print = functools.partial(print, file=sys.stderr)`; its stdout-only-JSON
  contract now holds simply because logging defaults to stderr (verified: a CLI
  query emits exactly one JSON line on stdout, all logs on stderr). `ask.py` lost
  its `DEBUG = True` flag (its debug line is now `logger.debug` on stderr, fixing
  the M3/M4 stdout mixing); `print(ask(q))` stays (the answer is real output).
  `setup_logging()` also dials `httpx`/`httpcore` down to WARNING unless
  `LOG_LEVEL=DEBUG`, so the Qdrant client's per-request INFO logs don't bury our
  own status. **Left as `print`:** the build `(y/n)` prompt (moved into
  `input(...)`) and `eval/run_eval.py`'s metrics table (report data, not a log).
  `mcp_server.py` was already print-free. New `tests/test_logging_setup.py` (3)
  + `LOG_LEVEL` cases in `test_config.py`; **89 tests green, ruff clean**, demo
  eval still 1.000/1.000/1.000 (no logic change). The other M11 polish items
  (typos, a Ukrainian comment) were already fixed in earlier refactors.

- **2026-06-13 (task 3.4 — incremental indexing, M6)** — New `--incremental`
  mode in `scripts/build_index.py`. Each file now carries a `file_hash`
  (sha256 of its text) stamped in `make_nodes` and — critically — added to
  `excluded_embed_metadata_keys`/`excluded_llm_metadata_keys`, so it is pure
  bookkeeping and never changes the vectors (eval numbers stay identical to a
  full rebuild). The hash lives in the Qdrant point payload, so the index is its
  own manifest (no side file to drift). `build_index_incremental()` resolves the
  alias to the live collection, reads the old `{file_path: file_hash}` map by
  scrolling (`load_file_hashes`), diffs it against disk (`diff_files` →
  changed/new/deleted), deletes points of changed+deleted files by `file_path`
  filter (`delete_files`), and re-embeds only changed+new files **in place**
  (no new collection, no alias swap). No index yet → falls back to a full
  `build_index(force=True)`. **Trade-off (decided with Oleh):** in-place, so an
  interrupted run can leave the index half-updated — a re-run recomputes the
  diff and converges; the full rebuild keeps the 3.3 crash-safe alias swap.
  New `tests/test_incremental.py` (9 tests: diff logic, the embed-exclusion
  invariant, and a fake-client orchestration test for delete/re-index). **86
  tests green, ruff clean.** Verified e2e on a throwaway collection (no-op /
  edit / add / delete all correct) and the demo eval stays 1.000/1.000/1.000
  (full-build path + quality unchanged). Docs: README (full vs incremental
  trade-off), this handoff, the plan doc. Note: `file_path` from
  `SimpleDirectoryReader` is absolute, so incremental must be run from the same
  project root each time — same assumption the hardcoded `PROJECT_PATH="./"`
  already makes.

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
