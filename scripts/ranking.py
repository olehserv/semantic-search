"""Pure ranking/fusion math for the hybrid retrieval pipeline.

Depends only on numpy — no LlamaIndex, no models — so scoring behavior is
unit-testable without the ML stack, and retrieval-quality changes stay
isolated in one module.
"""
import numpy as np

# Standard RRF dampening constant: with k=60 the gap between rank 1 and
# rank 2 is small, so one retriever cannot dominate the fusion.
RRF_K = 60


def expand_query(q, suffixes=("implementation",)):
    """Variants of the user query that are retrieved and fused together.

    suffixes : phrases appended to the query, one extra variant each.
    The original query always comes first (the fusion weighs it higher).
    Domain terms (e.g. ".NET backend") belong in the caller's configuration,
    not here.
    """
    return [q] + [f"{q} {s}" for s in suffixes]


def cosine(a, b):
    norm_a = np.linalg.norm(a)
    norm_b = np.linalg.norm(b)
    if norm_a == 0 or norm_b == 0:
        return 0.0
    return np.dot(a, b) / (norm_a * norm_b)


def rrf_scores(ranked_lists, weights=None, k=RRF_K):
    """Reciprocal Rank Fusion over several ranked lists of node ids.

    Uses only the *rank* of a node inside each list, never the raw retriever
    score, so cosine (0-1) and BM25 (unbounded) lists fuse fairly.

    ranked_lists : list of lists of node ids, best result first
    weights      : optional per-list weight (default 1.0 for every list)
    k            : dampening constant; each list adds weight / (k + rank)

    Returns {node_id: fused_score}, scaled so that rank 1 in every list
    gives exactly 1.0.
    """
    if not ranked_lists:
        return {}
    if weights is None:
        weights = [1.0] * len(ranked_lists)

    fused = {}
    for ids, weight in zip(ranked_lists, weights):
        for rank, node_id in enumerate(ids, start=1):
            fused[node_id] = fused.get(node_id, 0.0) + weight / (k + rank)

    best_possible = sum(w / (k + 1) for w in weights)
    return {nid: s / best_possible for nid, s in fused.items()}


def score_candidates(nodes, fused_scores, query_emb, text_embeddings,
                     alpha=0.7):
    """Blend the RRF fusion with an embedding re-rank into one final score.

    final = alpha * cosine(query, chunk) + (1 - alpha) * fused RRF score

    nodes           : candidate nodes exposing .node_id
    fused_scores    : node_id -> RRF score from rrf_scores()
    query_emb       : embedding of the original query
    text_embeddings : node_id -> embedding of the chunk text

    Returns [(node, final_score), ...] in input order (not sorted).
    """
    scored = []
    for n in nodes:
        emb_score = cosine(query_emb, text_embeddings[n.node_id])
        rrf_score = fused_scores.get(n.node_id, 0.0)
        scored.append((n, alpha * emb_score + (1 - alpha) * rrf_score))
    return scored
