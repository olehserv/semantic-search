"""Retrieval evaluation harness.

Runs the search pipeline's `query()` over a labelled golden set and reports
Recall@k, MRR, and nDCG@k — both per query and averaged. This is the gate that
makes every retrieval change (docs/reviews/2026-06-11-production-readiness-plan.md,
Phase 2) measurable instead of guessed.

Granularity: evaluation is at the **file** level. A query is "answered" when the
relevant file(s) appear among the retrieved chunks' files. Golden labels list
file *names* (e.g. "AuthService.cs").

Usage (from the project root, with the stack + an index built — see eval/README.md):

    python eval/run_eval.py --golden eval/golden.jsonl --k 5

Diagnostic noise from query_index goes to stderr; the final report is printed to
stdout (and optionally written as JSON with --out).
"""
import argparse
import json
import sys

from metrics import recall_at_k, reciprocal_rank, ndcg_at_k, aggregate


def load_golden(path):
    """Read a JSONL golden file into a list of {query, relevant_files} dicts."""
    entries = []
    with open(path, encoding="utf-8") as f:
        for line_no, line in enumerate(f, start=1):
            line = line.strip()
            if not line:
                continue
            entry = json.loads(line)
            if not entry.get("query") or not entry.get("relevant_files"):
                raise ValueError(
                    f"{path}:{line_no}: each entry needs a non-empty "
                    f"'query' and 'relevant_files'"
                )
            entries.append(entry)
    return entries


def ranked_files(result):
    """Extract rank-ordered, de-duplicated file names from a query() result."""
    seen = set()
    files = []
    for chunk in result.get("context", []):
        name = chunk.get("file")
        if name and name not in seen:
            seen.add(name)
            files.append(name)
    return files


def evaluate(golden, k, query_fn):
    """Run `query_fn` over every golden entry and compute metrics.

    `query_fn(query, top_k)` must return a query()-shaped dict. Returns
    {"per_query": [...rows...], "summary": {...means...}}.
    """
    recall_key = f"recall@{k}"
    ndcg_key = f"ndcg@{k}"

    per_query = []
    numeric_rows = []
    for entry in golden:
        relevant = set(entry["relevant_files"])
        retrieved = ranked_files(query_fn(entry["query"], top_k=k))

        scores = {
            recall_key: recall_at_k(retrieved, relevant, k),
            "mrr": reciprocal_rank(retrieved, relevant),
            ndcg_key: ndcg_at_k(retrieved, relevant, k),
        }
        numeric_rows.append(scores)
        per_query.append({"query": entry["query"], "retrieved": retrieved, **scores})

    return {"per_query": per_query, "summary": aggregate(numeric_rows)}


def _default_query_fn():
    """Lazily import the real pipeline so tests/imports don't need the ML stack."""
    from query_index import query  # noqa: E402 (deferred: heavy import side effects)
    return query


def _print_report(report, k):
    recall_key, ndcg_key = f"recall@{k}", f"ndcg@{k}"
    print(f"\n{'query':<48} {recall_key:>10} {'mrr':>8} {ndcg_key:>10}")
    print("-" * 78)
    for row in report["per_query"]:
        print(
            f"{row['query'][:47]:<48} "
            f"{row[recall_key]:>10.3f} {row['mrr']:>8.3f} {row[ndcg_key]:>10.3f}"
        )
    s = report["summary"]
    print("-" * 78)
    print(
        f"{'MEAN (' + str(len(report['per_query'])) + ' queries)':<48} "
        f"{s[recall_key]:>10.3f} {s['mrr']:>8.3f} {s[ndcg_key]:>10.3f}\n"
    )


def main():
    parser = argparse.ArgumentParser(description="Retrieval eval harness")
    parser.add_argument("--golden", default="eval/golden.jsonl",
                        help="Path to the JSONL golden set")
    parser.add_argument("--k", type=int, default=5, help="Cutoff k for the metrics")
    parser.add_argument("--out", help="Optional path to write the full report as JSON")
    args = parser.parse_args()

    golden = load_golden(args.golden)
    report = evaluate(golden, k=args.k, query_fn=_default_query_fn())

    _print_report(report, args.k)

    if args.out:
        with open(args.out, "w", encoding="utf-8") as f:
            json.dump(report, f, indent=2)
        print(f"Wrote report to {args.out}", file=sys.stderr)


if __name__ == "__main__":
    main()
