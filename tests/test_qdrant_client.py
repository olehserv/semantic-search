"""Unit tests for Qdrant client auth wiring (scripts/qdrant.py, plan 3.8, M10).

get_qdrant_client() must pass the API key + https flag from config to the one
QdrantClient it builds, so a secured/remote Qdrant works — while the local
default (no key) stays plain, unauthenticated http. A fake client records the
kwargs; no real connection is made.
"""
import pytest

pytest.importorskip("qdrant_client")

import qdrant  # noqa: E402


class FakeClient:
    def __init__(self, **kwargs):
        self.kwargs = kwargs

    def get_collections(self):
        return None  # the connectivity probe in get_qdrant_client() passes


def test_passes_api_key_and_https_when_set(monkeypatch):
    monkeypatch.setattr(qdrant, "QdrantClient", FakeClient)
    monkeypatch.setattr(qdrant, "QDRANT_API_KEY", "secret-token")
    monkeypatch.setattr(qdrant, "QDRANT_HTTPS", True)

    client = qdrant.get_qdrant_client()

    assert client is not None
    assert client.kwargs["api_key"] == "secret-token"
    assert client.kwargs["https"] is True
    assert client.kwargs["host"] == qdrant.QDRANT_HOST
    assert client.kwargs["port"] == qdrant.QDRANT_PORT


def test_empty_key_becomes_none_unauthenticated(monkeypatch):
    monkeypatch.setattr(qdrant, "QdrantClient", FakeClient)
    monkeypatch.setattr(qdrant, "QDRANT_API_KEY", "")
    monkeypatch.setattr(qdrant, "QDRANT_HTTPS", False)

    client = qdrant.get_qdrant_client()

    # "" -> None so qdrant-client treats it as unauthenticated (local default).
    assert client.kwargs["api_key"] is None
    assert client.kwargs["https"] is False
