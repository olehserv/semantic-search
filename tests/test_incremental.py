"""Unit tests for incremental indexing (scripts/build_index.py, plan 3.4).

Three layers, no real Qdrant and no embedding model:
  - diff_files: pure set logic (changed / new / deleted).
  - make_nodes: the file_hash is stamped on every chunk AND kept out of the
    embed/LLM text (the invariant that keeps eval numbers identical).
  - build_index_incremental: the orchestration deletes the right files and
    re-indexes only the changed/new ones, using fakes for Qdrant + embedding.
"""
import types

import pytest

# build_index imports the ML stack (llama_index) and qdrant_client at load.
# Skip cleanly on the bare CI interpreter, like the other heavy tests.
pytest.importorskip("llama_index.core")
pytest.importorskip("qdrant_client")

import build_index  # noqa: E402  (must come after the importorskip guards)
from llama_index.core import Document  # noqa: E402


# --- diff_files (pure) -------------------------------------------------------

def test_diff_files_detects_each_kind():
    old = {"a.cs": "h1", "b.cs": "h2", "gone.cs": "h3"}
    new = {"a.cs": "h1", "b.cs": "CHANGED", "new.cs": "h4"}
    changed, new_files, deleted = build_index.diff_files(old, new)
    assert changed == {"b.cs"}        # same path, different hash
    assert new_files == {"new.cs"}    # only on disk now
    assert deleted == {"gone.cs"}     # only in the old index


def test_diff_files_no_changes():
    same = {"a.cs": "h1", "b.cs": "h2"}
    assert build_index.diff_files(same, dict(same)) == (set(), set(), set())


def test_diff_files_first_build_empty_old():
    # No prior index: everything is new, nothing changed or deleted.
    changed, new_files, deleted = build_index.diff_files({}, {"a.cs": "h1"})
    assert changed == set()
    assert new_files == {"a.cs"}
    assert deleted == set()


def test_diff_files_missing_hash_counts_as_changed():
    # A point indexed before task 3.4 has file_hash == None; the file must be
    # treated as changed so it gets re-indexed once.
    changed, _, _ = build_index.diff_files({"a.cs": None}, {"a.cs": "h1"})
    assert changed == {"a.cs"}


# --- make_nodes stamps + excludes file_hash ----------------------------------

def test_make_nodes_stamps_and_excludes_file_hash():
    # A non-.cs file takes the SentenceSplitter path (no tree-sitter needed).
    text = "the quick brown fox jumps over the lazy dog"
    doc = Document(
        text=text,
        metadata={"file_name": "notes.txt", "file_path": "docs/notes.txt"},
    )
    nodes = build_index.make_nodes([doc])

    assert nodes, "expected at least one chunk"
    expected = build_index.file_content_hash(text)
    for node in nodes:
        # WHAT: every chunk carries its file's hash...
        assert node.metadata["file_hash"] == expected
        # ...but it must never reach the embedded text or the LLM context,
        # or the vectors (and the eval numbers) would change.
        assert "file_hash" in node.excluded_embed_metadata_keys
        assert "file_hash" in node.excluded_llm_metadata_keys


# --- orchestration -----------------------------------------------------------

class FakeScrollDeleteClient:
    """Stand-in for QdrantClient: serves stored points via scroll(), records
    delete() calls and the file_path each one filtered on."""

    def __init__(self, points):
        # points: list of dicts -> become payloads
        self._points = [types.SimpleNamespace(payload=p) for p in points]
        self.deleted_paths = []

    def scroll(self, collection_name, with_payload, with_vectors, limit, offset):
        # One page, then stop (offset None ends the loop in load_file_hashes).
        return self._points, None

    def delete(self, collection_name, points_selector):
        path = points_selector.filter.must[0].match.value
        self.deleted_paths.append(path)


def test_load_file_hashes_one_entry_per_file():
    client = FakeScrollDeleteClient([
        {"file_path": "a.cs", "file_hash": "h1"},   # two chunks, same file
        {"file_path": "a.cs", "file_hash": "h1"},
        {"file_path": "b.cs", "file_hash": "h2"},
    ])
    assert build_index.load_file_hashes(client, "live") == {"a.cs": "h1", "b.cs": "h2"}


