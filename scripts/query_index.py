from llama_index.core import Settings
from llama_index.core.retrievers import VectorIndexRetriever
from llama_index.retrievers.bm25 import BM25Retriever
from llama_index.vector_stores.qdrant import QdrantVectorStore
from llama_index.core import VectorStoreIndex
import numpy as np
from collections import defaultdict

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

INDEX_PATH = "./.claude/index"
EMB_CACHE_PATH = "./.claude/cache/embeddings.pkl"
BM25_CACHE_PATH = "./.claude/cache/bm25.pkl"

_embedding_cache = {}
bm25_retriever_nodes_cache = None

print(f"[DEBUG] [{datetime.now()}] BEGIN query_index")

def cache_warmup():
    global _embedding_cache, bm25_retriever_nodes_cache
    if os.path.exists(EMB_CACHE_PATH):
        with open(EMB_CACHE_PATH, "rb") as f:
            print("[DEBUG] Loading EMB cache ...")
            _embedding_cache = pickle.load(f)
            print(f"[DEBUG] EMB cache size: {len(_embedding_cache)}")
    
    if os.path.exists(BM25_CACHE_PATH):
        with open(BM25_CACHE_PATH, "rb") as f:
            print("[DEBUG] Loading BM25 nodes cache ...")
            bm25_retriever_nodes_cache = pickle.load(f)
            print(f"[DEBUG] BM25 nodes cache size: {len(bm25_retriever_nodes_cache)}")


cache_warmup()

def get_bm25_retriever(index):
    global bm25_retriever_nodes_cache
    # Treat an empty list the same as no cache: a pre-fix bm25.pkl may contain
    # [] (the old code persisted before validating), and an index loaded from
    # the Qdrant vector store always has an empty docstore.
    if not bm25_retriever_nodes_cache:
        nodes = list(index.docstore.docs.values())
        if not nodes:
            raise RuntimeError(
                "No nodes available for BM25 keyword search: the index was "
                "loaded from Qdrant (empty docstore) and no BM25 node cache "
                f"exists at {BM25_CACHE_PATH}. Run build_index.py first."
            )
        bm25_retriever_nodes_cache = nodes

        with open(BM25_CACHE_PATH, "wb") as f:
            pickle.dump(bm25_retriever_nodes_cache, f)

    return BM25Retriever.from_defaults(nodes=bm25_retriever_nodes_cache)


def load_index():
    qdrant.ensure_qdrant()
    client = qdrant.get_Qdrant_client()
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
    if query in _retrieve_cache:
            return _retrieve_cache[query]

    results = []
    print(f"[DEBUG] [{datetime.now()}] hybrid_retrieve start for query '{query}'")
    print(f"[DEBUG] [{datetime.now()}] vector.retrieve")
    results.extend(vector.retrieve(query))
    print(f"[DEBUG] [{datetime.now()}] bm25.retrieve")
    results.extend(bm25.retrieve(query))
    print(f"[DEBUG] [{datetime.now()}] vector_wide.retrieve")
    results.extend(vector_wide.retrieve(query))
    print(f"[DEBUG] [{datetime.now()}] hybrid_retrieve end for query '{query}'")

    _retrieve_cache[query] = results
    return results


_engine = None

def get_engine():
    global _engine
    if _engine is None:
        _engine = build_query_engine()
    return _engine


def expand_query(q):
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

def query(q, alpha=0.7, freq_weight=0.1, top_k=8):
    print(f"[DEBUG] [{datetime.now()}] query(): get_engine")
    
    vector, bm25, vector_wide = get_engine()

    queries = expand_query(q)
    
    all_nodes = []
    node_counts = defaultdict(int)

    print(f"[DEBUG] [{datetime.now()}] query(): gather nodes START")
    for sub_q in queries:
        if sub_q == q:
            weight = 1.5
        else:
            weight = 1.0

        nodes = hybrid_retrieve(sub_q, vector, bm25, vector_wide)        
        for n in nodes:
                node_counts[n.node_id] += weight
                all_nodes.append(n)

    seen = set()
    ordered_nodes = []

    for n in all_nodes:
        if n.node_id not in seen:
            seen.add(n.node_id)
            ordered_nodes.append(n)

    ordered_nodes = ordered_nodes[:30]

    print(f"Candidates: {len(ordered_nodes)}")
    print(f"[DEBUG] [{datetime.now()}] query(): gather nodes END")

    print(f"[DEBUG] [{datetime.now()}] query(): embedding START")
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
    print(f"[DEBUG] [{datetime.now()}] query(): embedding END")

    print(f"[DEBUG] [{datetime.now()}] query(): scores and ranks START")
    scores = [float(n.score) for n in ordered_nodes if n.score is not None]

    min_s = min(scores) if scores else 0
    max_s = max(scores) if scores else 1

    normalizeFunc = make_normalizer(min_s, max_s)
    scored = []

    for n in ordered_nodes:
        emb_score = cosine(query_emb, text_embeddings[n.node_id])
        retriever_score = normalizeFunc(float(n.score)) if n.score is not None else 0.0
        freq_boost = np.log1p(node_counts[n.node_id]) / np.log1p(max(node_counts.values()))
        final_score = (
            alpha * emb_score +
            (1 - alpha) * retriever_score +
            freq_weight * freq_boost
        )
        scored.append((n, final_score))

    ranked = sorted(scored, key=lambda x: x[1], reverse=True)
    print(f"[DEBUG] [{datetime.now()}] query(): scores and ranks START")

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