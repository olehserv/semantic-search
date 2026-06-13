"""Real MCP protocol round-trip for the server (plan 3.6, H5).

test_mcp_server.py covers the HTTP-forwarding function and its error paths.
This file fills the other gap: a genuine client<->server exchange over the MCP
SDK's in-memory transport — initialize, tools/list, tools/call — so the tool's
schema and the JSON-RPC wiring are exercised, not just the Python function.
No service runs: requests.post is faked so the tool returns canned search JSON.
"""
import asyncio
import json

import pytest

pytest.importorskip("requests")
pytest.importorskip("mcp")

import mcp_server  # noqa: E402
from mcp.shared.memory import (  # noqa: E402
    create_connected_server_and_client_session,
)


CANNED = {
    "answer": "Top 1 relevant code snippets retrieved.",
    "sources": ["Login.cs"],
    "context": [{"file": "Login.cs", "path": "src/Login.cs", "rank": 1}],
}


class FakeResp:
    status_code = 200
    text = ""

    def json(self):
        return CANNED


async def _drive_protocol():
    """initialize -> list_tools -> call_tool over the in-memory transport."""
    server = mcp_server.build_server()
    async with create_connected_server_and_client_session(server) as session:
        await session.initialize()
        tools = await session.list_tools()
        call = await session.call_tool("search_codebase", {"query": "auth"})
        return tools, call


def test_protocol_initialize_list_and_call(monkeypatch):
    # The tool forwards to the search service; fake that HTTP call.
    monkeypatch.setattr(mcp_server.requests, "post", lambda *a, **k: FakeResp())

    tools, call = asyncio.run(_drive_protocol())

    # tools/list advertises search_codebase with a `query` string input.
    names = [t.name for t in tools.tools]
    assert "search_codebase" in names
    tool = next(t for t in tools.tools if t.name == "search_codebase")
    assert "query" in tool.inputSchema.get("properties", {})

    # tools/call ran the tool and carried the canned payload back to the client.
    assert call.isError is False
    blob = json.dumps(call.structuredContent) + " ".join(
        getattr(c, "text", "") for c in call.content
    )
    assert "Login.cs" in blob


def test_protocol_call_propagates_service_down_error(monkeypatch):
    # When the service is unreachable, the tool returns a structured error dict
    # (not an exception) and the protocol still delivers it as a normal result.
    import requests

    def boom(*a, **k):
        raise requests.ConnectionError("refused")

    monkeypatch.setattr(mcp_server.requests, "post", boom)

    async def _call():
        server = mcp_server.build_server()
        async with create_connected_server_and_client_session(server) as session:
            await session.initialize()
            return await session.call_tool("search_codebase", {"query": "x"})

    call = asyncio.run(_call())
    blob = json.dumps(call.structuredContent) + " ".join(
        getattr(c, "text", "") for c in call.content
    )
    assert "unreachable" in blob
