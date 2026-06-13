from llama_index.core import Settings
from llama_index.core.retrievers import VectorIndexRetriever
from llama_index.core.schema import TextNode
from llama_index.core.vector_stores.utils import metadata_dict_to_node
from llama_index.retrievers.bm25 import BM25Retriever
from llama_index.vector_stores.qdrant import QdrantVectorStore
from llama_index.core import VectorStoreIndex

import os
import sys
import json
import pickle
import functools
from datetime import datetime
from time import time

# All diagnostic output in this module goes to stderr, so stdout carries only
# the final JSON result (mcp_server.py parses this script's stdout as JSON).
print = functools.partial(print, file=sys.stderr, flush=True)

# Imported for its side effect: configures Settings.embed_model / Settings.llm.
# Deliberately after the print-to-stderr redirect above (E402) so any import
# noise cannot contaminate the JSON on stdout.
import model_setup  # noqa: E402, F401
import qdrant  # noqa: E402
from ranking import (  # noqa: E402
    expand_query, rrf_scores, score_candidates, blend_cross_encoder,
)

EMB_CACHE_PATH = "./.claude/cache/embeddings.pkl"

# Comma-separated suffixes for extra query variants. Put domain terms for
# your codebase here (e.g. "implementation,.NET core backend") instead of
# hard-coding them in the pipeline.
QUERY_VARIANT_SUFFIXES = tuple(
    s.strip()
    for s in os.getenv("QUERY_VARIANT_SUFFIXES", "implementation").split(",")
    if s.strip()
)

# How many of the top fused candidates go to the embedding re-rank. The cut is a
# query-time knob (no reindex needed to change it), kept as an env var so its
# effect on recall can be measured by the eval (production-readiness plan 2.2).
RERANK_CANDIDATES = int(os.getenv("RERANK_CANDIDATES", "30"))

# Optional cross-encoder re-ranker (production-readiness plan 2.6). Off by
# default (empty). When set to a model name, the candidate pool is re-scored by a
# cross-encoder that reads each (query, chunk) pair jointly, blended with the RRF
# score. Recommended: "cross-encoder/ms-marco-MiniLM-L-6-v2" — on the real eval it
# beats the cosine re-rank (MRR 0.716 -> 0.751, nDCG@5 0.760 -> 0.782) for ~1 s/
# query on CPU. Bigger models (e.g. BAAI/bge-reranker-base) are marginally better
# but several times slower. It is blended, not used alone: pure cross-encoder
# ordering scored worse, because it drops the BM25/keyword signal code search needs.
CROSS_ENCODER_MODEL = os.getenv("CROSS_ENCODER_MODEL", "")
# Weight on the cross-encoder vs the RRF score in the blend (tuned on the eval).
CROSS_ENCODER_WEIGHT = 0.85

_cross_encoder = None


def _get_cross_encoder():
    """Load the cross-encoder once (it picks CUDA automatically when present)."""
    global _cross_encoder
    if _cross_encoder is None:
        from sentence_transformers import CrossEncoder
        print(f"[DEBUG] loading cross-encoder {CROSS_ENCODER_MODEL}")
        _cross_encoder = CrossEncoder(CROSS_ENCODER_MODEL)
    return _cross_encoder

_embedding_cache = {}

print(f"[DEBUG] [{datetime.now()}] BEGIN query_index")

def cache_warmup():
    global _embedding_cache
    if os.path.exists(EMB_CACHE_PATH):
        with open(EMB_CACHE_PATH, "rb") as f:
            print("[DEBUG] Loading EMB cache ...")
            # pickle is acceptable here: the file is local, written only by
            # this code. Replacing it (model-keyed, no pickle) is plan
            # item 3.2 — the approved Phase 1 design keeps it as-is.
            _embedding_cache = pickle.load(f)
            print(f"[DEBUG] EMB cache size: {len(_embedding_cache)}")


cache_warmup()

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
    # BM25 is in-memory only: rebuild it from the nodes stored in Qdrant each
    # time the engine is built (once per long-lived process).
    client = qdrant.get_qdrant_client()
    nodes = load_all_nodes(client, qdrant.COLLECTION_NAME)
    print(f"[DEBUG] BM25 nodes loaded from Qdrant: {len(nodes)}")
    if not nodes:
        # Lesson from review finding C2: an empty node list must fail with a
        # clear action, not BM25Retriever's opaque ValueError.
        raise RuntimeError(
            f"No nodes found in Qdrant collection '{qdrant.COLLECTION_NAME}' "
            "for BM25 keyword search. Run build_index.py first."
        )
    return BM25Retriever.from_defaults(nodes=nodes)


