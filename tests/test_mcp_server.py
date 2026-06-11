"""Tests for the thin MCP shim (scripts/mcp_server.py).

No ML stack, no running service, no MCP client needed: search_code() is
exercised with requests-level fakes. The `mcp` SDK import lives inside
build_server(), so importing the module needs only `requests`; the one test
that touches the SDK guards itself with importorskip.
"""
import pytest

requests = pytest.importorskip("requests")

import mcp_server  # noqa: E402  (must come after the importorskip guard)


class FakeResp:
    def __init__(self, status_code=200, payload=None, text="", bad_json=False):
        self.status_code = status_code
        self._payload = payload
        self.text = text
        self._bad_json = bad_json

    def json(self):
        if self._bad_json:
            raise ValueError("not json")
        return self._payload


def test_forward_success(monkeypatch):
    calls = {}

    def fake_post(url, json=None, timeout=None):
        calls.update(url=url, json=json, timeout=timeout)
        return FakeResp(payload={"sources": ["a.cs"]})

    monkeypatch.setattr(mcp_server.requests, "post", fake_post)

    assert mcp_server.search_code("hi") == {"sources": ["a.cs"]}
    assert calls["url"].endswith("/search")
    assert calls["json"] == {"query": "hi"}
    # requests has NO default timeout; a hung service must not hang the agent.
    assert calls["timeout"] == mcp_server.TIMEOUT_SECONDS


def test_service_down_returns_structured_error(monkeypatch):
    def boom(*args, **kwargs):
        raise requests.ConnectionError("connection refused")

    monkeypatch.setattr(mcp_server.requests, "post", boom)

    out = mcp_server.search_code("hi")
    assert "unreachable" in out["error"]
    assert mcp_server.SEARCH_SERVICE_URL in out["error"]
    assert "hint" in out


def test_non_200_returns_structured_error(monkeypatch):
    monkeypatch.setattr(
        mcp_server.requests, "post",
        lambda *a, **k: FakeResp(status_code=500, text="boom"),
    )
    out = mcp_server.search_code("hi")
    assert out["error"].startswith("search service returned 500")
    assert "boom" in out["error"]


def test_bad_json_returns_structured_error(monkeypatch):
    monkeypatch.setattr(
        mcp_server.requests, "post",
        lambda *a, **k: FakeResp(text="<html>oops</html>", bad_json=True),
    )
    out = mcp_server.search_code("hi")
    assert out["error"] == "search service returned invalid JSON"
    assert "oops" in out["body"]


def test_tool_is_registered():
    pytest.importorskip("mcp")
    import asyncio

    server = mcp_server.build_server()
    tools = asyncio.run(server.list_tools())
    assert [t.name for t in tools] == ["search_codebase"]
