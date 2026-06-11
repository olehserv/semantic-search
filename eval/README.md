# Retrieval Evaluation Harness

The measurement gate for the search system: **a retrieval change is an
improvement only if these metrics say so.** Every Phase 2 item in
`docs/reviews/2026-06-11-production-readiness-plan.md` (RRF fusion, code-aware
chunking, re-ranker, …) gets validated here before merging.

For each query in a labelled golden set, the harness runs the real `query()`
pipeline and compares the **files** the retrieved chunks came from against the
known-correct files:

| Metric | Question it answers |
|--------|---------------------|
| **Recall@k** | Of the files that should be found, what fraction landed in the top-k? |
| **MRR** | How high up is the first correct file? (1.0 = always rank 1) |
| **nDCG@k** | Are correct files ranked near the top, with diminishing credit lower down? |

| File | Role |
|------|------|
| `metrics.py` | Pure metric functions (stdlib only, unit-tested) |
| `run_eval.py` | Runs `query()` per golden entry, prints/writes the report |
| `golden.jsonl` | Labelled set: one `{"query", "relevant_files"}` per line |
| `sample_corpus/` | Tiny throwaway .NET app so the demo runs self-contained |
| `run_demo.sh` | One command: venv + Qdrant + index the corpus + run the eval |

## Run it

```bash
bash eval/run_demo.sh            # end-to-end demo on the sample corpus
python3 -m pytest tests/ -q     # unit tests (no Docker/ML stack needed)
```

**Warning:** the demo (re)builds the Qdrant collection named in
`scripts/qdrant.py` (`COLLECTION_NAME`) — it will replace a real index that
uses the same name.

## Use it on your project

1. Index your codebase (see the top-level README).
2. Replace `golden.jsonl` with **30–50** queries reflecting how people actually
   ask about your code, each labelled with the file(s) that answer it. Mix
   "where is X", "how does Y work", identifier lookups, and conceptual
   questions. Label quality beats quantity.
3. ```bash
   PYTHONPATH=scripts:eval python eval/run_eval.py --golden eval/golden.jsonl --k 5 --out report.json
   ```
4. Record the numbers. Make **one** retrieval change. Re-run. Keep what moves
   the metrics up.

### Golden file format

```json
{"query": "where is authentication handled", "relevant_files": ["AuthService.cs"]}
{"query": "what happens when an order is placed", "relevant_files": ["OrdersController.cs", "EmailService.cs"]}
```

`relevant_files` are file *names* (basenames); a query may have several.
