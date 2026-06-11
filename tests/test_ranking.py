"""Unit tests for the pure fusion math in scripts/ranking.py.

These pin down the CURRENT scoring behavior so retrieval changes (e.g. the
planned RRF fusion) show up as deliberate test updates, not silent drift.
Needs only numpy; skips on a bare interpreter (CI installs no ML stack).
"""
import types

import pytest

pytest.importorskip("numpy")

from ranking import cosine, expand_query, make_normalizer, score_candidates


def node(node_id, score):
    return types.SimpleNamespace(node_id=node_id, score=score)


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


# ── make_normalizer ──────────────────────────────────────────────────────────

def test_normalizer_maps_range_to_unit_interval():
    normalize = make_normalizer(2.0, 4.0)
    assert normalize(2.0) == 0.0
    assert normalize(4.0) == 1.0
    assert normalize(3.0) == 0.5


def test_normalizer_degenerate_range_returns_zero():
    assert make_normalizer(3.0, 3.0)(3.0) == 0.0


# ── score_candidates ─────────────────────────────────────────────────────────

def test_empty_candidates_score_to_empty_list():
    assert score_candidates([], {}, [1.0], {}) == []


def test_embedding_similarity_dominates_with_alpha_one():
    nodes = [node("close", 0.5), node("far", 0.5)]
    counts = {"close": 1, "far": 1}
    embs = {"close": [1.0, 0.0], "far": [0.0, 1.0]}

    scored = dict_scores(score_candidates(nodes, counts, [1.0, 0.0], embs,
                                          alpha=1.0, freq_weight=0.0))
    assert scored["close"] > scored["far"]


def test_retriever_score_dominates_with_alpha_zero():
    nodes = [node("high", 10.0), node("low", 1.0)]
    counts = {"high": 1, "low": 1}
    emb = [1.0, 0.0]
    embs = {"high": emb, "low": emb}

    scored = dict_scores(score_candidates(nodes, counts, emb, embs,
                                          alpha=0.0, freq_weight=0.0))
    assert scored["high"] == pytest.approx(1.0)  # normalized max
    assert scored["low"] == pytest.approx(0.0)   # normalized min


def test_none_retriever_score_contributes_zero():
    nodes = [node("scored", 5.0), node("unscored", None)]
    counts = {"scored": 1, "unscored": 1}
    emb = [1.0]
    embs = {"scored": emb, "unscored": emb}

    scored = dict_scores(score_candidates(nodes, counts, emb, embs,
                                          alpha=0.0, freq_weight=0.0))
    assert scored["unscored"] == 0.0


def test_frequency_boost_favors_repeatedly_retrieved_nodes():
    nodes = [node("popular", 1.0), node("rare", 1.0)]
    counts = {"popular": 4.5, "rare": 1.0}
    emb = [1.0]
    embs = {"popular": emb, "rare": emb}

    scored = dict_scores(score_candidates(nodes, counts, emb, embs,
                                          alpha=0.0, freq_weight=1.0))
    assert scored["popular"] == pytest.approx(1.0)  # log1p(max)/log1p(max)
    assert 0.0 < scored["rare"] < 1.0


def test_results_keep_input_order_not_sorted():
    nodes = [node("b", 1.0), node("a", 9.0)]
    counts = {"a": 1, "b": 1}
    emb = [1.0]
    embs = {"a": emb, "b": emb}

    result = score_candidates(nodes, counts, emb, embs)
    assert [n.node_id for n, _ in result] == ["b", "a"]


def dict_scores(scored):
    return {n.node_id: s for n, s in scored}
