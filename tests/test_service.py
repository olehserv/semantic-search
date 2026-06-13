"""Tests for the Flask search service (scripts/service.py).

Importing `service` imports `query_index`, which needs llama-index — so these
tests skip on a bare interpreter (CI installs no ML stack). The heavy work
(model load, engine warmup) lives in service.main(), which tests never call;
retrieval itself is monkeypatched.
"""
import pytest

pytest.importorskip("flask")
pytest.importorskip("llama_index.core")
pytest.importorskip("qdrant_client")

import service


def test_health_ok():
    client = service.app.test_client()
    resp = client.get("/health")
    assert resp.status_code == 200
    assert resp.get_json()["status"] == "ok"


def test_search_missing_query_is_400():
    client = service.app.test_client()
    resp = client.post("/search", json={})
    assert resp.status_code == 400
    assert "error" in resp.get_json()


def test_search_returns_query_result(monkeypatch):
    monkeypatch.setattr(service, "query", lambda q: {"sources": ["x.cs"], "context": []})
    client = service.app.test_client()
    resp = client.post("/search", json={"query": "where is auth"})
    assert resp.status_code == 200
    assert resp.get_json()["sources"] == ["x.cs"]


def test_search_error_is_500(monkeypatch):
    def boom(q):
        raise RuntimeError("kaboom")
    monkeypatch.setattr(service, "query", boom)
    client = service.app.test_client()
    resp = client.post("/search", json={"query": "x"})
    assert resp.status_code == 500
    assert "kaboom" in resp.get_json()["error"]


def test_search_non_string_query_is_400():
    # A list/dict query must be rejected before it reaches query() (plan 3.8).
    client = service.app.test_client()
    resp = client.post("/search", json={"query": [1, 2, 3]})
    assert resp.status_code == 400
    assert "error" in resp.get_json()


def test_search_oversize_body_is_413(monkeypatch):
    # Flask rejects a body bigger than MAX_CONTENT_LENGTH before parsing it.
    monkeypatch.setitem(service.app.config, "MAX_CONTENT_LENGTH", 16)
    client = service.app.test_client()
    resp = client.post("/search", data=b"x" * 64,
                        content_type="application/json")
    assert resp.status_code == 413
