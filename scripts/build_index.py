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
    qdrant.ensure_qdrant()
    client = qdrant.get_qdrant_client()
    if client is None:
        raise RuntimeError("Could not connect to Qdrant")
    qdrant_cols = client.get_collections()
    print("Collections:", qdrant_cols)

    # get_collections() returns a CollectionsResponse, not a list of names —
    # `name in response` never matched, so the replace guard was dead code.
    existing = [c.name for c in qdrant_cols.collections]
    if qdrant.COLLECTION_NAME in existing and not force:
        print(f"Replace existing Qdrant collection '{qdrant.COLLECTION_NAME}'? (y/n)")
        if input().lower() != "y":
            print(f"[DEBUG] [{datetime.now()}] Terminated.")
            return

    print(f"[DEBUG] [{datetime.now()}] Loading documents...")
    docs = load_documents()
    print(f"[DEBUG] [{datetime.now()}] Loaded {len(docs)} documents")

    nodes = make_nodes(docs)

    print(f"[DEBUG] [{datetime.now()}] Total nodes: {len(nodes)}")
    
    try:
        client.delete_collection(qdrant.COLLECTION_NAME)
    except Exception as e:
        print(f"Collection not found, skipping delete ({e})")
    
    vector_store = QdrantVectorStore(
        client=client,
        collection_name=qdrant.COLLECTION_NAME
    )

    print(f"[DEBUG] [{datetime.now()}] Building index in Qdrant...")

    # Wire the vector store through a StorageContext so the index is actually
    # persisted to Qdrant. Passing vector_store= to the constructor alone builds
    # an in-memory index and never creates/populates the Qdrant collection.
    storage_context = StorageContext.from_defaults(vector_store=vector_store)

    VectorStoreIndex(
        nodes,
        storage_context=storage_context,
        show_progress=True
    )

    print(f"[DEBUG] [{datetime.now()}] ✅ Index successfully built in Qdrant")

if __name__ == "__main__":
    import argparse

    parser = argparse.ArgumentParser(description="Build the Qdrant search index")
    parser.add_argument(
        "-y", "--force", action="store_true",
        help="replace an existing collection without prompting",
    )
    build_index(force=parser.parse_args().force)