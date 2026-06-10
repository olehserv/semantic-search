"""Tests for the eval orchestration in eval/run_eval.py.

These never import the ML stack: run_eval imports query_index lazily, and the
evaluation loop takes an injectable `query_fn`, so a fake stands in for real
retrieval here.
"""
import run_eval


def test_ranked_files_dedups_preserving_rank_order():
    # Two chunks from the same file plus another; keep first-seen order, no dupes.
    result = {
        "context": [
            {"file": "AuthService.cs", "rank": 1},
            {"file": "AuthService.cs", "rank": 2},
            {"file": "Program.cs", "rank": 3},
        ]
    }
    assert run_eval.ranked_files(result) == ["AuthService.cs", "Program.cs"]


def test_ranked_files_skips_missing_filenames():
    result = {"context": [{"file": None}, {"file": "A.cs"}, {}]}
    assert run_eval.ranked_files(result) == ["A.cs"]


def test_evaluate_scores_each_query_and_aggregates():
    golden = [
        {"query": "find auth", "relevant_files": ["AuthService.cs"]},
        {"query": "find db", "relevant_files": ["AppDbContext.cs"]},
    ]

    # Fake retrieval: first query nails it at rank 1, second misses entirely.
    canned = {
        "find auth": {"context": [{"file": "AuthService.cs"}, {"file": "Program.cs"}]},
        "find db": {"context": [{"file": "AuthService.cs"}, {"file": "Program.cs"}]},
    }

    def fake_query_fn(q, top_k):
        return canned[q]

    report = run_eval.evaluate(golden, k=5, query_fn=fake_query_fn)

    # First query: perfect. Second query: relevant file absent → all zero.
    assert report["summary"]["recall@5"] == 0.5
    assert report["summary"]["mrr"] == 0.5
    assert report["summary"]["ndcg@5"] == 0.5
    assert len(report["per_query"]) == 2
    assert report["per_query"][0]["query"] == "find auth"
