"""Build (or rebuild) the search index in Qdrant.

The one-time indexing step. It reads the code files, cuts them into chunks
(code-aware for C#, sentence windows for the rest), turns each chunk into a
vector with the embedding model, and stores the vectors in Qdrant. Run it once
before searching, and again whenever the code changes.

Crash-safe rebuild (plan 3.3, finding H6): instead of deleting the old index
first, we build into a NEW timestamped collection, then atomically point the
Qdrant alias (the name the query side uses) at it, then delete the old one.
If the build crashes, the old index keeps serving queries untouched. See
qdrant.promote_collection.

Run from the root of the project you want to search:

    python scripts/build_index.py            # asks before replacing an index
    python scripts/build_index.py --force     # replaces without asking
"""
from llama_index.core import SimpleDirectoryReader, VectorStoreIndex, StorageContext
from llama_index.core.node_parser import SentenceSplitter
from llama_index.core.schema import TextNode
from llama_index.vector_stores.qdrant import QdrantVectorStore
from qdrant_client import models
import hashlib
import logging

import model_setup
import qdrant
from chunking import chunk_csharp
from logging_setup import setup_logging

logger = logging.getLogger(__name__)

PROJECT_PATH = "./"


def file_content_hash(text):
    """A stable fingerprint of a file's text. Same text -> same hash, so the
    incremental build (task 3.4) can tell when a file actually changed."""
    return hashlib.sha256(text.encode("utf-8")).hexdigest()


def load_documents():
    """Read the code files we want to index from the current project folder.

    Only .cs/.csproj/.sln/.slnx files are read. Build output and tool folders
    (bin, obj, node_modules, .git, ...) are skipped — they are noise, not code.
    """
    return SimpleDirectoryReader(
        PROJECT_PATH,
        recursive=True,
        required_exts=[".cs", ".csproj", ".sln", ".slnx"],
        exclude=[
            "*/bin/*",
            "*/obj/*",
            "*/node_modules/*",
            ".git/*",
            ".vs/*",
            ".run/*",
            "build/*",
            "dist/*",
            "packages/*",
            "*/TestResults/*",
            "*/PackageRoot/*",
            "*.snk"
        ]
    ).load_data()

def make_nodes(docs):
    """Code-aware chunks for C# files (task 2.4); sentence windows for
    everything else and for C# that does not parse."""
    splitter = SentenceSplitter(chunk_size=800, chunk_overlap=150)
    nodes = []
    fallback_docs = []
    for doc in docs:
        # WHAT: a content fingerprint of the whole file, stamped on every chunk
        # of that file. WHY: incremental indexing (task 3.4) compares these
        # hashes to find which files changed. It is bookkeeping only — embedding
        # it would change the vector and break eval reproducibility, so we keep
        # it out of the embedded text and the LLM context.
        doc.metadata["file_hash"] = file_content_hash(doc.text)
        doc.excluded_embed_metadata_keys.append("file_hash")
        doc.excluded_llm_metadata_keys.append("file_hash")

        chunks = None
        if (doc.metadata.get("file_name") or "").endswith(".cs"):
            chunks = chunk_csharp(doc.text)
        if chunks is None:
            fallback_docs.append(doc)
            continue
        for text, extra in chunks:
            nodes.append(TextNode(
                text=text,
                metadata={**doc.metadata, **extra},
                # Keep the reader's embed/LLM exclusions (file size, dates…)
                # exactly as the SentenceSplitter nodes inherited them.
                excluded_embed_metadata_keys=list(doc.excluded_embed_metadata_keys),
                excluded_llm_metadata_keys=list(doc.excluded_llm_metadata_keys),
            ))
    code_aware = len(nodes)
    nodes.extend(splitter.get_nodes_from_documents(fallback_docs))
    logger.debug("code-aware chunks: %d, fallback docs: %d",
                 code_aware, len(fallback_docs))
    return nodes


def build_index(force=False):
    """Read the project, chunk + embed it, and store the vectors in Qdrant.

    If the collection already exists and force is False, asks before replacing
    it (so you do not wipe an index by accident). force=True (the --force flag)
    replaces it without asking — handy for scripts and CI.
    """
    qdrant.ensure_qdrant()
    model_setup.setup_models()  # configure the embed model VectorStoreIndex uses
    client = qdrant.get_qdrant_client()
    if client is None:
        raise RuntimeError("Could not connect to Qdrant")
    logger.debug("Collections: %s", client.get_collections())

    # resolve_active_collection follows the alias, so the prompt still fires
    # once COLLECTION_NAME is an alias (aliases are not listed as collections).
    if qdrant.resolve_active_collection(client, qdrant.COLLECTION_NAME) and not force:
        # A real prompt, not a log line: ask on stdout and read the answer.
        answer = input(f"Replace existing Qdrant index '{qdrant.COLLECTION_NAME}'? (y/n) ")
        if answer.lower() != "y":
            logger.info("Terminated.")
            return

    logger.info("Loading documents...")
    docs = load_documents()
    logger.info("Loaded %d documents", len(docs))

    nodes = make_nodes(docs)

    logger.info("Total nodes: %d", len(nodes))

    # Build into a fresh, timestamped collection — NOT the live one. Nothing is
    # deleted until this succeeds and the alias is swapped below.
    new_collection = qdrant.new_collection_name(qdrant.COLLECTION_NAME)
    vector_store = QdrantVectorStore(client=client, collection_name=new_collection)

    logger.info("Building index in '%s'...", new_collection)

    # Wire the vector store through a StorageContext so the index is actually
    # persisted to Qdrant. Passing vector_store= to the constructor alone builds
    # an in-memory index and never creates/populates the Qdrant collection.
    storage_context = StorageContext.from_defaults(vector_store=vector_store)

    try:
        VectorStoreIndex(nodes, storage_context=storage_context, show_progress=True)
    except BaseException:
        # On any failure (including Ctrl-C) drop the half-built collection so it
        # cannot linger as an orphan; the live alias was never touched.
        try:
            client.delete_collection(new_collection)
        except Exception as e:
            logger.warning("could not clean up '%s' (%s)", new_collection, e)
        raise

    # Success: atomically point the alias at the new collection and delete the
    # old index (and any crashed-build leftovers).
    qdrant.promote_collection(client, qdrant.COLLECTION_NAME, new_collection)

    logger.info("✅ Index built and '%s' now points to '%s'",
                qdrant.COLLECTION_NAME, new_collection)


