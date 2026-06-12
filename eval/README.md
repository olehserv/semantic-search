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
