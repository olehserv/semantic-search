"""Regression tests for the BM25 cache handling in scripts/query_index.py.

Covers review finding C2 (docs/reviews/2026-06-11-architecture-review.md):
running a query before any index build used to (a) pickle an EMPTY node list
to bm25.pkl — poisoning the cache for every later run — and then (b) crash
with an opaque ValueError from BM25Retriever.

These tests need the ML stack (llama-index, qdrant-client) installed, e.g. the
project .venv. Under a bare interpreter they skip cleanly, so the metric tests
still run anywhere. model_setup is stubbed in conftest.py, so no embedding
model is ever loaded.
"""
import os
import pickle
import types

import pytest

pytest.importorskip("llama_index.core")
pytest.importorskip("qdrant_client")

import query_index
from llama_index.core.schema import TextNode


def make_index(nodes=()):
    """Minimal stand-in for a loaded index: just .docstore.docs."""
    docs = {f"id-{i}": n for i, n in enumerate(nodes)}
    return types.SimpleNamespace(docstore=types.SimpleNamespace(docs=docs))


@pytest.fixture(autouse=True)
def isolated_bm25_cache(tmp_path, monkeypatch):
    """Point the BM25 cache at a tmp file and reset the module-level cache."""
    cache_path = str(tmp_path / "bm25.pkl")
    monkeypatch.setattr(query_index, "BM25_CACHE_PATH", cache_path)
    monkeypatch.setattr(query_index, "bm25_retriever_nodes_cache", None)
    yield cache_path


def test_empty_docstore_raises_actionable_error(isolated_bm25_cache):
    # Fresh machine: no bm25.pkl, index loaded from the Qdrant vector store has
    # an empty docstore. Must fail with advice, not an opaque ValueError.
    with pytest.raises(RuntimeError, match="build_index"):
        query_index.get_bm25_retriever(make_index())


def test_empty_docstore_does_not_poison_cache_file(isolated_bm25_cache):
    # The old behavior pickled the empty list BEFORE validating it, breaking
    # every subsequent run. The cache file must not be created on failure.
    with pytest.raises(RuntimeError):
        query_index.get_bm25_retriever(make_index())
    assert not os.path.exists(isolated_bm25_cache)


def test_poisoned_cache_from_old_version_raises_actionable_error(
    isolated_bm25_cache, monkeypatch
):
    # A bm25.pkl containing [] (written by the pre-fix code) was loaded at
    # warmup. It must be treated as missing, not handed to BM25Retriever.
    monkeypatch.setattr(query_index, "bm25_retriever_nodes_cache", [])
    with pytest.raises(RuntimeError, match="build_index"):
        query_index.get_bm25_retriever(make_index())


def test_nonempty_docstore_builds_retriever_and_persists_nodes(isolated_bm25_cache):
    nodes = [
        TextNode(text="public class AuthService { void Login() {} }"),
        TextNode(text="public class OrdersController { void Create() {} }"),
    ]
    retriever = query_index.get_bm25_retriever(make_index(nodes))

    assert retriever is not None
    # Safe: reading back a file this test's own code-under-test just wrote to a
    # pytest tmp dir. (Replacing pickle as the cache format is finding M2,
    # addressed by the Phase 1 service design, not Phase 0.)
    with open(isolated_bm25_cache, "rb") as f:
        persisted = pickle.load(f)
    assert len(persisted) == 2


def test_cached_nodes_are_reused_without_touching_docstore(isolated_bm25_cache, monkeypatch):
    # With warm nodes present, the (empty) docstore must not matter.
    nodes = [TextNode(text="public class EmailService { void Send() {} }")]
    monkeypatch.setattr(query_index, "bm25_retriever_nodes_cache", nodes)

    retriever = query_index.get_bm25_retriever(make_index())
    assert retriever is not None
    # And nothing should have been re-persisted.
    assert not os.path.exists(isolated_bm25_cache)
