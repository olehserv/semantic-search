# Retrieval Evaluation Harness

This is the measurement gate for the search system. Every retrieval change in
Phase 2 of `docs/reviews/2026-06-11-production-readiness-plan.md` (RRF fusion,
code-aware chunking, a re-ranker, a different embedding
model, …) should be validated here — **a change is an improvement only if the
metrics say so.** Without this, every tuning decision is a guess.

## What it measures

For each natural-language query in a labelled *golden set*, the harness runs the
real `query()` pipeline, looks at which **files** the retrieved chunks came from,
and compares them to the known-correct files. It reports three standard
information-retrieval metrics, per query and averaged:

| Metric | Question it answers |
|--------|---------------------|
| **Recall@k** | Of the files that *should* be found, what fraction landed in the top-k? |
| **MRR** | How high up is the *first* correct file? (1.0 = always rank 1) |
| **nDCG@k** | Are the correct files ranked near the top, with diminishing credit lower down? |

Evaluation is at the **file** level (chunks are de-duplicated to their file).

## Files

| File | Role |
|------|------|
| `metrics.py` | Pure metric functions (Recall@k, reciprocal rank, nDCG@k). Stdlib only, fully unit-tested. |
| `run_eval.py` | Loads the golden set, runs `query()` per query, prints (and optionally writes) the report. |
| `golden.jsonl` | The labelled set: one `{"query", "relevant_files"}` JSON object per line. |
| `sample_corpus/` | A tiny throwaway .NET app so the demo runs end-to-end with no external project. |
| `run_demo.sh` | One command: index the sample corpus → run the eval. |
| `requirements-eval.txt` | Just the deps needed for retrieval evaluation (no Ollama/LLM). |

## Run the demo

```bash
bash eval/run_demo.sh
```

This creates a `.venv`, installs the stack, starts Qdrant via Docker, indexes
`sample_corpus/`, and prints a report like:

```
query                                            recall@5      mrr     ndcg@5
------------------------------------------------------------------------------
where is the user password verified at login        1.000    1.000      1.000
...
------------------------------------------------------------------------------
MEAN (8 queries)                                    0.xxx    0.xxx      0.xxx
```

## Run the unit tests (no ML stack needed)

The metric math and the eval loop are tested without Docker, torch, or any
download:

```bash
python -m pytest tests/ -q
```

## Use it on YOUR project

1. Index your real codebase (`python scripts/build_index.py` from your project
   root), as in the top-level README.
2. Replace `golden.jsonl` with **30–50** queries that reflect how people actually
   ask about your code, each labelled with the file(s) that genuinely answer it.
   Aim for variety: "where is X", "how does Y work", exact identifier lookups,
   and conceptual questions. Quality of labels > quantity.
3. Run:
   ```bash
   PYTHONPATH=scripts:eval python eval/run_eval.py --golden eval/golden.jsonl --k 5 --out report.json
   ```
4. Record the numbers. Make one retrieval change. Re-run. Compare. Keep what
   moves the metrics up.

## Golden file format

```json
{"query": "where is authentication handled", "relevant_files": ["AuthService.cs"]}
{"query": "what happens when an order is placed", "relevant_files": ["OrdersController.cs", "EmailService.cs"]}
```

- `query` — the natural-language question (non-empty).
- `relevant_files` — file *names* (basenames) that count as correct (non-empty).
  A query may have several correct files.
