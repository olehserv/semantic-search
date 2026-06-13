"""End-to-end test of the query() pipeline with FAKE retrievers (plan 3.6, H5).

The pure scoring math is already covered by test_ranking.py. The gap this fills
is the ORCHESTRATION in query_index.query(): query variants -> three retrievers
-> RRF fusion -> candidate cut -> cosine re-rank -> top_k JSON. We feed canned
retriever output (real NodeWithScore objects, exactly what a real retriever
returns) and fake the embedding model + cache, so no Qdrant and no real model
are needed — only the fusion/scoring code runs for real.
"""
import types

import pytest

pytest.importorskip("llama_index.core")
pytest.importorskip("numpy")

import query_index  # noqa: E402
from llama_index.core.schema import TextNode, NodeWithScore  # noqa: E402


def nws(node_id, text, file_name, score):
    """A NodeWithScore shaped like what VectorIndexRetriever/BM25 return."""
    node = TextNode(
        id_=node_id,
        text=text,
        metadata={"file_name": file_name, "file_path": f"src/{file_name}"},
    )
    return NodeWithScore(node=node, score=score)


class FakeRetriever:
    """Returns a fixed result list, ignoring the query (the fusion logic under
    test does not care what produced the ranks)."""

    def __init__(self, results):
        self._results = results

    def retrieve(self, query):
        return list(self._results)


class FakeEmbed:
    """Deterministic 'embedding': a 3-d vector derived from the text, so the
    cosine re-rank has real (if meaningless) signal to work on."""

    def get_text_embedding(self, text):
        return [float(len(text)), float(ord(text[0]) if text else 0), 1.0]


class FakeCache:
    """Stands in for the SQLite embedding cache: just embeds each text."""

    def get_many(self, texts, embed_model):
        return [embed_model.get_text_embedding(t) for t in texts]


@pytest.fixture
def fake_pipeline(monkeypatch):
    # n1 is in both vector and bm25; n2 in vector + wide; n3 ONLY in bm25.
    vector = [
        nws("n1", "login password verify", "Login.cs", 0.9),
        nws("n2", "user repository code", "User.cs", 0.5),
    ]
    bm25 = [
        nws("n3", "token refresh handler", "Token.cs", 2.0),
        nws("n1", "login password verify", "Login.cs", 1.0),
    ]
    wide = [
        nws("n2", "user repository code", "User.cs", 0.4),
    ]
    monkeypatch.setattr(
        query_index, "get_engine",
        lambda: (FakeRetriever(vector), FakeRetriever(bm25), FakeRetriever(wide)),
    )
    monkeypatch.setattr(query_index, "_get_cache", lambda: FakeCache())
    # Replace the module's Settings reference so query() reads our fake embed
    # model (avoids llama-index's real embed_model setter/validation).
    monkeypatch.setattr(
        query_index, "Settings", types.SimpleNamespace(embed_model=FakeEmbed())
    )
    # The engine + per-query caches are module globals; reset them each test.
    monkeypatch.setattr(query_index, "_engine", None, raising=False)
    monkeypatch.setattr(query_index, "_retrieve_cache", {}, raising=False)
    return {"unique_nodes": 3}


def test_query_returns_expected_json_shape(fake_pipeline):
    out = query_index.query("where is the password verified")

    assert set(out.keys()) == {"answer", "sources", "context"}
    # Three unique nodes across the three retrievers; top_k default 8 keeps all.
    assert len(out["context"]) == 3
    for i, item in enumerate(out["context"]):
        assert set(item.keys()) == {"file", "path", "text", "score", "rank"}
        assert item["rank"] == i + 1                 # ranks are 1..n in order
        assert item["score"] == round(item["score"], 3)   # scores rounded to 3 dp


def test_all_three_retriever_lists_are_fused(fake_pipeline):
    # n3 (Token.cs) appears ONLY in the bm25 list. If fusion dropped any list,
    # it would be missing. Its presence proves all three lists are fused.
    out = query_index.query("token refresh")
    sources = out["sources"]
    assert "Token.cs" in sources
    assert sources == sorted(set(sources))           # sorted + de-duplicated


def test_scores_are_descending(fake_pipeline):
    out = query_index.query("login")
    scores = [item["score"] for item in out["context"]]
    assert scores == sorted(scores, reverse=True)    # best first


def test_top_k_limits_results(fake_pipeline):
    out = query_index.query("login", top_k=2)
    assert len(out["context"]) == 2
    assert len(out["sources"]) == 2


def test_candidate_cut_respected(fake_pipeline, monkeypatch):
    # The post-fusion cut (RERANK_CANDIDATES) limits how many nodes are re-ranked.
    monkeypatch.setattr(query_index, "RERANK_CANDIDATES", 1)
    out = query_index.query("login")
    assert len(out["context"]) == 1


# --- _retrieve_cache (LRU, finding M7) ---

def test_retrieve_cache_hit_skips_retrieval(monkeypatch):
    monkeypatch.setattr(query_index, "_retrieve_cache", {}, raising=False)
    calls = {"n": 0}

    class Counting:
        def retrieve(self, q):
            calls["n"] += 1
            return []

    c = Counting()
    query_index.hybrid_retrieve("q", c, c, c)
    query_index.hybrid_retrieve("q", c, c, c)   # served from cache
    assert calls["n"] == 3   # three retrievers, called once; second call cached


def test_retrieve_cache_evicts_least_recently_used(monkeypatch):
    monkeypatch.setattr(query_index, "RETRIEVE_CACHE_SIZE", 2)
    monkeypatch.setattr(query_index, "_retrieve_cache", {}, raising=False)
    r = FakeRetriever([nws("n1", "t", "F.cs", 1.0)])

    query_index.hybrid_retrieve("q1", r, r, r)
    query_index.hybrid_retrieve("q2", r, r, r)
    query_index.hybrid_retrieve("q1", r, r, r)   # touch q1 -> most recent
    query_index.hybrid_retrieve("q3", r, r, r)   # over cap -> evict LRU (q2)

    assert set(query_index._retrieve_cache) == {"q1", "q3"}
    assert len(query_index._retrieve_cache) == 2