def load_index():
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
    print(f"[DEBUG] [{datetime.now()}] build_query_engine(): START")

    # 1. load index
    print(f"[DEBUG] [{datetime.now()}] build_query_engine(): load index START")
    start = time()
    index = load_index()
    print(f"[DEBUG] len(index.docstore.docs) = {len(index.docstore.docs)}")
    print(f"[DEBUG] [{datetime.now()}] build_query_engine(): load index END. Duration {time() - start:.2f}")

    # 2. semantic search
    print(f"[DEBUG] [{datetime.now()}] build_query_engine(): VectorIndexRetriever START")
    vector_retriever = VectorIndexRetriever(
        index=index,
        similarity_top_k=6 #6
    )
    print(f"[DEBUG] [{datetime.now()}] build_query_engine(): VectorIndexRetriever END")

    # 3. keyword search
    print(f"[DEBUG] [{datetime.now()}] build_query_engine(): BM25Retriever START")
    bm25_retriever = get_bm25_retriever(index)
    print(f"[DEBUG] [{datetime.now()}] build_query_engine(): BM25Retriever END")

    # 4. wider search
    print(f"[DEBUG] [{datetime.now()}] build_query_engine(): VectorIndexRetriever START")
    vector_wide = VectorIndexRetriever(
        index=index, 
        similarity_top_k=12 #12
    )
    print(f"[DEBUG] [{datetime.now()}] build_query_engine(): VectorIndexRetriever END")

    print(f"[DEBUG] [{datetime.now()}] build_query_engine(): END")

    return vector_retriever, bm25_retriever, vector_wide

_retrieve_cache = {}

def hybrid_retrieve(query, vector, bm25, vector_wide):
    """Run the three retrievers; return one ranked result list per retriever
    (RRF fuses by rank, so the lists must stay separate)."""
    if query in _retrieve_cache:
        return _retrieve_cache[query]

    print(f"[DEBUG] [{datetime.now()}] hybrid_retrieve start for query '{query}'")
    print(f"[DEBUG] [{datetime.now()}] vector.retrieve")
    results = [vector.retrieve(query)]
    print(f"[DEBUG] [{datetime.now()}] bm25.retrieve")
    results.append(bm25.retrieve(query))
    print(f"[DEBUG] [{datetime.now()}] vector_wide.retrieve")
    results.append(vector_wide.retrieve(query))
    print(f"[DEBUG] [{datetime.now()}] hybrid_retrieve end for query '{query}'")

    _retrieve_cache[query] = results
    return results


_engine = None

def get_engine():
    global _engine
    if _engine is None:
        _engine = build_query_engine()
    return _engine


def get_embedding(text, embed_model):
    if text not in _embedding_cache:
        _embedding_cache[text] = embed_model.get_text_embedding(text)

        # Write to a temp file then atomically replace, so a crash mid-dump
        # can't corrupt the existing cache.
        os.makedirs(os.path.dirname(EMB_CACHE_PATH), exist_ok=True)
        tmp_path = f"{EMB_CACHE_PATH}.tmp"
        with open(tmp_path, "wb") as f:
            pickle.dump(_embedding_cache, f)
        os.replace(tmp_path, EMB_CACHE_PATH)

    return _embedding_cache[text]

def query(q, alpha=0.7, top_k=8):
    print(f"[DEBUG] [{datetime.now()}] query(): get_engine")

    vector, bm25, vector_wide = get_engine()

    queries = expand_query(q, QUERY_VARIANT_SUFFIXES)

    # One ranked id-list per (variant, retriever) pair; the original query's
    # lists weigh more, like the old frequency boost did.
    ranked_lists = []
    list_weights = []
    nodes_by_id = {}

    print(f"[DEBUG] [{datetime.now()}] query(): gather nodes START")
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

    print(f"Candidates: {len(ordered_nodes)}")
    print(f"[DEBUG] [{datetime.now()}] query(): gather nodes END")

    print(f"[DEBUG] [{datetime.now()}] query(): scores and ranks START")
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
        embed_model = Settings.embed_model
        query_emb = embed_model.get_text_embedding(q)
        texts = [n.text for n in ordered_nodes]
        try:
            embeddings = [get_embedding(t, embed_model) for t in texts]
        except Exception as e:
            print(f"[DEBUG] get_embedding failed ({e}); recomputing without cache")
            embeddings = [embed_model.get_text_embedding(t) for t in texts]
        text_embeddings = {
            n.node_id: emb for n, emb in zip(ordered_nodes, embeddings)
        }
        scored = score_candidates(
            ordered_nodes, fused, query_emb, text_embeddings, alpha=alpha,
        )
    ranked = sorted(scored, key=lambda x: x[1], reverse=True)
    print(f"[DEBUG] [{datetime.now()}] query(): scores and ranks END")

    final_nodes = [n for n, _ in ranked[:top_k]]

    print(f"Returned: {len(final_nodes)}")

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
    q = " ".join(sys.argv[1:])
    # Emit the result as JSON on real stdout (the module-level `print` above
    # is redirected to stderr).
    sys.stdout.write(json.dumps(query(q)) + "\n")