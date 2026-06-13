"""The hybrid search engine: turn a question into ranked code chunks.

This is the heart of the search. For one question it:
  1. makes a few query variants (the original + domain hints),
  2. runs three retrievers on each variant (see build_query_engine),
  3. fuses all the ranked lists into one with RRF (ranking.py),
  4. takes the best candidates and re-ranks them (cosine, or an optional
     cross-encoder), then
  5. returns the top chunks as JSON.

Output contract: this module writes ONLY the final JSON to stdout. All logging
goes to stderr (the logging module's default), because mcp_server.py and the CLI
read this script's stdout as JSON — log text there would corrupt it.
"""
from llama_index.core import Settings
from llama_index.core.retrievers import VectorIndexRetriever
from llama_index.core.schema import TextNode
from llama_index.core.vector_stores.utils import metadata_dict_to_node
from llama_index.retrievers.bm25 import BM25Retriever
from llama_index.vector_stores.qdrant import QdrantVectorStore
from llama_index.core import VectorStoreIndex

import sys
import json
import logging
from time import time

import model_setup
import qdrant
from config import settings
from embedding_cache import EmbeddingCache
from ranking import (
    expand_query, rrf_scores, score_candidates, blend_cross_encoder,
)
from logging_setup import setup_logging

logger = logging.getLogger(__name__)

# Comma-separated suffixes for extra query variants (env QUERY_VARIANT_SUFFIXES).
# Put domain terms for your codebase here (e.g. "implementation,.NET core
# backend") instead of hard-coding them in the pipeline.
QUERY_VARIANT_SUFFIXES = settings.query_variant_suffixes

# How many of the top fused candidates go to the embedding re-rank. The cut is a
# query-time knob (no reindex needed to change it), kept as an env var so its
# effect on recall can be measured by the eval (production-readiness plan 2.2).
RERANK_CANDIDATES = settings.rerank_candidates

# LRU cap for the per-query retriever-results cache (_retrieve_cache, below). The
# warm service is long-lived, so the cache must not grow without bound (M7).
RETRIEVE_CACHE_SIZE = settings.retrieve_cache_size

# Optional cross-encoder re-ranker (production-readiness plan 2.6). Off by
# default (empty). When set to a model name, the candidate pool is re-scored by a
# cross-encoder that reads each (query, chunk) pair jointly, blended with the RRF
# score. Recommended: "cross-encoder/ms-marco-MiniLM-L-6-v2" — on the real eval it
# beats the cosine re-rank (MRR 0.716 -> 0.751, nDCG@5 0.760 -> 0.782) for ~1 s/
# query on CPU. Bigger models (e.g. BAAI/bge-reranker-base) are marginally better
# but several times slower. It is blended, not used alone: pure cross-encoder
# ordering scored worse, because it drops the BM25/keyword signal code search needs.
CROSS_ENCODER_MODEL = settings.cross_encoder_model
# Weight on the cross-encoder vs the RRF score in the blend (tuned on the eval).
CROSS_ENCODER_WEIGHT = 0.85

_cross_encoder = None


def _get_cross_encoder():
    """Load the cross-encoder once (it picks CUDA automatically when present)."""
    global _cross_encoder
    if _cross_encoder is None:
        from sentence_transformers import CrossEncoder
        logger.info("loading cross-encoder %s", CROSS_ENCODER_MODEL)
        _cross_encoder = CrossEncoder(CROSS_ENCODER_MODEL)
    return _cross_encoder

logger.debug("BEGIN query_index")

# The embedding cache is built lazily on first use (see _get_cache), not at
# import time, so just importing this module never opens a database file. That
# keeps imports cheap and tests side-effect free.
_emb_cache = None


def _get_cache():
    """Return the shared EmbeddingCache, creating it on first call.

    The cache is keyed by the model NAME string (settings.embed_model), so it
    can never serve vectors from a different model than the one in use.
    """
    global _emb_cache
    if _emb_cache is None:
        _emb_cache = EmbeddingCache(
            settings.emb_cache_path,
            settings.embed_model,        # config string name -> part of the key
            settings.emb_cache_max_size,
        )
    return _emb_cache


def load_all_nodes(client, collection_name):
    """Rebuild all nodes from the Qdrant collection (keeps the node IDs, so
    deduplication against the vector retriever still works)."""
    nodes = []
    offset = None
    while True:
        points, offset = client.scroll(
            collection_name=collection_name,
            with_payload=True,
            with_vectors=False,
            limit=256,
            offset=offset,
        )
        for p in points:
            payload = p.payload or {}
            try:
                node = metadata_dict_to_node(payload, text=payload.get("text"))
            except Exception:
                node = TextNode(id_=str(p.id), text=payload.get("text", ""))
            nodes.append(node)
        if offset is None:
            break
    return nodes


