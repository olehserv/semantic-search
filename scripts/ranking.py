"""Pure ranking/fusion math for the hybrid retrieval pipeline.

Depends only on numpy — no LlamaIndex, no models — so scoring behavior is
unit-testable without the ML stack, and retrieval-quality changes (e.g. the
planned RRF fusion, production plan Phase 2) stay isolated in one module.
"""
import numpy as np


def expand_query(q):
    """Variants of the user query that are retrieved and fused together.

    NOTE: the ".NET core backend" suffix is a domain assumption; making this
    configurable is plan item 2.3.
    """
    return [
        q,
        f"{q} implementation",
        f"{q} .NET core backend",
    ]


def cosine(a, b):
    norm_a = np.linalg.norm(a)
    norm_b = np.linalg.norm(b)
    if norm_a == 0 or norm_b == 0:
        return 0.0
    return np.dot(a, b) / (norm_a * norm_b)


def make_normalizer(min_s, max_s):
    def normalize(x):
        if max_s - min_s == 0:
            return 0.0
        return (x - min_s) / (max_s - min_s)
    return normalize


def score_candidates(nodes, node_counts, query_emb, text_embeddings,
                     alpha=0.7, freq_weight=0.1):
    """Fuse the retrieval signals into one score per candidate node.

    final = alpha * cosine(query, chunk)
          + (1 - alpha) * min-max-normalized retriever score
          + freq_weight * log-scaled retrieval frequency

    nodes           : candidates exposing .node_id and .score (score may be None)
    node_counts     : node_id -> (weighted) number of times retrieved
    query_emb       : embedding of the original query
    text_embeddings : node_id -> embedding of the chunk text

    Returns [(node, final_score), ...] in input order (not sorted).

    Known limitation (review finding H1, plan item 2.1): the min-max
    normalization pools cosine and BM25 scores, whose scales differ wildly.
    """
    retriever_scores = [float(n.score) for n in nodes if n.score is not None]
    normalize = make_normalizer(
        min(retriever_scores) if retriever_scores else 0,
        max(retriever_scores) if retriever_scores else 1,
    )
    max_count = max(node_counts.values()) if node_counts else 1

    scored = []
    for n in nodes:
        emb_score = cosine(query_emb, text_embeddings[n.node_id])
        retriever_score = normalize(float(n.score)) if n.score is not None else 0.0
        freq_boost = np.log1p(node_counts[n.node_id]) / np.log1p(max_count)
        final_score = (
            alpha * emb_score +
            (1 - alpha) * retriever_score +
            freq_weight * freq_boost
        )
        scored.append((n, final_score))
    return scored
