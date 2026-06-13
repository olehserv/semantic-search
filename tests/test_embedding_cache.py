"""Unit tests for the SQLite embedding cache (scripts/embedding_cache.py, plan 3.2).

These need no ML stack: a tiny FakeEmbed stands in for the real model and counts
how many times it was asked to embed, so we can prove cache hits avoid recompute.
Each test writes to a fresh database under pytest's tmp_path.
"""
from embedding_cache import EmbeddingCache


class FakeEmbed:
    """Stand-in for the embedding model. Returns a deterministic vector and
    counts calls, so a test can check whether the cache recomputed or not."""

    def __init__(self):
        self.calls = 0

    def get_text_embedding(self, text):
        self.calls += 1
        # Vector depends on the text so different texts get different vectors.
        return [float(len(text)), 1.0, 2.0]


def _db(tmp_path):
    return str(tmp_path / "emb.db")


def test_miss_then_hit(tmp_path):
    # First lookup is a miss (computed once); the second is a hit (no recompute).
    cache = EmbeddingCache(_db(tmp_path), model_name="m1", max_size=100)
    fake = FakeEmbed()

    first = cache.get_many(["hello"], fake)
    assert fake.calls == 1
    assert first == [[5.0, 1.0, 2.0]]

    second = cache.get_many(["hello"], fake)
    assert fake.calls == 1  # unchanged -> served from cache
    assert second == first


def test_key_includes_model_name(tmp_path):
    # The same text under a different model must NOT reuse the first model's
    # vector. This is the H3 fix: models can never be mixed.
    path = _db(tmp_path)
    fake = FakeEmbed()

    EmbeddingCache(path, model_name="m1", max_size=100).get_many(["hi"], fake)
    assert fake.calls == 1

    # New cache object, same file, DIFFERENT model name -> miss -> recompute.
    EmbeddingCache(path, model_name="m2", max_size=100).get_many(["hi"], fake)
    assert fake.calls == 2


def test_lru_eviction(tmp_path):
    # With room for only 2 rows, adding a 3rd evicts the least-recently-used.
    cache = EmbeddingCache(_db(tmp_path), model_name="m1", max_size=2)
    fake = FakeEmbed()

    cache.get_many(["a"], fake)        # rows: a
    cache.get_many(["bb"], fake)       # rows: a, bb
    cache.get_many(["a"], fake)        # touch a -> a now newer than bb
    cache.get_many(["ccc"], fake)      # over limit -> evict oldest (bb)

    count = cache.conn.execute("SELECT COUNT(*) FROM embeddings").fetchone()[0]
    assert count == 2

    remaining = {
        row[0]
        for row in cache.conn.execute("SELECT text_hash FROM embeddings").fetchall()
    }
    assert EmbeddingCache._hash("bb") not in remaining   # evicted
    assert EmbeddingCache._hash("a") in remaining        # kept (recently used)
    assert EmbeddingCache._hash("ccc") in remaining      # kept (newest)


def test_persistence_across_instances(tmp_path):
    # A new cache object on the same file reads vectors written earlier.
    path = _db(tmp_path)
    fake = FakeEmbed()

    EmbeddingCache(path, model_name="m1", max_size=100).get_many(["data"], fake)
    assert fake.calls == 1

    reopened = EmbeddingCache(path, model_name="m1", max_size=100)
    reopened.get_many(["data"], fake)
    assert fake.calls == 1  # loaded from disk, not recomputed


def test_vector_round_trip(tmp_path):
    # Stored-then-loaded vectors match the original within float32 precision.
    cache = EmbeddingCache(_db(tmp_path), model_name="m1", max_size=100)

    class Vec:
        def get_text_embedding(self, text):
            return [0.1, -2.5, 3.14159, 42.0]

    # A miss returns the freshly computed vector (full precision); a hit returns
    # the value decoded from the float32 blob. Both must match the original
    # within float32 precision.
    original = [0.1, -2.5, 3.14159, 42.0]
    cache.get_many(["x"], Vec())            # miss: writes the blob
    loaded = cache.get_many(["x"], Vec())[0]  # hit: reads it back from the DB
    assert len(loaded) == len(original)
    for a, b in zip(original, loaded):
        assert abs(a - b) < 1e-5


def test_duplicate_texts_embedded_once(tmp_path):
    # A query repeating the same chunk should embed it only once.
    cache = EmbeddingCache(_db(tmp_path), model_name="m1", max_size=100)
    fake = FakeEmbed()

    result = cache.get_many(["same", "same", "same"], fake)
    assert fake.calls == 1
    assert result == [[4.0, 1.0, 2.0]] * 3
