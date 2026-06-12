"""Unit tests for the pure fusion math in scripts/ranking.py.

These pin down the CURRENT scoring behavior (RRF fusion + embedding
re-rank blend) so retrieval changes show up as deliberate test updates,
not silent drift. Needs only numpy; skips on a bare interpreter (CI
installs no ML stack).
"""
import types

import pytest

pytest.importorskip("numpy")

from ranking import cosine, expand_query, rrf_scores, score_candidates


def node(node_id):
    return types.SimpleNamespace(node_id=node_id)


def dict_scores(scored):
    return {n.node_id: s for n, s in scored}


# ── expand_query ─────────────────────────────────────────────────────────────

def test_expand_query_keeps_original_first():
    variants = expand_query("where is auth")
    assert variants[0] == "where is auth"
    assert len(variants) == 3
    assert all("where is auth" in v for v in variants)


# ── cosine ───────────────────────────────────────────────────────────────────

def test_cosine_identical_vectors():
    assert cosine([1.0, 2.0], [1.0, 2.0]) == pytest.approx(1.0)


def test_cosine_orthogonal_vectors():
    assert cosine([1.0, 0.0], [0.0, 1.0]) == pytest.approx(0.0)


def test_cosine_zero_vector_is_zero_not_nan():
    assert cosine([0.0, 0.0], [1.0, 1.0]) == 0.0


# ── rrf_scores ───────────────────────────────────────────────────────────────

def test_rrf_empty_input_gives_empty_dict():
    assert rrf_scores([]) == {}


def test_rrf_top_of_every_list_scores_one():
    fused = rrf_scores([["a", "b"], ["a", "c"]])
    assert fused["a"] == pytest.approx(1.0)


def test_rrf_better_rank_wins_within_one_list():
    fused = rrf_scores([["a", "b", "c"]])
    assert fused["a"] > fused["b"] > fused["c"]


def test_rrf_presence_in_more_lists_wins():
    # "b" is mid-ranked everywhere; "c" appears in only one list.
    fused = rrf_scores([["a", "b"], ["b", "c"]])
    assert fused["b"] > fused["c"]


def test_rrf_ignores_raw_scores_entirely():
    # The whole point of the H1 fix: rrf_scores never sees retriever
    # scores, only positions — a huge BM25 score cannot exist here.
    fused = rrf_scores([["winner", "loser"]])
    assert set(fused) == {"winner", "loser"}
    assert fused["winner"] > fused["loser"]


def test_rrf_weighted_list_counts_more():
    # Same rank-2 position in both lists, but the first list weighs double.
    fused = rrf_scores([["x", "a"], ["y", "b"]], weights=[2.0, 1.0])
    assert fused["a"] > fused["b"]
    assert fused["x"] > fused["y"]


def test_rrf_scores_never_exceed_one():
    fused = rrf_scores(
        [["a", "b", "c"], ["a", "c", "b"], ["a"]], weights=[1.5, 1.0, 1.0]
    )
    # "a" holds rank 1 everywhere — the theoretical maximum.
    assert fused["a"] == pytest.approx(1.0)
    assert all(0.0 < s <= 1.0 + 1e-9 for s in fused.values())


# ── score_candidates ─────────────────────────────────────────────────────────

def test_empty_candidates_score_to_empty_list():
    assert score_candidates([], {}, [1.0], {}) == []


def test_embedding_similarity_dominates_with_alpha_one():
    nodes = [node("close"), node("far")]
    fused = {"close": 0.1, "far": 0.9}
    embs = {"close": [1.0, 0.0], "far": [0.0, 1.0]}

    scored = dict_scores(
        score_candidates(nodes, fused, [1.0, 0.0], embs, alpha=1.0)
    )
    assert scored["close"] > scored["far"]


def test_rrf_score_dominates_with_alpha_zero():
    nodes = [node("fused_high"), node("fused_low")]
    fused = {"fused_high": 0.8, "fused_low": 0.2}
    emb = [1.0, 0.0]
    embs = {"fused_high": emb, "fused_low": emb}

    scored = dict_scores(score_candidates(nodes, fused, emb, embs, alpha=0.0))
    assert scored["fused_high"] == pytest.approx(0.8)
    assert scored["fused_low"] == pytest.approx(0.2)


def test_node_missing_from_fusion_contributes_zero():
    nodes = [node("fused"), node("unfused")]
    fused = {"fused": 0.5}
    emb = [1.0]
    embs = {"fused": emb, "unfused": emb}

    scored = dict_scores(score_candidates(nodes, fused, emb, embs, alpha=0.0))
    assert scored["unfused"] == 0.0


def test_results_keep_input_order_not_sorted():
    nodes = [node("b"), node("a")]
    fused = {"a": 0.9, "b": 0.1}
    emb = [1.0]
    embs = {"a": emb, "b": emb}

    result = score_candidates(nodes, fused, emb, embs)
    assert [n.node_id for n, _ in result] == ["b", "a"]
