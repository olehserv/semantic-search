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
from datetime import datetime

# Imported for its side effect: configures Settings.embed_model, which
# VectorStoreIndex uses to embed nodes during the build.
import model_setup  # noqa: F401
import qdrant
from chunking import chunk_csharp

PROJECT_PATH = "./"


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
    print(f"[DEBUG] code-aware chunks: {code_aware}, "
          f"fallback docs: {len(fallback_docs)}")
    return nodes


def build_index(force=False):
    """Read the project, chunk + embed it, and store the vectors in Qdrant.

    If the collection already exists and force is False, asks before replacing
    it (so you do not wipe an index by accident). force=True (the --force flag)
    replaces it without asking — handy for scripts and CI.
    """
    qdrant.ensure_qdrant()
    client = qdrant.get_qdrant_client()
    if client is None:
        raise RuntimeError("Could not connect to Qdrant")
    print("Collections:", client.get_collections())

    # resolve_active_collection follows the alias, so the prompt still fires
    # once COLLECTION_NAME is an alias (aliases are not listed as collections).
    if qdrant.resolve_active_collection(client, qdrant.COLLECTION_NAME) and not force:
        print(f"Replace existing Qdrant index '{qdrant.COLLECTION_NAME}'? (y/n)")
        if input().lower() != "y":
            print(f"[DEBUG] [{datetime.now()}] Terminated.")
            return

    print(f"[DEBUG] [{datetime.now()}] Loading documents...")
    docs = load_documents()
    print(f"[DEBUG] [{datetime.now()}] Loaded {len(docs)} documents")

    nodes = make_nodes(docs)

    print(f"[DEBUG] [{datetime.now()}] Total nodes: {len(nodes)}")

    # Build into a fresh, timestamped collection — NOT the live one. Nothing is
    # deleted until this succeeds and the alias is swapped below.
    new_collection = qdrant.new_collection_name(qdrant.COLLECTION_NAME)
    vector_store = QdrantVectorStore(client=client, collection_name=new_collection)

    print(f"[DEBUG] [{datetime.now()}] Building index in '{new_collection}'...")

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
            print(f"[DEBUG] could not clean up '{new_collection}' ({e})")
        raise

    # Success: atomically point the alias at the new collection and delete the
    # old index (and any crashed-build leftovers).
    qdrant.promote_collection(client, qdrant.COLLECTION_NAME, new_collection)

    print(f"[DEBUG] [{datetime.now()}] ✅ Index built and '{qdrant.COLLECTION_NAME}' "
          f"now points to '{new_collection}'")

if __name__ == "__main__":
    import argparse

    parser = argparse.ArgumentParser(description="Build the Qdrant search index")
    parser.add_argument(
        "-y", "--force", action="store_true",
        help="replace an existing collection without prompting",
    )
    build_index(force=parser.parse_args().force)