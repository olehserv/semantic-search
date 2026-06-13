"""Unit tests for the safe-rebuild alias logic (scripts/qdrant.py, plan 3.3).

No real Qdrant: a FakeClient keeps the collection/alias state in memory and
applies the same alias operations the real client would. We assert that
promote_collection swaps the alias and deletes exactly the right collections.
(A real containerized Qdrant test is the separate task 3.6.)
"""
import re
import types

import qdrant


class FakeClient:
    """Minimal stand-in for QdrantClient: tracks collections + aliases."""

    def __init__(self, collections=(), aliases=None):
        self.collections = set(collections)
        self.aliases = dict(aliases or {})   # alias_name -> collection_name
        self.deleted = []                    # order of delete_collection calls

    def get_collections(self):
        cols = [types.SimpleNamespace(name=n) for n in self.collections]
        return types.SimpleNamespace(collections=cols)

    def get_aliases(self):
        al = [
            types.SimpleNamespace(alias_name=a, collection_name=c)
            for a, c in self.aliases.items()
        ]
        return types.SimpleNamespace(aliases=al)

    def delete_collection(self, name):
        self.deleted.append(name)
        self.collections.discard(name)
        # An alias can only exist while its target does; drop dangling aliases.
        for a, c in list(self.aliases.items()):
            if c == name:
                del self.aliases[a]

    def update_collection_aliases(self, change_aliases_operations):
        # Apply ops in order, the way Qdrant does (atomic batch).
        for op in change_aliases_operations:
            if getattr(op, "delete_alias", None) is not None:
                self.aliases.pop(op.delete_alias.alias_name, None)
            elif getattr(op, "create_alias", None) is not None:
                ca = op.create_alias
                self.aliases[ca.alias_name] = ca.collection_name


def test_new_collection_name_format():
    name = qdrant.new_collection_name("demo")
    assert re.match(r"^demo-\d{8}-\d{6}$", name)


def test_promote_fresh_creates_alias():
    # Nothing exists yet (the new collection was just built by the caller).
    client = FakeClient(collections={"demo-20260613-141500"})
    qdrant.promote_collection(client, "demo", "demo-20260613-141500")

    assert client.aliases == {"demo": "demo-20260613-141500"}
    assert client.deleted == []   # nothing old to remove


def test_promote_legacy_real_collection_migrates():
    # "demo" is an old REAL collection (no alias). Migration must delete it so
    # the alias name is free, then create the alias.
    client = FakeClient(collections={"demo", "demo-20260613-141500"})
    qdrant.promote_collection(client, "demo", "demo-20260613-141500")

    assert "demo" in client.deleted               # legacy collection removed
    assert client.aliases == {"demo": "demo-20260613-141500"}
    assert client.collections == {"demo-20260613-141500"}


def test_promote_swaps_alias_and_deletes_old():
    # Normal rebuild: alias already points at an older timestamped build.
    client = FakeClient(
        collections={"demo-20260612-090000", "demo-20260613-141500"},
        aliases={"demo": "demo-20260612-090000"},
    )
    qdrant.promote_collection(client, "demo", "demo-20260613-141500")

    assert client.aliases == {"demo": "demo-20260613-141500"}
    assert "demo-20260612-090000" in client.deleted   # previous build gone
    assert client.collections == {"demo-20260613-141500"}


def test_promote_does_not_touch_sibling_collection():
    # "demo-eval" (and its builds) share the "demo-" prefix but are a SEPARATE
    # index. Rebuilding "demo" must never delete them.
    client = FakeClient(
        collections={
            "demo-20260613-141500",
            "demo-eval",
            "demo-eval-20260613-141500",
        },
        aliases={"demo-eval": "demo-eval-20260613-141500"},
    )
    qdrant.promote_collection(client, "demo", "demo-20260613-141500")

    assert "demo-eval" not in client.deleted
    assert "demo-eval-20260613-141500" not in client.deleted
    assert client.aliases["demo-eval"] == "demo-eval-20260613-141500"


def test_promote_sweeps_crashed_build_orphans():
    # A leftover timestamped collection from an earlier crashed build is swept
    # on the next successful promote.
    client = FakeClient(
        collections={
            "demo-20260610-000000",   # orphan from a crash (no alias points here)
            "demo-20260612-090000",   # the current live build
            "demo-20260613-141500",   # the new build
        },
        aliases={"demo": "demo-20260612-090000"},
    )
    qdrant.promote_collection(client, "demo", "demo-20260613-141500")

    assert set(client.deleted) == {"demo-20260610-000000", "demo-20260612-090000"}
    assert client.collections == {"demo-20260613-141500"}


def test_resolve_active_collection():
    # Alias -> its target; real collection -> itself; unknown -> None.
    client = FakeClient(
        collections={"demo-20260613-141500", "lfm"},
        aliases={"demo": "demo-20260613-141500"},
    )
    assert qdrant.resolve_active_collection(client, "demo") == "demo-20260613-141500"
    assert qdrant.resolve_active_collection(client, "lfm") == "lfm"
    assert qdrant.resolve_active_collection(client, "nope") is None
