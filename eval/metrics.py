"""Retrieval-quality metrics for the search eval harness.

All functions are pure and dependency-free (stdlib only) so they can be unit
tested without the ML stack. They operate on:

  retrieved : list[str]  — rank-ordered, de-duplicated retrieved item IDs
                           (rank 1 = index 0). For this project an "ID" is a
                           file path, since we evaluate at the file level.
  relevant  : set[str]   — the golden set of IDs that count as correct answers.

Binary relevance is assumed (an item is either relevant or it is not).
"""
import math


def recall_at_k(retrieved, relevant, k):
    """Fraction of the relevant items that appear in the top-k results.

    recall@k = |relevant ∩ retrieved[:k]| / |relevant|
    """
    if not relevant:
        raise ValueError("`relevant` must be non-empty (malformed golden entry?)")
    top_k = set(retrieved[:k])
    found = len(top_k & set(relevant))
    return found / len(relevant)


def reciprocal_rank(retrieved, relevant):
    """1 / (rank of the first relevant hit), with ranks starting at 1.

    Returns 0.0 if no relevant item appears anywhere in `retrieved`.
    The mean of this across all queries is MRR (Mean Reciprocal Rank).
    """
    relevant = set(relevant)
    for index, item in enumerate(retrieved):
        if item in relevant:
            return 1.0 / (index + 1)
    return 0.0


def ndcg_at_k(retrieved, relevant, k):
    """Normalized Discounted Cumulative Gain at k (binary relevance).

    DCG@k  = Σ_{i=1..k} rel_i / log2(i + 1)
    IDCG@k = DCG of the ideal ordering (all relevant items ranked first)
    nDCG@k = DCG@k / IDCG@k   (0.0 if there is nothing relevant to find)
    """
    relevant = set(relevant)

    dcg = 0.0
    for index, item in enumerate(retrieved[:k]):
        if item in relevant:
            dcg += 1.0 / math.log2(index + 2)  # +2 because ranks are 1-based

    ideal_hits = min(len(relevant), k)
    idcg = sum(1.0 / math.log2(i + 2) for i in range(ideal_hits))

    if idcg == 0:
        return 0.0
    return dcg / idcg


def aggregate(per_query):
    """Mean of each metric across a list of per-query metric dicts.

    Every dict is expected to carry the same keys (e.g. "recall@5", "mrr",
    "ndcg@5"). Returns {} for an empty input.
    """
    if not per_query:
        return {}
    keys = per_query[0].keys()
    return {key: sum(row[key] for row in per_query) / len(per_query) for key in keys}
