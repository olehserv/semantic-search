"""Long-running search service: holds the warm retrieval engine.

Start it from the project root you want to search (cache paths are relative
to the current directory):

    .venv/bin/python scripts/service.py

On startup it loads the embedding model, connects to Qdrant, rebuilds BM25
in memory from the collection, and keeps the three retrievers warm. After
that, every POST /search call reuses the warm engine — no cold start.

Endpoints:
    GET  /health  -> {"status": "ok"}
    POST /search  -> body {"query": "..."}; returns the query() JSON.
"""
from flask import Flask, request, jsonify

from config import settings
from query_index import query, get_engine
from logging_setup import setup_logging

app = Flask(__name__)


@app.route("/health")
def health():
    """Liveness check: returns 200 so callers know the service is up."""
    return jsonify({"status": "ok"})


@app.route("/search", methods=["POST"])
def search():
    """Run one search. Body: {"query": "..."}. Returns the query() JSON.

    Missing query -> 400. Any internal error -> 500 with the message, so the
    caller (the MCP shim) gets a clear reason instead of a dropped connection.
    """
    data = request.get_json(silent=True) or {}
    q = data.get("query")
    if not q:
        return jsonify({"error": "missing 'query'"}), 400
    try:
        return jsonify(query(q))
    except Exception as e:
        return jsonify({"error": str(e)}), 500


def main():
    # Heavy work happens only here, not at import time: configure logging, the
    # embedding model + LLM, then warm the retrievers once so the first real
    # request does not pay the build cost.
    setup_logging()
    import model_setup
    model_setup.setup_models()
    get_engine()
    app.run(host="0.0.0.0", port=settings.service_port)


if __name__ == "__main__":
    main()
