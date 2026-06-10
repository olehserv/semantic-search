# Architecture Review — Semantic Code Search

**Date:** 2026-06-11
**Reviewer:** Claude (senior AI architect/developer review, requested by Oleh)
**Scope:** full repository at commit `b4c8540` ("add evaluation")
**Verdict:** Solid prototype with a genuinely good evaluation harness, but **not
production-ready**. The headline feature (agent integration over MCP) does not
work with real MCP clients, one confirmed crash-and-poison bug breaks first-run
queries, and the approved redesign (continuous search service) was specced but
never implemented.

---

## 1. What the project is

A hybrid semantic + keyword code-search pipeline for .NET codebases:

```
build_index.py ──> Qdrant (vectors) + bm25.pkl (nodes)
query_index.py ──> vector(top6) + BM25 + vector(top12) → dedup → re-embed → weighted re-rank → JSON
ask.py         ──> retrieved snippets → local Ollama LLM → written answer
mcp_server.py  ──> stdin/stdout wrapper spawning query_index.py per request
eval/          ──> golden-set harness: Recall@k, MRR, nDCG@k (file-level)
```

## 2. Strengths (keep these)

- **The eval harness is the best part of the repo.** Pure, dependency-free
  metric functions (`eval/metrics.py`), an injectable `query_fn` for testing,
  17 passing unit tests, a runnable end-to-end demo (`eval/run_demo.sh`), and a
  README that correctly insists "a change is an improvement only if the metrics
  say so." This is exactly the right foundation for iterating on retrieval.
- **Honest, archaeological comments.** Code documents its own past bugs (the
  dead replace-guard in `build_index.py:53`, the StorageContext persistence fix
  at `build_index.py:86`, the OpenAI-default LLM trap in `model_setup.py:19`).
- **Clean stdout/stderr discipline** in `query_index.py:19` (module-level
  `print` redirected to stderr so stdout stays machine-parseable JSON).
- **Atomic write** for the embedding cache (`query_index.py:165-171`).
- **A real design doc exists** for the next architecture step
  (`docs/superpowers/specs/2026-06-01-continuous-search-service-design.md`,
  status "Approved") with a matching implementation plan. It is well-reasoned —
  it just hasn't been built.

## 3. Findings

Severity: **C** = critical (broken functionality), **H** = high (wrong results
or serious operational risk), **M** = medium (quality/maintainability).

### Critical

**C1 — `mcp_server.py` is not an MCP server.**
`scripts/mcp_server.py` speaks a custom one-JSON-object-per-line protocol
(`{"tool": "search_codebase", "input": {...}}`). Real MCP is JSON-RPC 2.0 over
stdio with an `initialize` handshake, `tools/list`, and `tools/call`. No actual
MCP client (Claude Code, Cursor, etc.) can talk to this server. The root
`mcp.json` is likewise not a format any mainstream client reads (Claude Code
expects `.mcp.json` with a `mcpServers` object). **The project's core value
proposition — "exposed to an AI agent over MCP" — currently does not function.**
Fix: rewrite with the official `mcp` Python SDK (FastMCP makes this ~20 lines).

**C2 — First query on a fresh machine crashes AND poisons the BM25 cache.** *(confirmed by reproduction)*
Sequence in `scripts/query_index.py:50-58`: with no `bm25.pkl` present,
`get_bm25_retriever()` falls back to `index.docstore.docs.values()` — which is
**empty** when the index is loaded from the Qdrant vector store
(`load_index()` at line 61 never rehydrates the docstore). It pickles that
empty list to `bm25.pkl`, then calls `BM25Retriever.from_defaults(nodes=[])`,
which raises `ValueError: Please pass exactly one of index, nodes, or
docstore.` Every subsequent query loads the poisoned empty pickle and crashes
the same way until someone reruns `build_index.py` or deletes the file.
Fix: validate non-empty before persisting; fail with an actionable message
("run build_index.py first").

**C3 — Cold start on every single query.**
`mcp_server.py:5-9` spawns a fresh `python query_index.py` subprocess per
request. Each spawn re-imports `model_setup`, which downloads/loads the
~400 MB BGE embedding model, re-reads both pickle caches, and reconnects to
Qdrant. Per-query latency is tens of seconds; the in-process `_retrieve_cache`
and `_engine` singletons are useless because the process dies after one query.
The approved continuous-service design exists precisely to fix this. Implement it.

**C4 — Fragile subprocess invocation.**
`mcp_server.py:6` runs `["python", ".claude/scripts/query_index.py", query]`:
`python` may not exist on PATH (Debian: `python3`), may resolve to the wrong
interpreter (not the venv), and the script path only resolves if CWD is a
deployed project root. Use `sys.executable` and a path derived from `__file__`.

### High

**H1 — Score fusion is statistically wrong.**
`query_index.py:228-244` min-max normalizes **one pooled list** of scores from
heterogeneous scales: cosine similarity (≈0–1) and BM25 (unbounded, often >10).
A single high BM25 score becomes `max_s` and compresses all vector scores
toward 0, so the `(1-alpha)·retriever_score` term is effectively BM25-only
noise. Industry standard fix: **Reciprocal Rank Fusion (RRF)** — rank-based,
scale-free, ~5 lines. The design doc already lists this as deferred item "R2".

**H2 — Candidates are truncated before re-ranking.**
`query_index.py:205` keeps the first 30 deduped nodes in *retriever insertion
order* (narrow vector → BM25 → wide vector, ×3 query expansions). With 3
expansions × (6+BM25+12) results, later retrievers' unique hits are silently
dropped before the re-ranker ever scores them — the re-rank stage can only
demote, never rescue. Truncate *after* fusion scoring, or interleave.