# --- Incremental indexing (plan 3.4, finding M6) -----------------------------
#
# A full rebuild re-reads, re-chunks, and re-embeds every file. Incremental mode
# only touches what actually changed on disk: it compares each file's content
# hash against the hash stored on the points already in Qdrant, then re-embeds
# the changed/new files and deletes the points of files removed from disk.
# Untouched files are never read or rewritten.

def diff_files(old, new):
    """Compare two {file_path: file_hash} maps. Return three sets of paths.

    - changed:   in both, but the hash differs (file edited)
    - new_files: only in `new` (file added since last index)
    - deleted:   only in `old` (file removed from disk)
    """
    changed = {p for p in new if p in old and new[p] != old[p]}
    new_files = {p for p in new if p not in old}
    deleted = {p for p in old if p not in new}
    return changed, new_files, deleted


def load_file_hashes(client, collection):
    """Read the {file_path: file_hash} the live index already knows about.

    Scrolls the collection asking only for the two payload fields we need (no
    vectors, no chunk text), paginated like query_index.load_all_nodes. A point
    indexed before task 3.4 has no `file_hash`; it reads as None, so its file
    looks "changed" and gets re-indexed once — harmless.
    """
    hashes = {}
    offset = None
    while True:
        points, offset = client.scroll(
            collection_name=collection,
            with_payload=["file_path", "file_hash"],
            with_vectors=False,
            limit=256,
            offset=offset,
        )
        for p in points:
            payload = p.payload or {}
            path = payload.get("file_path")
            if path is not None:
                # Many chunks share a file; they carry the same hash, so the
                # last write wins and the map ends up one entry per file.
                hashes[path] = payload.get("file_hash")
        if offset is None:
            break
    return hashes


def delete_files(client, collection, paths):
    """Remove every point whose `file_path` is in `paths` (delete-by-filter)."""
    for path in paths:
        client.delete(
            collection_name=collection,
            points_selector=models.FilterSelector(
                filter=models.Filter(
                    must=[
                        models.FieldCondition(
                            key="file_path",
                            match=models.MatchValue(value=path),
                        )
                    ]
                )
            ),
        )


def build_index_incremental():
    """Update the live index in place: re-embed only changed/new files and drop
    the vectors of files deleted from disk.

    Falls back to a full build when there is no index yet (nothing to diff
    against). Works on the collection the alias currently resolves to, so it
    never creates a new collection or swaps the alias.
    """
    qdrant.ensure_qdrant()
    model_setup.setup_models()  # configure the embed model VectorStoreIndex uses
    client = qdrant.get_qdrant_client()
    if client is None:
        raise RuntimeError("Could not connect to Qdrant")

    live = qdrant.resolve_active_collection(client, qdrant.COLLECTION_NAME)
    if live is None:
        logger.info("No existing index — doing a full build instead.")
        return build_index(force=True)

    logger.info("Incremental update of '%s'", live)

    # Old state: what the index already holds. New state: what is on disk now.
    # The new hashes use the same function make_nodes stamps with, so a file's
    # disk hash and its stored hash match exactly when it is unchanged.
    old = load_file_hashes(client, live)
    docs = load_documents()
    new = {doc.metadata["file_path"]: file_content_hash(doc.text) for doc in docs}

    changed, new_files, deleted = diff_files(old, new)
    to_reindex = changed | new_files

    logger.info("changed: %d, new: %d, deleted: %d",
                len(changed), len(new_files), len(deleted))

    if not to_reindex and not deleted:
        logger.info("Nothing changed — index left untouched.")
        return

    # Drop points of edited files (their fresh chunks are re-added below) and of
    # files removed from disk (gone for good).
    delete_files(client, live, changed | deleted)

    if to_reindex:
        nodes = make_nodes([d for d in docs if d.metadata["file_path"] in to_reindex])
        logger.info("Re-embedding %d chunks from %d files...",
                    len(nodes), len(to_reindex))
        vector_store = QdrantVectorStore(client=client, collection_name=live)
        storage_context = StorageContext.from_defaults(vector_store=vector_store)
        VectorStoreIndex(nodes, storage_context=storage_context, show_progress=True)

    logger.info("✅ Incremental update done on '%s'", live)


if __name__ == "__main__":
    import argparse

    parser = argparse.ArgumentParser(description="Build the Qdrant search index")
    parser.add_argument(
        "-y", "--force", action="store_true",
        help="replace an existing collection without prompting",
    )
    parser.add_argument(
        "-i", "--incremental", action="store_true",
        help="update the live index in place: only re-embed changed/new files "
             "and drop deleted ones (no full rebuild, no alias swap)",
    )
    args = parser.parse_args()
    setup_logging()
    if args.incremental:
        build_index_incremental()
    else:
        build_index(force=args.force)