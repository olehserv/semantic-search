"""Tests for query_index.load_all_nodes — rebuilding nodes from Qdrant.

BM25 is in-memory only (no pickle): the nodes are reconstructed by scrolling
the Qdrant collection. These tests use a fake client, so no Qdrant and no ML
stack beyond llama-index-core is needed; they skip on a bare interpreter.
"""
import types

import pytest

pytest.importorskip("llama_index.core")
pytest.importorskip("qdrant_client")

import query_index


class FakeClient:
    """Returns successive (points, next_offset) tuples from scroll()."""

    def __init__(self, pages):
        self.pages = pages
        self.calls = 0

    def scroll(self, **kwargs):
        page = self.pages[self.calls]
        self.calls += 1
        return page


def _point(pid, text):
    # A payload without _node_content forces the TextNode fallback path.
    return types.SimpleNamespace(id=pid, payload={"text": text})


def test_load_all_nodes_pages_and_preserves_ids():
    pages = [
        ([_point("a", "foo"), _point("b", "bar")], "offset-1"),
        ([_point("c", "baz")], None),
    ]
    client = FakeClient(pages)

    nodes = query_index.load_all_nodes(client, "col")

    assert [n.text for n in nodes] == ["foo", "bar", "baz"]
    assert [n.node_id for n in nodes] == ["a", "b", "c"]
    assert client.calls == 2


def test_load_all_nodes_empty_collection():
    nodes = query_index.load_all_nodes(FakeClient([([], None)]), "col")
    assert nodes == []


def test_load_all_nodes_handles_missing_payload():
    point = types.SimpleNamespace(id="x", payload=None)
    nodes = query_index.load_all_nodes(FakeClient([([point], None)]), "col")
    assert len(nodes) == 1
    assert nodes[0].text == ""
