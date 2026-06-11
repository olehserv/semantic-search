"""Tests for the BM25 retriever construction in scripts/query_index.py.

Since Phase 1, BM25 nodes come from the Qdrant collection (load_all_nodes),
not from a pickle file. These tests keep the lesson from review finding C2:
an empty collection must give a clear, actionable error — never the opaque
ValueError that BM25Retriever raises for an empty node list.

The tests need llama-index/qdrant-client installed (e.g. the project .venv)
and skip cleanly on a bare interpreter. model_setup is stubbed in conftest.py.
"""
import types

import pytest

pytest.importorskip("llama_index.core")
pytest.importorskip("qdrant_client")

import query_index
from llama_index.core.schema import TextNode


@pytest.fixture
def fake_qdrant(monkeypatch):
    """Replace the qdrant client + node loader plumbing with fakes."""
    state = {"nodes": []}
    monkeypatch.setattr(query_index.qdrant, "get_qdrant_client", lambda: object())
    monkeypatch.setattr(
        query_index, "load_all_nodes", lambda client, collection: state["nodes"]
    )
    return state


def test_empty_collection_raises_actionable_error(fake_qdrant):
    with pytest.raises(RuntimeError, match="build_index"):
        query_index.get_bm25_retriever(types.SimpleNamespace())


def test_nonempty_collection_builds_retriever(fake_qdrant):
    fake_qdrant["nodes"] = [
        TextNode(text="public class AuthService { void Login() {} }"),
        TextNode(text="public class OrdersController { void Create() {} }"),
    ]
    retriever = query_index.get_bm25_retriever(types.SimpleNamespace())
    assert retriever is not None
