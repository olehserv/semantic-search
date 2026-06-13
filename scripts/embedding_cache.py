"""SQLite-backed cache for chunk embeddings (production-readiness plan 3.2).

Embedding text is the slow part of search, so we save each vector and reuse it
next time. This replaces the old pickle dict, which had three problems:

  - it was keyed by the chunk text ALONE, so changing the embedding model
    silently mixed old and new vectors (review finding H3);
  - it rewrote the whole file on every cache miss (finding M7);
  - it grew without any size limit (finding M2).

This version fixes all three:

  - the key is (model_name, text_hash), so a different model simply misses and
    recomputes — vectors from two models can never be confused;
  - all reads/writes for one query happen in a single transaction, so a query
    causes exactly ONE disk write (the commit);
  - the table has a max size; the least-recently-used rows are evicted, which
    also ages out vectors left behind by an old model.

Simple idea: think of it as a notebook where each page is labelled with both
the model that wrote it and a fingerprint of the text. You only trust a page if
both labels match what you are looking for.
"""
import hashlib
import os
import sqlite3
from array import array
from time import time_ns


class EmbeddingCache:
    """Store and look up embedding vectors in a small SQLite database."""

    def __init__(self, db_path, model_name, max_size):
        """Open (or create) the cache database.

        db_path:    path to the SQLite file.
        model_name: the embedding model's name. It is part of every key, so
                    this cache only ever returns vectors made by this model.
        max_size:   maximum number of rows to keep (LRU eviction past this).
        """
        self.model_name = model_name
        self.max_size = max_size

        # Make sure the parent folder exists before SQLite tries to create the
        # file there (e.g. ./.claude/cache/).
        parent = os.path.dirname(db_path)
        if parent:
            os.makedirs(parent, exist_ok=True)

        # check_same_thread=False: the search service (Flask) may call from a
        # different thread than the one that opened the connection. We only ever
        # write inside one short transaction per query, so this is safe here.
        self.conn = sqlite3.connect(db_path, check_same_thread=False)
        self._ensure_schema()

    def _ensure_schema(self):
        """Create the table on first use. Safe to call every time."""
        # last_used is a nanosecond timestamp; it drives LRU eviction (oldest
        # last_used = evicted first). The primary key is (model_name, text_hash)
        # so the same text under two models lives in two separate rows.
        self.conn.execute(
            """
            CREATE TABLE IF NOT EXISTS embeddings (
                model_name TEXT NOT NULL,
                text_hash  TEXT NOT NULL,
                vector     BLOB NOT NULL,
                last_used  INTEGER NOT NULL,
                PRIMARY KEY (model_name, text_hash)
            )
            """
        )
        self.conn.commit()

    @staticmethod
    def _hash(text):
        """Fingerprint the text so we store a short fixed-length key, not the
        whole chunk. SHA-256 collisions are effectively impossible here."""
        return hashlib.sha256(text.encode("utf-8")).hexdigest()

    @staticmethod
    def _to_blob(vector):
        """Pack a list of floats into compact float32 bytes (no pickle)."""
        return array("f", vector).tobytes()

    @staticmethod
    def _from_blob(blob):
        """Unpack float32 bytes back into a plain list of floats."""
        out = array("f")
        out.frombytes(blob)
        return list(out)

    def get_many(self, texts, embed_model):
        """Return one vector per text, in the same order as `texts`.

        Cached texts are read from disk; missing ones are computed with
        embed_model.get_text_embedding(text) and stored. Everything happens in
        ONE transaction, so the whole call costs a single disk write (commit).
        """
        # Nanosecond clock, not milliseconds: rapid back-to-back queries can fall
        # inside the same millisecond, which would give several rows an identical
        # last_used and make LRU eviction order (ORDER BY last_used) a tie broken
        # arbitrarily by rowid — so a just-touched row could be evicted before an
        # older one. Nanosecond resolution keeps each call strictly ordered.
        now = time_ns()
        hashes = [self._hash(t) for t in texts]

        # One read for all the rows we might already have. We de-duplicate the
        # hashes so a query repeating the same chunk asks the DB only once.
        unique_hashes = list(set(hashes))
        cached = {}
        if unique_hashes:
            placeholders = ",".join("?" for _ in unique_hashes)
            rows = self.conn.execute(
                f"SELECT text_hash, vector FROM embeddings "
                f"WHERE model_name = ? AND text_hash IN ({placeholders})",
                [self.model_name, *unique_hashes],
            ).fetchall()
            cached = {h: blob for h, blob in rows}

        results = []
        inserts = []  # rows to add this call: (model, hash, blob, last_used)
        for text, h in zip(texts, hashes):
            if h in cached:
                # Cache hit: decode the stored bytes back to floats.
                results.append(self._from_blob(cached[h]))
            else:
                # Cache miss: compute the vector and remember it for the write
                # below. Store the blob in `cached` too, so a repeated text in
                # this same batch is only embedded once.
                vector = embed_model.get_text_embedding(text)
                blob = self._to_blob(vector)
                cached[h] = blob
                inserts.append((self.model_name, h, blob, now))
                results.append(vector)

        # Stage all writes. INSERT OR REPLACE also refreshes last_used for any
        # row we just recomputed.
        if inserts:
            self.conn.executemany(
                "INSERT OR REPLACE INTO embeddings "
                "(model_name, text_hash, vector, last_used) VALUES (?, ?, ?, ?)",
                inserts,
            )
        # Bump last_used on every touched row (hits included) so popular chunks
        # survive eviction. Done as one statement over the de-duplicated hashes.
        if unique_hashes:
            placeholders = ",".join("?" for _ in unique_hashes)
            self.conn.execute(
                f"UPDATE embeddings SET last_used = ? "
                f"WHERE model_name = ? AND text_hash IN ({placeholders})",
                [now, self.model_name, *unique_hashes],
            )

        self._evict_if_needed()
        self.conn.commit()  # the single disk write for this query
        return results

    def _evict_if_needed(self):
        """If the table is over its size limit, delete the oldest rows.

        Eviction spans all models, so vectors from a model you stopped using
        (their last_used never refreshes) are the first to go.
        """
        count = self.conn.execute("SELECT COUNT(*) FROM embeddings").fetchone()[0]
        overflow = count - self.max_size
        if overflow > 0:
            self.conn.execute(
                "DELETE FROM embeddings WHERE rowid IN ("
                "  SELECT rowid FROM embeddings ORDER BY last_used ASC LIMIT ?"
                ")",
                (overflow,),
            )

    def close(self):
        """Close the database connection."""
        self.conn.close()
