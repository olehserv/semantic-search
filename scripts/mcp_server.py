"""Real MCP server (stdio): exposes `search_codebase` to MCP clients.

This is a thin shim. It imports NO ML stack — only `requests` at module
level, and the `mcp` SDK lazily inside build_server(). The heavy search
engine lives in the long-running service (scripts/service.py); start it
first:

    .venv/bin/python scripts/service.py

stdout belongs to the MCP protocol — never print() in this module; any
diagnostics must go to stderr.
"""
import os

import requests

SEARCH_SERVICE_URL = os.getenv("SEARCH_SERVICE_URL", "http://localhost:8000")
TIMEOUT_SECONDS = float(os.getenv("SEARCH_SERVICE_TIMEOUT", "120"))


def search_code(query: str) -> dict:
    """Forward one query to the search service.

    Never raises: every failure mode comes back as a structured
    {"error": ...} dict, which gives the model more to work with than an
    exception frame.
    """
    try:
        resp = requests.post(
            f"{SEARCH_SERVICE_URL}/search",
            json={"query": query},
            timeout=TIMEOUT_SECONDS,
        )
    except requests.RequestException as exc:
        return {
            "error": f"search service unreachable at {SEARCH_SERVICE_URL}: {exc}",
            "hint": "Start it with: .venv/bin/python scripts/service.py "
                    "(or check SEARCH_SERVICE_URL).",
        }
    if resp.status_code != 200:
        return {"error": f"search service returned {resp.status_code}: {resp.text[:500]}"}
    try:
        return resp.json()
    except ValueError:
        return {
            "error": "search service returned invalid JSON",
            "body": resp.text[:500],
        }


def build_server():
    """Construct the FastMCP server. Separate from main() so tests can check
    the tool registration without running the stdio loop."""
    from mcp.server.fastmcp import FastMCP  # deferred: keeps bare import light

    server = FastMCP("code-search")

    @server.tool()
    def search_codebase(query: str) -> dict:
        """Hybrid semantic + keyword search over the indexed codebase.

        Returns ranked source files and code snippets for a natural-language
        question about the code (e.g. "where is authentication handled").
        """
        return search_code(query)

    return server


def main() -> None:
    build_server().run()  # stdio transport is the default


if __name__ == "__main__":
    main()
