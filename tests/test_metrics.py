"""Unit tests for the retrieval-quality metrics (eval/metrics.py).

Metrics operate on rank-ordered, de-duplicated lists of retrieved item IDs
(e.g. file paths) compared against a set of relevant (golden) IDs.
"""
import math

import pytest

from metrics import recall_at_k, reciprocal_rank, ndcg_at_k, aggregate


# ── recall_at_k ──────────────────────────────────────────────────────────────

def test_recall_finds_single_relevant_within_k():
    # one relevant item, present at rank 2, k=3 → all relevant found → 1.0
    assert recall_at_k(["a", "b", "c"], {"b"}, k=3) == 1.0


def test_recall_misses_relevant_outside_k():
    # relevant item sits at rank 3 but k=2 → not found in top-2 → 0.0
    assert recall_at_k(["a", "b", "c"], {"c"}, k=2) == 0.0


def test_recall_is_fraction_of_relevant_found():
    # two relevant items, only one within top-3 → 0.5
    assert recall_at_k(["a", "b", "c"], {"b", "z"}, k=3) == 0.5


def test_recall_raises_on_empty_relevant():
    # empty golden set is malformed data and must be surfaced, not silently 0/1
    with pytest.raises(ValueError):
        recall_at_k(["a"], set(), k=1)


# ── reciprocal_rank (MRR is the mean of this across queries) ─────────────────

def test_reciprocal_rank_first_position():
    assert reciprocal_rank(["a", "b"], {"a"}) == 1.0


def test_reciprocal_rank_third_position():
    assert reciprocal_rank(["a", "b", "c"], {"c"}) == pytest.approx(1 / 3)


def test_reciprocal_rank_uses_first_relevant_hit():
    # both b and c are relevant; first hit is at rank 2 → 1/2
    assert reciprocal_rank(["a", "b", "c"], {"b", "c"}) == 0.5


def test_reciprocal_rank_zero_when_absent():
    assert reciprocal_rank(["a", "b"], {"z"}) == 0.0


# ── ndcg_at_k ────────────────────────────────────────────────────────────────

def test_ndcg_perfect_when_only_relevant_is_first():
    assert ndcg_at_k(["a", "b", "c"], {"a"}, k=3) == pytest.approx(1.0)


def test_ndcg_discounts_lower_rank():
    # single relevant at rank 2: DCG = 1/log2(3), IDCG = 1/log2(2) = 1
    expected = (1 / math.log2(3)) / 1.0
    assert ndcg_at_k(["a", "b", "c"], {"b"}, k=3) == pytest.approx(expected)


def test_ndcg_zero_when_no_relevant_in_topk():
    assert ndcg_at_k(["a", "b", "c"], {"z"}, k=3) == 0.0


def test_ndcg_perfect_for_ideal_multi_relevant_ordering():
    # two relevant items occupying the top two ranks = ideal ordering → 1.0
    assert ndcg_at_k(["a", "b", "c"], {"a", "b"}, k=3) == pytest.approx(1.0)


# ── aggregate ────────────────────────────────────────────────────────────────

def test_aggregate_means_each_metric_across_queries():
    per_query = [
        {"recall@5": 1.0, "mrr": 1.0, "ndcg@5": 1.0},
        {"recall@5": 0.0, "mrr": 0.0, "ndcg@5": 0.0},
    ]
    out = aggregate(per_query)
    assert out["recall@5"] == 0.5
    assert out["mrr"] == 0.5
    assert out["ndcg@5"] == 0.5


def test_aggregate_empty_is_empty():
    assert aggregate([]) == {}
