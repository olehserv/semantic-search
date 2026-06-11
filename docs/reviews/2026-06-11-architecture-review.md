# Architecture Review — Semantic Code Search

**Date:** 2026-06-11
**Reviewer:** Claude (senior AI architect/developer review, requested by Oleh)
**Scope:** full repository at commit `b4c8540` ("add evaluation")
**Follow-up:** C2/C4/H4/M5/M9 are fixed, and M11 is mostly fixed, by the
Phase 0 work on branch `phase-0-fixes` (same day). See `/handoff.md` for the
current status.
**Result:** A solid prototype with a very good evaluation harness. But it is
**not production-ready**. The main feature (agent access over MCP) does not
work with real MCP clients. One confirmed bug breaks the first query on a
fresh machine. The approved redesign (a long-running search service) was
written down but never built.

---

## 1. What the project is

Hybrid semantic + keyword code search for .NET codebases:

```
build_index.py ──> Qdrant (vectors) + bm25.pkl (chunks)
query_index.py ──> vector(top6) + BM25 + vector(top12) → dedup → re-embed → weighted re-rank → JSON
ask.py         ──> found code chunks → local Ollama LLM → written answer
mcp_server.py  ──> stdin/stdout wrapper, starts query_index.py as a new process per request
eval/          ──> golden-set harness: Recall@k, MRR, nDCG@k (file level)
```

## 2. Strengths (keep these)

- **The eval harness is the best part of the repo.** Pure metric functions
  with no dependencies (`eval/metrics.py`), a replaceable `query_fn` for
  testing, passing unit tests, a working end-to-end demo
  (`eval/run_demo.sh`), and a README that correctly says: a change is an
  improvement only if the metrics say so. This is the right base for
  improving the search step by step.
- **Honest comments.** The code documents its own past bugs (the dead
  replace-check in `build_index.py:53`, the StorageContext fix at
  `build_index.py:86`, the OpenAI-default LLM trap in `model_setup.py:19`).
- **Clean stdout/stderr separation** in `query_index.py:19` (all logs go to
  stderr, so stdout stays valid JSON for machines).
- **Atomic write** for the embedding cache (`query_index.py:165-171`) — a
  crash cannot corrupt the cache file.
- **A real design document exists** for the next step
  (`docs/superpowers/specs/2026-06-01-continuous-search-service-design.md`,
  status "Approved") with a matching step-by-step plan. The design is good —
  it just was never implemented.

## 3. Findings

Severity: **C** = critical (something is broken), **H** = high (wrong results
or a serious operations risk), **M** = medium (quality / maintainability).

### Critical

**C1 — `mcp_server.py` is not a real MCP server.**
`scripts/mcp_server.py` uses its own protocol: one JSON object per line
(`{"tool": "search_codebase", "input": {...}}`). Real MCP is JSON-RPC 2.0
over stdio, with an `initialize` handshake, `tools/list`, and `tools/call`.
No real MCP client (Claude Code, Cursor, …) can talk to this server. The root
`mcp.json` is also not a format that common clients read (Claude Code expects
`.mcp.json` with a `mcpServers` object). **The core promise of the project —
"exposed to an AI agent over MCP" — does not work today.**
Fix: rewrite it with the official `mcp` Python SDK (FastMCP needs ~20 lines).

**C2 — The first query on a fresh machine crashes AND breaks the BM25 cache.**
*(confirmed by reproduction)*
What happens in `scripts/query_index.py:50-58`: when `bm25.pkl` does not
exist, `get_bm25_retriever()` takes the nodes from `index.docstore.docs` —
but that store is **empty** when the index is loaded from Qdrant
(`load_index()` never fills it). The code saves this empty list to
`bm25.pkl`, and then `BM25Retriever.from_defaults(nodes=[])` raises
`ValueError`. After that, every next query loads the broken cache file and
crashes the same way, until someone runs `build_index.py` again or deletes
the file.
Fix: check the list before saving it; fail with a clear message
("run build_index.py first").

**C3 — Every query pays a full cold start.**
`mcp_server.py:5-9` starts a new `python query_index.py` process per request.
Every start loads the ~400 MB BGE embedding model again, reads both cache
files again, and reconnects to Qdrant. One query takes tens of seconds. The
in-process caches (`_retrieve_cache`, `_engine`) are useless because the
process dies after one query. The approved service design exists exactly to
fix this. Implement it.

**C4 — Fragile process start.**
`mcp_server.py:6` runs `["python", ".claude/scripts/query_index.py", query]`.
Problems: `python` may not exist on PATH (Debian uses `python3`), it may
point to the wrong interpreter (not the venv), and the script path only works
when the current directory is a deployed project root. Use `sys.executable`
and a path based on `__file__`.

### High

**H1 — The score fusion math is wrong.**
`query_index.py:228-244` normalizes **one mixed list** of scores with
min-max. But the scores come from different scales: cosine similarity is
about 0–1, BM25 has no upper limit (often >10). One high BM25 score becomes
the maximum and pushes all vector scores near 0. The `(1-alpha)` part of the
formula then carries almost no information. Standard fix: **Reciprocal Rank
Fusion (RRF)** — it uses only ranks, not raw scores, and needs ~5 lines. The
design doc already lists this as deferred item "R2".