def get_bm25_retriever(index):
    """Build the BM25 keyword retriever from the nodes stored in Qdrant.

    BM25 is the classic keyword search (exact word matches). LlamaIndex's BM25
    lives in memory, so we load every chunk back out of Qdrant and feed them in.
    This runs once per process (the service stays warm), so the cost is paid once.
    """
    # BM25 is in-memory only: rebuild it from the nodes stored in Qdrant each
    # time the engine is built (once per long-lived process).
    client = qdrant.get_qdrant_client()
    nodes = load_all_nodes(client, qdrant.COLLECTION_NAME)
    logger.debug("BM25 nodes loaded from Qdrant: %d", len(nodes))
    if not nodes:
        # Lesson from review finding C2: an empty node list must fail with a
        # clear action, not BM25Retriever's opaque ValueError.
        raise RuntimeError(
            f"No nodes found in Qdrant collection '{qdrant.COLLECTION_NAME}' "
            "for BM25 keyword search. Run build_index.py first."
        )
    return BM25Retriever.from_defaults(nodes=nodes)


def load_index():
    """Connect to Qdrant and wrap the collection as a LlamaIndex vector index."""
    qdrant.ensure_qdrant()
    client = qdrant.get_qdrant_client()
    if client is None:
        raise RuntimeError("Could not connect to Qdrant")
    vector_store = QdrantVectorStore(
        client=client,
        collection_name=qdrant.COLLECTION_NAME
    )

    return VectorStoreIndex.from_vector_store(vector_store)

def build_query_engine():
    """Build the three retrievers we search with. Slow, so done once per process.

    WHY three retrievers, not one — each is good at something different, and
    fusing them (step 3 in query()) beats any one alone:
      - narrow vector (top-6): semantic search, precise. The most likely chunks.
      - BM25: keyword search. Code search needs exact word/identifier matches
        (a class name, a method name) that meaning-based vectors can miss.
      - wide vector (top-12): semantic search, recall safety net. Casts a wider
        net so a correct chunk that just missed the top-6 still gets a chance.

    The `#6` / `#12` notes are just the tuned top_k values.
    """
    logger.debug("build_query_engine(): START")

    # Configure the embedding model now (lazy, plan 3.7): the retrievers below
    # and the cosine re-rank in query() need Settings.embed_model. query() always
    # builds the engine first, so this covers the whole search path.
    model_setup.setup_models()

    # 1. load index
    logger.debug("build_query_engine(): load index START")
    start = time()
    index = load_index()
    logger.debug("len(index.docstore.docs) = %d", len(index.docstore.docs))
    logger.debug("build_query_engine(): load index END. Duration %.2f", time() - start)

    # 2. semantic search, narrow: the top few chunks by meaning.
    logger.debug("build_query_engine(): VectorIndexRetriever (narrow) START")
    vector_retriever = VectorIndexRetriever(
        index=index,
        similarity_top_k=6 #6
    )
    logger.debug("build_query_engine(): VectorIndexRetriever (narrow) END")

    # 3. keyword search: exact word/identifier matches (BM25).
    logger.debug("build_query_engine(): BM25Retriever START")
    bm25_retriever = get_bm25_retriever(index)
    logger.debug("build_query_engine(): BM25Retriever END")

    # 4. semantic search, wide: more chunks by meaning, as a recall safety net.
    logger.debug("build_query_engine(): VectorIndexRetriever (wide) START")
    vector_wide = VectorIndexRetriever(
        index=index,
        similarity_top_k=12 #12
    )
    logger.debug("build_query_engine(): VectorIndexRetriever (wide) END")

    logger.debug("build_query_engine(): END")

    return vector_retriever, bm25_retriever, vector_wide

# Insertion-ordered dict used as a small LRU (plain dict keeps insertion order in
# Python 3.7+): on a hit we pop+reinsert to mark "most recent"; on overflow we
# drop the first (least-recently-used) key. Bounds memory in the warm service (M7).
_retrieve_cache = {}

def hybrid_retrieve(query, vector, bm25, vector_wide):
    """Run the three retrievers; return one ranked result list per retriever
    (RRF fuses by rank, so the lists must stay separate). Results are cached per
    query, LRU-capped at RETRIEVE_CACHE_SIZE."""
    if query in _retrieve_cache:
        results = _retrieve_cache.pop(query)
        _retrieve_cache[query] = results   # reinsert -> now most-recently-used
        return results

    logger.debug("hybrid_retrieve start for query '%s'", query)
    logger.debug("vector.retrieve")
    results = [vector.retrieve(query)]
    logger.debug("bm25.retrieve")
    results.append(bm25.retrieve(query))
    logger.debug("vector_wide.retrieve")
    results.append(vector_wide.retrieve(query))
    logger.debug("hybrid_retrieve end for query '%s'", query)

    _retrieve_cache[query] = results
    # Evict the least-recently-used entry once we exceed the cap.
    if len(_retrieve_cache) > RETRIEVE_CACHE_SIZE:
        del _retrieve_cache[next(iter(_retrieve_cache))]
    return results


_engine = None

