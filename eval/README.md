# Search Quality Evaluation

This folder measures how good the search is. The rule for the project:
**a search change is an improvement only if these numbers get better.**
Every Phase 2 task in `docs/reviews/2026-06-11-production-readiness-plan.md`
(RRF fusion, code-aware chunking, re-ranker, …) must be checked here before
it is merged.

How it works: for every question in a labelled "golden set", the harness runs
the real `query()` pipeline. It looks at which **files** the results came
from, and compares them with the files that are known to be correct.

| Metric | What it tells you |
|--------|-------------------|
| **Recall@k** | Of the correct files, how many are in the top-k results? |
| **MRR** | How high is the first correct file? (1.0 = always at rank 1) |
| **nDCG@k** | Are the correct files near the top? Lower positions get less credit. |

| File | Role |
|------|------|
| `metrics.py` | The metric functions (only stdlib, unit-tested) |
| `run_eval.py` | Runs `query()` for every golden question and prints the report |
| `golden.jsonl` | The labelled set: one `{"query", "relevant_files"}` per line |
| `golden_real.jsonl` | 40 questions for the real project in `sample_real/` (see below) |
| `sample_corpus/` | A small fake .NET app, so the demo works alone |
| `sample_real/` | A real .NET project (LookingForMentor) for honest numbers — ships with the repo |
| `baselines/` | Recorded eval reports; Phase 2 changes must beat these numbers |
| `run_demo.sh` | One command: venv + Qdrant + index the corpus + run the eval |

## Run it

```bash
bash eval/run_demo.sh            # full demo on the sample corpus
python3 -m pytest tests/ -q     # unit tests (no Docker or ML stack needed)
```

**Warning:** the demo rebuilds the Qdrant collection named in
`scripts/qdrant.py` (`COLLECTION_NAME`). If your real index uses the same
name, the demo will replace it. The name is env-driven
(`QDRANT_COLLECTION`, default `demo`), so give real indexes their own
collection.

## Run it in CI (manual)

There is a manual GitHub Actions job, `.github/workflows/eval.yml` (trigger:
"Run workflow" / `workflow_dispatch`). It installs the full ML stack, starts a
Qdrant service container, indexes the **sample corpus**, runs the eval, and
**fails if any metric drops below** the saved sample baseline
(`baselines/2026-06-13-sample-chunking.json`, which is 1.000 on everything). The
report is uploaded as the `eval-report` artifact. It is manual on purpose: it
downloads CPU torch and the embedding model, so it is too heavy for every push
(the fast `ci.yml` stays ML-free).

## Tuning knobs (env vars)

| Env var | Default | Effect |
|---------|---------|--------|
| `QUERY_VARIANT_SUFFIXES` | `implementation` | Extra query variants; set domain hints here (see below). |
| `RERANK_CANDIDATES` | `30` | How many top fused candidates go to the embedding re-rank. Query-time only — no reindex needed to change it. |
| `CROSS_ENCODER_MODEL` | _(empty = off)_ | Set to a cross-encoder name to re-rank with it instead of the cosine blend. Recommended: `cross-encoder/ms-marco-MiniLM-L-6-v2`. |

`RERANK_CANDIDATES` was added to measure task 2.2 (does the post-fusion cut hurt
recall?). On the real project the answer is **no**: the candidate pool per query
is only ~16–23, always under 30, so every value from 30 up to "no cut" gives the
same numbers (Recall@5 0.912 / MRR 0.716 / nDCG@5 0.760). The knob stays for
future tuning if the retrievers are ever widened.

`CROSS_ENCODER_MODEL` (task 2.6) turns on an optional cross-encoder re-ranker,
blended with the RRF score. It is **off by default** because it trades latency
for a modest quality gain. Measured on the real project:

| Re-ranker | Recall@5 | MRR | nDCG@5 | CPU latency / query |
|-----------|---------:|----:|-------:|--------------------:|
| cosine + RRF (default) | 0.912 | 0.716 | 0.760 | ~0.16 s |
| `ms-marco-MiniLM-L-6-v2` + RRF | 0.925 | **0.751** | **0.782** | ~1.0 s |
| `BAAI/bge-reranker-base` + RRF | **0.938** | 0.747 | 0.786 | ~5.9 s |

MiniLM-L6 is the sweet spot (best MRR, ~90 MB, ~1 s on CPU). The cross-encoder is
**blended, not used alone** — pure cross-encoder ordering scored *worse* than the
default (0.875 / 0.686 / 0.721), because it drops the BM25/keyword signal that
code search relies on. The MiniLM-on report is saved as
`baselines/2026-06-13-real-cross-encoder.json`.

## The real-project baseline

`golden_real.jsonl` holds 40 questions about the LookingForMentor project
(a .NET CQRS + Blazor app in `sample_real/`, part of the repo). The labels use
file names that exist exactly once in that project (`ErrorHandlingMiddleware.cs`
exists twice, but both copies are correct answers). To reproduce the baseline:

```bash
cd eval/sample_real/LookingForMentor-main
QDRANT_COLLECTION=lfm PYTHONPATH=../../../scripts \
  ../../../.venv/bin/python ../../../scripts/build_index.py --force
QUERY_VARIANT_SUFFIXES="implementation,.NET core backend" \
  QDRANT_COLLECTION=lfm PYTHONPATH=../../../scripts:../../../eval \
  ../../../.venv/bin/python ../../../eval/run_eval.py \
  --golden ../../../eval/golden_real.jsonl --k 5 \
  --out ../../../eval/baselines/<date>-real.json
```

The recorded numbers use the `.NET core backend` variant suffix — it is a
domain hint that measurably helps on this .NET corpus (without it:
Recall@5 0.738 vs 0.787). Keep it set when comparing against the
baselines, or the comparison is unfair.

## Use it on your own project

1. Index your codebase (see the top-level README).
2. Replace `golden.jsonl` with **30–50** questions that people really ask
   about your code. For each question, write the file(s) that answer it.
   Mix different types: "where is X", "how does Y work", exact name lookups,
   and concept questions. Good labels matter more than many labels.
3. ```bash
   PYTHONPATH=scripts:eval python eval/run_eval.py --golden eval/golden.jsonl --k 5 --out report.json
   ```
4. Write down the numbers. Change **one** thing in the search. Run again.
   Keep the change only if the numbers go up.

### Golden file format

```json
{"query": "where is authentication handled", "relevant_files": ["AuthService.cs"]}
{"query": "what happens when an order is placed", "relevant_files": ["OrdersController.cs", "EmailService.cs"]}
```

`relevant_files` are file *names* (without the path). One question can have
several correct files.