**H2 — Candidates are cut before ranking.**
`query_index.py:205` keeps only the first 30 unique nodes, in the order the
retrievers returned them (narrow vector first, then BM25, then wide vector,
×3 question variants). Unique results from the later retrievers are silently
dropped before the ranking step can score them. The ranker can only push
items down — it can never rescue a dropped item. Cut *after* scoring, or mix
the lists fairly.

**H3 — Embedding cache: grows forever, slow writes, no model check.**
`query_index.py:161-173` writes the **whole** cache dict to disk on every
cache miss (up to 30 full writes per query) and the cache never shrinks.
Worse: the cache key is only the text. If you change the embedding model in
`model_setup.py`, the old vectors are still used — wrong vector space,
possibly wrong dimension. Fix: key the cache by `(model_name, text)` or store
the model name in the file and drop it on mismatch; write to disk once per
query; limit the size (LRU).

**H4 — No dependency file, no version pins.**
The dependencies exist only as a `pip install` block in `README.md` and an
unpinned `eval/requirements-eval.txt`. Task 1 of the implementation plan
(root `requirements.txt` + `requirements-dev.txt`) was never done. Builds are
not reproducible — LlamaIndex often breaks its API between minor versions.

**H5 — The pipeline itself has zero tests.**
All existing tests cover `eval/` (metrics + orchestration). There are no
tests for `query_index.py`, `build_index.py`, `qdrant.py`, `ask.py`, or
`mcp_server.py`, and no CI that runs anything.

**H6 — Index rebuild deletes first, builds second.**
`build_index.py:74-95` deletes the existing collection *before* the new one
is built. If the build crashes in the middle (out of memory, Ctrl-C, Qdrant
problem), you have **no index at all**. Production pattern: build into a new
collection, then switch a Qdrant alias, then delete the old one.

**H7 — Hardcoded configuration everywhere; demo shares the production collection.**
`COLLECTION_NAME = "demo"`, `localhost:6333` (`qdrant.py:7-10`), the model
names (`model_setup.py`), and — worst — a domain assumption inside the
generic pipeline: `expand_query()` adds `".NET core backend"` to every query
(`query_index.py:137-142`). Because the eval demo and a real deployment use
the same collection name `"demo"`, `eval/run_demo.sh` **deletes your real
index** (the script itself warns about this). All of this needs an
environment/config layer.

### Medium

- **M1 — Heavy work at import time.** `query_index.py` runs `cache_warmup()`
  when imported (line 48); importing `model_setup` loads the HF model and
  changes global `Settings`. This is why `tests/conftest.py` must replace
  `model_setup` with a fake. Move this work into explicit init functions.
- **M2 — Pickle as a storage format.** `pickle.load` on `bm25.pkl` /
  `embeddings.pkl` runs arbitrary code if the file was modified. Also, the
  design doc itself says LlamaIndex nodes do not pickle reliably. The
  approved design (BM25 rebuilt from Qdrant, in memory) removes the worst case.
- **M3 — Weak answer-quality check in `ask.py`.** Lines 50-61 decide the
  answer is bad by searching for phrases like "not enough information". This
  depends on the model and silently fails in both directions. Also
  `MAX_CONTEXT_CHARS = 6000` (~1.5k tokens) is very small for code questions.
- **M4 — `ask.py` mixes logs into stdout.** `DEBUG = True` prints
  `[DEBUG] Mode: ...` to stdout next to the answer (unlike `query_index.py`,
  which sends logs to stderr).
- **M5 — Interactive `input()` in `build_index.py:56-59`** blocks all
  automation; `run_demo.sh` works around it with `echo y`. Add `--force`.
- **M6 — No incremental indexing.** Any change in the source means
  re-embedding the whole repository.
- **M7 — `_retrieve_cache` grows without limit** and is useless in the
  current process-per-query model (it dies with the process).
- **M8 — `SentenceSplitter` on C# code.** A text splitter cuts methods in
  the middle and separates signatures from bodies. Code-aware chunking
  (AST / tree-sitter) is the biggest expected quality win, and the eval
  harness can measure it.
- **M9 — Broken documentation links.** `eval/README.md`, `run_eval.py`, and
  `run_demo.sh` all point to "TODO.md §0", but no `TODO.md` exists in the
  repo (only `TODO.Instruction.md`, which is agent guidance). The roadmap
  document was lost; the design/plan docs in `docs/superpowers/` cover part
  of it.
- **M10 — Qdrant runs without authentication**, and `ensure_qdrant()` calls
  the Docker CLI, which cannot work from inside a container (the design doc
  fixes this with a `QDRANT_AUTOSTART` env switch).
- **M11 — Small polish items:** a Ukrainian comment at `qdrant.py:43`, typos
  ("Termanated", "Are you wanna"), `print`-based logging with a replaced
  built-in instead of the `logging` module.

## 4. Overall judgment

The split into stages (index / search / answer / serve) is right, and the
search recipe (dense + BM25 + re-rank) is a sound hybrid-search shape. The
problems sit in the connections between the parts: process lifetime (cold
start per query), storage (pickle), protocol (not real MCP), and
configuration (hardcoded values). Important: the team already understood most
of this — the 2026-06-01 design doc gives the correct medicine. **The most
important conclusion of this review: the next step is not to design something
new. It is to build the design that is already approved**, plus replace the
MCP layer with the real protocol (the old design did not cover that part —
its `mcp_server.py` keeps the same fake line protocol and only changes
subprocess to HTTP).

See `2026-06-11-production-readiness-plan.md` (same directory) for the
ordered work plan, and `/handoff.md` for the live project status.