def _fake_doc(path, text):
    return types.SimpleNamespace(text=text, metadata={"file_path": path})


def test_incremental_deletes_and_reindexes_the_right_files(monkeypatch):
    # Old index: a.cs (h_a), b.cs (h_b), gone.cs (h_g).
    # On disk now: a.cs unchanged, b.cs edited, new.cs added, gone.cs removed.
    client = FakeScrollDeleteClient([
        {"file_path": "a.cs", "file_hash": build_index.file_content_hash("AAA")},
        {"file_path": "b.cs", "file_hash": build_index.file_content_hash("BBB")},
        {"file_path": "gone.cs", "file_hash": build_index.file_content_hash("GGG")},
    ])
    docs = [
        _fake_doc("a.cs", "AAA"),          # unchanged
        _fake_doc("b.cs", "BBB-EDITED"),   # changed
        _fake_doc("new.cs", "NEW"),        # new
    ]

    reindexed = {}

    monkeypatch.setattr(build_index.qdrant, "ensure_qdrant", lambda: None)
    monkeypatch.setattr(build_index.qdrant, "get_qdrant_client", lambda: client)
    monkeypatch.setattr(
        build_index.qdrant, "resolve_active_collection", lambda c, n: "live"
    )
    monkeypatch.setattr(build_index, "load_documents", lambda: docs)
    # Capture which files reach the re-index step; skip real chunking/embedding.
    monkeypatch.setattr(
        build_index, "make_nodes",
        lambda ds: reindexed.update(paths={d.metadata["file_path"] for d in ds}) or ["node"],
    )
    monkeypatch.setattr(build_index, "QdrantVectorStore", lambda **kw: object())
    monkeypatch.setattr(
        build_index.StorageContext, "from_defaults", staticmethod(lambda **kw: object())
    )
    monkeypatch.setattr(build_index, "VectorStoreIndex", lambda *a, **kw: object())

    build_index.build_index_incremental()

    # Edited + removed files have their old points deleted; unchanged/new do not.
    assert set(client.deleted_paths) == {"b.cs", "gone.cs"}
    # Only edited + new files are re-embedded; unchanged and deleted are not.
    assert reindexed["paths"] == {"b.cs", "new.cs"}


def test_incremental_no_changes_does_nothing(monkeypatch):
    client = FakeScrollDeleteClient([
        {"file_path": "a.cs", "file_hash": build_index.file_content_hash("AAA")},
    ])
    monkeypatch.setattr(build_index.qdrant, "ensure_qdrant", lambda: None)
    monkeypatch.setattr(build_index.qdrant, "get_qdrant_client", lambda: client)
    monkeypatch.setattr(
        build_index.qdrant, "resolve_active_collection", lambda c, n: "live"
    )
    monkeypatch.setattr(build_index, "load_documents", lambda: [_fake_doc("a.cs", "AAA")])

    called = {"make_nodes": False}
    monkeypatch.setattr(
        build_index, "make_nodes",
        lambda ds: called.update(make_nodes=True) or [],
    )

    build_index.build_index_incremental()

    assert client.deleted_paths == []          # nothing deleted
    assert called["make_nodes"] is False       # nothing re-embedded


def test_incremental_falls_back_to_full_build_when_no_index(monkeypatch):
    monkeypatch.setattr(build_index.qdrant, "ensure_qdrant", lambda: None)
    monkeypatch.setattr(build_index.qdrant, "get_qdrant_client", lambda: object())
    monkeypatch.setattr(
        build_index.qdrant, "resolve_active_collection", lambda c, n: None
    )
    full = {"called": False}
    monkeypatch.setattr(
        build_index, "build_index",
        lambda force=False: full.update(called=True, force=force),
    )

    build_index.build_index_incremental()

    assert full["called"] is True
    assert full["force"] is True   # non-interactive fallback