def get_engine():
    """Return the warm engine, building it on the first call (then reusing it)."""
    global _engine
    if _engine is None:
        _engine = build_query_engine()
    return _engine


def query(q, alpha=0.7, top_k=8):
    """Search the codebase for `q` and return the top_k chunks as a dict.

    Steps: expand the query into variants -> run the three retrievers on each
    -> fuse all the ranked lists with RRF -> keep the best candidates -> re-rank
    them (cosine blend, or an optional cross-encoder) -> return the top_k.

    Returns {"answer": <summary>, "sources": [file names], "context": [chunks]}.
    """
    logger.debug("query(): get_engine")

    vector, bm25, vector_wide = get_engine()

    queries = expand_query(q, QUERY_VARIANT_SUFFIXES)

    # One ranked id-list per (variant, retriever) pair. The user's original
    # words weigh more than the added variants: variants help recall, but the
    # real question should still decide the ranking (weight 1.5 vs 1.0).
    ranked_lists = []
    list_weights = []
    nodes_by_id = {}

    logger.debug("query(): gather nodes START")
    for sub_q in queries:
        weight = 1.5 if sub_q == q else 1.0
        for retriever_results in hybrid_retrieve(sub_q, vector, bm25, vector_wide):
            ranked_lists.append([n.node_id for n in retriever_results])
            list_weights.append(weight)
            for n in retriever_results:
                nodes_by_id.setdefault(n.node_id, n)

    # RRF fuses by rank only, so cosine and BM25 scales cannot clash (H1).
    fused = rrf_scores(ranked_lists, weights=list_weights)

    # The candidate cut now happens AFTER fusion: the best fused nodes go to the
    # embedding re-rank, instead of the first ones in arrival order. The cut size
    # is RERANK_CANDIDATES (default 30).
    ordered_nodes = sorted(
        nodes_by_id.values(), key=lambda n: fused[n.node_id], reverse=True
    )[:RERANK_CANDIDATES]

    logger.debug("Candidates: %d", len(ordered_nodes))
    logger.debug("query(): gather nodes END")

    logger.debug("query(): scores and ranks START")
    # Re-rank step: take a closer look at the candidates and give a final score.
    # Two ways to do it — see docs/CONCEPTS.md (re-ranking):
    #   - cross-encoder (optional): slower but more accurate, reads the query and
    #     the chunk together. On only when CROSS_ENCODER_MODEL is set.
    #   - cosine blend (default): fast, compares the query vector and chunk vector.
    if CROSS_ENCODER_MODEL:
        # Cross-encoder: score each (query, chunk) pair jointly. No cached chunk
        # embeddings — the score depends on the query, so we pay one forward pass
        # per candidate here (production-readiness plan 2.6).
        ce = _get_cross_encoder()
        ce_logits = ce.predict([(q, n.text) for n in ordered_nodes])
        scored = blend_cross_encoder(
            ordered_nodes, ce_logits, fused, CROSS_ENCODER_WEIGHT,
        )
    else:
        # Default: bi-encoder cosine blended with the RRF score.
        # Note: Settings.embed_model (capital S) is the llama-index model
        # INSTANCE that computes vectors; settings.embed_model (lowercase) is
        # the model NAME string the cache keys on.
        embed_model = Settings.embed_model
        query_emb = embed_model.get_text_embedding(q)
        texts = [n.text for n in ordered_nodes]
        try:
            # One batched cache call: hits read from disk, misses are computed
            # and stored, all in a single transaction (one disk write).
            embeddings = _get_cache().get_many(texts, embed_model)
        except Exception as e:
            logger.warning("cache failed (%s); recomputing without cache", e)
            embeddings = [embed_model.get_text_embedding(t) for t in texts]
        text_embeddings = {
            n.node_id: emb for n, emb in zip(ordered_nodes, embeddings)
        }
        scored = score_candidates(
            ordered_nodes, fused, query_emb, text_embeddings, alpha=alpha,
        )
    ranked = sorted(scored, key=lambda x: x[1], reverse=True)
    logger.debug("query(): scores and ranks END")

    final_nodes = [n for n, _ in ranked[:top_k]]

    logger.debug("Returned: %d", len(final_nodes))

    score_map = {n.node_id: s for n, s in scored}
    context = [
        {
            "file": n.metadata.get("file_name"),
            "path": n.metadata.get("file_path"),
            "text": n.text[:800],
            "score": round(score_map[n.node_id], 3),
            "rank": i + 1
        }
        for i, n in enumerate(final_nodes)
    ]

    return {
        "answer": f"Top {len(context)} relevant code snippets retrieved. See context for details.",
        "sources": sorted(set(n["file"] for n in context)),
        "context": context
    }

if __name__ == "__main__":
    setup_logging()  # logs to stderr; stdout stays reserved for the JSON below
    q = " ".join(sys.argv[1:])
    # Emit the result as JSON on real stdout (logging goes to stderr, so the
    # stdout stream carries only this JSON — the CLI / MCP contract).
    sys.stdout.write(json.dumps(query(q)) + "\n")