**H3 — Embedding cache: unbounded, O(n) rewrite per miss, no invalidation key.**
`query_index.py:161-173` re-pickles the **entire** cache dict to disk on every
cache miss (up to 30 full dumps per query) and grows without bound. Worse, the
cache is keyed by text only — swapping the embedding model in `model_setup.py`
silently serves vectors from the old model (wrong space, possibly wrong
dimension). Key the cache by `(model_name, text)` or store the model name in
the file and discard on mismatch; batch the dump once per query; bound it (LRU).

**H4 — No dependency manifest at the root, nothing pinned.**
Dependencies exist only as a `pip install` block in `README.md:58-66` and a
partial `eval/requirements-eval.txt` (unpinned). The implementation plan's
Task 1 (root `requirements.txt` + `requirements-dev.txt`) was never executed.
Unreproducible builds are a production blocker — LlamaIndex breaks APIs
between minor versions routinely.

**H5 — Zero test coverage on the pipeline itself.**
All 17 tests cover `eval/` (metrics + orchestration). `query_index.py`,
`build_index.py`, `qdrant.py`, `ask.py`, and `mcp_server.py` have no tests at
all, and there is no CI to run even the existing ones.

**H6 — Destructive, non-atomic index rebuild.**
`build_index.py:74-95` deletes the existing collection *before* building the
replacement. A crash mid-embed (OOM, Ctrl-C, Qdrant hiccup) leaves **no index
at all**. Production pattern: build into a new collection, then switch an
alias (Qdrant supports aliases natively), then drop the old one.

**H7 — Hardcoded configuration throughout; demo shares the prod collection.**
`COLLECTION_NAME = "demo"`, `localhost:6333` (`qdrant.py:7-10`), the embedding
and LLM model names (`model_setup.py`), and — worst — a domain assumption baked
into the generic pipeline: `expand_query()` appends `".NET core backend"` to
every query (`query_index.py:137-142`). Because the eval demo and a real
deployment share the literal collection name `"demo"`, `eval/run_demo.sh`
**destroys your real index** (the script itself warns about this). Everything
listed needs an env/config layer.

### Medium

- **M1 — Import-time side effects.** `query_index.py` runs `cache_warmup()` at
  import (line 48); importing `model_setup` loads the HF model and configures
  global `Settings`. This is why `tests/conftest.py` must stub `model_setup`.
  Move to lazy/explicit init.
- **M2 — Pickle as a persistence format.** `pickle.load` on `bm25.pkl` /
  `embeddings.pkl` executes arbitrary code if the file is tampered with, and
  the design doc itself notes LlamaIndex nodes don't round-trip cleanly. The
  approved design (BM25 rebuilt from Qdrant, in memory) eliminates the worst one.
- **M3 — Brittle LLM answer-quality detection.** `ask.py:50-61` decides the
  answer was insufficient by substring-matching phrases like "not enough
  information". Model-dependent and silently wrong in both directions. Also
  `MAX_CONTEXT_CHARS = 6000` (~1.5k tokens) is very small for code Q&A.
- **M4 — `ask.py` mixes diagnostics into stdout.** `DEBUG = True` prints
  `[DEBUG] Mode: ...` to stdout alongside the answer (no stderr redirect here,
  unlike `query_index.py`).
- **M5 — Interactive `input()` prompt in `build_index.py:56-59`** blocks any
  automation; `run_demo.sh` works around it with `echo y`. Add `--force`.
- **M6 — No incremental indexing.** Any source change requires re-embedding the
  whole repository.
- **M7 — `_retrieve_cache` is unbounded** and pointless under the current
  process-per-query model (dies with the process).
- **M8 — `SentenceSplitter` on C# source.** Prose-oriented chunking splits
  methods mid-body and severs signatures from bodies. Code-aware (AST/tree-
  sitter) chunking is the highest-leverage retrieval-quality improvement and is
  measurable with the existing eval harness.
- **M9 — Dangling documentation reference.** `eval/README.md`, `run_eval.py`,
  and `run_demo.sh` all cite "TODO.md §0", but no `TODO.md` exists in the repo
  (only `TODO.Instruction.md`, which is agent guidance). The roadmap document
  was lost; the design/plan docs in `docs/superpowers/` partially reconstruct it.
- **M10 — Qdrant runs unauthenticated** and `ensure_qdrant()` shells out to the
  Docker CLI, which cannot work from inside a container (design doc fixes this
  with a `QDRANT_AUTOSTART` env gate).
- **M11 — Polish:** Ukrainian comment at `qdrant.py:43`, typos
  ("Termanated", "Are you wanna replace"), `print`-based logging with a
  shadowed builtin instead of the `logging` module.

## 4. Architectural assessment

The stage separation (index / retrieve / answer / serve) is right, and the
retrieval recipe (dense + BM25 + re-rank) is a sound hybrid-search shape. The
problems are concentrated in the *seams*: process lifecycle (cold start per
query), persistence (pickle), protocol (fake MCP), and configuration
(hardcoded). Notably, the team already diagnosed most of this — the
2026-06-01 design doc is a correct prescription. **The single most important
observation of this review is that the project's next step is not to design
anything new; it is to execute the design it already approved**, plus replace
the MCP layer with the real protocol (which that design did not cover — its
`mcp_server.py` shim keeps the same fake line protocol, only swapping
subprocess for HTTP).

See `2026-06-11-production-readiness-plan.md` (same directory) for the
prioritized execution plan, and `/handoff.md` for project state tracking.
