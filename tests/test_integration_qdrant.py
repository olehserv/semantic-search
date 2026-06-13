"""Integration test against a real, throwaway Qdrant container (plan 3.6, H5).

Everything else in the suite fakes Qdrant (FakeClient) or the retrievers. This
one starts an actual `qdrant/qdrant` container with testcontainers and runs the
real round-trip: build_index (load -> chunk -> embed -> alias promote) then
query_index.query(). It uses llama-index's MockEmbedding so no 400 MB model is
downloaded — we are testing the PLUMBING (real upsert / scroll / BM25-from-
Qdrant / alias resolve / query orchestration), not ranking quality. BM25 carries
the keyword match, so the round-trip assertion holds with mock vectors.

Skips cleanly when testcontainers, qdrant-client, llama-index, or a usable
Docker daemon is absent — so ci.yml (no Docker) stays green.
"""
import pytest

pytest.importorskip("testcontainers")
pytest.importorskip("qdrant_client")
pytest.importorskip("llama_index.core")

from qdrant_client import QdrantClient  # noqa: E402
from testcontainers.core.container import DockerContainer  # noqa: E402

QDRANT_IMAGE = "qdrant/qdrant:v1.12.1"
HTTP_PORT = 6333


@pytest.fixture(scope="module")
def qdrant_endpoint():
    """Start a throwaway Qdrant container; yield (host, port); tear it down.

    Skips the whole module if Docker is unavailable (daemon down, no socket,
    image cannot be pulled) so the test never fails for environment reasons.
    """
    container = DockerContainer(QDRANT_IMAGE).with_exposed_ports(HTTP_PORT)
    try:
        container.start()
    except Exception as exc:  # docker missing / not running / pull failed
        pytest.skip(f"Docker/Qdrant container unavailable: {exc}")

    host = container.get_container_host_ip()
    port = int(container.get_exposed_port(HTTP_PORT))

    # Wait until Qdrant answers (it boots in a second or two).
    import time
    ready = False
    for _ in range(30):
        try:
            QdrantClient(host=host, port=port).get_collections()
            ready = True
            break
        except Exception:
            time.sleep(1)
    if not ready:
        container.stop()
        pytest.skip("Qdrant container did not become ready in time")

    try:
        yield host, port
    finally:
        container.stop()


def _write_project(root):
    """A tiny C# project with one clearly named file to search for."""
    (root / "LoginService.cs").write_text(
        "namespace Auth {\n"
        "    public class LoginService {\n"
        "        public bool VerifyPassword(string user, string pw) { return true; }\n"
        "    }\n"
        "}\n"
    )
    (root / "OrderRepository.cs").write_text(
        "namespace Shop {\n"
        "    public class OrderRepository {\n"
        "        public int Count() { return 0; }\n"
        "    }\n"
        "}\n"
    )


def test_build_then_query_roundtrip(qdrant_endpoint, tmp_path, monkeypatch):
    host, port = qdrant_endpoint

    import build_index
    import qdrant
    import query_index
    from llama_index.core import Settings
    from llama_index.core.embeddings import MockEmbedding

    # Point the code at the container and at a throwaway collection name.
    monkeypatch.setattr(qdrant, "QDRANT_HOST", host)
    monkeypatch.setattr(qdrant, "QDRANT_PORT", port)
    monkeypatch.setattr(qdrant, "COLLECTION_NAME", "test-pipeline")

    # Mock embedding: no model download; deterministic 8-d vectors. Set the
    # backing field directly — assigning Settings.embed_model goes through a
    # property whose getter still resolves "default" (OpenAI) when unset.
    monkeypatch.setattr(Settings, "_embed_model", MockEmbedding(embed_dim=8))
    # Keep the embedding cache out of the repo: point it at the tmp dir.
    monkeypatch.setattr(query_index.settings, "emb_cache_path",
                        str(tmp_path / "cache.db"))
    # The engine + per-query caches are module globals; start clean.
    monkeypatch.setattr(query_index, "_engine", None, raising=False)
    monkeypatch.setattr(query_index, "_retrieve_cache", {}, raising=False)

    # Index a tiny project that lives in tmp (not the whole repo).
    project = tmp_path / "proj"
    project.mkdir()
    _write_project(project)
    monkeypatch.setattr(build_index, "PROJECT_PATH", str(project))

    # Build into the container, then query it back.
    build_index.build_index(force=True)
    out = query_index.query("LoginService VerifyPassword")

    # The named file came back through the real Qdrant + BM25 + query() path.
    assert "LoginService.cs" in out["sources"]
    assert out["context"], "expected at least one ranked chunk"
    assert all(set(c) >= {"file", "path", "text", "score", "rank"}
               for c in out["context"])
